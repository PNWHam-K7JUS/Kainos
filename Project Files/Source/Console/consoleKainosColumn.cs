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
    // its panel in the column below (AetherSDR's applets): METERS, BAND, RX, TX.
    //
    // Three ways Thetis's controls come into the column, none of which changes Thetis's own code:
    //  - Kainos-drawn stand-ins bound to Thetis buttons, like the dock: band, mode and filter (drop-downs,
    //    KainosDropDown), the filter shift reset (KainosButtonGrid).
    //  - Thetis controls moved into the column as they are (filter width / shift / low / high; the AF, AGC,
    //    preamp, squelch, drive and tune controls) and moved back in Classic. Thetis decides whether each is
    //    shown (RX2 AF only with RX2 on, preamp or step attenuator...); the column only places them.
    //  - Thetis controls that Thetis places itself stay console controls and are pinned over the column: the
    //    meter container, the mode panel (phone / CW / digital / FM, whichever Thetis shows) and the VFO sync
    //    box (VFO SYNC: Thetis's box between its VFO A and B boxes, which are collapsed; the VFO tab replaces them).
    // Thetis's own panels that the column replaces are collapsed to zero size (not hidden). While Thetis's
    // display is collapsed (its Collapse menu), the dock and column step aside and Thetis lays out as usual.
    public partial class Console
    {
        private KainosColumn _kainosColumn;
        private KainosDropDown _kainosBandDrop, _kainosModeDrop, _kainosFilterDrop;
        private KainosButtonGrid _kainosShiftReset;
        private readonly Dictionary<Control, Size> _kainosCollapsed = new Dictionary<Control, Size>();
        private readonly Dictionary<Control, Func<Size, Size>> _kainosCollapseTo = new Dictionary<Control, Func<Size, Size>>();
        private readonly Dictionary<Control, KeyValuePair<Control, Point>> _kainosMovedIn = new Dictionary<Control, KeyValuePair<Control, Point>>();
        private bool _kainosShown;
        private bool _kainosPlacing;
        private bool _kainosPartsOn;            // the dock and column are in place (off while Thetis's display is collapsed)

        // Setup > Appearance > Kainos keeps these (hidden boxes) so they are saved with the other options
        public string KainosColumnTabs = "meters,band,rx,tx";
        public string KainosMeterId = "";
        public event EventHandler KainosSettingsChanged;

        private Control[] kainosCollapseTargets
        {
            get
            {
                // the panels under the panadapter: DSP (the flags' DSP tab), VFO (split / copy / swap / zero beat / IF in
                // the dock, RIT / XIT on the flags, VAC in the dock), display and multi-RX (the panadapter's bar)
                return new Control[] { panelBandHF, panelBandGEN, panelBandVHF, panelMode, panelFilter, grpMultimeter, grpMultimeterMenus, panelSoundControls,
                                       grpVFOA, grpVFOB, panelDSP, panelVFO, panelDisplay2, panelMultiRX };
            }
        }

        // the filter controls Thetis never moves (it only looks for the radio buttons inside panelFilter)
        private Control[] kainosFilterExtras
        {
            get { return new Control[] { lblFilterWidth, ptbFilterWidth, lblFilterShift, ptbFilterShift, lblFilterLow, udFilterLow, lblFilterHigh, udFilterHigh }; }
        }

        // RX: label / control rows (a control Thetis has hidden is left out)
        private Control[][] kainosRxRows
        {
            get
            {
                return new[]
                {
                    new Control[] { lblAF, ptbAF }, new Control[] { lblRX1AF, ptbRX1AF }, new Control[] { lblRX2AF, ptbRX2AF },
                    new Control[] { lblRF, ptbRF },
                    new Control[] { lblAGC, comboAGC, lblPreamp, comboPreamp, udRX1StepAttData },
                    new Control[] { chkSquelch, ptbSquelch, picSquelch },
                };
            }
        }

        private Control[][] kainosTxRows
        {
            get { return new[] { new Control[] { lblPWR, ptbPWR }, new Control[] { lblTune, ptbTune } }; }
        }

        private Control[] kainosModePanels
        {
            get { return new Control[] { panelModeSpecificPhone, panelModeSpecificCW, panelModeSpecificDigital, panelModeSpecificFM }; }
        }

        private void kainosColumnOn()
        {
            if (_kainosColumn == null)
            {
                _kainosColumn = new KainosColumn(this);
                Controls.Add(_kainosColumn);

                _kainosBandDrop = new KainosDropDown("BAND", KainosUI.Tone.Gold);
                _kainosModeDrop = new KainosDropDown("MODE", KainosUI.Tone.Gold);
                _kainosFilterDrop = new KainosDropDown("FILTER", KainosUI.Tone.Ice);
                _kainosShiftReset = new KainosButtonGrid(1, KainosUI.Tone.Ice) { LabelFor = b => "Reset" };   // the skin's image button
                kainosAddVfoSection();          // the VFO faces: consoleKainosFlag.cs
                _kainosColumn.AddSection("sync", "VFO SYNC", kainosSyncMeasure, kainosSyncArrange);
                _kainosColumn.AddSection("meters", "METERS", kainosMetersMeasure, kainosMetersArrange);
                _kainosColumn.AddSection("band", "BAND", kainosBandMeasure, kainosBandArrange);
                _kainosColumn.AddSection("rx", "RX", w => kainosRowsHeight(kainosRxRows), r => kainosRowsArrange(kainosRxRows, r));
                _kainosColumn.AddSection("tx", "TX", kainosTxMeasure, kainosTxArrange);
                kainosAddMoreSections();        // EQ, KAINOS AUDIO, FREEDV, MEMORY: consoleKainosTabs.cs
                _kainosColumn.TabsChanged += (s, e) =>
                {
                    KainosColumnTabs = _kainosColumn.TabState;
                    KainosSettingsChanged?.Invoke(this, EventArgs.Empty);
                    positionKainosColumn();
                };

                // the HF / GEN / VHF band panels are swapped by Thetis (its VHF+ / HF buttons)
                foreach (Control p in new Control[] { panelBandHF, panelBandGEN, panelBandVHF })
                    p.VisibleChanged += (s, e) => { if (_kainosLayout) { refreshKainosBandGrid(); positionKainosColumn(); } };
                // Thetis shows the mode panel of the current mode, and shows / hides some RX and TX controls
                foreach (Control c in kainosModePanels.Concat(kainosRxRows.SelectMany(r => r)).Concat(kainosTxRows.SelectMany(r => r)))
                    c.VisibleChanged += (s, e) => { if (_kainosLayout && !_kainosPlacing) positionKainosColumn(); };

                // anything Thetis moves or resizes can change the free space on the right
                SizeChanged += (s, e) => positionKainosColumn();
                panelDisplay.SizeChanged += (s, e) => positionKainosColumn();
                foreach (Control c in Controls)
                    if (!(c is KainosColumn) && !(c is KainosDock))
                    {
                        c.LocationChanged += (s, e) => { if (_kainosLayout && !_kainosPlacing) positionKainosColumn(); };
                        c.VisibleChanged += (s, e) => { if (_kainosLayout && !_kainosPlacing) positionKainosColumn(); };     // RX2's panels under the panadapter
                    }

                Shown += (s, e) => { _kainosShown = true; if (_kainosLayout) BeginInvoke(new Action(kainosAttachMeter)); };
                KainosUI.ScaleChanged += (s, e) => { kainosUnfitPanels(); positionKainosColumn(); };
            }
            _kainosColumn.TabState = KainosColumnTabs;
            kainosUnfitPanels();        // a skin load can have set the mode panels' layout again
            kainosPartsOn();
            if (_kainosShown) kainosAttachMeter();
            positionKainosColumn();
        }

        private void kainosColumnOff()
        {
            if (_kainosColumn == null) return;
            kainosPartsOff();
            kainosDetachMeter();
            kainosRestoreDisplayLocation();
            ResizeConsole(h_delta, v_delta);     // Thetis puts the panadapter and its panels back
        }

        // moves and collapses (the parts of Kainos layout that take Thetis controls over)
        private void kainosPartsOn()
        {
            refreshKainosBandGrid();
            _kainosModeDrop.SetTargets(orderedButtons(panelMode));
            _kainosFilterDrop.SetTargets(orderedButtons(panelFilter).Where(b => b is RadioButton));
            _kainosShiftReset.SetTargets(new ButtonBase[] { btnFilterShiftReset });
            foreach (Control c in new Control[] { _kainosBandDrop, _kainosModeDrop, _kainosFilterDrop, _kainosShiftReset })
                if (c.Parent != _kainosColumn.Viewport) _kainosColumn.Viewport.Controls.Add(c);
            kainosMoveIn(kainosFilterExtras);
            kainosMoveIn(kainosRxRows.SelectMany(r => r));
            kainosMoveIn(kainosTxRows.SelectMany(r => r));
            foreach (Control c in kainosCollapseTargets) kainosCollapse(c);
            kainosBarOn();          // the panadapter's bar: consoleKainosBar.cs
            lblPAProfile.Visible = false;       // the PA PROFILE tab: consoleKainosTabs.cs
            _kainosColumn.Visible = true;
            _kainosPartsOn = true;
        }

        private void kainosPartsOff()
        {
            _kainosPartsOn = false;
            _kainosColumn.Visible = false;
            kainosBarOff();
            setPAProfileLabelPos();
            foreach (KeyValuePair<Control, KeyValuePair<Control, Point>> kv in _kainosMovedIn.ToList())
            {
                kv.Key.ParentChanged -= kainosMovedParentChanged;
                if (kv.Key.Parent == _kainosColumn.Viewport)      // still ours (Thetis's collapse code moves some itself)
                {
                    kv.Value.Key.Controls.Add(kv.Key);
                    kv.Key.Location = kv.Value.Value;
                }
            }
            _kainosMovedIn.Clear();
            foreach (KeyValuePair<Control, Size> kv in _kainosCollapsed.ToList())
                kv.Key.Size = kv.Value;
            _kainosCollapsed.Clear();
            _kainosCollapseTo.Clear();
            kainosUnfitPanels();
            foreach (Control p in kainosModePanels) kainosUnpin(p);
            kainosUnpin(grpVFOBetween);
            if (_kainosMeter != null) kainosUnpin(_kainosMeter);
        }

        // Thetis controls into the column's viewport, remembering their own parent and place for Classic
        private void kainosMoveIn(IEnumerable<Control> controls)
        {
            foreach (Control c in controls)
            {
                if (c == null) continue;
                if (c.Parent != _kainosColumn.Viewport)
                {
                    _kainosMovedIn[c] = new KeyValuePair<Control, Point>(c.Parent, c.Location);
                    _kainosColumn.Viewport.Controls.Add(c);
                }
                c.ParentChanged -= kainosMovedParentChanged;
                c.ParentChanged += kainosMovedParentChanged;
            }
        }

        // Thetis puts some of these back itself (ExpandDisplay re-parents the AF, AGC and preamp controls, also at
        // start-up): take its parent and place as their home for Classic, and bring them back into the column
        private void kainosMovedParentChanged(object sender, EventArgs e)
        {
            Control c = (Control)sender;
            if (!_kainosPartsOn || _kainosColumn == null || c.Parent == _kainosColumn.Viewport || c.Parent == null) return;
            _kainosMovedIn[c] = new KeyValuePair<Control, Point>(c.Parent, c.Location);
            BeginInvoke(new Action(() =>
            {
                if (!_kainosPartsOn || c.Parent == _kainosColumn.Viewport) return;
                _kainosMovedIn[c] = new KeyValuePair<Control, Point>(c.Parent, c.Location);
                _kainosColumn.Viewport.Controls.Add(c);
                positionKainosColumn();
            }));
        }

        // collapse to zero size (or the size 'to' gives for the full size); a resize or skin load that sizes it again
        // is caught and the new size kept for Classic
        private void kainosCollapse(Control c, Func<Size, Size> to = null)
        {
            to = to ?? (full => Size.Empty);
            _kainosCollapseTo[c] = to;
            if (!_kainosCollapsed.ContainsKey(c))
            {
                _kainosCollapsed[c] = c.Size;
                c.SizeChanged -= kainosCollapsedSizeChanged;
                c.SizeChanged += kainosCollapsedSizeChanged;
            }
            else if (c.Size != to(_kainosCollapsed[c])) _kainosCollapsed[c] = c.Size;
            c.Size = to(_kainosCollapsed[c]);
        }

        private void kainosCollapsedSizeChanged(object sender, EventArgs e)
        {
            Control c = (Control)sender;
            if (!_kainosPartsOn || !_kainosCollapsed.ContainsKey(c) || !_kainosCollapseTo.ContainsKey(c)) return;
            Func<Size, Size> to = _kainosCollapseTo[c];
            if (c.Size == to(_kainosCollapsed[c])) return;
            _kainosCollapsed[c] = c.Size;
            BeginInvoke(new Action(() =>
            {
                if (_kainosPartsOn && _kainosCollapsed.ContainsKey(c) && _kainosCollapseTo.ContainsKey(c)) c.Size = _kainosCollapseTo[c](_kainosCollapsed[c]);
            }));
        }

        private static IEnumerable<ButtonBase> orderedButtons(Control panel)
        {
            return panel.Controls.OfType<ButtonBase>().OrderBy(b => b.Top).ThenBy(b => b.Left);
        }

        private void refreshKainosBandGrid()
        {
            Control p = panelBandVHF.Visible ? panelBandVHF : panelBandGEN.Visible ? panelBandGEN : panelBandHF;
            _kainosBandDrop.SetTargets(orderedButtons(p));
        }

        // The column runs down the right-hand side, below and above any Thetis panels that still reach into that
        // space (at narrow window sizes the VFO B box and the bottom panels can), and the panadapter gives way
        private void positionKainosColumn()
        {
            if (_kainosColumn == null || !_kainosLayout || _kainosPlacing) return;
            _kainosPlacing = true;
            try
            {
                // Thetis's collapsed display has its own layout: step aside, and come back when it's expanded
                if (collapsedDisplay)
                {
                    if (_kainosPartsOn) kainosPartsOff();
                    if (_kainosDock != null) _kainosDock.Visible = false;
                    return;
                }
                if (!_kainosPartsOn) { kainosPartsOn(); positionKainosDock(); }

                int width = KainosUI.S(312);
                int x = ClientSize.Width - width - 4;
                int top = menuStrip1.Bottom + 4;
                int bottom = (statusStripMain.Visible ? statusStripMain.Top : ClientSize.Height) - 4;
                int mid = (top + bottom) / 2;
                foreach (Control c in Controls)
                {
                    if (c == _kainosColumn || c is KainosDock || c == panelDisplay || c == menuStrip1 || c == statusStripMain) continue;
                    if (c == _kainosMeter || c == grpVFOBetween || kainosModePanels.Contains(c)) continue;
                    if (_kainosCollapsed.ContainsKey(c)) continue;      // ours, collapsed (Thetis can size one again for a moment)
                    if (!c.Visible || c.Width == 0 || c.Height == 0) continue;
                    if (c.Right <= x || c.Left >= x + width) continue;
                    if (c.Bottom < mid) top = Math.Max(top, c.Bottom + 4);
                    else bottom = Math.Min(bottom, c.Top - 4);
                }
                _kainosColumn.SetBounds(x, top, width, Math.Max(80, bottom - top));
                _kainosColumn.SendToBack();
                kainosFitDisplay(x - 6);
                _kainosColumn.ArrangeSections();
            }
            finally
            {
                _kainosPlacing = false;
            }
        }

        // the panadapter fills the space between the dock and the column, and from the menu down (the VFO boxes'
        // row is gone); its bottom stays where Thetis puts it (Classic puts its place back, and Thetis its size)
        private Point _kainosDisplayLocation = new Point(-1, -1);
        private void kainosFitDisplay(int right)
        {
            if (_kainosDisplayLocation.X < 0) _kainosDisplayLocation = panelDisplay.Location;
            if (_kainosDock != null && _kainosDock.Visible)
            {
                int left = _kainosDock.Right + 6;
                if (panelDisplay.Left != left) panelDisplay.Left = left;
            }
            int w = Math.Max(200, right - panelDisplay.Left);
            if (panelDisplay.Width != w) panelDisplay.Width = w;
            int top = menuStrip1.Bottom + 4;
            int bottom = kainosDisplayBottom();
            int term = kainosTermDockHeight;
            if (term > 0 && bottom - term - 4 - top > 160) bottom -= term + 4;      // the RTTY / CW terminal under it
            if (bottom - top > 100 && (panelDisplay.Top != top || panelDisplay.Height != bottom - top))
                panelDisplay.SetBounds(panelDisplay.Left, top, panelDisplay.Width, bottom - top);

            // Thetis places the pan and zoom sliders (anchored to the top of the panel) for its own height: down by
            // what the panel has grown (the zoom buttons and info bar are anchored to the bottom and follow)
            int grown = panelDisplay.Height - (gr_display_size_basis.Height + v_delta);
            kainosSetTop(lblDisplayPan, lbl_displaypan_basis.Y + v_delta + grown);
            kainosSetTop(ptbDisplayPan, tb_displaypan_basis.Y + v_delta + grown);
            kainosSetTop(btnDisplayPanCenter, ptbDisplayPan.Top);
            kainosSetTop(lblDisplayZoom, lbl_display_zoom_basis.Y + v_delta + grown);
            kainosSetTop(ptbDisplayZoom, tb_display_zoom_basis.Y + v_delta + grown);
            positionKainosBar();
            kainosPlaceTermDock();
        }

        // with Thetis's panels under the panadapter collapsed, it reaches down to the status bar, or to the top of
        // whatever Thetis still shows under it (RX2's panels when RX2 is on)
        private int kainosDisplayBottom()
        {
            int thetis = gr_display_basis.Y + gr_display_size_basis.Height + v_delta;
            int bottom = (statusStripMain.Visible ? statusStripMain.Top : ClientSize.Height) - 4;
            foreach (Control c in Controls)
            {
                if (c == panelDisplay || c == statusStripMain || c == menuStrip1 || c is KainosColumn || c is KainosDock || c is IKainosTerminal) continue;
                if (_kainosCollapsed.ContainsKey(c) || c == _kainosMeter || c == grpVFOBetween || kainosModePanels.Contains(c)) continue;
                if (!c.Visible || c.Width == 0 || c.Height == 0 || c.Top < -10000) continue;
                if (c.Right <= panelDisplay.Left || c.Left >= panelDisplay.Right || c.Top < thetis - 2) continue;
                bottom = Math.Min(bottom, c.Top - 4);
            }
            return Math.Max(thetis, bottom);
        }

        private static void kainosSetTop(Control c, int top) { if (c.Top != top) c.Top = top; }

        internal void kainosRestoreDisplayLocation()
        {
            if (_kainosDisplayLocation.X >= 0)
            {
                panelDisplay.Location = _kainosDisplayLocation;
                // the pan and zoom sliders where Thetis has them (its ExpandDisplay)
                kainosSetTop(lblDisplayPan, lbl_displaypan_basis.Y + v_delta);
                kainosSetTop(ptbDisplayPan, tb_displaypan_basis.Y + v_delta);
                kainosSetTop(btnDisplayPanCenter, ptbDisplayPan.Top);
                kainosSetTop(lblDisplayZoom, lbl_display_zoom_basis.Y + v_delta);
                kainosSetTop(ptbDisplayZoom, tb_display_zoom_basis.Y + v_delta);
            }
            _kainosDisplayLocation = new Point(-1, -1);
        }

        // pinned controls: placed over the column in console coordinates; parked off screen when their tab is off
        // or they are scrolled out of the column
        private void kainosPin(Control c, Rectangle viewportRect, bool sizeToWidth)
        {
            Rectangle vp = _kainosColumn.Viewport.Bounds;
            if (sizeToWidth && c.Width != viewportRect.Width) c.Width = viewportRect.Width;
            bool inView = viewportRect.Top >= 0 && viewportRect.Top + c.Height <= vp.Height && _kainosColumn.Visible;
            Point p = inView ? new Point(_kainosColumn.Left + vp.Left + viewportRect.Left, _kainosColumn.Top + vp.Top + viewportRect.Top)
                             : new Point(-20000, -20000);
            if (c.Location != p) c.Location = p;
            if (inView) c.BringToFront();
        }

        private void kainosUnpin(Control c)
        {
            // Thetis places these itself; a resize puts them back
            if (c.Left < -10000) c.Location = new Point(0, 0);
        }

        #region BAND

        private int kainosBandMeasure(int w)
        {
            return KainosDropDown.PreferredHeight + KainosUI.S(8) + KainosUI.S(24) * 3 + KainosUI.S(4) * 2;
        }

        private void kainosBandArrange(Rectangle r)
        {
            int gap = KainosUI.S(6), y = r.Top;
            int dw = (r.Width - gap * 2) / 3;
            _kainosBandDrop.SetBounds(r.Left, y, dw, KainosDropDown.PreferredHeight);
            _kainosModeDrop.SetBounds(r.Left + dw + gap, y, dw, KainosDropDown.PreferredHeight);
            _kainosFilterDrop.SetBounds(r.Left + (dw + gap) * 2, y, r.Width - (dw + gap) * 2, KainosDropDown.PreferredHeight);
            y += KainosDropDown.PreferredHeight + KainosUI.S(8);
            // width and shift sliders, each with its label, then low / high
            int row = KainosUI.S(24), lw = KainosUI.S(46);
            placeLabel(lblFilterWidth, r.Left, y, lw, row);
            ptbFilterWidth.SetBounds(r.Left + lw, y, r.Width - lw, row);
            y += row + KainosUI.S(4);
            placeLabel(lblFilterShift, r.Left, y, lw, row);
            int reset = KainosUI.S(48);
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
            Label lbl = l as Label;
            if (lbl != null) { lbl.AutoSize = false; lbl.TextAlign = ContentAlignment.MiddleLeft; }
        }

        #endregion

        #region VFO SYNC

        private int kainosSyncMeasure(int w)
        {
            kainosFitPanel(grpVFOBetween, w);
            return grpVFOBetween.Height;
        }

        private void kainosSyncArrange(Rectangle r)
        {
            kainosFitPanel(grpVFOBetween, r.Width);
            kainosPin(grpVFOBetween, new Rectangle(r.Left, r.Top, r.Width, grpVFOBetween.Height), false);
        }

        #endregion

        #region RX and TX rows

        // A row of Thetis controls: a label and slider become label above, slider full width; the AGC / preamp row
        // is two label + box pairs side by side; squelch is its check box above the slider
        private static bool kainosShown(Control c) { return c != null && c.Visible; }

        private int kainosRowHeight(Control[] row)
        {
            if (!row.Any(kainosShown)) return 0;
            if (row.Length == 2) return KainosUI.S(18) + KainosUI.S(26);
            if (row.Length == 3) return KainosUI.S(24) + KainosUI.S(26);      // squelch
            return KainosUI.S(26);
        }

        private int kainosRowsHeight(Control[][] rows)
        {
            int h = 0, gap = KainosUI.S(6);
            foreach (Control[] row in rows) { int rh = kainosRowHeight(row); if (rh > 0) h += rh + gap; }
            return Math.Max(0, h - gap);
        }

        private void kainosRowsArrange(Control[][] rows, Rectangle r)
        {
            int y = r.Top, gap = KainosUI.S(6);
            foreach (Control[] row in rows)
            {
                int rh = kainosRowHeight(row);
                if (rh == 0)
                {
                    foreach (Control c in row) c.Top = -30000;       // all hidden by Thetis: keep them clear
                    continue;
                }
                if (row.Length == 2)
                {
                    row[0].SetBounds(r.Left, y, r.Width, KainosUI.S(18));
                    Label l = row[0] as Label;
                    if (l != null) { l.AutoSize = false; l.TextAlign = ContentAlignment.MiddleLeft; }
                    row[1].SetBounds(r.Left, y + KainosUI.S(18), r.Width, KainosUI.S(26));
                }
                else if (row.Length == 3)
                {
                    row[0].SetBounds(r.Left, y, r.Width, KainosUI.S(22));
                    row[1].SetBounds(r.Left, y + KainosUI.S(24), r.Width, KainosUI.S(22));
                    row[2].SetBounds(r.Left + 8, y + KainosUI.S(24) + KainosUI.S(22), r.Width - 16, 3);
                }
                else
                {
                    // AGC: label + mode; preamp: label + whichever of the preamp box / step attenuator Thetis shows
                    int half = r.Width / 2, lw = KainosUI.S(52);
                    placeLabel(row[0], r.Left, y, lw, rh);
                    row[1].SetBounds(r.Left + lw, y + 2, half - lw - 6, rh - 4);
                    placeLabel(row[2], r.Left + half, y, lw, rh);
                    for (int i = 3; i < row.Length; i++) row[i].SetBounds(r.Left + half + lw, y + 2, half - lw, rh - 4);
                }
                y += rh + gap;
            }
        }

        private Control kainosModePanel { get { return kainosModePanels.FirstOrDefault(p => p.Visible); } }

        private int kainosTxMeasure(int w)
        {
            Control p = kainosModePanel;
            int rows = kainosRowsHeight(kainosTxRows);
            if (p != null) kainosFitPanel(p, w);
            return rows + (p != null ? KainosUI.S(8) + p.Height : 0);
        }

        private void kainosTxArrange(Rectangle r)
        {
            kainosRowsArrange(kainosTxRows, r);
            int y = r.Top + kainosRowsHeight(kainosTxRows) + KainosUI.S(8);
            Control shown = kainosModePanel;
            foreach (Control p in kainosModePanels)
            {
                if (p == shown)
                {
                    kainosFitPanel(p, r.Width);
                    kainosPin(p, new Rectangle(r.Left, y, r.Width, p.Height), false);
                }
                else if (p.Left > -10000) p.Location = new Point(-20000, -20000);
            }
        }

        // The mode panels are laid out for 336 pixels; in a narrower column the panel and everything in it (place,
        // size and font) is scaled down to fit. The exact original values are kept and put back in Classic, after a
        // skin load and when the UI scale changes, so nothing drifts.
        private class KainosPanelFit
        {
            public float Factor;
            public readonly Dictionary<Control, KeyValuePair<Rectangle, Font>> Original = new Dictionary<Control, KeyValuePair<Rectangle, Font>>();
        }
        private readonly Dictionary<Control, KainosPanelFit> _kainosFits = new Dictionary<Control, KainosPanelFit>();

        private void kainosFitPanel(Control panel, int width)
        {
            KainosPanelFit fit;
            if (!_kainosFits.TryGetValue(panel, out fit))
            {
                fit = new KainosPanelFit { Factor = 1f };
                kainosRecord(panel, fit);
                _kainosFits[panel] = fit;
            }
            int w0 = fit.Original[panel].Key.Width;
            float f = Math.Min(1f, width / (float)Math.Max(1, w0));
            if (Math.Abs(f - fit.Factor) < 0.005f) return;
            fit.Factor = f;
            panel.SuspendLayout();
            foreach (KeyValuePair<Control, KeyValuePair<Rectangle, Font>> kv in fit.Original)
            {
                Rectangle b = kv.Value.Key;
                Font font = kv.Value.Value;
                if (kv.Key == panel) kv.Key.Size = new Size((int)Math.Round(b.Width * f), (int)Math.Round(b.Height * f));
                else kv.Key.SetBounds((int)Math.Round(b.X * f), (int)Math.Round(b.Y * f), (int)Math.Round(b.Width * f), (int)Math.Round(b.Height * f));
                if (font != null && f < 1f) kv.Key.Font = new Font(font.FontFamily, font.Size * f, font.Style, font.Unit);
                else if (font != null) kv.Key.Font = font;
            }
            panel.ResumeLayout();
        }

        private static void kainosRecord(Control c, KainosPanelFit fit)
        {
            fit.Original[c] = new KeyValuePair<Rectangle, Font>(c.Bounds, c.Font);
            foreach (Control child in c.Controls) kainosRecord(child, fit);
        }

        private void kainosUnfitPanels()
        {
            foreach (KeyValuePair<Control, KainosPanelFit> pf in _kainosFits)
            {
                if (pf.Value.Factor >= 0.999f) continue;
                pf.Key.SuspendLayout();
                foreach (KeyValuePair<Control, KeyValuePair<Rectangle, Font>> kv in pf.Value.Original)
                {
                    Rectangle b = kv.Value.Key;
                    if (kv.Key == pf.Key) kv.Key.Size = b.Size; else kv.Key.Bounds = b;
                    kv.Key.Font = kv.Value.Value;
                }
                pf.Key.ResumeLayout();
            }
            _kainosFits.Clear();
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
                KainosMeterType = MeterType.ANANMM.ToString();
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
                BeginInvoke(new Action(() => kainosOfferFtdx(true)));     // once: OE3IDE's FTDX-5000 skin
            }
            if (all == null || !all.ContainsKey(KainosMeterId)) return;
            ucMeter uc = all[KainosMeterId];
            if (_kainosMeter != uc)
            {
                _kainosMeter = uc;
                kainosHookMeterMenu(uc);
                uc.LocationChanged += (s, e) => { if (_kainosLayout && !_kainosPlacing) positionKainosColumn(); };
                uc.SizeChanged += (s, e) => { if (_kainosLayout && !_kainosPlacing) positionKainosColumn(); };
            }
            MeterManager.enableContainer(KainosMeterId, _kainosColumn.IsOn("meters"));
            positionKainosDock();       // and the column
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
            kainosPin(_kainosMeter, r, true);
        }

        #endregion
    }

    // The column: a bar of toggle tabs, then a viewport with the panels of the tabs that are on, scrolled with
    // the mouse wheel if they don't all fit
    internal class KainosColumn : Panel
    {
        private class Section
        {
            public string Key, Title;
            public bool On, DefaultOn;
            public Func<int, int> Measure;
            public Action<Rectangle> Arrange;
            public RectangleF TabRect;
            public Rectangle HeaderRect;
        }

        private readonly Console _console;
        private readonly List<Section> _sections = new List<Section>();
        private int _scroll, _contentHeight;
        private Section _hoverTab;
        public readonly KainosViewport Viewport;
        public event EventHandler TabsChanged;

        public KainosColumn(Console console)
        {
            _console = console;
            Name = "kainosColumn";
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = KainosUI.Surface;
            Viewport = new KainosViewport(this);
            Controls.Add(Viewport);
        }

        public void AddSection(string key, string title, Func<int, int> measure, Action<Rectangle> arrange, bool defaultOn = true)
        {
            _sections.Add(new Section { Key = key, Title = title, On = defaultOn, DefaultOn = defaultOn, Measure = measure, Arrange = arrange });
        }

        public bool IsOn(string key) { Section s = _sections.Find(x => x.Key == key); return s != null && s.On; }

        // "meters,band,-rx": tabs that are on, and "-" before the ones turned off; a tab not listed (new in this
        // version of Kainos) starts as its section says
        public string TabState
        {
            get { return string.Join(",", _sections.Select(s => (s.On ? "" : "-") + s.Key)); }
            set
            {
                string[] items = (value ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (Section s in _sections)
                    s.On = items.Contains(s.Key) || (!items.Contains("-" + s.Key) && s.DefaultOn);
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
            int top = tabBarHeight();
            Viewport.SetBounds(1, top, Width - 1, Math.Max(0, Height - top));
            int pad = KainosUI.S(8), header = KainosUI.S(24), gap = KainosUI.S(10);
            int w = Viewport.Width - pad * 2;
            int total = 0;
            foreach (Section s in _sections) if (s.On) total += header + s.Measure(w) + gap;
            _contentHeight = total;
            _scroll = Math.Max(0, Math.Min(_scroll, total - Viewport.Height));
            int y = -_scroll;
            foreach (Section s in _sections)
            {
                if (!s.On) { s.HeaderRect = Rectangle.Empty; s.Arrange(new Rectangle(pad, -30000, w, 0)); continue; }
                int h = s.Measure(w);
                s.HeaderRect = new Rectangle(pad, y, w, header);
                s.Arrange(new Rectangle(pad, y + header, w, h));
                y += header + h + gap;
            }
            Invalidate();
            Viewport.Invalidate();
        }

        internal void ScrollBy(int delta)
        {
            int before = _scroll;
            _scroll = Math.Max(0, Math.Min(_scroll - Math.Sign(delta) * KainosUI.S(40), Math.Max(0, _contentHeight - Viewport.Height)));
            if (_scroll != before) ArrangeSections();
        }

        internal void PaintHeaders(Graphics g)
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (Font f = new Font("Segoe UI", Math.Max(8f, KainosUI.S(11)), FontStyle.Bold, GraphicsUnit.Pixel))
            using (Brush b = new SolidBrush(KainosUI.Ice))
            using (Pen line = new Pen(KainosUI.Line))
                foreach (Section s in _sections)
                {
                    if (!s.On || s.HeaderRect.IsEmpty) continue;
                    g.DrawString(s.Title, f, b, s.HeaderRect.Left, s.HeaderRect.Top + KainosUI.S(5));
                    g.DrawLine(line, s.HeaderRect.Left, s.HeaderRect.Bottom - 3, s.HeaderRect.Right, s.HeaderRect.Bottom - 3);
                }
        }

        protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); ScrollBy(e.Delta); if (e is HandledMouseEventArgs) ((HandledMouseEventArgs)e).Handled = true; }

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
            using (Pen p = new Pen(KainosUI.Line)) g.DrawLine(p, 0, top - 1, Width, top - 1);
        }
    }

    // the scrolling area under the tab bar; it clips the controls the column has moved in
    internal class KainosViewport : Panel
    {
        private readonly KainosColumn _column;

        public KainosViewport(KainosColumn column)
        {
            _column = column;
            Name = "kainosViewport";
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = KainosUI.Surface;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            _column.PaintHeaders(e.Graphics);
        }

        protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); _column.ScrollBy(e.Delta); if (e is HandledMouseEventArgs) ((HandledMouseEventArgs)e).Handled = true; }
    }

    // A drop-down bound to a set of Thetis buttons (band, mode or filter): it shows its caption and the button
    // that is on; a click lists the buttons (the one that is on ticked) and choosing one is a real click on it. A
    // right click is a right click on the button that is on (the filter's opens Thetis's filter editor).
    internal class KainosDropDown : Control
    {
        public static int PreferredHeight { get { return KainosUI.S(30); } }

        private readonly string _caption;
        private readonly KainosUI.Tone _tone;
        private List<ButtonBase> _targets = new List<ButtonBase>();
        private readonly Func<ComboBox> _comboSource;   // or bound to a Thetis combo box (display mode, PA profile)
        private ComboBox _comboHooked;
        private ComboBox _combo
        {
            get
            {
                if (_comboSource == null) return null;
                ComboBox c = _comboSource();
                if (c != null && c != _comboHooked)
                {
                    _comboHooked = c;
                    c.SelectedIndexChanged += changed;
                    c.EnabledChanged += changed;
                    c.TextChanged += changed;
                }
                return c;
            }
        }
        private bool _isCombo { get { return _comboSource != null; } }
        private bool _hover, _open;
        private static readonly System.Reflection.MethodInfo _onClick = typeof(Control).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        private static readonly System.Reflection.MethodInfo _onMouseDown = typeof(Control).GetMethod("OnMouseDown", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        private static readonly System.Reflection.MethodInfo _onMouseUp = typeof(Control).GetMethod("OnMouseUp", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        public KainosDropDown(string caption, KainosUI.Tone tone)
        {
            _caption = caption;
            _tone = tone;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = KainosUI.Surface;
            Cursor = Cursors.Hand;
        }

        public KainosDropDown(ComboBox combo) : this(() => combo, null) { }

        public KainosDropDown(Func<ComboBox> combo, string caption) : this(caption, KainosUI.Tone.Ice)
        {
            _comboSource = combo;
            ComboBox c = _combo;        // hooks it now if it's there
        }

        public void SetTargets(IEnumerable<ButtonBase> targets)
        {
            foreach (ButtonBase t in _targets) { t.TextChanged -= changed; t.VisibleChanged -= changed; if (t is RadioButton) ((RadioButton)t).CheckedChanged -= changed; }
            _targets = targets.ToList();
            foreach (ButtonBase t in _targets) { t.TextChanged += changed; t.VisibleChanged += changed; if (t is RadioButton) ((RadioButton)t).CheckedChanged += changed; }
            Invalidate();
        }

        private void changed(object sender, EventArgs e)
        {
            if (IsDisposed || !IsHandleCreated) return;
            if (InvokeRequired) BeginInvoke(new Action(Invalidate)); else Invalidate();
        }

        private static string label(ButtonBase b) { return b.Text.Replace("&&", "&"); }
        private IEnumerable<ButtonBase> shown { get { return _targets.Where(t => t.Visible && label(t).Length > 0); } }
        private ButtonBase current { get { return shown.FirstOrDefault(t => t is RadioButton && ((RadioButton)t).Checked); } }
        private string currentText
        {
            get
            {
                if (_isCombo) { ComboBox cb = _combo; return cb != null && cb.Text.Length > 0 ? cb.Text : null; }
                ButtonBase c = current;
                return c != null ? label(c) : null;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Color accent = _tone == KainosUI.Tone.Gold ? KainosUI.Gold : KainosUI.Ice;
            using (System.Drawing.Drawing2D.GraphicsPath path = KainosUI.RoundedRect(r, KainosUI.S(4)))
            {
                using (Brush b = new SolidBrush(_open ? KainosUI.Selected : KainosUI.Raised)) g.FillPath(b, path);
                using (Pen p = new Pen(_hover || _open ? accent : KainosUI.Line)) g.DrawPath(p, path);
            }
            float pad = KainosUI.S(7);
            bool caption = _caption != null && Height >= KainosUI.S(28);
            if (caption)
                using (Font cap = new Font("Segoe UI", Math.Max(7f, KainosUI.S(9)), FontStyle.Bold, GraphicsUnit.Pixel))
                using (Brush b = new SolidBrush(KainosUI.Faint))
                    g.DrawString(_caption, cap, b, pad - 2, KainosUI.S(1));
            string value = currentText;
            using (Font f = new Font("Segoe UI", Math.Max(9f, KainosUI.S(caption ? 13 : 12)), FontStyle.Bold, GraphicsUnit.Pixel))
            using (Brush b = new SolidBrush(value != null ? accent : KainosUI.Faint))
            using (StringFormat sf = new StringFormat { LineAlignment = caption ? StringAlignment.Far : StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                g.DrawString(value ?? "-", f, b, new RectangleF(pad - 2, 0, Width - pad - KainosUI.S(16), Height - (caption ? KainosUI.S(2) : 0)), sf);
            // the chevron
            float cx = Width - KainosUI.S(11), cy = Height / 2f + (caption ? KainosUI.S(3) : KainosUI.S(1)), cw = KainosUI.S(4);
            using (Pen p = new Pen(KainosUI.Dim, Math.Max(1f, KainosUI.S(1.5f))))
                g.DrawLines(p, new[] { new PointF(cx - cw, cy - cw / 2), new PointF(cx, cy + cw / 2), new PointF(cx + cw, cy - cw / 2) });
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; Invalidate(); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Right && !_isCombo)
            {
                ButtonBase c = current;
                if (c == null || !c.Enabled) return;
                MouseEventArgs m = new MouseEventArgs(MouseButtons.Right, 1, 1, 1, 0);
                _onMouseDown.Invoke(c, new object[] { m });
                _onMouseUp.Invoke(c, new object[] { m });
                return;
            }
            if (e.Button != MouseButtons.Left) return;
            ContextMenuStrip menu = new ContextMenuStrip
            {
                Renderer = new KainosToolStripRenderer(),
                BackColor = KainosUI.Raised,
                ForeColor = KainosUI.Text,
                ShowImageMargin = false,
                ShowCheckMargin = true,
                Font = new Font("Segoe UI", Math.Max(9f, KainosUI.S(13)), FontStyle.Regular, GraphicsUnit.Pixel),
                MinimumSize = new Size(Width, 0),
            };
            ComboBox combo = _combo;
            if (combo != null && combo.Enabled)
                for (int i = 0; i < combo.Items.Count; i++)
                {
                    int index = i;
                    string text = combo.GetItemText(combo.Items[i]);
                    ToolStripMenuItem item = new ToolStripMenuItem(text) { Checked = i == combo.SelectedIndex || (combo.SelectedIndex < 0 && text == combo.Text), ForeColor = KainosUI.Text };
                    item.Click += (s, a) => { if (combo.SelectedIndex != index) combo.SelectedIndex = index; };
                    menu.Items.Add(item);
                }
            foreach (ButtonBase t in shown)
            {
                ButtonBase target = t;
                ToolStripMenuItem item = new ToolStripMenuItem(label(t))
                {
                    Checked = t is RadioButton && ((RadioButton)t).Checked,
                    Enabled = t.Enabled,
                    ForeColor = KainosUI.Text,
                };
                item.Click += (s, a) => _onClick.Invoke(target, new object[] { EventArgs.Empty });
                menu.Items.Add(item);
            }
            menu.Closed += (s, a) => { _open = false; Invalidate(); BeginInvoke(new Action(menu.Dispose)); };
            _open = true;
            Invalidate();
            if (menu.Items.Count == 0) { _open = false; menu.Dispose(); return; }
            menu.Show(this, new Point(0, Height));
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            // let the column scroll
            base.OnMouseWheel(e);
            KainosViewport v = Parent as KainosViewport;
            if (v != null) typeof(Control).GetMethod("OnMouseWheel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(v, new object[] { e });
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
            KainosViewport v = Parent as KainosViewport;
            if (v != null) typeof(Control).GetMethod("OnMouseWheel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(v, new object[] { e });
        }
    }
}
