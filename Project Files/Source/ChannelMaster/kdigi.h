/*  kdigi.h

    Kainos digital-mode audio taps (RTTY, later CW): a copy of each receiver's demodulated audio for the
    console's decoders, and a transmit feed that replaces the mic audio with the console's modulator output.

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

#ifndef _kdigi_h
#define _kdigi_h

#ifndef PORT
#define PORT __declspec(dllexport)
#endif

#ifdef __cplusplus
extern "C" {
#endif

/* splices, called from xpipe (pipe.c) */
void xkdigi_rx(int rx, double* audio);      /* receiver audio (interleaved L/R doubles), read only */
void xkdigi_tx(double* mic_io);             /* mic audio (interleaved I/Q doubles), replaced while the feed is on */

/* receive tap: the left channel as floats, in a ring the console reads */
PORT void KDigiRxTap(int rx, int on);
PORT int  KDigiRxRate(int rx);
PORT int  KDigiRxRead(int rx, float* dst, int max);

/* transmit feed: the console writes samples at KDigiTxRate(); while on, the mic audio is replaced by them
   (silence when the ring runs dry) */
PORT void KDigiTxEnable(int on);
PORT int  KDigiTxRate(void);
PORT int  KDigiTxWrite(const float* src, int n);
PORT int  KDigiTxQueued(void);
PORT void KDigiTxClear(void);

#ifdef __cplusplus
}
#endif

#endif
