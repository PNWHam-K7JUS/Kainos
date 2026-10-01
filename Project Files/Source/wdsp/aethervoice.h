/*  aethervoice.h

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

#ifndef _aethervoice_h
#define _aethervoice_h

// AetherVoice exciter: two parallel bands mixed back into the dry signal.
//   Body    (low band):  LPF, then Aphex "Big Bottom" soft saturation with a
//                        dynamic boost, or a Behringer-style 4:1 compressor and
//                        all-pass phase rotator.
//   Clarity (high band): HPF, then drive into an asymmetric (Aphex) or
//                        symmetric (Behringer) tanh soft-clipper.
// AetherSDR calls the bands "Poo" (Body) and "Doo" (Clarity).

typedef struct _aethervoice
{
	int run;
	int size;						// complex samples per block
	double* in;
	double* out;
	double samplerate;

	// user parameters
	int mode;						// 0 = Aphex, 1 = Behringer
	double body_drive_db;			// 0 .. 24 dB
	double body_tune_hz;			// 50 .. 160 Hz, LPF corner
	double body_mix;				// 0 .. 1
	double clarity_tune_hz;			// 1000 .. 10000 Hz, HPF corner
	double clarity_harmonics_db;	// 0 .. 24 dB
	double clarity_mix;				// 0 .. 1

	// derived (calc_aethervoice)
	double hpf[5];					// biquad b0, b1, b2, a1, a2
	double lpf[5];
	double allpass[5];
	double body_drive_lin;
	double clarity_drive_lin;
	double env_attack;
	double env_release;

	// state, per channel: [0] = I (left), [1] = Q (right)
	double hpf_z[2][2];
	double lpf_z[2][2];
	double allpass_z[2][2];
	double dc_x1[2];
	double dc_y1[2];
	double lf_env;					// shared LF envelope follower
} aethervoice, *AETHERVOICE;

extern AETHERVOICE create_aethervoice (int run, int size, double* in, double* out, int samplerate);

extern void destroy_aethervoice (AETHERVOICE a);

extern void flush_aethervoice (AETHERVOICE a);

// stereo = 0: process I only (Q passes through); stereo = 1: process I and Q as left and right
extern void xaethervoice (AETHERVOICE a, int stereo);

extern void setBuffers_aethervoice (AETHERVOICE a, double* in, double* out);

extern void setSamplerate_aethervoice (AETHERVOICE a, int rate);

extern void setSize_aethervoice (AETHERVOICE a, int size);

#endif
