/*  kntap.c

This file is part of Kainos, a program that implements a Software-Defined Radio.
Copyright (C) 2026 Justin Cron K7JUS

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.

*/

#include "comm.h"

#define KNTAP_RING (1 << 16)						// complex samples, ~1.4 s at 48 kHz
#define KNTAP_MASK (KNTAP_RING - 1)

// One writer (the channel's DSP thread, in xrxa) and one reader (a console thread): a free-running write counter
// is enough, as in ChannelMaster's kdigi.c. The ring is allocated on first use and kept.
typedef struct _kntap
{
	volatile LONG on;
	volatile LONG w;
	float* ring;									// interleaved I, Q
} kntap;

static kntap kt[MAX_CHANNELS];

void xkntap (int channel)
{
	kntap* t = &kt[channel];
	double* in;
	int i, n;
	LONG w;
	if (!t->on || !t->ring) return;
	in = rxa[channel].midbuff;
	n = ch[channel].dsp_size;
	w = InterlockedCompareExchange (&t->w, 0, 0);
	for (i = 0; i < n; i++)
	{
		t->ring[2 * ((w + i) & KNTAP_MASK) + 0] = (float)in[2 * i + 0];
		t->ring[2 * ((w + i) & KNTAP_MASK) + 1] = (float)in[2 * i + 1];
	}
	InterlockedExchange (&t->w, w + n);
}

PORT
void KainosNotchTapRun (int channel, int on)
{
	if (channel < 0 || channel >= MAX_CHANNELS) return;
	if (on && !kt[channel].ring)
		kt[channel].ring = (float *) malloc0 (2 * KNTAP_RING * sizeof (float));
	InterlockedExchange (&kt[channel].on, on ? 1 : 0);
}

// The newest n complex samples, oldest first, into dst (2 * n floats). Returns the total number of samples
// written so far (so the caller can tell how much is new), or -1 when the tap is off or n is too large.
PORT
int KainosNotchTapRead (int channel, float* dst, int n)
{
	kntap* t;
	LONG w;
	int i;
	if (channel < 0 || channel >= MAX_CHANNELS) return -1;
	t = &kt[channel];
	if (!t->on || !t->ring || n <= 0 || n > KNTAP_RING / 2) return -1;
	w = InterlockedCompareExchange (&t->w, 0, 0);
	for (i = 0; i < n; i++)
	{
		LONG k = (w - n + i) & KNTAP_MASK;
		dst[2 * i + 0] = t->ring[2 * k + 0];
		dst[2 * i + 1] = t->ring[2 * k + 1];
	}
	return (int)w;
}

// info[0] the DSP rate, info[1] the RF frequency of 0 Hz in the tap (the notch database's tune + shift, so a
// carrier at f Hz in the tap is at info[1] + f Hz, the frequency RXANBPAddNotch takes), info[2] / info[3] the
// passband's low and high edges in the tap (Hz)
PORT
void KainosNotchTapInfo (int channel, double* info)
{
	if (channel < 0 || channel >= MAX_CHANNELS) return;
	EnterCriticalSection (&ch[channel].csDSP);
	info[0] = ch[channel].dsp_rate;
	info[1] = rxa[channel].ndb.p->tunefreq + rxa[channel].ndb.p->shift;
	info[2] = rxa[channel].nbp0.p->flow;
	info[3] = rxa[channel].nbp0.p->fhigh;
	LeaveCriticalSection (&ch[channel].csDSP);
}
