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
    // mouse is at that edge; the panadapter fills the window, the flags stay, and a bar of the controls used most
    // runs along the bottom (right-click it to choose its groups). Expand puts everything back.
    //
    // The panadapter is drawn by DirectX, which covers any control placed over it, so the dock and the column slide
    // in as small windows of their own (like the flags), and the Thetis panels the column lays over itself (the TX
    // tab's mode panel, VFO SYNC, the meter) go with it.
    public partial class Console
    {
        private bool _kainosCompactOn;
        private KainosEdgeForm _kainosEdgeLeft, _kainosEdgeRight;
        private Panel _kainosStripLeft, _kainosStripRight;
        private KainosCompactBar _kainosCompactBar;
        private Timer _kainosEdgeTimer;
        private Size _kainosCompactSavedMin;
        private readonly int[] _kainosEdgeAway = new int[2];             // how long the mouse has been off each edge window (ticks)
        private readonly HashSet<string> _kainosCompactHidden = new HashSet<string>();

        // saved with the options (hidden Setup box txtKainosCompact): "on=1;hide=tx,dsp"
        public string KainosCompactSettings = "";

        internal bool KainosCompactOn { get { return _kainosCompactOn; } }

        // the menu bar's Collapse / Expand: true when it was ours (the Kainos layout)
        internal bool KainosCompactMenu()
        {
            if (!_kainosLayout || collapsedDisplay) return false;
            SetKainosCompact(!_kainosCompactOn);
            return true;
        }

        private void kainosCompactMenuText()
        {
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
                _kainosStripLeft.Visible = _kainosStripRight.Visible = _kainosCompactBar.Visible = on;
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
            _kainosCompactBar = new KainosCompactBar(this) { Name = "kainosCompactBar", Visible = false };
            Controls.AddRange(new Control[] { _kainosStripLeft, _kainosStripRight, _kainosCompactBar });
            _kainosEdgeTimer = new Timer { Interval = 120 };
            _kainosEdgeTimer.Tick += (s, e) => edgeTick();
            _kainosEdgeTimer.Start();
            FormClosing += (s, e) => { _kainosEdgeTimer.Stop(); };
            kainosBuildCompactBar();
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
            int barH = _kainosCompactBar.PreferredHeight(w - strip * 2);
            _kainosCompactBar.SetBounds(strip, bottom - barH, w - strip * 2, barH);
            _kainosCompactBar.BringToFront();
            int areaBottom = bottom - barH - 2;
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
            int bottom = _kainosCompactBar.Top - 2;
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

        // ---- the bar ----

        private void kainosBuildCompactBar()
        {
            KainosCompactBar b = _kainosCompactBar;
            Func<KainosUI.Tone, KeyValuePair<ButtonBase, string>[], KainosButtonGrid> grid = (tone, items) =>
            {
                KainosButtonGrid g = new KainosButtonGrid(items.Length, tone);
                Dictionary<ButtonBase, string> labels = items.ToDictionary(i => i.Key, i => i.Value);
                g.LabelFor = t => labels.ContainsKey(t) ? labels[t] : t.Text;
                g.SetTargets(items.Select(i => i.Key));
                return g;
            };
            Func<ButtonBase, string, KeyValuePair<ButtonBase, string>> it = (t, l) => new KeyValuePair<ButtonBase, string>(t, l);
            int bw = KainosUI.S(46);

            b.AddGroup("radio", "RADIO",
                Tuple.Create<Control, int>(grid(KainosUI.Tone.Ice, new[] { it(chkPower, "POWER") }), KainosUI.S(60)),
                Tuple.Create<Control, int>(grid(KainosUI.Tone.Tx, new[] { it(chkMOX, "MOX"), it(chkTUN, "TUN") }), bw * 2),
                Tuple.Create<Control, int>(grid(KainosUI.Tone.Gold, new[] { it(chkVOX, "VOX") }), bw),
                Tuple.Create<Control, int>(grid(KainosUI.Tone.Violet, new[] { it(chkRX2, "RX2") }), bw));

            KainosDropDown band = new KainosDropDown("BAND", KainosUI.Tone.Gold);
            band.BeforeOpen = () => band.SetTargets(orderedButtons(_bands_VHF_selected ? panelBandVHF : _bands_GEN_selected ? panelBandGEN : panelBandHF));
            band.BeforeOpen();
            KainosDropDown mode = new KainosDropDown("MODE", KainosUI.Tone.Gold);
            mode.SetTargets(orderedButtons(panelMode));
            KainosDropDown filter = new KainosDropDown("FILTER", KainosUI.Tone.Ice);
            filter.SetTargets(orderedButtons(panelFilter).Where(x => x is RadioButton));
            b.AddGroup("band", "BAND AND MODE",
                Tuple.Create<Control, int>(band, KainosUI.S(64)), Tuple.Create<Control, int>(mode, KainosUI.S(64)), Tuple.Create<Control, int>(filter, KainosUI.S(64)));

            b.AddGroup("vfo", "VFO",
                Tuple.Create<Control, int>(grid(KainosUI.Tone.Gold, new[] { it(chkVFOSplit, "SPLT") }), bw),
                Tuple.Create<Control, int>(grid(KainosUI.Tone.Ice, new[] { it(btnVFOAtoB, "A > B"), it(btnVFOSwap, "A <> B") }), KainosUI.S(108)),
                Tuple.Create<Control, int>(grid(KainosUI.Tone.Gold, new[] { it(chkRIT, "RIT") }), bw));

            b.AddGroup("audio", "AUDIO",
                Tuple.Create<Control, int>(new KainosSlider(ptbAF, "AF", true), KainosUI.S(120)),
                Tuple.Create<Control, int>(grid(KainosUI.Tone.Tx, new[] { it(chkMUT, "MUTE") }), KainosUI.S(52)),
                Tuple.Create<Control, int>(grid(KainosUI.Tone.Gold, new[] { it(chkSquelch, "SQL") }), bw));

            b.AddGroup("dsp", "DSP",
                Tuple.Create<Control, int>(grid(KainosUI.Tone.Ice, new[] { it(chkNR, "NR"), it(chkNB, "NB"), it(chkANF, "ANF") }), bw * 3),
                Tuple.Create<Control, int>(new KainosDropDown(comboAGC), KainosUI.S(74)));

            b.AddGroup("tx", "TRANSMIT",
                Tuple.Create<Control, int>(new KainosSlider(ptbPWR, "DRIVE", true), KainosUI.S(130)),
                Tuple.Create<Control, int>(grid(KainosUI.Tone.Gold, new[] { it(chkMicMute, "MIC"), it(chkCPDR, "COMP") }), KainosUI.S(100)));

            b.MenuWanted += kainosCompactBarMenu;
            b.ApplyHidden(_kainosCompactHidden);
        }

        private void kainosCompactBarMenu(Point at)
        {
            ContextMenuStrip menu = new ContextMenuStrip { Renderer = new KainosToolStripRenderer(), BackColor = KainosUI.Raised, ForeColor = KainosUI.Text };
            menu.Items.Add(new ToolStripMenuItem("Show on the bar") { Enabled = false });
            foreach (KeyValuePair<string, string> g in _kainosCompactBar.Groups)
            {
                string key = g.Key;
                ToolStripMenuItem item = new ToolStripMenuItem("   " + g.Value) { Checked = !_kainosCompactHidden.Contains(key), ForeColor = KainosUI.Text };
                item.Click += (s, e) =>
                {
                    if (!_kainosCompactHidden.Remove(key)) _kainosCompactHidden.Add(key);
                    _kainosCompactBar.ApplyHidden(_kainosCompactHidden);
                    positionKainosColumn();
                    kainosCompactSave();
                };
                menu.Items.Add(item);
            }
            menu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem expand = new ToolStripMenuItem("Expand (dock and column back)") { ForeColor = KainosUI.Text };
            expand.Click += (s, e) => SetKainosCompact(false);
            menu.Items.Add(expand);
            menu.Closed += (s, e) => BeginInvoke(new Action(menu.Dispose));
            menu.Show(_kainosCompactBar, at);
        }

        // ---- settings ----

        internal void KainosCompactLoad()
        {
            bool on = false;
            _kainosCompactHidden.Clear();
            foreach (string kv in (KainosCompactSettings ?? "").Split(';'))
            {
                int eq = kv.IndexOf('=');
                if (eq <= 0) continue;
                string k = kv.Substring(0, eq), v = kv.Substring(eq + 1);
                if (k == "on") on = v == "1";
                else if (k == "hide") foreach (string g in v.Split(',')) if (g.Length > 0) _kainosCompactHidden.Add(g);
            }
            if (_kainosCompactBar != null) _kainosCompactBar.ApplyHidden(_kainosCompactHidden);
            // after start-up has laid the console out
            if (on != _kainosCompactOn && IsHandleCreated) BeginInvoke(new Action(() => SetKainosCompact(on)));
            else if (on && !IsHandleCreated) Shown += (s, e) => BeginInvoke(new Action(() => SetKainosCompact(true)));
        }

        private void kainosCompactSave()
        {
            KainosCompactSettings = "on=" + (_kainosCompactOn ? 1 : 0) + ";hide=" + string.Join(",", _kainosCompactHidden);
            KainosSettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    // A borderless window owned by the console for the dock or the column in the compact view
    internal class KainosEdgeForm : Form
    {
        public KainosEdgeForm()
        {
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

    // The compact view's bar: groups of controls (caption over a row), wrapping to more rows when the window is narrow
    internal class KainosCompactBar : Control
    {
        private class Group
        {
            public string Key, Title;
            public List<Tuple<Control, int>> Items = new List<Tuple<Control, int>>();
            public bool Hidden;
            public RectangleF CaptionRect;
        }

        private readonly Console _console;
        private readonly List<Group> _groups = new List<Group>();
        private readonly Timer _follow;
        public event Action<Point> MenuWanted;

        public KainosCompactBar(Console console)
        {
            _console = console;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = KainosUI.Surface;
            // Thetis changes the buttons' states behind them (CAT, keyboard, the flags)
            _follow = new Timer { Interval = 250 };
            _follow.Tick += (s, e) => { if (Visible) foreach (Control c in Controls) c.Invalidate(); };
            _follow.Start();
        }

        protected override void Dispose(bool disposing) { if (disposing) _follow.Dispose(); base.Dispose(disposing); }

        public IEnumerable<KeyValuePair<string, string>> Groups { get { return _groups.Select(g => new KeyValuePair<string, string>(g.Key, g.Title)); } }

        public void AddGroup(string key, string title, params Tuple<Control, int>[] items)
        {
            Group g = new Group { Key = key, Title = title };
            g.Items.AddRange(items);
            _groups.Add(g);
            foreach (Tuple<Control, int> i in items) Controls.Add(i.Item1);
        }

        public void ApplyHidden(ICollection<string> hidden)
        {
            foreach (Group g in _groups)
            {
                g.Hidden = hidden.Contains(g.Key);
                foreach (Tuple<Control, int> i in g.Items) i.Item1.Visible = !g.Hidden;
            }
            Invalidate();
        }

        private static int rowH { get { return KainosUI.S(24); } }
        private static int capH { get { return KainosUI.S(14); } }
        private static int gap { get { return KainosUI.S(4); } }
        private static int groupGap { get { return KainosUI.S(16); } }
        private static int pad { get { return KainosUI.S(6); } }

        private int groupWidth(Group g) { return g.Items.Sum(i => i.Item2) + gap * (g.Items.Count - 1); }

        // the rows the groups fall into at this width
        private List<List<Group>> rows(int width)
        {
            List<List<Group>> r = new List<List<Group>> { new List<Group>() };
            int x = pad;
            foreach (Group g in _groups.Where(g => !g.Hidden))
            {
                int w = groupWidth(g);
                if (r.Last().Count > 0 && x + w > width - pad) { r.Add(new List<Group>()); x = pad; }
                r.Last().Add(g);
                x += w + groupGap;
            }
            return r;
        }

        public int PreferredHeight(int width) { return pad + rows(width).Count * (capH + rowH + pad); }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            int y = pad;
            foreach (List<Group> row in rows(Width))
            {
                int x = pad;
                foreach (Group g in row)
                {
                    g.CaptionRect = new RectangleF(x, y, groupWidth(g), capH);
                    int ix = x;
                    foreach (Tuple<Control, int> i in g.Items)
                    {
                        i.Item1.SetBounds(ix, y + capH, i.Item2, rowH);
                        ix += i.Item2 + gap;
                    }
                    x += groupWidth(g) + groupGap;
                }
                y += capH + rowH + pad;
            }
            Invalidate();
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); PerformLayout(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen line = new Pen(KainosUI.Line)) e.Graphics.DrawLine(line, 0, 0, Width, 0);
            using (Font f = new Font("Segoe UI", Math.Max(7f, KainosUI.S(9)), FontStyle.Bold, GraphicsUnit.Pixel))
            using (Brush b = new SolidBrush(KainosUI.Faint))
                foreach (Group g in _groups.Where(g => !g.Hidden))
                    e.Graphics.DrawString(g.Title, f, b, g.CaptionRect.Left, g.CaptionRect.Top);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Right) MenuWanted?.Invoke(e.Location);
        }
    }
}
