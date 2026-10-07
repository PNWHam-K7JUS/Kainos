/*  KainosKiwiWaterfall.cs

This file is part of Kainos, a program that implements a Software Defined Radio.

Copyright (C) 2026 Justin Cron, K7JUS

This program is free software; you can redistribute it and/or modify it under the terms of the GNU General Public
License as published by the Free Software Foundation; either version 2 of the License, or (at your option) any later
version.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied
warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License along with this program; if not, write to the Free
Software Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
*/

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Thetis
{
    // The KiwiSDR being listened to, as a panadapter and waterfall (its own W/F stream, KiwiWaterfallClient): click
    // or wheel to tune (with Follow on that tunes VFO A, so the Kiwi works like the radio even with the HL2 off),
    // Ctrl + wheel or the buttons to zoom. The view re-centres when the tuned frequency nears its edge.
    internal class KainosKiwiWaterfall : Form
    {
        private readonly Console _console;
        private readonly KiwiSpectrumView _view;
        private readonly Label _title;

        public KainosKiwiWaterfall(Console console)
        {
            _console = console;
            Text = "KiwiSDR";
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            BackColor = KainosWindowTheme.WindowBg;
            ForeColor = KainosWindowTheme.Text;
            Font = new Font("Segoe UI", 9.5f);
            AutoScaleDimensions = new SizeF(96f, 96f);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(960, 440);
            MinimumSize = new Size(480, 260);

            _title = new Label { Location = new Point(12, 10), Size = new Size(600, 22), Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = KainosUI.GoldHi,
                                 Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            Controls.Add(_title);
            KainosWizardButton zoomOut = new KainosWizardButton("Zoom -") { Location = new Point(660, 6), Size = new Size(84, 28), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            KainosWizardButton zoomIn = new KainosWizardButton("Zoom +") { Location = new Point(750, 6), Size = new Size(84, 28), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            KainosWizardButton centre = new KainosWizardButton("Centre") { Location = new Point(840, 6), Size = new Size(108, 28), Anchor = AnchorStyles.Top | AnchorStyles.Right, Accent = true };
            zoomOut.Click += (s, e) => _console.KiwiWaterfallZoom(-1);
            zoomIn.Click += (s, e) => _console.KiwiWaterfallZoom(+1);
            centre.Click += (s, e) => _console.KiwiWaterfallCentre();
            Controls.AddRange(new Control[] { zoomOut, zoomIn, centre });

            _view = new KiwiSpectrumView(console) { Location = new Point(0, 40), Size = new Size(960, 400), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
            Controls.Add(_view);

            Load += (s, e) =>
            {
                Form o = Owner;
                if (o != null) Location = new Point(o.Left + (o.Width - Width) / 2, o.Top + (o.Height - Height) / 2);
            };
            FormClosed += (s, e) => _console.KiwiWaterfallClosed();
        }

        public KiwiSpectrumView View { get { return _view; } }
        public void SetTitle(string t) { if (_title.Text != t) _title.Text = t; }
    }

    // The spectrum (top third) and the waterfall under it, with the frequency scale between them
    internal class KiwiSpectrumView : Control
    {
        private readonly Console _console;
        private const int Bins = KiwiWaterfallClient.Bins, Rows = 300;
        private readonly int[] _wf = new int[Bins * Rows];       // the waterfall's pixels, newest line at the top
        private Bitmap _wfBmp = new Bitmap(Bins, Rows, PixelFormat.Format32bppRgb);
        private float[] _line, _smooth;
        private double _centre = 7100, _span = 58.6;
        private float _floor = -110, _range = 60;
        private static readonly int[] _palette = buildPalette();

        public KiwiSpectrumView(Console console)
        {
            _console = console;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = KainosUI.Bg;
            Cursor = Cursors.Cross;
        }

        // a line from the waterfall stream (on its own thread)
        public void AddLine(float[] dbm, double centreKhz, double spanKhz)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke(new Action(() => addLine(dbm, centreKhz, spanKhz))); } catch { }
        }

        private void addLine(float[] dbm, double centreKhz, double spanKhz)
        {
            if (dbm.Length != Bins) return;
            if (Math.Abs(centreKhz - _centre) > 1e-6 || Math.Abs(spanKhz - _span) > 1e-6) Array.Clear(_wf, 0, _wf.Length);   // a new view: start the waterfall again
            _centre = centreKhz;
            _span = spanKhz;
            _line = dbm;
            if (_smooth == null || _smooth.Length != Bins) _smooth = (float[])dbm.Clone();
            for (int i = 0; i < Bins; i++) _smooth[i] = _smooth[i] * 0.5f + dbm[i] * 0.5f;

            // the noise floor (a low percentile of the line), followed slowly, sets the colours
            float[] sorted = (float[])dbm.Clone();
            Array.Sort(sorted);
            float floor = sorted[Bins / 5];
            _floor = _floor * 0.9f + (floor - 5) * 0.1f;

            Array.Copy(_wf, 0, _wf, Bins, Bins * (Rows - 1));
            for (int i = 0; i < Bins; i++)
            {
                float t = (dbm[i] - _floor) / _range;
                _wf[i] = _palette[(int)(Math.Max(0, Math.Min(1, t)) * (_palette.Length - 1))];
            }
            BitmapData bd = _wfBmp.LockBits(new Rectangle(0, 0, Bins, Rows), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
            try { Marshal.Copy(_wf, 0, bd.Scan0, _wf.Length); } finally { _wfBmp.UnlockBits(bd); }
            Invalidate();
        }

        // the waterfall's colours: navy through blue, cyan, green, yellow and red to magenta (as the 3D panadapter's)
        private static int[] buildPalette()
        {
            float[] pos = { 0f, 2f / 9, 3f / 9, 4f / 9, 5f / 9, 7f / 9, 8f / 9, 1f };
            int[,] rgb = { { 4, 10, 30 }, { 0, 0, 200 }, { 0, 220, 255 }, { 0, 230, 0 }, { 255, 255, 0 }, { 255, 0, 0 }, { 255, 0, 255 }, { 192, 124, 255 } };
            int[] p = new int[256];
            for (int i = 0; i < 256; i++)
            {
                float t = i / 255f;
                int k = 0;
                while (k < pos.Length - 2 && t > pos[k + 1]) k++;
                float f = (t - pos[k]) / (pos[k + 1] - pos[k]);
                int r = (int)(rgb[k, 0] + (rgb[k + 1, 0] - rgb[k, 0]) * f), g = (int)(rgb[k, 1] + (rgb[k + 1, 1] - rgb[k, 1]) * f), b = (int)(rgb[k, 2] + (rgb[k + 1, 2] - rgb[k, 2]) * f);
                p[i] = (0xff << 24) | (r << 16) | (g << 8) | b;
            }
            return p;
        }

        private int specH { get { return Math.Max(60, Height / 3); } }
        private const int ScaleH = 20;
        private float x(double khz) { return (float)((khz - (_centre - _span / 2)) / _span * Width); }
        private double khzAt(int px) { return _centre - _span / 2 + px / (double)Math.Max(1, Width) * _span; }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(KainosUI.Bg);
            int sh = specH, top = sh + ScaleH;

            // the waterfall, stretched to the width
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.DrawImage(_wfBmp, new Rectangle(0, top, Width, Height - top), new Rectangle(0, 0, Bins, Math.Min(Rows, Height - top)), GraphicsUnit.Pixel);

            // the tuned frequency's passband, and its line
            double tuned = _console.KiwiViewTunedKhz;
            int lo, hi;
            _console.KiwiViewPassband(out lo, out hi);
            float px0 = x(tuned + lo / 1000.0), px1 = x(tuned + hi / 1000.0), pt = x(tuned);
            using (Brush pb = new SolidBrush(Color.FromArgb(50, KainosUI.Ice))) g.FillRectangle(pb, Math.Min(px0, px1), 0, Math.Abs(px1 - px0), sh);
            using (Pen p = new Pen(KainosUI.Tx, 1.5f)) g.DrawLine(p, pt, 0, pt, Height);

            // the spectrum
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            if (_smooth != null)
            {
                PointF[] pts = new PointF[Bins + 2];
                for (int i = 0; i < Bins; i++)
                {
                    float t = Math.Max(0, Math.Min(1, (_smooth[i] - _floor + 4) / (_range * 0.75f)));     // the noise a little above the bottom, strong signals near the top
                    pts[i + 1] = new PointF(i / (float)(Bins - 1) * Width, sh - 2 - t * (sh - 6));
                }
                pts[0] = new PointF(0, sh);
                pts[Bins + 1] = new PointF(Width, sh);
                using (Brush fill = new SolidBrush(Color.FromArgb(70, KainosUI.Ice))) g.FillPolygon(fill, pts);
                PointF[] trace = new PointF[Bins];
                Array.Copy(pts, 1, trace, 0, Bins);
                using (Pen line = new Pen(KainosUI.IceHi, 1.2f)) g.DrawLines(line, trace);
            }

            // the scale between them
            using (Brush sb = new SolidBrush(KainosUI.Surface)) g.FillRectangle(sb, 0, sh, Width, ScaleH);
            double step = niceStep(_span / 8);
            using (Font f = new Font("Segoe UI", 8.5f))
            using (Brush tb = new SolidBrush(KainosWindowTheme.TextMid))
            using (Pen tick = new Pen(KainosUI.Line))
            {
                double first = Math.Ceiling((_centre - _span / 2) / step) * step;
                for (double k = first; k <= _centre + _span / 2; k += step)
                {
                    float px = x(k);
                    g.DrawLine(tick, px, 0, px, sh);
                    string s = (k / 1000).ToString(step < 1 ? "0.0000" : step < 10 ? "0.000" : "0.00");
                    SizeF sz = g.MeasureString(s, f);
                    g.DrawString(s, f, tb, px - sz.Width / 2, sh + (ScaleH - sz.Height) / 2);
                }
            }
            if (_line == null)
                using (Font f = new Font("Segoe UI", 11f))
                using (Brush b = new SolidBrush(KainosWindowTheme.TextDim))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    g.DrawString(_console.KiwiViewWaiting, f, b, ClientRectangle, sf);
        }

        private static double niceStep(double raw)
        {
            double p = Math.Pow(10, Math.Floor(Math.Log10(raw)));
            foreach (double m in new[] { 1, 2, 5, 10 }) if (raw <= m * p) return m * p;
            return 10 * p;
        }

        // click: tune there; wheel: tune by a step to suit the span; Ctrl + wheel: zoom
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left) _console.KiwiViewTuneTo(khzAt(e.X));
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if ((ModifierKeys & Keys.Control) != 0) { _console.KiwiWaterfallZoom(Math.Sign(e.Delta)); return; }
            double step = _span < 20 ? 0.05 : _span < 100 ? 0.5 : _span < 1000 ? 1 : 5;       // kHz
            _console.KiwiViewTuneTo(_console.KiwiViewTunedKhz + Math.Sign(e.Delta) * step, true);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _wfBmp?.Dispose();
            base.Dispose(disposing);
        }
    }
}
