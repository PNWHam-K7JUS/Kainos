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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;

namespace Thetis
{
    // Kainos's drawing on the panadapter (calls from DrawPanadapterDX2D in display.cs)
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

        // ---- the 3D stacked-trace panadapter (AetherSDR's) ----
        //
        // Behind the live trace, the last few seconds of traces stacked back into the distance: each older one up and
        // to the right a step and dimmer, filled with the background so nearer traces hide the ones behind them.
        // Speed: a trace is turned into a Direct2D geometry once, when it's taken (Rate a second, not every frame),
        // and kept; each frame only redraws the kept geometries, moved back by a transform. Points are taken every
        // other pixel (the higher of the two). The history is cleared when the span, size, scale or TX/RX changes,
        // since the old traces no longer line up.

        public static bool Kainos3D = false;
        public static int Kainos3DDepth = 40;           // traces in the stack
        public static int Kainos3DRate = 10;            // traces taken a second
        public static float Kainos3DHeight = 0.35f;     // how far up the stack reaches, as part of the panadapter's height

        private class K3DTrace { public PathGeometry Geometry; }
        private class K3DStack
        {
            public readonly List<K3DTrace> Traces = new List<K3DTrace>();     // newest first
            public string Key = "";
            public long LastTakenMs = -100000;
        }
        private static readonly K3DStack[] _k3d = { new K3DStack(), new K3DStack() };
        private static readonly Stopwatch _k3dClock = Stopwatch.StartNew();

        private static void k3dClear(K3DStack st)
        {
            foreach (K3DTrace t in st.Traces) t.Geometry?.Dispose();
            st.Traces.Clear();
        }

        // called before the live trace is drawn; data are the trace's dBm per decimated pixel
        private static void drawKainos3DStack(int rx, int W, int H, int nVerticalShift, float[] data, int nDecimatedWidth, int decimation,
                                              float fOffset, int grid_max, int grid_min, float dbmToPixel, bool local_mox)
        {
            K3DStack st = _k3d[rx == 1 ? 0 : 1];
            if (!Kainos3D || _d2dRenderTarget == null || _d2dFactory == null || W <= 0 || H <= 0 || data == null)
            {
                if (st.Traces.Count > 0) k3dClear(st);
                return;
            }

            int low = rx == 1 ? rx_display_low : rx2_display_low, high = rx == 1 ? rx_display_high : rx2_display_high;
            string key = W + "/" + H + "/" + nVerticalShift + "/" + low + "/" + high + "/" + grid_max + "/" + grid_min + "/" + local_mox + "/" + decimation;
            if (key != st.Key) { k3dClear(st); st.Key = key; }

            // take a trace now and then
            long now = _k3dClock.ElapsedMilliseconds;
            if (now - st.LastTakenMs >= 1000 / Math.Max(1, Kainos3DRate))
            {
                st.LastTakenMs = now;
                PathGeometry g = new PathGeometry(_d2dFactory);
                using (GeometrySink sink = g.Open())
                {
                    float bottom = nVerticalShift + H;
                    sink.BeginFigure(new RawVector2(0, bottom), FigureBegin.Filled);
                    int step = Math.Max(1, 2 / Math.Max(1, decimation));
                    for (int i = 0; i < nDecimatedWidth; i += step)
                    {
                        float v = data[i];
                        if (step > 1 && i + 1 < nDecimatedWidth && data[i + 1] > v) v = data[i + 1];
                        float y = (grid_max - (v + fOffset)) * dbmToPixel + nVerticalShift;
                        if (y > bottom) y = bottom;
                        sink.AddLine(new RawVector2(i * decimation, y));
                    }
                    sink.AddLine(new RawVector2(W, bottom));
                    sink.EndFigure(FigureEnd.Closed);
                    sink.Close();
                }
                st.Traces.Insert(0, new K3DTrace { Geometry = g });
                while (st.Traces.Count > Math.Max(2, Kainos3DDepth)) { st.Traces[st.Traces.Count - 1].Geometry?.Dispose(); st.Traces.RemoveAt(st.Traces.Count - 1); }
            }

            // draw them back to front; the newest kept trace sits one step behind the live one
            int n = st.Traces.Count;
            if (n == 0) return;
            float dy = H * Kainos3DHeight / Math.Max(1, Kainos3DDepth), dx = Math.Max(1f, W * 0.0025f);
            SharpDX.Color4 bgc = m_cDX2_display_background_clear_colour;
            SharpDX.Direct2D1.Brush fill = getDXBrushForColour(Color.FromArgb(235, (int)(bgc.Red * 255), (int)(bgc.Green * 255), (int)(bgc.Blue * 255)));
            if (fill == null) return;
            RawMatrix3x2 saved = _d2dRenderTarget.Transform;
            try
            {
                for (int k = n - 1; k >= 0; k--)
                {
                    float depth = (k + 1) / (float)Math.Max(1, Kainos3DDepth);            // 0 near .. 1 far
                    RawMatrix3x2 t = saved;
                    t.M31 += dx * (k + 1);
                    t.M32 -= dy * (k + 1);
                    _d2dRenderTarget.Transform = t;
                    _d2dRenderTarget.FillGeometry(st.Traces[k].Geometry, fill);
                    // Kainos ice, fading into the distance
                    int a = (int)(220 * (1 - depth * 0.85f));
                    SharpDX.Direct2D1.Brush line = getDXBrushForColour(Color.FromArgb(Math.Max(20, a & 0xF0), 0x7f, 0xb0, 0xcc));
                    if (line != null) _d2dRenderTarget.DrawGeometry(st.Traces[k].Geometry, line, 1f);
                }
            }
            finally
            {
                _d2dRenderTarget.Transform = saved;
            }
        }
    }
}
