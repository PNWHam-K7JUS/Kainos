/*  aethervoice.c

This file is part of Kainos, a program that implements a Software-Defined Radio.

AetherVoice is ported from the AetherSDR exciter (src/core/ClientPudu.h/.cpp,
https://github.com/aethersdr/AetherSDR, commit 20d022d5), by the AetherSDR
contributors. Ported to WDSP for Kainos by Justin Cron K7JUS, 2026.

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

/********************************************************************************************************
*																										*
*											Helpers														*
*																										*
********************************************************************************************************/

static double av_clamp (double x, double lo, double hi)
{
	return x < lo ? lo : (x > hi ? hi : x);
}

static double av_db_to_lin (double db)
{
	return pow (10.0, db * 0.05);
}

static double av_coeff_from_ms (double ms, double rate)
{
	if (ms <= 0.0) return 1.0;
	return 1.0 - exp (-1.0 / (rate * ms * 0.001));
}

// RBJ biquads, Q = 0.707.  c[] = b0, b1, b2, a1, a2 (normalised by a0)
static void av_highpass (double fc, double rate, double* c)
{
	double w = TWOPI * fc / rate, cosw = cos (w), alpha = sin (w) / (2.0 * 0.70710678);
	double a0 = 1.0 + alpha;
	c[0] =  (1.0 + cosw) / 2.0 / a0;
	c[1] = -(1.0 + cosw) / a0;
	c[2] =  (1.0 + cosw) / 2.0 / a0;
	c[3] = -2.0 * cosw / a0;
	c[4] =  (1.0 - alpha) / a0;
}

static void av_lowpass (double fc, double rate, double* c)
{
	double w = TWOPI * fc / rate, cosw = cos (w), alpha = sin (w) / (2.0 * 0.70710678);
	double a0 = 1.0 + alpha;
	c[0] = (1.0 - cosw) / 2.0 / a0;
	c[1] = (1.0 - cosw) / a0;
	c[2] = (1.0 - cosw) / 2.0 / a0;
	c[3] = -2.0 * cosw / a0;
	c[4] = (1.0 - alpha) / a0;
}

// 2nd-order all-pass at fc: unity magnitude, phase rotation (Behringer Body path)
static void av_allpass (double fc, double rate, double* c)
{
	double w = TWOPI * fc / rate, cosw = cos (w), alpha = sin (w) / (2.0 * 0.70710678);
	double a0 = 1.0 + alpha;
	c[0] = (1.0 - alpha) / a0;
	c[1] = -2.0 * cosw / a0;
	c[2] = (1.0 + alpha) / a0;
	c[3] = -2.0 * cosw / a0;
	c[4] = (1.0 - alpha) / a0;
}

// transposed direct form II
static __inline double av_biquad (double x, const double* c, double* z)
{
	double y = c[0] * x + z[0];
	z[0] = c[1] * x - c[3] * y + z[1];
	z[1] = c[2] * x - c[4] * y;
	return y;
}

/********************************************************************************************************
*																										*
*											AetherVoice													*
*																										*
********************************************************************************************************/

static void calc_aethervoice (AETHERVOICE a)
{
	av_highpass (a->clarity_tune_hz, a->samplerate, a->hpf);
	av_lowpass (a->body_tune_hz, a->samplerate, a->lpf);
	av_allpass (a->body_tune_hz, a->samplerate, a->allpass);
	a->clarity_drive_lin = av_db_to_lin (a->clarity_harmonics_db);
	a->body_drive_lin = av_db_to_lin (a->body_drive_db);
	// LF envelope ballistics: AetherSDR's middle ground for both modes
	a->env_attack  = av_coeff_from_ms (  8.0, a->samplerate);
	a->env_release = av_coeff_from_ms (120.0, a->samplerate);
}

AETHERVOICE create_aethervoice (int run, int size, double* in, double* out, int samplerate)
{
	AETHERVOICE a = (AETHERVOICE) malloc0 (sizeof (aethervoice));
	a->run = run;
	a->size = size;
	a->in = in;
	a->out = out;
	a->samplerate = (double)samplerate;
	// AetherSDR defaults
	a->mode = 0;
	a->body_drive_db = 0.0;
	a->body_tune_hz = 100.0;
	a->body_mix = 0.5;
	a->clarity_tune_hz = 5000.0;
	a->clarity_harmonics_db = 6.0;
	a->clarity_mix = 0.5;
	a->wet_rms_db = -120.0;
	calc_aethervoice (a);
	flush_aethervoice (a);
	return a;
}

void destroy_aethervoice (AETHERVOICE a)
{
	_aligned_free (a);
}

void flush_aethervoice (AETHERVOICE a)
{
	memset (a->hpf_z, 0, sizeof (a->hpf_z));
	memset (a->lpf_z, 0, sizeof (a->lpf_z));
	memset (a->allpass_z, 0, sizeof (a->allpass_z));
	memset (a->dc_x1, 0, sizeof (a->dc_x1));
	memset (a->dc_y1, 0, sizeof (a->dc_y1));
	a->lf_env = 0.0;
}

void xaethervoice (AETHERVOICE a, int stereo)
{
	int i, c;
	int nch = stereo ? 2 : 1;
	int aphex = (a->mode == 0);
	double dry[2], lf[2], wet_lf[2], hf, comp = 1.0;
	double wet_sumsq = 0.0, wet, wet_rms;

	if (!a->run)
	{
		if (a->in != a->out)
			memcpy (a->out, a->in, a->size * sizeof (complex));
		a->wet_rms_db = -120.0;
		return;
	}

	for (i = 0; i < a->size; i++)
	{
		double lp_abs = 0.0, env_coeff, dyn_boost;

		dry[0] = a->in[2 * i + 0];
		dry[1] = a->in[2 * i + 1];

		// LF envelope follower on the LPF output (both modes)
		for (c = 0; c < nch; c++)
		{
			lf[c] = av_biquad (dry[c], a->lpf, a->lpf_z[c]);
			if (fabs (lf[c]) > lp_abs) lp_abs = fabs (lf[c]);
		}
		env_coeff = (lp_abs > a->lf_env) ? a->env_attack : a->env_release;
		a->lf_env += env_coeff * (lp_abs - a->lf_env);

		if (aphex)
		{
			// Big Bottom dynamic EQ: quiet lows get the full drive, loud lows less
			dyn_boost = 1.0 + a->body_drive_lin * (1.0 - min (a->lf_env, 1.0));
			for (c = 0; c < nch; c++)
				wet_lf[c] = tanh (lf[c] * dyn_boost) * 0.5;
		}
		else
		{
			// feed-forward 4:1 compressor, threshold falls as drive rises
			double thr = 0.5 - 0.3 * log10 (a->body_drive_lin + 0.1);
			comp = 1.0;
			if (a->lf_env > thr)
				comp = pow (a->lf_env / max (thr, 1.0e-6), -0.75);
			for (c = 0; c < nch; c++)
				wet_lf[c] = av_biquad (lf[c] * comp, a->allpass, a->allpass_z[c]);
		}

		for (c = 0; c < nch; c++)
		{
			hf = av_biquad (dry[c], a->hpf, a->hpf_z[c]) * a->clarity_drive_lin;
			if (aphex)
			{
				// one-sided (diode-like) clip, then DC block for the resulting offset
				double y;
				hf = (hf >= 0.0) ? tanh (hf) : hf;
				y = hf - a->dc_x1[c] + 0.995 * a->dc_y1[c];
				a->dc_x1[c] = hf;
				a->dc_y1[c] = y;
				hf = y;
			}
			else
				hf = tanh (hf);

			wet = a->body_mix * wet_lf[c] + a->clarity_mix * hf;
			wet_sumsq += wet * wet;
			a->out[2 * i + c] = dry[c] + wet;
		}
		if (!stereo)
			a->out[2 * i + 1] = dry[1];
	}

	// AetherSDR counts two channels per frame (mono duplicates left into right)
	wet_rms = sqrt (wet_sumsq / (a->size * nch));
	a->wet_rms_db = 20.0 * log10 (max (wet_rms, 1.0e-6));
}

void setBuffers_aethervoice (AETHERVOICE a, double* in, double* out)
{
	a->in = in;
	a->out = out;
}

void setSamplerate_aethervoice (AETHERVOICE a, int rate)
{
	a->samplerate = (double)rate;
	calc_aethervoice (a);
	flush_aethervoice (a);
}

void setSize_aethervoice (AETHERVOICE a, int size)
{
	a->size = size;
}

/********************************************************************************************************
*																										*
*										RXA Properties													*
*																										*
********************************************************************************************************/

PORT
void SetRXAAetherVoiceRun (int channel, int run)
{
	AETHERVOICE a;
	EnterCriticalSection (&ch[channel].csDSP);
	a = rxa[channel].aethervoice.p;
	if (run && !a->run)
		flush_aethervoice (a);		// start from clean filter state
	a->run = run;
	LeaveCriticalSection (&ch[channel].csDSP);
}

// RMS of what the exciter is adding, in dB (-120 when off); read without the lock, it is only a meter
PORT
double GetRXAAetherVoiceWetRms (int channel)
{
	return rxa[channel].aethervoice.p->wet_rms_db;
}

PORT
void SetRXAAetherVoiceMode (int channel, int mode)
{
	EnterCriticalSection (&ch[channel].csDSP);
	rxa[channel].aethervoice.p->mode = mode ? 1 : 0;
	LeaveCriticalSection (&ch[channel].csDSP);
}

PORT
void SetRXAAetherVoiceBody (int channel, double drive_db, double tune_hz, double mix)
{
	AETHERVOICE a;
	EnterCriticalSection (&ch[channel].csDSP);
	a = rxa[channel].aethervoice.p;
	a->body_drive_db = av_clamp (drive_db, 0.0, 24.0);
	a->body_tune_hz = av_clamp (tune_hz, 50.0, 160.0);
	a->body_mix = av_clamp (mix, 0.0, 1.0);
	calc_aethervoice (a);
	LeaveCriticalSection (&ch[channel].csDSP);
}

PORT
void SetRXAAetherVoiceClarity (int channel, double tune_hz, double harmonics_db, double mix)
{
	AETHERVOICE a;
	EnterCriticalSection (&ch[channel].csDSP);
	a = rxa[channel].aethervoice.p;
	a->clarity_tune_hz = av_clamp (tune_hz, 1000.0, 10000.0);
	a->clarity_harmonics_db = av_clamp (harmonics_db, 0.0, 24.0);
	a->clarity_mix = av_clamp (mix, 0.0, 1.0);
	calc_aethervoice (a);
	LeaveCriticalSection (&ch[channel].csDSP);
}

/********************************************************************************************************
*																										*
*							AetherSDR channel strip (aetherstrip.cpp): TXA access						*
*																										*
********************************************************************************************************/

PORT
void SetRXAStripParam (int channel, int stage, int param, double value)
{
	setParam_aetherstrip (rxa[channel].aetherstrip.p, stage, param, value);
}

PORT
double GetRXAStripMeter (int channel, int stage, int meter)
{
	return getMeter_aetherstrip (rxa[channel].aetherstrip.p, stage, meter);
}

// chain order (see aetherstrip.h); changed under the channel's DSP lock so a block never sees half an order
PORT
int SetTXAStripOrder (int channel, int* order, int n)
{
	int ok;
	EnterCriticalSection (&ch[channel].csDSP);
	ok = setOrder_aetherstrip (txa[channel].aetherstrip.p, order, n);
	LeaveCriticalSection (&ch[channel].csDSP);
	return ok;
}

PORT
int SetRXAStripOrder (int channel, int* order, int n)
{
	int ok;
	EnterCriticalSection (&ch[channel].csDSP);
	ok = setOrder_aetherstrip (rxa[channel].aetherstrip.p, order, n);
	LeaveCriticalSection (&ch[channel].csDSP);
	return ok;
}

// AetherSDR's EQ band response, for drawing the EQ curve (no channel needed)
PORT
double GetAetherEqBandMagnitudeDb (int type, double freq, double gain, double q, int on, int slope,
	int family, double probeHz, double samplerate)
{
	return aetherstrip_eqBandMagnitudeDb (type, freq, gain, q, on, slope, family, probeHz, samplerate);
}

// stage / param / meter numbers are listed in aetherstrip.h; AetherSDR's setters are lock-free
PORT
void SetTXAStripParam (int channel, int stage, int param, double value)
{
	setParam_aetherstrip (txa[channel].aetherstrip.p, stage, param, value);
}

PORT
double GetTXAStripMeter (int channel, int stage, int meter)
{
	return getMeter_aetherstrip (txa[channel].aetherstrip.p, stage, meter);
}

/********************************************************************************************************
*																										*
*										TXA Properties													*
*																										*
********************************************************************************************************/

PORT
void SetTXAAetherVoiceRun (int channel, int run)
{
	AETHERVOICE a;
	EnterCriticalSection (&ch[channel].csDSP);
	a = txa[channel].aethervoice.p;
	if (run && !a->run)
		flush_aethervoice (a);
	a->run = run;
	LeaveCriticalSection (&ch[channel].csDSP);
}

PORT
double GetTXAAetherVoiceWetRms (int channel)
{
	return txa[channel].aethervoice.p->wet_rms_db;
}

PORT
void SetTXAAetherVoiceMode (int channel, int mode)
{
	EnterCriticalSection (&ch[channel].csDSP);
	txa[channel].aethervoice.p->mode = mode ? 1 : 0;
	LeaveCriticalSection (&ch[channel].csDSP);
}

PORT
void SetTXAAetherVoiceBody (int channel, double drive_db, double tune_hz, double mix)
{
	AETHERVOICE a;
	EnterCriticalSection (&ch[channel].csDSP);
	a = txa[channel].aethervoice.p;
	a->body_drive_db = av_clamp (drive_db, 0.0, 24.0);
	a->body_tune_hz = av_clamp (tune_hz, 50.0, 160.0);
	a->body_mix = av_clamp (mix, 0.0, 1.0);
	calc_aethervoice (a);
	LeaveCriticalSection (&ch[channel].csDSP);
}

PORT
void SetTXAAetherVoiceClarity (int channel, double tune_hz, double harmonics_db, double mix)
{
	AETHERVOICE a;
	EnterCriticalSection (&ch[channel].csDSP);
	a = txa[channel].aethervoice.p;
	a->clarity_tune_hz = av_clamp (tune_hz, 1000.0, 10000.0);
	a->clarity_harmonics_db = av_clamp (harmonics_db, 0.0, 24.0);
	a->clarity_mix = av_clamp (mix, 0.0, 1.0);
	calc_aethervoice (a);
	LeaveCriticalSection (&ch[channel].csDSP);
}
