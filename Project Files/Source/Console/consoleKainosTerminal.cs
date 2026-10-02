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
            if (!open) { pane.Visible = false; if (window != null) window.Hide(); return window; }
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
            pane.SetBounds(panelDisplay.Left, panelDisplay.Bottom + 4, panelDisplay.Width, KainosUI.S(KainosTerminalHeight));
            pane.BringToFront();
        }

        // the height the docked terminal takes from the panadapter, and placing it (consoleKainosColumn.cs)
        private int kainosTermDockHeight
        {
            get
            {
                bool docked = (_rttyOpen && _rttyPane != null && !RttyPopped) || (_cwOpen && _cwPane != null && !CwPopped);
                return docked ? KainosUI.S(KainosTerminalHeight) : 0;
            }
        }

        private void kainosPlaceTermDock()
        {
            kainosTermPlaceDock(_rttyPane, _rttyOpen, RttyPopped);
            kainosTermPlaceDock(_cwPane, _cwOpen, CwPopped);
        }

        // after a switch between Classic and Kainos layout
        private void kainosTermsPlace()
        {
            if (_rttyOpen) rttyPlace();
            if (_cwOpen) cwPlace();
        }
    }
}
