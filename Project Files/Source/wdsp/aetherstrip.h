/*  aetherstrip.h

This file is part of Kainos, a program that implements a Software-Defined Radio.

The AetherSDR channel strip in WDSP: AetherSDR's own processors (wdsp/aethersdr/, from
https://github.com/aethersdr/AetherSDR, commit 20d022d5, by the AetherSDR contributors) driven
from a WDSP channel. Copyright (C) 2026 Justin Cron K7JUS.

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

#ifndef _aetherstrip_h
#define _aetherstrip_h

// Stages. The strip runs in two parts around the AetherVoice exciter (aethervoice.c), in
// AetherSDR's chain orders:
//   transmit  xaetherstrip_pre: gate, EQ, de-esser, compressor, tube   xaetherstrip_post: reverb, final limiter
//   receive   xaetherstrip_pre: EQ, gate, compressor, tube             (AetherRX has no post stages)
#define AS_GATE			0
#define AS_DEESS		1
#define AS_COMP			2
#define AS_TUBE			3
#define AS_REVERB		4
#define AS_LIMITER		5
#define AS_EQ			6
#define AS_NSTAGES		7

#define AS_EQ_BANDS		10			// AetherSDR's default 10-band layout (ClientEq supports 16)
#define AS_EQ_BAND0		10			// first band parameter

// Parameters, per stage. 0 is always 'enabled' (0/1). Units and ranges are AetherSDR's.
//   gate:    1 mode (0 expander, 1 gate), 2 threshold dB, 3 ratio, 4 attack ms, 5 release ms,
//            6 hold ms, 7 floor dB, 8 return dB, 9 lookahead ms
//   deess:   1 frequency Hz, 2 Q, 3 threshold dB, 4 amount dB, 5 attack ms, 6 release ms, 7 slope stages
//   comp:    1 threshold dB, 2 ratio, 3 attack ms, 4 release ms, 5 knee dB, 6 makeup dB,
//            7 limiter on, 8 limiter ceiling dB, 9 drive dB, 10 phase rotator stages
//   tube:    1 model (0 A, 1 B, 2 C), 2 drive dB, 3 bias, 4 tone, 5 output dB, 6 dry/wet,
//            7 envelope amount, 8 release ms
//   reverb:  1 size, 2 decay s, 3 damping, 4 pre-delay ms, 5 mix
//   limiter: 1 ceiling dB, 2 output trim dB, 3 DC block on
//   eq:      1 master gain dB, 2 filter family (0 Butterworth, 1 Chebyshev, 2 Bessel, 3 Elliptic),
//            band b (0..9): AS_EQ_BAND0 + 6*b + { 0 frequency Hz, 1 gain dB, 2 Q,
//            3 type (0 peak, 1 low shelf, 2 high shelf, 3 low pass, 4 high pass), 4 on, 5 slope dB/oct }
// Meters (dB):
//   gate 0 gain reduction; deess 0 gain reduction; comp 0 gain reduction, 1 limiter gain reduction;
//   tube 0 drive applied; reverb 0 wet RMS; limiter 0 gain reduction, 1 output peak, 2 output RMS;
//   every stage: 10 input peak, 11 output peak

typedef struct _aetherstrip* AETHERSTRIP;

#ifdef __cplusplus
extern "C" {
#endif

// rx: 0 = transmit chain order, 1 = receive chain order
extern AETHERSTRIP create_aetherstrip (int size, double* in, double* out, int samplerate, int rx);

extern void destroy_aetherstrip (AETHERSTRIP a);

extern void flush_aetherstrip (AETHERSTRIP a);

// stereo = 0: process I only (Q passes through); stereo = 1: I and Q as left and right
extern void xaetherstrip_pre (AETHERSTRIP a, int stereo);

extern void xaetherstrip_post (AETHERSTRIP a, int stereo);

extern void setBuffers_aetherstrip (AETHERSTRIP a, double* in, double* out);

extern void setSamplerate_aetherstrip (AETHERSTRIP a, int rate);

extern void setSize_aetherstrip (AETHERSTRIP a, int size);

extern void setParam_aetherstrip (AETHERSTRIP a, int stage, int param, double value);

extern double getMeter_aetherstrip (AETHERSTRIP a, int stage, int meter);

// AetherSDR's ClientEq::bandMagnitudeDb: one band's response in dB at a probe frequency (for drawing)
extern double aetherstrip_eqBandMagnitudeDb (int type, double freq, double gain, double q, int on, int slope,
	int family, double probeHz, double samplerate);

#ifdef __cplusplus
}
#endif

#endif
