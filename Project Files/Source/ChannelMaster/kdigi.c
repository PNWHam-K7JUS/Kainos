/*  kdigi.c

    Kainos digital-mode audio taps (RTTY, later CW). See kdigi.h.

    This file is part of Kainos, a program that implements a Software-Defined Radio.
    Kainos is based on Thetis : https://github.com/ramdor/Thetis

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

#include "cmcomm.h"

/* Each ring has one writer and one reader (the audio thread and a console thread), so free-running 32-bit
   counters with interlocked reads and writes are enough: the reader never takes more than was written, and a
   reader that falls more than a ring behind skips to the newest data. */

#define KDIGI_NRX   2
#define KDIGI_RING  (1 << 17)               /* 131072 samples, ~2.7 s at 48 kHz */
#define KDIGI_MASK  (KDIGI_RING - 1)

static float         rx_ring[KDIGI_NRX][KDIGI_RING];
static volatile LONG rx_w[KDIGI_NRX], rx_r[KDIGI_NRX];
static volatile LONG rx_on[KDIGI_NRX];
static volatile LONG rx_rate[KDIGI_NRX];

static float         tx_ring[KDIGI_RING];
static volatile LONG tx_w, tx_r;
static volatile LONG tx_on;
static volatile LONG tx_clear;              /* set by the console, done by the audio thread (the ring's reader) */

void xkdigi_rx(int rx, double* audio)
{
    int i, n;
    LONG w;
    if (rx < 0 || rx >= KDIGI_NRX || !pcm) return;
    if (!_InterlockedAnd(&rx_on[rx], 1)) return;
    n = pcm->rcvr[rx].ch_outsize;
    if (n <= 0) return;
    InterlockedExchange(&rx_rate[rx], pcm->rcvr[rx].ch_outrate);
    w = InterlockedCompareExchange(&rx_w[rx], 0, 0);
    for (i = 0; i < n; i++)
        rx_ring[rx][(w + i) & KDIGI_MASK] = (float)audio[2 * i];
    InterlockedExchange(&rx_w[rx], w + n);
}

PORT void KDigiRxTap(int rx, int on)
{
    if (rx < 0 || rx >= KDIGI_NRX) return;
    if (on) InterlockedExchange(&rx_r[rx], InterlockedCompareExchange(&rx_w[rx], 0, 0));     /* start from now */
    InterlockedExchange(&rx_on[rx], on ? 1 : 0);
}

PORT int KDigiRxRate(int rx)
{
    if (rx < 0 || rx >= KDIGI_NRX) return 0;
    if (pcm && pcm->rcvr[rx].ch_outrate > 0) return pcm->rcvr[rx].ch_outrate;
    return (int)InterlockedCompareExchange(&rx_rate[rx], 0, 0);
}

PORT int KDigiRxRead(int rx, float* dst, int max)
{
    LONG w, r, have;
    int i;
    if (rx < 0 || rx >= KDIGI_NRX || !dst || max <= 0) return 0;
    w = InterlockedCompareExchange(&rx_w[rx], 0, 0);
    r = InterlockedCompareExchange(&rx_r[rx], 0, 0);
    have = w - r;
    if (have > KDIGI_RING - 4096) { r = w - (KDIGI_RING - 4096); have = w - r; }     /* fell behind: newest data */
    if (have > max) have = max;
    for (i = 0; i < have; i++) dst[i] = rx_ring[rx][(r + i) & KDIGI_MASK];
    InterlockedExchange(&rx_r[rx], r + have);
    return (int)have;
}

void xkdigi_tx(double* mic_io)
{
    int i, n;
    LONG w, r;
    if (!pcm || !_InterlockedAnd(&tx_on, 1)) return;
    n = pcm->xcm_insize[inid(1, 0)];
    if (n <= 0) return;
    w = InterlockedCompareExchange(&tx_w, 0, 0);
    r = InterlockedCompareExchange(&tx_r, 0, 0);
    if (InterlockedExchange(&tx_clear, 0)) r = w;
    for (i = 0; i < n; i++)
    {
        float s = 0.0f;
        if (r != w) { s = tx_ring[r & KDIGI_MASK]; r++; }
        mic_io[2 * i] = (double)s;
        mic_io[2 * i + 1] = 0.0;
    }
    InterlockedExchange(&tx_r, r);
}

PORT void KDigiTxEnable(int on)
{
    InterlockedExchange(&tx_on, on ? 1 : 0);
}

PORT int KDigiTxRate(void)
{
    if (!pcm) return 0;
    return pcm->xcm_inrate[inid(1, 0)];
}

PORT int KDigiTxWrite(const float* src, int n)
{
    LONG w, r, room;
    int i;
    if (!src || n <= 0) return 0;
    w = InterlockedCompareExchange(&tx_w, 0, 0);
    r = InterlockedCompareExchange(&tx_r, 0, 0);
    room = (KDIGI_RING - 1) - (w - r);
    if (n > room) n = (int)room;
    for (i = 0; i < n; i++) tx_ring[(w + i) & KDIGI_MASK] = src[i];
    InterlockedExchange(&tx_w, w + n);
    return n;
}

PORT int KDigiTxQueued(void)
{
    return (int)(InterlockedCompareExchange(&tx_w, 0, 0) - InterlockedCompareExchange(&tx_r, 0, 0));
}

PORT void KDigiTxClear(void)
{
    if (_InterlockedAnd(&tx_on, 1)) InterlockedExchange(&tx_clear, 1);       /* the audio thread empties it */
    else InterlockedExchange(&tx_r, InterlockedCompareExchange(&tx_w, 0, 0));  /* nothing is reading it */
}
