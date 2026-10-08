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

        private const int Size = 32768;                 // a power of two: about 0.68 s at 48 kHz
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

    // What the AF spectrum shows, from the console: the span (Hz of audio), the receive filter's passband in audio
    // terms, the CW pitch (NaN: none), and whether the transmit signal's sideband is below the carrier
    internal struct KainosAfInfo
    {
        public float Lo, Hi, PassLo, PassHi, Pitch;
        public bool LowerSideband;
    }

    // The AUDIO SCOPE tab's view: the waveform (held still on a rising zero crossing, scaled to fit since the tap's
    // level follows the RX1 AF slider), the audio spectrum (AF), or both, one above the other. Click to hold;
    // right-click for the options (the console builds the menu).
    internal class KainosScopeView : Control
    {
        public enum Shows { Scope, Af, Both }
        public enum Channels { Both, Left, Right }
        public static readonly int[] Timebases = { 2, 5, 10, 20, 50, 100 };        // ms across; the first four have buttons
        public static readonly int[] FullScales = { 0, -10, -20, -30, -40, -50 };   // fixed full scale, dBFS

        private const int FftSize = 4096;               // about 12 Hz a bin at 48 kHz
        private readonly Timer _frame;
        private readonly float[] _l = new float[16384], _r = new float[16384];
        private float[] _shownL = new float[0], _shownR = new float[0];
        private bool _shownTx, _stereo;
        private float _gainPeak = 1e-4f, _peak;

        // the options (saved by the console)
        public Shows Show = Shows.Scope;
        public Channels Channel = Channels.Both;
        public bool Trigger = true, PeakHold = true, Smooth = true, Hold;
        public int FullScale = 1;                       // 1: auto; otherwise dBFS (0, -10 ...)
        private int _timebase = 10;

        public Func<int> RxRate = () => 48000, TxRate = () => 48000;
        public Func<KainosAfInfo> AfInfo = () => new KainosAfInfo { Lo = 0, Hi = 3000, PassLo = 100, PassHi = 2900, Pitch = float.NaN };
        public event Action<Point> MenuWanted;

        // the spectrum: per pixel column, smoothed and peak, dBFS
        private float[] _af = new float[0], _afPeak = new float[0];
        private readonly double[] _re = new double[FftSize], _im = new double[FftSize];
        private static readonly double[] _window = hann();
        private KainosAfInfo _afShown = new KainosAfInfo { Lo = 0, Hi = 3000, Pitch = float.NaN };
        private float _afTop = -20, _strongestHz, _strongestDb = -200;

        public KainosScopeView()
        {
            Name = "kainosScopeView";
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = KainosUI.Bg;
            Cursor = Cursors.Hand;
            _frame = new Timer { Interval = 33 };
            _frame.Tick += (s, e) => { if (!Hold) { capture(); Invalidate(); } };
        }

        protected override void Dispose(bool disposing) { if (disposing) _frame.Dispose(); base.Dispose(disposing); }

        public int Timebase
        {
            get { return _timebase; }
            set { _timebase = Array.IndexOf(Timebases, value) >= 0 ? value : 10; Invalidate(); }
        }

        public void ClearPeak() { for (int i = 0; i < _afPeak.Length; i++) _afPeak[i] = -200; Invalidate(); }

        // drawing while on screen (the console decides); slower in light mode
        public void SetRunning(bool run, bool light)
        {
            _frame.Interval = light ? 66 : 33;
            if (_frame.Enabled != run) _frame.Enabled = run;
        }

        private int split { get { return (int)(Height * 0.42f); } }
        private RectangleF scopeRect { get { return new RectangleF(0, 0, Width, Show == Shows.Both ? split : Height); } }
        private RectangleF afRect
        {
            get
            {
                float top = Show == Shows.Both ? split + KainosUI.S(4) : 0;
                return new RectangleF(0, top, Width, Height - top);
            }
        }

        private void capture()
        {
            bool tx;
            int rate = 48000;
            KainosScopeTap.Latest(_l, _r, _l.Length, out tx);
            try { rate = tx ? TxRate() : RxRate(); } catch { }
            if (rate <= 0) rate = 48000;
            _shownTx = tx;
            if (Show != Shows.Af) captureScope(rate, tx);
            if (Show != Shows.Scope) captureAf(rate, tx);
        }

        private void captureScope(int rate, bool tx)
        {
            // two views' worth, so a trigger can be found with a whole view after it
            int view = Math.Max(16, Math.Min(_l.Length / 2, rate * _timebase / 1000));
            int span = view * 2, first = _l.Length - span;
            float[] trig = !tx && Channel == Channels.Right ? _r : _l;

            float peak = 0, diff = 0;
            for (int i = first; i < _l.Length; i++)
            {
                float a = tx || Channel == Channels.Both ? Math.Max(Math.Abs(_l[i]), Math.Abs(_r[i])) : Math.Abs(trig[i]);
                peak = Math.Max(peak, a);
                diff = Math.Max(diff, Math.Abs(_l[i] - _r[i]));
            }
            _peak = peak;
            // the auto scale follows the level up at once and down slowly, so speech doesn't pump
            _gainPeak = Math.Max(Math.Max(peak, 1e-4f), _gainPeak * 0.92f);
            _stereo = !tx && Channel == Channels.Both && diff > peak * 0.05f && peak > 1e-5f;

            // the latest rising zero crossing that still has a whole view after it (armed below -10% of the peak);
            // none (noise, silence) or the trigger off: the latest view as it is
            int start = _l.Length - view;
            if (Trigger)
            {
                float arm = -0.1f * peak;
                for (int i = _l.Length - view; i > first; i--)
                    if (trig[i - 1] < 0 && trig[i] >= 0)
                    {
                        bool armed = false;
                        for (int j = i - 1; j > Math.Max(first, i - view / 2); j--) if (trig[j] < arm) { armed = true; break; }
                        if (armed) { start = i; break; }
                    }
            }

            if (_shownL.Length != view) { _shownL = new float[view]; _shownR = new float[view]; }
            Array.Copy(_l, start, _shownL, 0, view);
            Array.Copy(_r, start, _shownR, 0, view);
        }

        private void captureAf(int rate, bool tx)
        {
            KainosAfInfo info;
            try { info = AfInfo(); } catch { info = _afShown; }
            int cols = Math.Max(1, Width);
            if (_af.Length != cols || info.Lo != _afShown.Lo || info.Hi != _afShown.Hi)
            {
                _af = new float[cols];
                _afPeak = new float[cols];
                for (int i = 0; i < cols; i++) { _af[i] = -200; _afPeak[i] = -200; }
            }
            _afShown = info;

            // receive: the audio (left, right or both mixed) as a real signal; transmit: I and Q, the sideband's half
            int off = _l.Length - FftSize;
            for (int i = 0; i < FftSize; i++)
            {
                float l = _l[off + i], r = _r[off + i];
                if (tx) { _re[i] = l * _window[i]; _im[i] = r * _window[i]; }
                else
                {
                    _re[i] = (Channel == Channels.Left ? l : Channel == Channels.Right ? r : (l + r) * 0.5f) * _window[i];
                    _im[i] = 0;
                }
            }
            fft(_re, _im);
            // a full-scale sine reads about 0 dBFS: the Hann window's gain is 0.5, and a real signal splits over +/- f
            double norm = tx ? FftSize * 0.5 : FftSize * 0.25;
            float hzPerBin = rate / (float)FftSize;
            bool lower = tx && info.LowerSideband;
            float best = -200, bestHz = 0;
            for (int c = 0; c < cols; c++)
            {
                float f0 = info.Lo + (info.Hi - info.Lo) * c / cols, f1 = info.Lo + (info.Hi - info.Lo) * (c + 1) / cols;
                int b0 = Math.Max(0, (int)Math.Floor(f0 / hzPerBin)), b1 = Math.Max(b0, (int)Math.Ceiling(f1 / hzPerBin) - 1);
                double m = 0;
                for (int b = b0; b <= b1 && b < FftSize / 2; b++)
                {
                    int k = lower ? (FftSize - b) % FftSize : b;
                    m = Math.Max(m, _re[k] * _re[k] + _im[k] * _im[k]);
                }
                float db = (float)(10 * Math.Log10(m / (norm * norm) + 1e-20));
                _af[c] = Smooth && _af[c] > -199 ? _af[c] * 0.65f + db * 0.35f : db;
                _afPeak[c] = PeakHold ? Math.Max(_afPeak[c] - 0.3f, _af[c]) : -200;
                if (_af[c] > best) { best = _af[c]; bestHz = (f0 + f1) / 2; }
            }
            _strongestDb = best;
            _strongestHz = bestHz;
            // the auto top: 10 dB steps above the strongest, moving down slowly
            float want = Math.Min(0, (float)Math.Ceiling((best + 6) / 10) * 10);
            _afTop = want > _afTop ? want : Math.Max(want, _afTop - 0.2f);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left) { Hold = !Hold; Invalidate(); }
            else if (e.Button == MouseButtons.Right) MenuWanted?.Invoke(e.Location);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            using (Font f = new Font("Segoe UI", Math.Max(8f, KainosUI.S(10)), FontStyle.Bold, GraphicsUnit.Pixel))
            {
                if (Show != Shows.Af) paintScope(g, scopeRect, f);
                if (Show != Shows.Scope) paintAf(g, afRect, f);
                if (Hold)
                    using (Brush b = new SolidBrush(KainosUI.GoldHi))
                        g.DrawString("HOLD", f, b, KainosUI.S(4), Height - KainosUI.S(4) - f.Height);
            }
        }

        private void paintScope(Graphics g, RectangleF r, Font f)
        {
            float w = r.Width, h = r.Height, mid = r.Top + h / 2f;
            using (Pen grid = new Pen(KainosUI.Line))
            {
                for (int i = 1; i < 10; i++) g.DrawLine(grid, r.Left + w * i / 10f, r.Top, r.Left + w * i / 10f, r.Bottom);
                g.DrawLine(grid, r.Left, r.Top + h / 4f, r.Right, r.Top + h / 4f);
                g.DrawLine(grid, r.Left, r.Top + h * 3 / 4f, r.Right, r.Top + h * 3 / 4f);
            }
            using (Pen axis = new Pen(KainosUI.Steel)) g.DrawLine(axis, r.Left, mid, r.Right, mid);

            float full = FullScale == 1 ? _gainPeak : (float)Math.Pow(10, FullScale / 20.0);
            float scale = (h / 2f - KainosUI.S(4)) / full;
            Region clip = g.Clip;
            g.SetClip(r);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (_shownTx || Channel == Channels.Left) drawTrace(g, r, _shownL, _shownTx ? KainosUI.Tx : KainosUI.IceHi, scale, mid);
            else if (Channel == Channels.Right) drawTrace(g, r, _shownR, KainosUI.Violet, scale, mid);
            else
            {
                if (_stereo) drawTrace(g, r, _shownR, Color.FromArgb(150, KainosUI.Violet), scale, mid);
                drawTrace(g, r, _shownL, KainosUI.IceHi, scale, mid);
            }
            g.SmoothingMode = SmoothingMode.None;
            g.Clip = clip;

            float pad = KainosUI.S(4);
            string source = _shownTx ? "TX" : Channel == Channels.Left ? "RX  L" : Channel == Channels.Right ? "RX  R" : _stereo ? "RX  L / R" : "RX";
            using (Brush b = new SolidBrush(_shownTx ? KainosUI.Tx : KainosUI.Dim)) g.DrawString(source, f, b, r.Left + pad, r.Top + pad);
            string level = _peak > 1e-5f ? "peak " + (20 * Math.Log10(_peak)).ToString("0") + " dBFS" : "no audio";
            if (FullScale != 1) level += "  (scale " + FullScale + ")";
            using (Brush b = new SolidBrush(KainosUI.Dim))
            using (StringFormat far = new StringFormat { Alignment = StringAlignment.Far })
            {
                g.DrawString(level, f, b, new RectangleF(r.Left, r.Top + pad, w - pad, f.Height + 2), far);
                g.DrawString(_timebase + " ms" + (Trigger ? "" : "  free run"), f, b, new RectangleF(r.Left, r.Bottom - pad - f.Height, w - pad, f.Height + 2), far);
            }
            using (Pen border = new Pen(KainosUI.Line)) g.DrawRectangle(border, r.Left, r.Top, r.Width - 1, r.Height - 1);
        }

        private void drawTrace(Graphics g, RectangleF r, float[] s, Color c, float scale, float mid)
        {
            if (s.Length < 2) return;
            float w = r.Width;
            // at most about two points a pixel
            int step = Math.Max(1, s.Length / Math.Max(1, (int)(w * 2)));
            int n = (s.Length - 1) / step + 1;
            PointF[] pts = new PointF[n];
            for (int i = 0, k = 0; k < n; i += step, k++)
                pts[k] = new PointF(r.Left + i * (w - 1) / (s.Length - 1), Math.Max(r.Top - 2, Math.Min(r.Bottom + 2, mid - s[i] * scale)));
            using (Pen p = new Pen(c, Math.Max(1f, KainosUI.S(1.5f)))) g.DrawLines(p, pts);
        }

        private void paintAf(Graphics g, RectangleF r, Font f)
        {
            KainosAfInfo info = _afShown;
            float span = Math.Max(1, info.Hi - info.Lo);
            float top = FullScale == 1 ? _afTop : FullScale, range = 80;
            Func<float, float> x = hz => r.Left + (hz - info.Lo) / span * r.Width;
            Func<float, float> y = db => r.Top + (top - db) / range * r.Height;

            // the receive filter's passband
            if (!_shownTx && info.PassHi > info.PassLo)
                using (Brush b = new SolidBrush(Color.FromArgb(34, KainosUI.Ice)))
                {
                    float x0 = Math.Max(r.Left, x(info.PassLo)), x1 = Math.Min(r.Right, x(info.PassHi));
                    if (x1 > x0) g.FillRectangle(b, x0, r.Top, x1 - x0, r.Height);
                }

            // grid: frequency steps that suit the span, and every 10 dB
            float step = span <= 1200 ? 100 : span <= 4000 ? 500 : 1000;
            using (Pen grid = new Pen(KainosUI.Line))
            using (Brush lb = new SolidBrush(KainosUI.Faint))
            using (Font small = new Font("Segoe UI", Math.Max(7f, KainosUI.S(9)), FontStyle.Regular, GraphicsUnit.Pixel))
            {
                for (float hz = (float)Math.Ceiling(info.Lo / step) * step; hz <= info.Hi; hz += step)
                {
                    float gx = x(hz);
                    g.DrawLine(grid, gx, r.Top, gx, r.Bottom);
                    float every = span <= 1200 ? 200 : span <= 4000 ? 1000 : 2000;
                    bool label = Math.Abs(hz - Math.Round(hz / every) * every) < 1;
                    if (label && gx > r.Left + KainosUI.S(10) && gx < r.Right - KainosUI.S(24))
                        g.DrawString((hz / 1000f).ToString(span <= 1200 ? "0.0" : "0.#") + "k", small, lb, gx + 2, r.Bottom - small.Height - 2);
                }
                for (float db = top - 10; db > top - range; db -= 10)
                    g.DrawLine(grid, r.Left, y(db), r.Right, y(db));
            }

            if (!float.IsNaN(info.Pitch))
                using (Pen p = new Pen(Color.FromArgb(180, KainosUI.Gold)) { DashStyle = DashStyle.Dash })
                    g.DrawLine(p, x(info.Pitch), r.Top, x(info.Pitch), r.Bottom);

            int cols = _af.Length;
            if (cols > 1)
            {
                Region clip = g.Clip;
                g.SetClip(r);
                Color c = _shownTx ? KainosUI.Tx : KainosUI.IceHi;
                PointF[] line = new PointF[cols];
                for (int i = 0; i < cols; i++) line[i] = new PointF(r.Left + i * r.Width / cols, Math.Min(r.Bottom + 1, y(_af[i])));
                PointF[] fill = new PointF[cols + 2];
                Array.Copy(line, fill, cols);
                fill[cols] = new PointF(line[cols - 1].X, r.Bottom + 1);
                fill[cols + 1] = new PointF(line[0].X, r.Bottom + 1);
                using (Brush b = new SolidBrush(Color.FromArgb(60, c))) g.FillPolygon(b, fill);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (Pen p = new Pen(c, Math.Max(1f, KainosUI.S(1.2f)))) g.DrawLines(p, line);
                if (PeakHold)
                {
                    PointF[] pk = new PointF[cols];
                    for (int i = 0; i < cols; i++) pk[i] = new PointF(line[i].X, Math.Min(r.Bottom + 1, y(_afPeak[i])));
                    using (Pen p = new Pen(Color.FromArgb(170, KainosUI.Gold))) g.DrawLines(p, pk);
                }
                g.SmoothingMode = SmoothingMode.None;
                g.Clip = clip;
            }

            float pad = KainosUI.S(4);
            using (Brush b = new SolidBrush(_shownTx ? KainosUI.Tx : KainosUI.Dim)) g.DrawString(_shownTx ? "TX  AF" : "AF", f, b, r.Left + pad, r.Top + pad);
            string strongest = _strongestDb > -120 ? _strongestHz.ToString("0") + " Hz  " + _strongestDb.ToString("0") + " dBFS" : "no audio";
            using (Brush b = new SolidBrush(KainosUI.Dim))
            using (StringFormat far = new StringFormat { Alignment = StringAlignment.Far })
                g.DrawString(strongest, f, b, new RectangleF(r.Left, r.Top + pad, r.Width - pad, f.Height + 2), far);
            using (Pen border = new Pen(KainosUI.Line)) g.DrawRectangle(border, r.Left, r.Top, r.Width - 1, r.Height - 1);
        }

        private static double[] hann()
        {
            double[] w = new double[FftSize];
            for (int i = 0; i < FftSize; i++) w[i] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (FftSize - 1));
            return w;
        }

        // in-place radix-2 FFT
        private static void fft(double[] re, double[] im)
        {
            int n = re.Length;
            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1) j ^= bit;
                j ^= bit;
                if (i < j) { double t = re[i]; re[i] = re[j]; re[j] = t; t = im[i]; im[i] = im[j]; im[j] = t; }
            }
            for (int len = 2; len <= n; len <<= 1)
            {
                double ang = -2 * Math.PI / len, wr = Math.Cos(ang), wi = Math.Sin(ang);
                for (int i = 0; i < n; i += len)
                {
                    double cr = 1, ci = 0;
                    for (int k = 0; k < len / 2; k++)
                    {
                        int a = i + k, b = a + len / 2;
                        double xr = re[b] * cr - im[b] * ci, xi = re[b] * ci + im[b] * cr;
                        re[b] = re[a] - xr; im[b] = im[a] - xi;
                        re[a] += xr; im[a] += xi;
                        double t = cr * wr - ci * wi; ci = cr * wi + ci * wr; cr = t;
                    }
                }
            }
        }
    }
}
