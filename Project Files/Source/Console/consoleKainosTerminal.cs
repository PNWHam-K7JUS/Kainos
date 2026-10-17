/*  consoleKainosTerminal.cs

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
using System.Windows.Forms;

namespace Thetis
{
    // The digital-mode terminals (RTTY, CW) share where they live: docked under the panadapter in Kainos layout
    // (the panadapter gives up the height), or in their own window (Pop out, and always in Classic). One is open at
    // a time: they read the same copy of the receiver's audio.
    internal interface IKainosTerminal { }

    public partial class Console
    {
        internal const int KainosTerminalHeight = 236;

        // put a terminal in its place; returns its window (made the first time it's needed)
        private Form kainosTermPlace(Control pane, Form window, string title, bool open, bool popped, Action close)
        {
            if (pane == null) return window;
            if (!open)
            {
                pane.Visible = false;
                if (window != null) window.Hide();
                if (_kainosLayout) positionKainosColumn();      // the panadapter takes its height back
                return window;
            }
            if (popped)
            {
                if (window == null || window.IsDisposed)
                {
                    window = new Form
                    {
                        Text = title,
                        Owner = this,
                        ShowInTaskbar = false,
                        StartPosition = FormStartPosition.Manual,
                        BackColor = KainosUI.Bg,
                        Size = new Size(KainosUI.S(1120), KainosUI.S(320)),
                        MinimumSize = new Size(KainosUI.S(1080), KainosUI.S(240)),      // the settings row fits
                        Location = new Point(Left + 80, Top + Height / 2),
                        Icon = Icon,
                    };
                    window.FormClosing += (s, e) => { if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; close(); } };
                }
                if (pane.Parent != window) { pane.Dock = DockStyle.Fill; window.Controls.Add(pane); }
                pane.Visible = true;
                if (!window.Visible) window.Show(this);
            }
            else
            {
                if (window != null) window.Hide();
                if (pane.Parent != this) { pane.Dock = DockStyle.None; Controls.Add(pane); }
                pane.Visible = true;
                pane.BringToFront();
            }
            if (_kainosLayout) positionKainosColumn();
            pane.Invalidate(true);
            return window;
        }

        private void kainosTermPlaceDock(Control pane, bool open, bool popped)
        {
            if (pane == null || !open || popped || pane.Parent != this) return;
            pane.SetBounds(panelDisplay.Left, panelDisplay.Bottom + KainosTermGap, panelDisplay.Width, KainosUI.S(kainosTermHeightNow));
            pane.BringToFront();
        }

        // ---- the docked terminal's height: dragged with the bar between it and the panadapter ----

        internal const int KainosTermGap = 6;               // the bar's height (px): the gap between panadapter and terminal
        private const int KainosTermMinHeight = 140;        // design px
        private int _kainosTermHeight = KainosTerminalHeight;
        private KainosTermGrip _kainosTermGrip;

        // saved with the options (a hidden Setup box): the height in design px, "" for the default
        public string KainosTermHeightSetting
        {
            get { return _kainosTermHeight == KainosTerminalHeight ? "" : _kainosTermHeight.ToString(); }
            set
            {
                int h;
                h = int.TryParse(value, out h) ? Math.Max(KainosTermMinHeight, Math.Min(1200, h)) : KainosTerminalHeight;
                if (h == _kainosTermHeight) return;         // Setup applies its settings often: only a change moves anything
                _kainosTermHeight = h;
                if (_kainosLayout && kainosTermDockHeight > 0) positionKainosColumn();
            }
        }

        // the tallest the terminal may be (design px), leaving the panadapter 160 px
        internal int KainosTermMaxHeight
        {
            get
            {
                int room = kainosDisplayBottom() - (menuStrip1.Bottom + 4) - KainosTermGap - 164;
                return Math.Max(KainosTermMinHeight, (int)(room / KainosUI.Scale));
            }
        }

        internal int KainosTermHeight { get { return kainosTermHeightNow; } }

        // the saved height, less whatever a smaller window can't spare
        private int kainosTermHeightNow { get { return Math.Min(_kainosTermHeight, KainosTermMaxHeight); } }

        internal void KainosSetTermHeight(int designPx)
        {
            int h = Math.Max(KainosTermMinHeight, Math.Min(KainosTermMaxHeight, designPx));
            if (h == _kainosTermHeight) return;
            _kainosTermHeight = h;
            positionKainosColumn();
            KainosRaiseSettingsChanged();
        }

        private void kainosPlaceTermGrip()
        {
            bool docked = kainosTermDockHeight > 0 && _kainosLayout;
            if (!docked) { if (_kainosTermGrip != null) _kainosTermGrip.Visible = false; return; }
            if (_kainosTermGrip == null) { _kainosTermGrip = new KainosTermGrip(this) { Name = "kainosTermGrip" }; Controls.Add(_kainosTermGrip); }
            if (_kainosTermGrip.Dragging) return;
            _kainosTermGrip.SetBounds(panelDisplay.Left, panelDisplay.Bottom, panelDisplay.Width, KainosTermGap);
            _kainosTermGrip.Visible = true;
            _kainosTermGrip.BringToFront();
        }

        // the height the docked terminal takes from the panadapter, and placing it (consoleKainosColumn.cs)
        private int kainosTermDockHeight
        {
            get
            {
                bool docked = (_rttyOpen && _rttyPane != null && !RttyPopped) || (_cwOpen && _cwPane != null && !CwPopped) || kainosModulePaneDocked;
                return docked ? KainosUI.S(kainosTermHeightNow) : 0;
            }
        }

        private void kainosPlaceTermDock()
        {
            kainosTermPlaceDock(_rttyPane, _rttyOpen, RttyPopped);
            kainosTermPlaceDock(_cwPane, _cwOpen, CwPopped);
            kainosPlaceModulePanes();       // consoleKainosModules.cs
            kainosPlaceTermGrip();
        }

        // after a switch between Classic and Kainos layout
        private void kainosTermsPlace()
        {
            if (_rttyOpen) rttyPlace();
            if (_cwOpen) cwPlace();
            kainosModulePanesPlace();
            kainosPlaceTermGrip();
        }
    }

    // The bar between the panadapter and a docked terminal: drag it up for a taller terminal (the panadapter and
    // waterfall give up the height), down for a shorter one; double-click for the standard height. While dragging only
    // the bar moves; the panadapter is resized once, on release (resizing it rebuilds its DirectX surfaces).
    internal class KainosTermGrip : Control, IKainosTerminal
    {
        private readonly Console _c;
        private int _fromY, _fromTop, _fromH;
        private bool _hover;
        public bool Dragging { get; private set; }

        public KainosTermGrip(Console c)
        {
            _c = c;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = KainosUI.Bg;
            Cursor = Cursors.SizeNS;
            TabStop = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Dragging || _hover ? KainosUI.Raised : KainosUI.Bg);
            // a short grip in the middle, gold while dragging
            Color c = Dragging ? KainosUI.Gold : _hover ? KainosUI.Ice : KainosUI.Line;
            int w = Math.Min(60, Width / 4), x = (Width - w) / 2, y = Height / 2 - 1;
            using (Pen p = new Pen(c, 2)) g.DrawLine(p, x, y, x + w, y);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            Dragging = true;
            _fromY = Cursor.Position.Y;
            _fromTop = Top;
            _fromH = _c.KainosTermHeight;
            Capture = true;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!Dragging) return;
            int h = clampHeight(_fromH + (int)Math.Round((_fromY - Cursor.Position.Y) / KainosUI.Scale));
            Top = _fromTop - (int)Math.Round((h - _fromH) * KainosUI.Scale);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!Dragging) return;
            int h = clampHeight(_fromH + (int)Math.Round((_fromY - Cursor.Position.Y) / KainosUI.Scale));
            Dragging = false;
            Capture = false;
            Invalidate();
            _c.KainosSetTermHeight(h);
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (Dragging) { Dragging = false; Top = _fromTop; Invalidate(); }      // capture lost (Alt+Tab ...): no change
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            _c.KainosSetTermHeight(Console.KainosTerminalHeight);
        }

        private int clampHeight(int h) { return Math.Max(140, Math.Min(_c.KainosTermMaxHeight, h)); }
    }
}
