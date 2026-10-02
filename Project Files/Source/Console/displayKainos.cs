/*  displayKainos.cs

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

using System.Drawing;
using SharpDX.Direct2D1;

namespace Thetis
{
    // Kainos's drawing on the panadapter (one call from DrawPanadapterDX2D in display.cs)
    partial class Display
    {
        // The RTTY terminal's mark and space tones on its receiver's panadapter: a gold line for mark and an ice line
        // for space, labelled near the bottom (the flag and the scale are at the top), where the decoder (with AFC, where it has moved them) is listening
        private static void drawKainosDigiMarkersDX2D(int rx, int W, int H, int nVerticalShift)
        {
            if (console == null || W <= 0 || _d2dRenderTarget == null) return;
            double[] tones;
            try { tones = console.KainosDigiMarkers(rx); } catch { return; }
            if (tones == null || console.MOX) return;

            int low = rx == 1 ? rx_display_low : rx2_display_low;
            int high = rx == 1 ? rx_display_high : rx2_display_high;
            int fDiff = rx == 1 ? freq_diff : rx2_freq_diff;
            float width = high - low;
            if (width <= 0) return;

            SharpDX.Direct2D1.Brush[] brushes =
            {
                getDXBrushForColour(Color.FromArgb(0xd4, 0xad, 0x6a), 220),     // mark: Kainos gold
                getDXBrushForColour(Color.FromArgb(0x7f, 0xb0, 0xcc), 220),     // space: Kainos ice
            };
            string[] labels = { "M", "S" };
            float top = nVerticalShift + 18, bottom = nVerticalShift + H;
            float[] xs = new float[2];
            for (int i = 0; i < 2 && i < tones.Length; i++) xs[i] = (float)((tones[i] - low - fDiff) / width * W);
            for (int i = 0; i < tones.Length && i < 2; i++)
            {
                if (brushes[i] == null) continue;
                float x = xs[i];
                if (x < 0 || x > W) continue;
                drawLineDX2D(brushes[i], x, top, x, bottom, 2f);
                // each label on the outer side of its line, so the two don't run together
                bool leftSide = x < xs[1 - i];
                float ly = bottom - 40, lx = leftSide ? x - 13 : x + 3;
                _d2dRenderTarget.DrawText(labels[i], fontDX2d_callout, new SharpDX.Mathematics.Interop.RawRectangleF(lx, ly, lx + 16, ly + 16), brushes[i], DrawTextOptions.None);
            }
        }
    }
}
