/*  kntap.h

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

#ifndef _kntap_h
#define _kntap_h

// Kainos auto-notch tap: a copy of a receiver's I/Q at the DSP rate, taken just before the notch filter (nbp0),
// so the console can find steady carriers even after they have been notched. The console reads it and sets the
// notches through the usual RXANBP* calls.

extern void xkntap (int channel);

extern __declspec (dllexport) void KainosNotchTapRun (int channel, int on);
extern __declspec (dllexport) int KainosNotchTapRead (int channel, float* dst, int n);
extern __declspec (dllexport) void KainosNotchTapInfo (int channel, double* info);

#endif
