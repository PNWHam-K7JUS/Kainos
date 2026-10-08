/*  KainosAudioScope.cs

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
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Thetis
{
    // The AUDIO SCOPE tab's audio: ChannelMaster's scope tap (the one Thetis's Scope display uses), which carries
    // RX1's audio (with its sub-receiver, so LISTEN too) and, while transmitting, the transmit signal. DoScope.xscope
    // (cmaster.cs) hands each block here from the audio thread; the view copies the latest samples out.
    internal static class KainosScopeTap
    {
        // set by the console while the tab is on screen; cmaster keeps the tap running while it is
        public static volatile bool Wanted;

        private const int Size = 16384;                 // a power of two: about 0.34 s at 48 kHz
        private static readonly float[] _left = new float[Size], _right = new float[Size];
        private static int _write;                      // the next sample to write (total written, wraps)
        private static bool _tx;
        private static readonly object _lock = new object();

        public static unsafe void Feed(float* left, float* right, int count, int state)
        {
            if (!Wanted || count <= 0) return;
            lock (_lock)
            {
                if (_tx != (state == 1)) { _tx = state == 1; Array.Clear(_left, 0, Size); Array.Clear(_right, 0, Size); }
                for (int i = 0; i < count; i++)
                {
                    int w = (_write + i) & (Size - 1);
                    _left[w] = left[i];
                    _right[w] = right[i];
                }
                _write = (_write + count) & (Size - 1);
            }
        }

        // the latest n samples, oldest first; tx: they're the transmit signal (I and Q) rather than receive audio
        public static void Latest(float[] left, float[] right, int n, out bool tx)
        {
            n = Math.Min(n, Size);
            lock (_lock)
            {
                tx = _tx;
                int start = (_write - n) & (Size - 1);
                for (int i = 0; i < n; i++)
                {
                    int r = (start + i) & (Size - 1);
                    left[i] = _left[r];
                    right[i] = _right[r];
                }
            }
        }
    }

    // The scope: the audio's waveform, held still on a rising zero crossing, scaled to fit (the tap's level follows
    // the RX1 AF slider), with its peak level. Click it to hold the trace; click again to run.
    internal class KainosScopeView : Control
    {
        public static readonly int[] Timebases = { 2, 5, 10, 20 };     // ms across the view

        private readonly Timer _frame;
        private readonly float[] _l = new float[8192], _r = new float[8192];
        private float[] _shownL = new float[0], _shownR = new float[0];
        private bool _shownTx, _stereo, _hold;
        private float _gainPeak = 1e-4f, _peak;
        private int _timebase = 10;

        public Func<int> RxRate = () => 48000, TxRate = () => 48000;

        public KainosScopeView()
        {
            Name = "kainosScopeView";
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = KainosUI.Bg;
            Cursor = Cursors.Hand;
            _frame = new Timer { Interval = 33 };
            _frame.Tick += (s, e) => { if (!_hold) { capture(); Invalidate(); } };
        }

        protected override void Dispose(bool disposing) { if (disposing) _frame.Dispose(); base.Dispose(disposing); }

        public int Timebase
        {
            get { return _timebase; }
            set { _timebase = Array.IndexOf(Timebases, value) >= 0 ? value : 10; Invalidate(); }
        }

        // drawing while on screen (the console decides); slower in light mode
        public void SetRunning(bool run, bool light)
        {
            _frame.Interval = light ? 66 : 33;
            if (_frame.Enabled != run) _frame.Enabled = run;
        }

        private void capture()
        {
            bool tx;
            int rate = 48000;
            // two views' worth, so a trigger can be found with a whole view after it
            KainosScopeTap.Latest(_l, _r, _l.Length, out tx);
            try { rate = tx ? TxRate() : RxRate(); } catch { }
            if (rate <= 0) rate = 48000;
            int view = Math.Max(16, Math.Min(_l.Length / 2, rate * _timebase / 1000));
            int span = view * 2, first = _l.Length - span;

            float peak = 0, diff = 0;
            for (int i = first; i < _l.Length; i++)
            {
                peak = Math.Max(peak, Math.Max(Math.Abs(_l[i]), Math.Abs(_r[i])));
                diff = Math.Max(diff, Math.Abs(_l[i] - _r[i]));
            }
            _peak = peak;
            // the scale follows the level up at once and down slowly, so speech doesn't pump
            _gainPeak = Math.Max(Math.Max(peak, 1e-4f), _gainPeak * 0.92f);
            _stereo = !tx && diff > peak * 0.05f && peak > 1e-5f;

            // the latest rising zero crossing that still has a whole view after it (armed below -10% of the peak);
            // none (noise, silence): the latest view as it is
            int start = _l.Length - view;
            float arm = -0.1f * peak;
            for (int i = _l.Length - view; i > first; i--)
                if (_l[i - 1] < 0 && _l[i] >= 0)
                {
                    bool armed = false;
                    for (int j = i - 1; j > Math.Max(first, i - view / 2); j--) if (_l[j] < arm) { armed = true; break; }
                    if (armed) { start = i; break; }
                }

            if (_shownL.Length != view) { _shownL = new float[view]; _shownR = new float[view]; }
            Array.Copy(_l, start, _shownL, 0, view);
            Array.Copy(_r, start, _shownR, 0, view);
            _shownTx = tx;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left) { _hold = !_hold; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            float w = Width, h = Height, mid = h / 2f;

            using (Pen grid = new Pen(KainosUI.Line))
            {
                for (int i = 1; i < 10; i++) g.DrawLine(grid, w * i / 10f, 0, w * i / 10f, h);
                g.DrawLine(grid, 0, h / 4f, w, h / 4f);
                g.DrawLine(grid, 0, h * 3 / 4f, w, h * 3 / 4f);
            }
            using (Pen axis = new Pen(KainosUI.Steel)) g.DrawLine(axis, 0, mid, w, mid);

            g.SmoothingMode = SmoothingMode.AntiAlias;
            float scale = (h / 2f - KainosUI.S(4)) / _gainPeak;
            if (_stereo) drawTrace(g, _shownR, Color.FromArgb(150, KainosUI.Violet), scale, mid);
            drawTrace(g, _shownL, _shownTx ? KainosUI.Tx : KainosUI.IceHi, scale, mid);
            g.SmoothingMode = SmoothingMode.None;

            using (Font f = new Font("Segoe UI", Math.Max(8f, KainosUI.S(10)), FontStyle.Bold, GraphicsUnit.Pixel))
            {
                float pad = KainosUI.S(4);
                string source = _shownTx ? "TX" : _stereo ? "RX  L / R" : "RX";
                using (Brush b = new SolidBrush(_shownTx ? KainosUI.Tx : KainosUI.Dim)) g.DrawString(source, f, b, pad, pad);
                string level = _peak > 1e-5f ? "peak " + (20 * Math.Log10(_peak)).ToString("0") + " dBFS" : "no audio";
                using (Brush b = new SolidBrush(KainosUI.Dim))
                using (StringFormat far = new StringFormat { Alignment = StringAlignment.Far })
                {
                    g.DrawString(level, f, b, new RectangleF(0, pad, w - pad, h), far);
                    g.DrawString(_timebase + " ms", f, b, new RectangleF(0, h - pad - f.Height, w - pad, f.Height + 2), far);
                }
                if (_hold)
                    using (Brush b = new SolidBrush(KainosUI.GoldHi)) g.DrawString("HOLD", f, b, pad, h - pad - f.Height);
            }
            using (Pen border = new Pen(KainosUI.Line)) g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        }

        private void drawTrace(Graphics g, float[] s, Color c, float scale, float mid)
        {
            if (s.Length < 2) return;
            float w = Width;
            float lim = Height;
            // at most about two points a pixel
            int step = Math.Max(1, s.Length / Math.Max(1, (int)(w * 2)));
            int n = (s.Length - 1) / step + 1;
            PointF[] pts = new PointF[n];
            for (int i = 0, k = 0; k < n; i += step, k++)
                pts[k] = new PointF(i * (w - 1) / (s.Length - 1), Math.Max(0, Math.Min(lim, mid - s[i] * scale)));
            using (Pen p = new Pen(c, Math.Max(1f, KainosUI.S(1.5f)))) g.DrawLines(p, pts);
        }
    }
}
