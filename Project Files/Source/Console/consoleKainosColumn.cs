/*  consoleKainosColumn.cs

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
    // Kainos layout, stage 3: the right-hand column. A bar of toggle tabs at the top; each tab that is on shows
    // its panel in the column below (AetherSDR's applets). Stage 3a: METERS and BAND.
    //
    // Three ways Thetis's controls come into the column, none of which changes Thetis's own code:
    //  - Kainos-drawn stand-ins (KainosButtonGrid) bound to Thetis buttons, like the dock: band, mode, filter.
    //  - Thetis controls that Thetis never moves are moved into the column as they are (the filter width and
    //    shift sliders, low / high boxes) and moved back in Classic.
    //  - Thetis controls that Thetis does move stay where Thetis keeps them and are pinned over the column: the
    //    meter container, which MeterManager places on every resize.
    // Thetis's own band, mode, filter and meter panels are collapsed to zero size (not hidden) while the
    // column shows them.
    public partial class Console
    {
        private KainosColumn _kainosColumn;
        private KainosButtonGrid _kainosBandGrid, _kainosModeGrid, _kainosFilterGrid, _kainosShiftReset;
        private readonly Dictionary<Control, Size> _kainosCollapsed = new Dictionary<Control, Size>();
        private readonly List<KeyValuePair<Control, Point>> _kainosMovedIn = new List<KeyValuePair<Control, Point>>();
        private bool _kainosShown;
        private bool _kainosPlacing;

        // Setup > Appearance > Kainos keeps these (hidden boxes) so they are saved with the other options
        public string KainosColumnTabs = "meters,band";
        public string KainosMeterId = "";
        public event EventHandler KainosSettingsChanged;

        private Control[] kainosCollapseTargets
        {
            get { return new Control[] { panelBandHF, panelBandGEN, panelBandVHF, panelMode, panelFilter, grpMultimeter, grpMultimeterMenus }; }
        }

        // the filter controls Thetis never moves (it only looks for the radio buttons inside panelFilter)
        private Control[] kainosFilterExtras
        {
            get { return new Control[] { lblFilterWidth, ptbFilterWidth, lblFilterShift, ptbFilterShift, lblFilterLow, udFilterLow, lblFilterHigh, udFilterHigh }; }
        }

        private void kainosColumnOn()
        {
            if (_kainosColumn == null)
            {
                _kainosColumn = new KainosColumn(this);
                Controls.Add(_kainosColumn);

                _kainosBandGrid = new KainosButtonGrid(3, KainosUI.Tone.Gold);
                _kainosModeGrid = new KainosButtonGrid(3, KainosUI.Tone.Gold);
                _kainosFilterGrid = new KainosButtonGrid(3, KainosUI.Tone.Ice);
                _kainosShiftReset = new KainosButtonGrid(1, KainosUI.Tone.Ice) { LabelFor = b => "Reset" };   // the skin's image button
                _kainosColumn.AddSection("meters", "METERS", kainosMetersMeasure, kainosMetersArrange);
                _kainosColumn.AddSection("band", "BAND", kainosBandMeasure, kainosBandArrange);
                _kainosColumn.TabsChanged += (s, e) =>
                {
                    KainosColumnTabs = _kainosColumn.TabState;
                    KainosSettingsChanged?.Invoke(this, EventArgs.Empty);
                    positionKainosColumn();
                };

                // the HF / GEN / VHF band panels are swapped by Thetis (its VHF+ / HF buttons)
                foreach (Control p in new Control[] { panelBandHF, panelBandGEN, panelBandVHF })
                    p.VisibleChanged += (s, e) => { if (_kainosLayout) { refreshKainosBandGrid(); positionKainosColumn(); } };

                // anything Thetis moves or resizes can change the free space on the right
                SizeChanged += (s, e) => positionKainosColumn();
                panelDisplay.SizeChanged += (s, e) => positionKainosColumn();
                foreach (Control c in Controls)
                    if (!(c is KainosColumn) && !(c is KainosDock))
                        c.LocationChanged += (s, e) => { if (_kainosLayout && !_kainosPlacing) positionKainosColumn(); };

                Shown += (s, e) => { _kainosShown = true; if (_kainosLayout) BeginInvoke(new Action(kainosAttachMeter)); };
                KainosUI.ScaleChanged += (s, e) => positionKainosColumn();
            }

            _kainosColumn.TabState = KainosColumnTabs;
            refreshKainosBandGrid();
            _kainosModeGrid.SetTargets(orderedButtons(panelMode));
            _kainosFilterGrid.SetTargets(orderedButtons(panelFilter).Where(b => b is RadioButton));
            _kainosShiftReset.SetTargets(new ButtonBase[] { btnFilterShiftReset });

            // filter sliders and boxes into the column
            if (_kainosMovedIn.Count == 0)
                foreach (Control c in kainosFilterExtras)
                {
                    _kainosMovedIn.Add(new KeyValuePair<Control, Point>(c, c.Location));
                    _kainosColumn.Controls.Add(c);       // leaves panelFilter
                }
            foreach (Control c in new Control[] { _kainosBandGrid, _kainosModeGrid, _kainosFilterGrid, _kainosShiftReset })
                if (c.Parent != _kainosColumn) _kainosColumn.Controls.Add(c);

            foreach (Control c in kainosCollapseTargets) kainosCollapse(c);
            _kainosColumn.Visible = true;
            if (_kainosShown) kainosAttachMeter();
            positionKainosColumn();
        }

        private void kainosColumnOff()
        {
            if (_kainosColumn == null) return;
            _kainosColumn.Visible = false;
            foreach (KeyValuePair<Control, Point> kv in _kainosMovedIn)
            {
                panelFilter.Controls.Add(kv.Key);
                kv.Key.Location = kv.Value;
            }
            _kainosMovedIn.Clear();
            foreach (KeyValuePair<Control, Size> kv in _kainosCollapsed.ToList())
                kv.Key.Size = kv.Value;
            _kainosCollapsed.Clear();
            kainosDetachMeter();
            ResizeConsole(h_delta, v_delta);     // Thetis puts the panadapter and its panels back
        }

        // collapse to zero size; a resize or skin load that sizes it again is caught and the new size kept for Classic
        private void kainosCollapse(Control c)
        {
            if (!_kainosCollapsed.ContainsKey(c))
            {
                _kainosCollapsed[c] = c.Size;
                c.SizeChanged += kainosCollapsedSizeChanged;
            }
            else if (c.Size != Size.Empty) _kainosCollapsed[c] = c.Size;
            c.Size = Size.Empty;
        }

        private void kainosCollapsedSizeChanged(object sender, EventArgs e)
        {
            Control c = (Control)sender;
            if (!_kainosLayout || !_kainosCollapsed.ContainsKey(c) || c.Size == Size.Empty) return;
            _kainosCollapsed[c] = c.Size;
            BeginInvoke(new Action(() => { if (_kainosLayout && _kainosCollapsed.ContainsKey(c)) c.Size = Size.Empty; }));
        }

        private static IEnumerable<ButtonBase> orderedButtons(Control panel)
        {
            return panel.Controls.OfType<ButtonBase>().OrderBy(b => b.Top).ThenBy(b => b.Left);
        }

        private void refreshKainosBandGrid()
        {
            Control p = panelBandVHF.Visible ? panelBandVHF : panelBandGEN.Visible ? panelBandGEN : panelBandHF;
            _kainosBandGrid.SetTargets(orderedButtons(p));
        }

        // The column runs down the right-hand side, below and above any Thetis panels that still reach into that
        // space (at narrow window sizes the VFO B box and the bottom panels can), and the panadapter gives way
        private void positionKainosColumn()
        {
            if (_kainosColumn == null || !_kainosLayout || _kainosPlacing) return;
            _kainosPlacing = true;
            try
            {
                int width = KainosUI.S(300);
                int x = ClientSize.Width - width - 4;
                int top = menuStrip1.Bottom + 4;
                int bottom = (statusStripMain.Visible ? statusStripMain.Top : ClientSize.Height) - 4;
                int mid = (top + bottom) / 2;
                foreach (Control c in Controls)
                {
                    if (c == _kainosColumn || c is KainosDock || c == panelDisplay || c == menuStrip1 || c == statusStripMain) continue;
                    if (!c.Visible || c.Width == 0 || c.Height == 0) continue;
                    if (_kainosMeter != null && c == _kainosMeter) continue;
                    if (c.Right <= x || c.Left >= x + width) continue;
                    if (c.Bottom < mid) top = Math.Max(top, c.Bottom + 4);
                    else bottom = Math.Min(bottom, c.Top - 4);
                }
                _kainosColumn.SetBounds(x, top, width, Math.Max(80, bottom - top));
                _kainosColumn.SendToBack();
                if (panelDisplay.Right > x - 6) panelDisplay.Width = Math.Max(200, x - 6 - panelDisplay.Left);
                _kainosColumn.ArrangeSections();
            }
            finally
            {
                _kainosPlacing = false;
            }
        }

        #region BAND

        private int kainosBandMeasure(int w)
        {
            int gap = KainosUI.S(8);
            return _kainosBandGrid.PreferredHeight(w) + gap + _kainosModeGrid.PreferredHeight(w) + gap + _kainosFilterGrid.PreferredHeight(w)
                   + gap + kainosFilterExtrasHeight();
        }

        private int kainosFilterExtrasHeight() { return KainosUI.S(24) * 3 + KainosUI.S(4) * 2; }

        private void kainosBandArrange(Rectangle r)
        {
            int gap = KainosUI.S(8), y = r.Top;
            foreach (KainosButtonGrid g in new[] { _kainosBandGrid, _kainosModeGrid, _kainosFilterGrid })
            {
                int h = g.PreferredHeight(r.Width);
                g.SetBounds(r.Left, y, r.Width, h);
                y += h + gap;
            }
            // width and shift sliders, each with its label, then low / high
            int row = KainosUI.S(24), lw = KainosUI.S(42);
            placeLabel(lblFilterWidth, r.Left, y, lw, row);
            ptbFilterWidth.SetBounds(r.Left + lw, y, r.Width - lw, row);
            y += row + KainosUI.S(4);
            placeLabel(lblFilterShift, r.Left, y, lw, row);
            int reset = KainosUI.S(44);
            ptbFilterShift.SetBounds(r.Left + lw, y, r.Width - lw - reset - 4, row);
            _kainosShiftReset.SetBounds(r.Right - reset, y, reset, row);
            y += row + KainosUI.S(4);
            int half = r.Width / 2;
            placeLabel(lblFilterLow, r.Left, y, lw, row);
            udFilterLow.SetBounds(r.Left + lw, y + 2, half - lw - 6, row - 4);
            placeLabel(lblFilterHigh, r.Left + half, y, lw, row);
            udFilterHigh.SetBounds(r.Left + half + lw, y + 2, half - lw, row - 4);
        }

        private static void placeLabel(Control l, int x, int y, int w, int h)
        {
            l.SetBounds(x, y, w, h);
            l.ForeColor = KainosUI.Dim;
            Label lbl = l as Label;
            if (lbl != null) { lbl.AutoSize = false; lbl.TextAlign = ContentAlignment.MiddleLeft; }
        }

        #endregion

        #region METERS

        // The METERS tab holds a Thetis meter container of its own (made the first time, an ANAN multimeter, the
        // meter OE3IDE's FTDX-5000 skin draws). It stays a docked container that MeterManager owns and saves; Kainos
        // only sizes it to the column and keeps it there, and turns it off in Classic.
        private ucMeter _kainosMeter;

        private void kainosAttachMeter()
        {
            if (!_kainosLayout || !_kainosShown) return;
            Dictionary<string, ucMeter> all = MeterManager.MeterContainers;
            if (string.IsNullOrEmpty(KainosMeterId) || all == null || !all.ContainsKey(KainosMeterId))
            {
                KainosMeterId = MeterManager.AddMeterContainer(1, false);
                MeterManager.clsMeter m = MeterManager.MeterFromId(KainosMeterId);
                if (m != null)
                {
                    m.AddMeter(MeterType.ANANMM);
                    m.ZeroOut(true, true);
                    m.Rebuild();
                }
                MeterManager.NoTitle(KainosMeterId, true);
                MeterManager.AutoContainerHeight(KainosMeterId, true);
                KainosSettingsChanged?.Invoke(this, EventArgs.Empty);
                all = MeterManager.MeterContainers;
            }
            if (all == null || !all.ContainsKey(KainosMeterId)) return;
            ucMeter uc = all[KainosMeterId];
            if (_kainosMeter != uc)
            {
                _kainosMeter = uc;
                uc.LocationChanged += (s, e) => { if (_kainosLayout && !_kainosPlacing) positionKainosColumn(); };
                uc.SizeChanged += (s, e) => { if (_kainosLayout && !_kainosPlacing) positionKainosColumn(); };
            }
            MeterManager.enableContainer(KainosMeterId, _kainosColumn.IsOn("meters"));
            positionKainosColumn();
        }

        private void kainosDetachMeter()
        {
            if (_kainosMeter != null && !string.IsNullOrEmpty(KainosMeterId))
                MeterManager.enableContainer(KainosMeterId, false);
        }

        private int kainosMetersMeasure(int w)
        {
            if (_kainosMeter == null) return (int)(w * 0.55f);
            return Math.Max(KainosUI.S(40), _kainosMeter.Height);
        }

        private void kainosMetersArrange(Rectangle r)
        {
            if (_kainosMeter == null || string.IsNullOrEmpty(KainosMeterId)) return;
            bool on = _kainosColumn.IsOn("meters") && _kainosColumn.Visible;
            if (_kainosMeter.MeterEnabled != on) MeterManager.enableContainer(KainosMeterId, on);
            if (!on) return;
            Point p = new Point(_kainosColumn.Left + r.Left, _kainosColumn.Top + r.Top);
            if (_kainosMeter.Width != r.Width) _kainosMeter.Width = r.Width;
            if (_kainosMeter.Location != p) _kainosMeter.Location = p;
            _kainosMeter.BringToFront();
        }

        #endregion
    }

    // The column: a bar of toggle tabs, then the panels of the tabs that are on, scrolled with the mouse wheel
    // if they don't all fit
    internal class KainosColumn : Panel
    {
        private class Section
        {
            public string Key, Title;
            public bool On;
            public Func<int, int> Measure;
            public Action<Rectangle> Arrange;
            public RectangleF TabRect;
            public Rectangle HeaderRect;
        }

        private readonly Console _console;
        private readonly List<Section> _sections = new List<Section>();
        private int _scroll, _contentHeight;
        private Section _hoverTab;
        public event EventHandler TabsChanged;

        public KainosColumn(Console console)
        {
            _console = console;
            Name = "kainosColumn";
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = KainosUI.Surface;
        }

        public void AddSection(string key, string title, Func<int, int> measure, Action<Rectangle> arrange)
        {
            _sections.Add(new Section { Key = key, Title = title, On = true, Measure = measure, Arrange = arrange });
        }

        public bool IsOn(string key) { Section s = _sections.Find(x => x.Key == key); return s != null && s.On; }

        // "meters,band": the tabs that are on
        public string TabState
        {
            get { return string.Join(",", _sections.Where(s => s.On).Select(s => s.Key)); }
            set
            {
                HashSet<string> on = new HashSet<string>((value ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
                foreach (Section s in _sections) s.On = on.Contains(s.Key);
                Invalidate();
            }
        }

        private int tabBarHeight()
        {
            layoutTabs();
            float bottom = 0;
            foreach (Section s in _sections) bottom = Math.Max(bottom, s.TabRect.Bottom);
            return (int)bottom + KainosUI.S(8);
        }

        private void layoutTabs()
        {
            float pad = KainosUI.S(8), gap = KainosUI.S(4), h = KainosUI.S(24), x = pad, y = pad;
            using (Graphics g = CreateGraphics())
            using (Font f = tabFont())
                foreach (Section s in _sections)
                {
                    float w = g.MeasureString(s.Title, f).Width + KainosUI.S(16);
                    if (x + w > Width - pad && x > pad) { x = pad; y += h + gap; }
                    s.TabRect = new RectangleF(x, y, w, h);
                    x += w + gap;
                }
        }

        private static Font tabFont() { return new Font("Segoe UI", Math.Max(8f, KainosUI.S(10)), FontStyle.Bold, GraphicsUnit.Pixel); }

        // lays out the panels of the tabs that are on (called by the console after it places the column)
        public void ArrangeSections()
        {
            int pad = KainosUI.S(8), header = KainosUI.S(24), gap = KainosUI.S(8);
            int top = tabBarHeight(), w = Width - pad * 2;
            int total = 0;
            foreach (Section s in _sections) if (s.On) total += header + s.Measure(w) + gap;
            _contentHeight = total;
            _scroll = Math.Max(0, Math.Min(_scroll, total - (Height - top)));
            int y = top - _scroll;
            foreach (Section s in _sections)
            {
                if (!s.On) { s.HeaderRect = Rectangle.Empty; s.Arrange(new Rectangle(pad, -10000, w, 0)); continue; }
                int h = s.Measure(w);
                s.HeaderRect = new Rectangle(pad, y, w, header);
                s.Arrange(new Rectangle(pad, y + header, w, h));
                y += header + h + gap;
            }
            // section contents scrolled above the tab bar are hidden behind it
            foreach (Control c in Controls) c.Visible = c.Bottom > top && sectionOnFor(c);
            Invalidate();
        }

        // a control placed far off (its tab is off) stays hidden
        private bool sectionOnFor(Control c) { return c.Top > -5000; }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            int before = _scroll;
            _scroll = Math.Max(0, Math.Min(_scroll - Math.Sign(e.Delta) * KainosUI.S(40), Math.Max(0, _contentHeight - (Height - tabBarHeight()))));
            if (_scroll != before) ArrangeSections();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Section h = _sections.Find(s => s.TabRect.Contains(e.Location));
            if (h != _hoverTab) { _hoverTab = h; Cursor = h != null ? Cursors.Hand : Cursors.Default; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hoverTab = null; Invalidate(); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            Section s = _sections.Find(x => x.TabRect.Contains(e.Location));
            if (s == null || e.Button != MouseButtons.Left) return;
            s.On = !s.On;
            TabsChanged?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (Pen p = new Pen(KainosUI.Line)) g.DrawLine(p, 0, 0, 0, Height);
            int top = tabBarHeight();
            using (Font f = tabFont())
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                foreach (Section s in _sections)
                {
                    Color border = s.On ? KainosUI.Gold : KainosUI.Line, fore = s.On ? KainosUI.GoldHi : (s == _hoverTab ? KainosUI.Dim : KainosUI.Faint);
                    using (System.Drawing.Drawing2D.GraphicsPath path = KainosUI.RoundedRect(s.TabRect, s.TabRect.Height / 2f))
                    {
                        if (s.On) using (Brush b = new SolidBrush(KainosUI.Selected)) g.FillPath(b, path);
                        using (Pen p = new Pen(border)) g.DrawPath(p, path);
                    }
                    using (Brush b = new SolidBrush(fore)) g.DrawString(s.Title, f, b, s.TabRect, sf);
                }
            }
            using (Pen p = new Pen(KainosUI.Line)) g.DrawLine(p, 0, top - KainosUI.S(4), Width, top - KainosUI.S(4));
            g.SetClip(new Rectangle(0, top - KainosUI.S(2), Width, Height));
            using (Font f = new Font("Segoe UI", Math.Max(8f, KainosUI.S(11)), FontStyle.Bold, GraphicsUnit.Pixel))
            using (Brush b = new SolidBrush(KainosUI.Ice))
            using (Pen line = new Pen(KainosUI.Line))
                foreach (Section s in _sections)
                {
                    if (!s.On || s.HeaderRect.IsEmpty) continue;
                    g.DrawString(s.Title, f, b, s.HeaderRect.Left, s.HeaderRect.Top + KainosUI.S(5));
                    g.DrawLine(line, s.HeaderRect.Left, s.HeaderRect.Bottom - 2, s.HeaderRect.Right, s.HeaderRect.Bottom - 2);
                }
        }
    }

    // A grid of Kainos-drawn buttons bound to Thetis buttons (radio buttons, check boxes or plain buttons): a click
    // is a real click on the Thetis button, a right click its right click, and each shows the Thetis button's text,
    // state and enabled / visible state
    internal class KainosButtonGrid : Control
    {
        private class Cell { public ButtonBase Target; public RectangleF Rect; }

        // the text to show for a button (default: the Thetis button's own text)
        public Func<ButtonBase, string> LabelFor = b => b.Text.Replace("&&", "&");

        private readonly int _columns;
        private readonly KainosUI.Tone _tone;
        private List<Cell> _cells = new List<Cell>();
        private Cell _hover;

        private static readonly System.Reflection.MethodInfo _onClick = typeof(Control).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        private static readonly System.Reflection.MethodInfo _onMouseDown = typeof(Control).GetMethod("OnMouseDown", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        private static readonly System.Reflection.MethodInfo _onMouseUp = typeof(Control).GetMethod("OnMouseUp", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        public KainosButtonGrid(int columns, KainosUI.Tone tone)
        {
            _columns = columns;
            _tone = tone;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = KainosUI.Surface;
        }

        public void SetTargets(IEnumerable<ButtonBase> targets)
        {
            foreach (Cell c in _cells) unhook(c.Target);
            _cells = targets.Select(t => new Cell { Target = t }).ToList();
            foreach (Cell c in _cells) hook(c.Target);
            Invalidate();
        }

        private void hook(ButtonBase t)
        {
            t.TextChanged += changed; t.EnabledChanged += changed; t.VisibleChanged += changed;
            if (t is RadioButton) ((RadioButton)t).CheckedChanged += changed;
            if (t is CheckBox) ((CheckBox)t).CheckedChanged += changed;
        }

        private void unhook(ButtonBase t)
        {
            t.TextChanged -= changed; t.EnabledChanged -= changed; t.VisibleChanged -= changed;
            if (t is RadioButton) ((RadioButton)t).CheckedChanged -= changed;
            if (t is CheckBox) ((CheckBox)t).CheckedChanged -= changed;
        }

        private void changed(object sender, EventArgs e)
        {
            if (IsDisposed || !IsHandleCreated) return;
            if (InvokeRequired) BeginInvoke(new Action(Invalidate)); else Invalidate();
        }

        // a Thetis button counts as shown unless it was hidden itself (its panel is collapsed, not hidden)
        private bool shown(ButtonBase t) { return t.Visible && LabelFor(t).Length > 0; }

        private List<Cell> shownCells() { return _cells.Where(c => shown(c.Target)).ToList(); }

        public int PreferredHeight(int width)
        {
            int rows = (shownCells().Count + _columns - 1) / _columns;
            return rows == 0 ? 0 : rows * KainosUI.S(26) + (rows - 1) * KainosUI.S(4);
        }

        private static bool isOn(ButtonBase t)
        {
            RadioButton r = t as RadioButton;
            if (r != null) return r.Checked;
            CheckBox c = t as CheckBox;
            return c != null && c.Checked;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            List<Cell> cells = shownCells();
            float gap = KainosUI.S(4), h = Math.Min(KainosUI.S(26), Height), w = (Width - gap * (_columns - 1)) / (float)_columns;
            foreach (Cell c in _cells) c.Rect = RectangleF.Empty;
            for (int i = 0; i < cells.Count; i++)
            {
                Cell c = cells[i];
                c.Rect = new RectangleF((i % _columns) * (w + gap), (i / _columns) * (h + gap), w, h);
                KainosUI.DrawButton(e.Graphics, c.Rect, LabelFor(c.Target), isOn(c.Target), c.Target.Enabled, c == _hover, _tone, Math.Max(9f, KainosUI.S(12)));
            }
        }

        private Cell hit(Point p) { return _cells.Find(c => !c.Rect.IsEmpty && c.Rect.Contains(p)); }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Cell h = hit(e.Location);
            if (h != _hover) { _hover = h; Cursor = h != null ? Cursors.Hand : Cursors.Default; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = null; Invalidate(); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Cell c = hit(e.Location);
            if (c == null || !c.Target.Enabled) return;
            if (e.Button == MouseButtons.Left) _onClick.Invoke(c.Target, new object[] { EventArgs.Empty });
            else if (e.Button == MouseButtons.Right)
            {
                MouseEventArgs m = new MouseEventArgs(MouseButtons.Right, 1, 1, 1, 0);
                _onMouseDown.Invoke(c.Target, new object[] { m });
                _onMouseUp.Invoke(c.Target, new object[] { m });
            }
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            // let the column scroll
            base.OnMouseWheel(e);
            if (Parent != null) typeof(Control).GetMethod("OnMouseWheel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(Parent, new object[] { e });
        }
    }
}
