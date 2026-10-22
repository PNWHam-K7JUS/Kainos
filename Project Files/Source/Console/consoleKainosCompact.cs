/*  consoleKainosCompact.cs

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
    // Kainos's compact view: the menu bar's Collapse in the Kainos layout (Thetis's own collapsed layout is for Classic).
    // The left dock and the right column tuck away at the window's edges and slide in over the panadapter while the
    // mouse is at that edge; the panadapter fills the window and the flags stay. Expand puts everything back.
    //
    // The panadapter is drawn by DirectX, which covers any control placed over it, so the dock and the column slide
    // in as small windows of their own (like the flags), and the Thetis panels the column lays over itself (the TX
    // tab's mode panel, VFO SYNC, the meter) go with it.
    public partial class Console
    {
        private bool _kainosCompactOn;
        private KainosEdgeForm _kainosEdgeLeft, _kainosEdgeRight;
        private Panel _kainosStripLeft, _kainosStripRight;
        private Timer _kainosEdgeTimer;
        private Size _kainosCompactSavedMin;
        private readonly int[] _kainosEdgeAway = new int[2];             // how long the mouse has been off each edge window (ticks)

        // saved with the options (hidden Setup box txtKainosCompact): "on=1"
        public string KainosCompactSettings = "";

        internal bool KainosCompactOn { get { return _kainosCompactOn; } }

        // the menu bar's Collapse / Expand: true when it was ours (the Kainos layout)
        internal bool KainosCompactMenu()
        {
            if (!_kainosLayout || collapsedDisplay) return false;
            SetKainosCompact(!_kainosCompactOn);
            return true;
        }

        private bool _kainosCollapseTextHooked;
        private void kainosCompactMenuText()
        {
            if (!_kainosCollapseTextHooked)
            {
                // Thetis names it Collapse again as it starts up (ExpandDisplay): keep Kainos's name in the Kainos layout
                _kainosCollapseTextHooked = true;
                collapseToolStripMenuItem.TextChanged += (s, e) =>
                {
                    if (_kainosLayout && !collapsedDisplay && collapseToolStripMenuItem.Text == "Collapse") kainosCompactMenuText();
                };
            }
            if (collapsedDisplay) return;          // Thetis's own collapsed layout names it
            collapseToolStripMenuItem.Text = !_kainosLayout ? "Collapse" : _kainosCompactOn ? "Expand" : "Compact";
        }

        internal void SetKainosCompact(bool on)
        {
            if (on && (!_kainosLayout || collapsedDisplay || _kainosColumn == null || _kainosDock == null)) return;
            if (on == _kainosCompactOn) { kainosCompactMenuText(); return; }
            kainosCompactCreate();
            _kainosCompactOn = on;
            SuspendLayout();
            try
            {
                if (on)
                {
                    _kainosEdgeLeft.Controls.Add(_kainosDock);
                    _kainosEdgeRight.Controls.Add(_kainosColumn);
                    kainosCompactPinIn();       // the column's pinned panels (meter, mode panel, VFO SYNC) go with it
                    _kainosCompactSavedMin = MinimumSize;
                    MinimumSize = new Size(Math.Min(MinimumSize.Width, 720), Math.Min(MinimumSize.Height, 420));
                }
                else
                {
                    hideEdge(0);
                    hideEdge(1);
                    Controls.Add(_kainosDock);
                    Controls.Add(_kainosColumn);
                    foreach (Control c in kainosPinned)
                        if (c != null && c.Parent != this) Controls.Add(c);
                    if (!_kainosCompactSavedMin.IsEmpty) MinimumSize = _kainosCompactSavedMin;
                }
                _kainosStripLeft.Visible = _kainosStripRight.Visible = on;
                _kainosDock.Visible = true;
                _kainosColumn.Visible = true;
            }
            finally { ResumeLayout(); }
            kainosCompactMenuText();
            positionKainosDock();           // and the column and panadapter
            placeKainosFlags();
            kainosCompactSave();
        }

        private void kainosCompactCreate()
        {
            if (_kainosEdgeLeft != null) return;
            _kainosEdgeLeft = new KainosEdgeForm();
            _kainosEdgeRight = new KainosEdgeForm();
            _kainosStripLeft = new Panel { Name = "kainosStripLeft", BackColor = KainosUI.Line, Visible = false, Cursor = Cursors.Hand };
            _kainosStripRight = new Panel { Name = "kainosStripRight", BackColor = KainosUI.Line, Visible = false, Cursor = Cursors.Hand };
            _kainosStripLeft.MouseEnter += (s, e) => showEdge(0);
            _kainosStripRight.MouseEnter += (s, e) => showEdge(1);
            Controls.AddRange(new Control[] { _kainosStripLeft, _kainosStripRight });
            _kainosEdgeTimer = new Timer { Interval = 120 };
            _kainosEdgeTimer.Tick += (s, e) => edgeTick();
            _kainosEdgeTimer.Start();
            FormClosing += (s, e) => { _kainosEdgeTimer.Stop(); };
        }

        // the Thetis panels the column lays over itself (they go into the edge window with it)
        private IEnumerable<Control> kainosPinned
        {
            get { return kainosModePanels.Concat(new Control[] { grpVFOBetween, _kainosMeter }); }
        }

        // ---- the edge windows ----

        private KainosEdgeForm edgeForm(int side) { return side == 0 ? _kainosEdgeLeft : _kainosEdgeRight; }

        private void showEdge(int side)
        {
            if (!_kainosCompactOn) return;
            KainosEdgeForm f = edgeForm(side);
            _kainosEdgeAway[side] = 0;
            if (f.Visible) return;
            kainosPlaceEdges();
            f.ActiveControl = f.FocusSink;      // a click that activates it mustn't focus the VFO SYNC box (GitHub #32)
            f.Show(this);
            if (side == 1) { _kainosColumn.ArrangeSections(); kainosCompactPinIn(); }
            else _kainosDock.Invalidate();
        }

        private void hideEdge(int side)
        {
            KainosEdgeForm f = edgeForm(side);
            if (f == null || !f.Visible) return;
            bool hadFocus = f.ContainsFocus;
            f.Hide();
            if (hadFocus) Activate();
        }

        // the column's pinned panels belong in its window while it's there
        private void kainosCompactPinIn()
        {
            foreach (Control c in kainosPinned)
                if (c != null && c.Parent != _kainosEdgeRight) _kainosEdgeRight.Controls.Add(c);
            _kainosColumn.ArrangeSections();
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(Point p);

        // an edge window slides away half a second after the mouse leaves it (not while a button is held or one of its
        // menus or lists is open)
        private void edgeTick()
        {
            if (!_kainosCompactOn) return;
            // a pinned panel made after the compact view came on (the meter, at start-up) joins the column's window
            if (kainosPinned.Any(c => c != null && c.Parent != _kainosEdgeRight)) kainosCompactPinIn();
            Point at = Cursor.Position;
            for (int side = 0; side < 2; side++)
            {
                KainosEdgeForm f = edgeForm(side);
                if (!f.Visible) { _kainosEdgeAway[side] = 0; continue; }
                Rectangle keep = f.Bounds;
                keep.Inflate(KainosUI.S(8), KainosUI.S(8));
                Panel strip = side == 0 ? _kainosStripLeft : _kainosStripRight;
                bool overStrip = strip.Visible && strip.RectangleToScreen(strip.ClientRectangle).Contains(at);
                Control under = Control.FromChildHandle(WindowFromPoint(at));
                bool menu = under is ToolStripDropDown || under is KainosEdgeForm && under != f;
                if (keep.Contains(at) || overStrip || menu || Control.MouseButtons != MouseButtons.None || f.ContainsFocus && f.ActiveControl is TextBox)
                    _kainosEdgeAway[side] = 0;
                else if (++_kainosEdgeAway[side] * _kainosEdgeTimer.Interval >= 500)
                    hideEdge(side);
            }
        }

        // ---- layout ----

        // called by positionKainosColumn in place of its own layout while compact
        private void kainosCompactPlace()
        {
            int strip = KainosUI.S(6);
            int top = menuStrip1.Bottom + 4;
            int bottom = (statusStripMain.Visible ? statusStripMain.Top : ClientSize.Height) - 2;
            int w = ClientSize.Width;
            int areaBottom = bottom - 2;
            _kainosStripLeft.SetBounds(0, top, strip, Math.Max(10, areaBottom - top));
            _kainosStripRight.SetBounds(w - strip, top, strip, Math.Max(10, areaBottom - top));
            _kainosStripLeft.BringToFront();
            _kainosStripRight.BringToFront();
            kainosFitDisplay(w - strip - 2);
            kainosPlaceEdges();
        }

        private void kainosPlaceEdges()
        {
            if (_kainosEdgeLeft == null || !_kainosCompactOn) return;
            int strip = KainosUI.S(6);
            int top = menuStrip1.Bottom + 4;
            int bottom = (statusStripMain.Visible ? statusStripMain.Top : ClientSize.Height) - 4;
            int h = Math.Max(120, bottom - top);
            int dockW = KainosUI.S(80), colW = KainosUI.S(312);
            Point l = PointToScreen(new Point(strip, top)), r = PointToScreen(new Point(ClientSize.Width - strip - colW, top));
            Rectangle lb = new Rectangle(l, new Size(dockW + 8, h)), rb = new Rectangle(r, new Size(colW, h));
            if (_kainosEdgeLeft.Bounds != lb) _kainosEdgeLeft.Bounds = lb;
            if (_kainosEdgeRight.Bounds != rb) _kainosEdgeRight.Bounds = rb;
            _kainosDock.SetBounds(4, 4, dockW, h - 8);
            _kainosDock.ShowPower = panelPower.Visible;
            _kainosDock.ShowOptions = panelOptions.Visible;
            if (_kainosColumn.Bounds != new Rectangle(0, 0, colW, h)) _kainosColumn.SetBounds(0, 0, colW, h);
            if (_kainosEdgeRight.Visible) { _kainosColumn.ArrangeSections(); }
        }

        // ---- settings ----

        internal void KainosCompactLoad()
        {
            bool on = false;
            foreach (string kv in (KainosCompactSettings ?? "").Split(';'))
            {
                int eq = kv.IndexOf('=');
                if (eq <= 0) continue;
                string k = kv.Substring(0, eq), v = kv.Substring(eq + 1);
                if (k == "on") on = v == "1";
            }
            // after start-up has laid the console out
            if (on != _kainosCompactOn && IsHandleCreated) BeginInvoke(new Action(() => SetKainosCompact(on)));
            else if (on && !IsHandleCreated) Shown += (s, e) => BeginInvoke(new Action(() => SetKainosCompact(true)));
        }

        private void kainosCompactSave()
        {
            KainosCompactSettings = "on=" + (_kainosCompactOn ? 1 : 0);
            KainosSettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    // A borderless window owned by the console for the dock or the column in the compact view
    internal class KainosEdgeForm : Form
    {

        // where focus goes in this window when it's activated (by a click), instead of the first control that takes it:
        // that was the VFO SYNC panel's wheel-tune box, whose Thetis handler sends focus to a button in the console,
        // which took the activation straight back and lost the click (GitHub #32)
        public readonly Button FocusSink = new Button { Name = "kainosEdgeFocusSink", Size = new Size(1, 1), Location = new Point(-10, -10), TabStop = false };

        public KainosEdgeForm()
        {
            Controls.Add(FocusSink);
            ActiveControl = FocusSink;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = KainosUI.Surface;
            KeyPreview = false;
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00000080;       // WS_EX_TOOLWINDOW: not in Alt+Tab
                return cp;
            }
        }
    }
}
