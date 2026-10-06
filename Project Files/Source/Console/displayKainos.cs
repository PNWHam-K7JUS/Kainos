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

        // The band plan (KainosBandPlan.cs): a band along the bottom of the panadapter (the top has the scale, the
        // slice flags and the spot tags). Its colour is the operator's privileges; its labels what each part of the
        // band is used for (CW, DIGITAL, SSB ...); ticks above it mark the popular spot frequencies (FT8, FT4 ...).
        // Not while transmitting.
        private static void drawKainosBandPlanDX2D(int rx, int W, int H, int nVerticalShift)
        {
            if (console == null || W <= 0 || _d2dRenderTarget == null || console.MOX) return;
            List<KeyValuePair<float[], KainosBandPlan.Kind>> spans;
            try { spans = console.KainosBandPlanSpans(rx, W); } catch { return; }
            if (spans == null || spans.Count == 0) return;
            float y1 = nVerticalShift + H, y0 = y1 - 20;

            // the privileges: the band's colour (gold all modes, ice CW and data, violet CW only, red not yours, grey
            // band edges only), see-through, with a solid top edge
            foreach (KeyValuePair<float[], KainosBandPlan.Kind> sp in spans)
            {
                Color c = bandPlanColour(sp.Value);
                SharpDX.Direct2D1.Brush fill = getDXBrushForColour(c, 60), edge = getDXBrushForColour(c, 230);
                if (fill == null || edge == null) continue;
                _d2dRenderTarget.FillRectangle(new RawRectangleF(sp.Key[0], y0, sp.Key[1], y1), fill);
                _d2dRenderTarget.FillRectangle(new RawRectangleF(sp.Key[0], y0, sp.Key[1], y0 + 2), edge);
            }

            // what each part of the band is used for: a divider where it starts, and its name in white ("(not yours)"
            // added where it's outside the privileges), the longest wording that fits
            SharpDX.Direct2D1.Brush ink = getDXBrushForColour(Color.FromArgb(0xe6, 0xee, 0xf6), 235), divider = getDXBrushForColour(Color.White, 110);
            List<Tuple<float, float, string, bool>> uses = null;
            try { uses = console.KainosBandUseSpans(rx, W); } catch { }
            if (uses != null && ink != null && divider != null)
                foreach (Tuple<float, float, string, bool> u in uses)
                {
                    float x0 = Math.Max(0, u.Item1), x1 = Math.Min(W, u.Item2);
                    if (u.Item1 >= 0) drawLineDX2D(divider, u.Item1 + 0.5f, y0, u.Item1 + 0.5f, y1, 1f);
                    string shortName = bandPlanShort(u.Item3);
                    string[] text = u.Item4 ? new[] { u.Item3 + " (not yours)", u.Item3, shortName } : new[] { u.Item3, shortName };
                    foreach (string t in text)
                    {
                        SizeF sz = measureStringDX2D(t, fontDX2d_font9b, cacheStringLength: true);
                        if (sz.Width + 10 > x1 - x0) continue;
                        float lx = x0 + (x1 - x0 - sz.Width) / 2, ly = y0 + 2 + (y1 - y0 - 2 - sz.Height) / 2;
                        _d2dRenderTarget.DrawText(t, fontDX2d_font9b, new RawRectangleF(lx, ly, lx + sz.Width + 4, ly + sz.Height + 2), ink, DrawTextOptions.None);
                        break;
                    }
                }

            // the popular spot frequencies (FT8, FT4, WSPR, PSK31, SSTV, AM, QRP): a tick and a tag just above the band
            // (a tag that would run into the one before is left out)
            List<KeyValuePair<float, string>> spots = null;
            try { spots = console.KainosBandSpots(rx, W); } catch { }
            SharpDX.Direct2D1.Brush tag = getDXBrushForColour(Color.FromArgb(0x7f, 0xb0, 0xcc), 240);
            if (spots != null && tag != null)
            {
                float lastRight = -1000;
                foreach (KeyValuePair<float, string> sp in spots)
                {
                    drawLineDX2D(tag, sp.Key, y0 - 7, sp.Key, y0, 1.5f);
                    SizeF sz = measureStringDX2D(sp.Value, fontDX2d_font9b, cacheStringLength: true);
                    float lx = sp.Key - sz.Width / 2;
                    if (lx < lastRight + 4) continue;
                    _d2dRenderTarget.DrawText(sp.Value, fontDX2d_font9b, new RawRectangleF(lx, y0 - 8 - sz.Height, lx + sz.Width + 4, y0 - 6), tag, DrawTextOptions.None);
                    lastRight = lx + sz.Width;
                }
            }
        }

        private static Color bandPlanColour(KainosBandPlan.Kind k)
        {
            switch (k)
            {
                case KainosBandPlan.Kind.AllModes: return Color.FromArgb(0xd4, 0xad, 0x6a);     // Kainos gold
                case KainosBandPlan.Kind.CwData: return Color.FromArgb(0x7f, 0xb0, 0xcc);       // Kainos ice
                case KainosBandPlan.Kind.CwOnly: return Color.FromArgb(0x9a, 0x86, 0xd8);       // Kainos violet
                case KainosBandPlan.Kind.NotYours: return Color.FromArgb(0xc0, 0x40, 0x40);
                default: return Color.FromArgb(0x60, 0x70, 0x80);
            }
        }

        private static string bandPlanShort(string name)
        {
            switch (name)
            {
                case "DIGITAL": return "DIGI";
                case "BEACONS": return "BCN";
                case "SATELLITE": return "SAT";
                case "CW / DIGITAL": return "CW/DIGI";
                case "CW / BEACONS": return "CW/BCN";
                case "DIGITAL / OTHER": return "DIGI";
                case "FM / OTHER": return "FM";
                default: return name;
            }
        }

        // ---- the 3D stacked-trace panadapter (AetherSDR's) ----
        //
        // Behind the live trace, the last few seconds of traces stacked back into the distance: each older one up and
        // to the right a step and dimmer, filled with the background so nearer traces hide the ones behind them.
        // Speed: a trace is turned into a Direct2D geometry once, when it's taken (Rate a second, not every frame),
        // and the whole stack is drawn then into an offscreen image; each frame only pastes that image (Direct2D
        // re-tessellates filled geometries on the CPU every time they're drawn, so drawing them every frame cost
        // about a third of the CPU). Points are taken every
        // third pixel, lightly smoothed (peaks kept). The history is cleared when the span, size, scale or TX/RX changes,
        // since the old traces no longer line up.

        public static bool Kainos3D = false;
        public static int Kainos3DDepth = 40;           // traces in the stack
        public static int Kainos3DRate = 10;            // traces taken a second
        public static float Kainos3DHeight = 0.35f;     // how far up the stack reaches, as part of the panadapter's height
        public static bool Kainos3DWaterfallColours = true;     // the lines in the waterfall's colours by strength (or Kainos ice)

        private class K3DTrace { public PathGeometry Geometry; }
        private class K3DStack
        {
            public readonly List<K3DTrace> Traces = new List<K3DTrace>();     // newest first
            public string Key = "";
            public long LastTakenMs = -100000;
            public BitmapRenderTarget Image;        // the stack drawn once per new trace, pasted every frame
            public RenderTarget ImageOwner;         // the render target it was made for (Thetis remakes its own at times)
            public SharpDX.Direct2D1.Factory GeometryFactory;
        }
        private static readonly K3DStack[] _k3d = { new K3DStack(), new K3DStack() };
        private static readonly Stopwatch _k3dClock = Stopwatch.StartNew();

        private static void k3dClear(K3DStack st)
        {
            foreach (K3DTrace t in st.Traces) t.Geometry?.Dispose();
            st.Traces.Clear();
            st.Image?.Dispose();
            st.Image = null;
            st.ImageOwner = null;
        }

        // called before the live trace is drawn (it takes the traces and redraws the stack; pasteKainos3DStack shows
        // it); data are the trace's dBm per decimated pixel
        private static void drawKainos3DStack(int rx, int W, int H, int nVerticalShift, float[] data, int nDecimatedWidth, int decimation,
                                              float fOffset, int grid_max, int grid_min, float dbmToPixel, bool local_mox)
        {
            K3DStack st = _k3d[rx == 1 ? 0 : 1];
            bool powered = console != null && console.PowerOn;      // off: nothing new is coming, so no stack
            if (!Kainos3D || !powered || _d2dRenderTarget == null || _d2dFactory == null || W <= 0 || H <= 0 || data == null)
            {
                if (st.Traces.Count > 0 || st.Image != null) k3dClear(st);
                return;
            }

            int low = rx == 1 ? rx_display_low : rx2_display_low, high = rx == 1 ? rx_display_high : rx2_display_high;
            string key = W + "/" + H + "/" + low + "/" + high + "/" + grid_max + "/" + grid_min + "/" + local_mox + "/" + decimation;
            if (key != st.Key || st.GeometryFactory != _d2dFactory || (st.ImageOwner != null && st.ImageOwner != _d2dRenderTarget))
            {
                k3dClear(st);
                st.Key = key;
                st.GeometryFactory = _d2dFactory;
            }

            // take a trace now and then (in the panadapter's own coordinates, 0 at its top), and draw the stack again
            long now = _k3dClock.ElapsedMilliseconds;
            if (now - st.LastTakenMs >= 1000 / Math.Max(1, Kainos3DRate))
            {
                st.LastTakenMs = now;
                PathGeometry g = new PathGeometry(_d2dFactory);
                using (GeometrySink sink = g.Open())
                {
                    sink.BeginFigure(new RawVector2(0, H), FigureBegin.Filled);
                    // a point every 3 pixels: half the average of the 5 around it (smooths the noise), half their
                    // highest (keeps the signals' peaks)
                    int step = Math.Max(1, 3 / Math.Max(1, decimation));
                    for (int i = 0; i < nDecimatedWidth; i += step)
                    {
                        float sum = 0, peak = float.MinValue;
                        int cnt = 0;
                        for (int j = Math.Max(0, i - 2); j <= Math.Min(nDecimatedWidth - 1, i + 2); j++) { sum += data[j]; if (data[j] > peak) peak = data[j]; cnt++; }
                        float v = 0.5f * (sum / cnt) + 0.5f * peak;
                        float y = (grid_max - (v + fOffset)) * dbmToPixel;
                        if (y > H) y = H;
                        sink.AddLine(new RawVector2(i * decimation, y));
                    }
                    sink.AddLine(new RawVector2(W, H));
                    sink.EndFigure(FigureEnd.Closed);
                    sink.Close();
                }
                st.Traces.Insert(0, new K3DTrace { Geometry = g });
                while (st.Traces.Count > Math.Max(2, Kainos3DDepth)) { st.Traces[st.Traces.Count - 1].Geometry?.Dispose(); st.Traces.RemoveAt(st.Traces.Count - 1); }
                // the waterfall's levels for this receiver (its AGC levels when waterfall AGC is on)
                float wfLow, wfHigh;
                if (rx == 1)
                {
                    wfHigh = waterfall_high_threshold;
                    wfLow = rx1_waterfall_agc && !m_bRX1_spectrum_thresholds ? _RX1waterfallPreviousMinValue - m_fWaterfallAGCOffsetRX1 : waterfall_low_threshold;
                }
                else
                {
                    wfHigh = rx2_waterfall_high_threshold;
                    wfLow = rx2_waterfall_agc && !m_bRX2_spectrum_thresholds ? _RX2waterfallPreviousMinValue - m_fWaterfallAGCOffsetRX2 : rx2_waterfall_low_threshold;
                }
                if (wfHigh - wfLow < 5) wfHigh = wfLow + 5;
                k3dRender(st, W, H, (grid_max - wfLow) * dbmToPixel, (grid_max - wfHigh) * dbmToPixel);
            }

        }

        // every frame, first thing in the panadapter (before Thetis's grid, filter shading and VFO / TX lines, so
        // they stay on top of the stack, as over the live trace): paste the stack
        private static void pasteKainos3DStack(int rx, int W, int H, int nVerticalShift)
        {
            K3DStack st = _k3d[rx == 1 ? 0 : 1];
            if (console == null || !console.PowerOn) { if (st.Traces.Count > 0 || st.Image != null) k3dClear(st); return; }
            if (!Kainos3D || _d2dRenderTarget == null) return;
            if (st.Image == null || st.ImageOwner != _d2dRenderTarget) return;
            using (SharpDX.Direct2D1.Bitmap b = st.Image.Bitmap)
                _d2dRenderTarget.DrawBitmap(b, new RawRectangleF(0, nVerticalShift, W, nVerticalShift + H), 1f, BitmapInterpolationMode.NearestNeighbor);
            // the stack's navy fill hides the backdrop's logo: put the logo back over it (it's faint, and the grid,
            // filter and trace still go on top)
            drawKainosBackdropLogo(W, H, nVerticalShift);
        }

        private static GradientStop[] k3dStops()
        {
            GradientStop[] g = new GradientStop[K3DStopPos.Length];
            for (int i = 0; i < g.Length; i++)
                g[i] = new GradientStop { Position = K3DStopPos[i], Color = new RawColor4(K3DStopRgb[i, 0] / 255f, K3DStopRgb[i, 1] / 255f, K3DStopRgb[i, 2] / 255f, 1f) };
            return g;
        }

        // draw the stack, back to front, into the offscreen image; the newest kept trace sits one step behind the live one
        // the waterfall's "enhanced" colours, from its low level (0) to its high level (1); a dim blue at the bottom
        // instead of the waterfall's background, so the noise floor's lines still show
        private static readonly float[] K3DStopPos = { 0f, 2f / 9, 3f / 9, 4f / 9, 5f / 9, 7f / 9, 8f / 9, 1f };
        private static readonly int[,] K3DStopRgb = { { 0, 0, 120 }, { 0, 0, 255 }, { 0, 255, 255 }, { 0, 255, 0 }, { 255, 255, 0 }, { 255, 0, 0 }, { 255, 0, 255 }, { 192, 124, 255 } };

        private static void k3dRender(K3DStack st, int W, int H, float yLow, float yHigh)
        {
            if (st.Image == null)
            {
                st.Image = new BitmapRenderTarget(_d2dRenderTarget, CompatibleRenderTargetOptions.None, new SharpDX.Size2F(W, H));
                st.ImageOwner = _d2dRenderTarget;
            }
            BitmapRenderTarget rt = st.Image;
            // filled with the background: the Kainos navy over the Kainos backdrop, else Thetis's background colour
            SharpDX.Color4 bgc = m_cDX2_display_background_clear_colour;
            RawColor4 fillColour = KainosBackdrop && console != null && console.KainosLayout
                ? new RawColor4(0x08 / 255f, 0x12 / 255f, 0x1d / 255f, 0.92f) : new RawColor4(bgc.Red, bgc.Green, bgc.Blue, 0.92f);
            using (SolidColorBrush fill = new SolidColorBrush(rt, fillColour))
            using (SolidColorBrush line = new SolidColorBrush(rt, new RawColor4(0x7f / 255f, 0xb0 / 255f, 0xcc / 255f, 1f)))
            using (GradientStopCollection stops = new GradientStopCollection(rt, k3dStops(), ExtendMode.Clamp))
            using (LinearGradientBrush rainbow = new LinearGradientBrush(rt, new LinearGradientBrushProperties { StartPoint = new RawVector2(0, yLow), EndPoint = new RawVector2(0, yHigh) }, stops))
            {
                rt.BeginDraw();
                rt.Clear(new RawColor4(0, 0, 0, 0));
                int n = st.Traces.Count;
                float dy = H * Kainos3DHeight / Math.Max(1, Kainos3DDepth), dx = Math.Max(1f, W * 0.0025f);
                for (int k = n - 1; k >= 0; k--)
                {
                    float depth = (k + 1) / (float)Math.Max(1, Kainos3DDepth);            // 0 near .. 1 far
                    rt.Transform = new RawMatrix3x2(1, 0, 0, 1, dx * (k + 1), -dy * (k + 1));
                    rt.FillGeometry(st.Traces[k].Geometry, fill);
                    // fading into the distance; the gradient moves with the trace (the transform applies to brushes
                    // too), so each line is coloured by its own levels
                    float fade = 1 - depth * 0.85f;
                    if (Kainos3DWaterfallColours)
                    {
                        rainbow.Opacity = 0.85f * fade;
                        rt.DrawGeometry(st.Traces[k].Geometry, rainbow, 1f);
                    }
                    else
                    {
                        line.Color = new RawColor4(0x7f / 255f, 0xb0 / 255f, 0xcc / 255f, 0.67f * (1 - depth * 0.9f));    // Kainos ice
                        rt.DrawGeometry(st.Traces[k].Geometry, line, 1f);
                    }
                }
                rt.Transform = new RawMatrix3x2(1, 0, 0, 1, 0, 0);
                rt.EndDraw();
            }
        }

        // ---- the Kainos backdrop (AetherSDR's look): a dark navy panadapter with the Kainos logo faint behind it ----
        //
        // Drawn first in the panadapter (before the 3D stack and Thetis's grid, filter and trace), over the panadapter's
        // own area only (so in panafall it stays out of the waterfall), in Kainos layout. The logo is the splash's
        // flame and lettering, cut out by brightness so only the bright parts show, at KainosBackdropLogo opacity.

        public static bool KainosBackdrop = true;
        public static float KainosBackdropLogo = 0.12f;
        private static SharpDX.Direct2D1.Bitmap _kbLogo;
        private static RenderTarget _kbLogoOwner;

        private static System.Drawing.Bitmap kainosLogoCutout()
        {
            System.Drawing.Bitmap splash = Properties.Resources.kainos_splash;
            Rectangle crop = new Rectangle(206, 48, 308, 178);          // the flame, KAINOΣ and the tag line
            System.Drawing.Bitmap logo = new System.Drawing.Bitmap(crop.Width, crop.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            for (int y = 0; y < crop.Height; y++)
                for (int x = 0; x < crop.Width; x++)
                {
                    Color c = splash.GetPixel(crop.X + x, crop.Y + y);
                    int lum = (c.R * 299 + c.G * 587 + c.B * 114) / 1000;
                    int a = lum <= 95 ? 0 : lum >= 185 ? 255 : (lum - 95) * 255 / 90;       // only the bright logo, not the waves behind it
                    logo.SetPixel(x, y, Color.FromArgb(a, c.R, c.G, c.B));
                }
            return logo;
        }

        private static void drawKainosBackdrop(int W, int H, int nVerticalShift)
        {
            if (!KainosBackdrop || console == null || !console.KainosLayout || _d2dRenderTarget == null || W <= 0 || H <= 0) return;

            // navy, a little lighter at the top
            RawRectangleF r = new RawRectangleF(0, nVerticalShift, W, nVerticalShift + H);
            using (GradientStopCollection stops = new GradientStopCollection(_d2dRenderTarget, new[]
                   {
                       new GradientStop { Position = 0f, Color = new RawColor4(0x0e / 255f, 0x1b / 255f, 0x29 / 255f, 1f) },
                       new GradientStop { Position = 1f, Color = new RawColor4(0x05 / 255f, 0x0b / 255f, 0x13 / 255f, 1f) },
                   }))
            using (LinearGradientBrush bg = new LinearGradientBrush(_d2dRenderTarget, new LinearGradientBrushProperties { StartPoint = new RawVector2(0, r.Top), EndPoint = new RawVector2(0, r.Bottom) }, stops))
                _d2dRenderTarget.FillRectangle(r, bg);

            drawKainosBackdropLogo(W, H, nVerticalShift);
        }

        // the faint logo, centred on the panadapter
        private static void drawKainosBackdropLogo(int W, int H, int nVerticalShift)
        {
            if (!KainosBackdrop || console == null || !console.KainosLayout || _d2dRenderTarget == null || W <= 0 || H <= 0) return;
            // the logo, made once per render target (Thetis remakes its render target at times)
            if (_kbLogo == null || _kbLogoOwner != _d2dRenderTarget)
            {
                if (_kbLogo != null) { _kbLogo.Dispose(); _kbLogo = null; }
                try
                {
                    using (System.Drawing.Bitmap logo = kainosLogoCutout())
                        _kbLogo = SDXBitmapFromSysBitmap(_d2dRenderTarget, logo);
                    _kbLogoOwner = _d2dRenderTarget;
                }
                catch { _kbLogo = null; }
            }
            if (_kbLogo == null || KainosBackdropLogo <= 0) return;

            // centred, about half the panadapter's height (and never wider than half its width)
            float lw = _kbLogo.PixelSize.Width, lh = _kbLogo.PixelSize.Height;
            float h = H * 0.5f, w = h * lw / lh;
            if (w > W * 0.5f) { w = W * 0.5f; h = w * lh / lw; }
            float x = (W - w) / 2, y = nVerticalShift + (H - h) / 2;
            _d2dRenderTarget.DrawBitmap(_kbLogo, new RawRectangleF(x, y, x + w, y + h), KainosBackdropLogo, BitmapInterpolationMode.Linear);
        }
    }
}
