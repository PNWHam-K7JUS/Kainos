/*  consoleKainosFlag.cs

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
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Thetis
{
    // Kainos layout, stage 4: the slice flag (SmartSDR's). A small panel on the panadapter beside the VFO A line:
    // the VFO's letter, antenna, filter width, active DSP, TX, the frequency and the mode.
    //
    // The panadapter is drawn by DirectX into pnlDisplay's window, and in Thetis's "flip" present mode that covers
    // any control placed over it, so the flag is its own small window owned by the console (it stays with the
    // console, never takes the keyboard focus, and draws on top whatever the present mode). It follows VFO A with
    // Thetis's own frequency-to-pixel conversion (HzToPixel, which allows for CTUN, RIT, zoom and pan) on a timer.
    public partial class Console
    {
        private KainosFlagForm _kainosFlagA;
        private Timer _kainosFlagTimer;

        private void kainosFlagOn()
        {
            if (_kainosFlagA == null)
            {
                _kainosFlagA = new KainosFlagForm(new KainosFlagView(this, 1));
                _kainosFlagTimer = new Timer { Interval = 80 };
                _kainosFlagTimer.Tick += (s, e) => placeKainosFlags();
                Move += (s, e) => placeKainosFlags();
                Resize += (s, e) => placeKainosFlags();
            }
            _kainosFlagTimer.Start();
            placeKainosFlags();
        }

        private void kainosFlagOff()
        {
            if (_kainosFlagTimer != null) _kainosFlagTimer.Stop();
            if (_kainosFlagA != null) _kainosFlagA.Hide();
        }

        // Where the VFO A line is on the panadapter, in pnlDisplay's pixels (the calculation Thetis uses for its own
        // filter overlay)
        internal int KainosVfoAPixel { get { return HzToPixel((float)((VFOAFreq - CentreFrequency) * 1e6)); } }

        private void placeKainosFlags()
        {
            if (_kainosFlagA == null) return;
            bool show = _kainosLayout && _kainosPartsOn && WindowState != FormWindowState.Minimized && Visible
                        && pnlDisplay.Visible && pnlDisplay.Width > 100 && pnlDisplay.Height > 60 && !collapsedDisplay;
            int x = 0;
            if (show)
            {
                try { x = KainosVfoAPixel; } catch { show = false; }
                if (x < 0 || x > pnlDisplay.Width) show = false;      // the VFO is off the panadapter
            }
            if (!show)
            {
                if (_kainosFlagA.Visible) _kainosFlagA.Hide();
                return;
            }

            // hang the flag to the left of the line, or to the right when there's no room on the left
            Size size = _kainosFlagA.View.PreferredFlagSize();
            int gap = KainosUI.S(6);
            int left = x - gap - size.Width;
            if (left < 2) left = x + gap;
            left = Math.Max(2, Math.Min(left, pnlDisplay.Width - size.Width - 2));
            int top = KainosUI.S(26);                     // below the frequency scale
            Point screen = pnlDisplay.PointToScreen(new Point(left, top));
            Rectangle want = new Rectangle(screen, size);
            if (_kainosFlagA.Bounds != want) _kainosFlagA.Bounds = want;
            if (!_kainosFlagA.Visible) _kainosFlagA.Show(this);
            _kainosFlagA.View.Invalidate();
        }

        // ---- what the flag shows ----

        internal string KainosFilterText()
        {
            RadioButton r = panelFilter.Controls.OfType<RadioButton>().FirstOrDefault(b => b.Checked);
            if (r != null && r.Text.Length > 0 && !r.Text.StartsWith("Var")) return r.Text;
            try
            {
                double w = Math.Abs(radio.GetDSPRX(0, 0).RXFilterHigh - radio.GetDSPRX(0, 0).RXFilterLow);
                return w >= 1000 ? (w / 1000).ToString("0.0") + "k" : w.ToString("0");
            }
            catch { return ""; }
        }

        internal string KainosDspText()
        {
            List<string> on = new List<string>();
            if (chkNR.Checked) on.Add(chkNR.Text);
            if (chkNB.Checked) on.Add(chkNB.Text);
            if (chkDSPNB2.Checked) on.Add("SNB");
            if (chkANF.Checked) on.Add("ANF");
            if (chkBIN.Checked) on.Add("BIN");
            return string.Join(" ", on);
        }

        internal string KainosRxAntText()
        {
            string t = toolStripStatusLabelRXAnt.Text ?? "";
            return t.Replace("Rx ", "").Replace("RX ", "").Trim().ToUpperInvariant().Replace(" ", "");
        }

        internal bool KainosVfoATx { get { return chkVFOATX.Checked || !RX2Enabled; } }
        internal bool KainosMox { get { return _mox; } }
        internal string KainosModeText { get { return RadeEnabled ? "RADE" : _rx1_dsp_mode.ToString(); } }
    }

    // a borderless window owned by the console that never takes the focus
    internal class KainosFlagForm : Form
    {
        public readonly KainosFlagView View;

        public KainosFlagForm(KainosFlagView view)
        {
            View = view;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = KainosUI.Bg;
            view.Dock = DockStyle.Fill;
            Controls.Add(view);
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x08000000 | 0x00000080;      // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW
                return cp;
            }
        }

        // a click on the flag must not activate it (the console keeps the keyboard)
        protected override void WndProc(ref Message m)
        {
            const int WM_MOUSEACTIVATE = 0x21, MA_NOACTIVATE = 3;
            if (m.Msg == WM_MOUSEACTIVATE) { m.Result = (IntPtr)MA_NOACTIVATE; return; }
            base.WndProc(ref m);
        }
    }

    // the flag's face
    internal class KainosFlagView : Control
    {
        private readonly Console _console;
        private readonly int _rx;

        public KainosFlagView(Console console, int rx)
        {
            _console = console;
            _rx = rx;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = KainosUI.Bg;
        }

        public Size PreferredFlagSize() { return new Size(KainosUI.S(250), KainosUI.S(66)); }

        private Color tone { get { return _rx == 1 ? KainosUI.Gold : KainosUI.Violet; } }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            float s = KainosUI.Scale;
            using (Brush b = new SolidBrush(Color.FromArgb(0x06, 0x0e, 0x17))) g.FillRectangle(b, ClientRectangle);
            using (Pen p = new Pen(tone, 1.5f)) g.DrawRectangle(p, 0.75f, 0.75f, Width - 1.5f, Height - 1.5f);

            // identity row: letter, antenna, filter, DSP ... TX
            float pad = 7 * s, y = 6 * s;
            float badge = 18 * s;
            using (Brush b = new SolidBrush(tone)) g.FillEllipse(b, pad, y, badge, badge);
            using (Font f = new Font("Segoe UI", 11 * s, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Brush b = new SolidBrush(KainosUI.Bg))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(_rx == 1 ? "A" : "B", f, b, new RectangleF(pad, y, badge, badge), sf);
            float x = pad + badge + 7 * s;
            using (Font f = new Font("Segoe UI", 11 * s, FontStyle.Bold, GraphicsUnit.Pixel))
            {
                x = drawItem(g, f, _console.KainosRxAntText(), tone, x, y, badge);
                x = drawItem(g, f, _console.KainosFilterText(), KainosUI.Dim, x, y, badge);
            }
            using (Font f = new Font("Segoe UI", 10 * s, FontStyle.Regular, GraphicsUnit.Pixel))
                drawItem(g, f, _console.KainosDspText(), KainosUI.Faint, x, y, badge);

            // TX: outlined on the transmit VFO, filled red while transmitting
            if (_console.KainosVfoATx)
            {
                RectangleF tx = new RectangleF(Width - pad - 26 * s, y + 1 * s, 26 * s, badge - 2 * s);
                bool keyed = _console.KainosMox;
                using (System.Drawing.Drawing2D.GraphicsPath path = KainosUI.RoundedRect(tx, 3 * s))
                {
                    if (keyed) using (Brush b = new SolidBrush(KainosUI.Tx)) g.FillPath(b, path);
                    using (Pen p = new Pen(KainosUI.Tx)) g.DrawPath(p, path);
                }
                using (Font f = new Font("Segoe UI", 10 * s, FontStyle.Bold, GraphicsUnit.Pixel))
                using (Brush b = new SolidBrush(keyed ? Color.White : KainosUI.Tx))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    g.DrawString("TX", f, b, tx, sf);
            }

            // frequency, right-aligned with the last three digits dimmer (Hz), mode at the left
            double mhz = _rx == 1 ? _console.VFOAFreq : _console.VFOBFreq;
            // as Thetis shows it: "14.225" then "000" (Hz) smaller
            string whole = mhz.ToString("0.000000", System.Globalization.CultureInfo.InvariantCulture);
            string head = whole.Substring(0, whole.Length - 3), tail = whole.Substring(whole.Length - 3);
            float fy = y + badge + 2 * s, fh = Height - fy - 3 * s;
            using (Font big = new Font("Consolas", 26 * s, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font small = new Font("Consolas", 20 * s, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Brush bh = new SolidBrush(KainosUI.Text))
            using (Brush bt = new SolidBrush(KainosUI.Dim))
            {
                SizeF tailSize = g.MeasureString(tail, small);
                SizeF headSize = g.MeasureString(head, big);
                float right = Width - pad + 3 * s;
                float baseY = fy + (fh - headSize.Height) / 2;
                g.DrawString(tail, small, bt, right - tailSize.Width, baseY + (headSize.Height - tailSize.Height) * 0.75f);
                g.DrawString(head, big, bh, right - tailSize.Width - headSize.Width + 6 * s, baseY);
            }
            using (Font f = new Font("Segoe UI", 12 * s, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Brush b = new SolidBrush(KainosUI.Ice))
            using (StringFormat sf = new StringFormat { LineAlignment = StringAlignment.Center })
                g.DrawString(_rx == 1 ? _console.KainosModeText : "", f, b, new RectangleF(pad, fy, 70 * s, fh), sf);
        }

        private static float drawItem(Graphics g, Font f, string text, Color c, float x, float y, float h)
        {
            if (string.IsNullOrEmpty(text)) return x;
            using (Brush b = new SolidBrush(c))
            using (StringFormat sf = new StringFormat { LineAlignment = StringAlignment.Center })
                g.DrawString(text, f, b, new RectangleF(x, y, 200, h), sf);
            return x + g.MeasureString(text, f).Width + 4;
        }
    }
}
