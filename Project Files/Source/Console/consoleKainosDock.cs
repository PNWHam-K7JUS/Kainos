/*  consoleKainosDock.cs

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
using System.Reflection;
using System.Windows.Forms;

namespace Thetis
{
    // Kainos layout, stage 2: the left dock and the transmit readouts in the status bar.
    //
    // The dock takes the place of Thetis's POWER panel (POWER, RX2) and options panel (MON, TUN, MOX, 2TON, DUP,
    // PS-A, xPA, REC, PLAY), and adds VOX, the VFO buttons (split, A>B, A<B, swap, zero beat, IF>V) and VAC1 / VAC2 (from Thetis's VFO panel at the bottom; a right click
    // opens their setup, as in Thetis). Its buttons are Kainos-drawn stand-ins for the real Thetis buttons:
    // a click on one is a real click on the Thetis button, so all of Thetis's keying logic runs unchanged, and the
    // dock shows each button's text, state and enabled/visible state as Thetis sets them. The two Thetis panels
    // stay in the console at zero size (not hidden), so Thetis can still show and hide them and their buttons;
    // the dock follows.
    public partial class Console
    {
        private KainosDock _kainosDock;
        private Size _kainosPowerSize, _kainosOptionsSize;
        private bool _kainosPanelsCollapsed;
        private ToolStripStatusLabel _kainosFwd, _kainosSwr, _kainosAlc;
        private Timer _kainosTxTimer;

        private void kainosDockOn()
        {
            if (_kainosDock == null)
            {
                _kainosDock = new KainosDock(this, new[]
                {
                    new[] { dockItem(chkPower, KainosUI.Tone.Ice, "POWER"), dockItem(chkRX2, KainosUI.Tone.Violet, "RX2") },
                    new[] { dockItem(chkMOX, KainosUI.Tone.Tx), dockItem(chkTUN, KainosUI.Tone.Tx),
                            dockItem(chk2TONE, KainosUI.Tone.Tx), dockItem(chkMON, KainosUI.Tone.Gold),
                            dockItem(chkVOX, KainosUI.Tone.Gold), dockItem(chkRX2SR, KainosUI.Tone.Gold),
                            dockItem(chkFWCATUBypass, KainosUI.Tone.Gold), dockItem(chkExternalPA, KainosUI.Tone.Gold) },
                    new[] { dockItem(chkVFOSplit, KainosUI.Tone.Gold), dockItem(btnVFOAtoB, KainosUI.Tone.Ice), dockItem(btnVFOBtoA, KainosUI.Tone.Ice),
                            dockItem(btnVFOSwap, KainosUI.Tone.Ice), dockItem(btnZeroBeat, KainosUI.Tone.Ice), dockItem(btnIFtoVFO, KainosUI.Tone.Ice) },
                    new[] { dockItem(ckQuickRec, KainosUI.Tone.Tx, "REC"), dockItem(ckQuickPlay, KainosUI.Tone.Gold, "PLAY") },
                    new[] { dockItem(chkVAC1, KainosUI.Tone.Ice, "VAC1"), dockItem(chkVAC2, KainosUI.Tone.Ice, "VAC2") },
                });
                Controls.Add(_kainosDock);
                // Thetis moves these panels on every resize, and shows / hides them in its collapsed layouts
                foreach (Control c in new Control[] { panelPower, panelOptions, panelSoundControls })
                {
                    c.LocationChanged += (s, e) => positionKainosDock();
                    c.VisibleChanged += (s, e) => positionKainosDock();
                }
                SizeChanged += (s, e) => positionKainosDock();
                KainosUI.ScaleChanged += (s, e) => { if (_kainosDock != null) _kainosDock.Invalidate(); };
            }

            // collapse Thetis's two panels (a skin can set their sizes again, so this runs after every skin load)
            if (panelPower.Size != Size.Empty) _kainosPowerSize = panelPower.Size;
            if (panelOptions.Size != Size.Empty) _kainosOptionsSize = panelOptions.Size;
            panelPower.Size = Size.Empty;
            panelOptions.Size = Size.Empty;
            _kainosPanelsCollapsed = true;

            positionKainosDock();
            kainosStatusOn();
        }

        private void kainosDockOff()
        {
            if (_kainosPanelsCollapsed)
            {
                panelPower.Size = _kainosPowerSize;
                panelOptions.Size = _kainosOptionsSize;
                _kainosPanelsCollapsed = false;
            }
            if (_kainosDock != null) _kainosDock.Visible = false;
            kainosRestoreDisplayLocation();
            kainosStatusOff();
        }

        private static KainosDock.Item dockItem(ButtonBase target, KainosUI.Tone tone, string label = null)
        {
            return new KainosDock.Item { Target = target, Tone = tone, Label = label };
        }

        // The dock is a single column down the left-hand side (the RX controls that shared that side are in the right
        // column), from the POWER panel's top to the status bar, above any Thetis panel that reaches into it
        private void positionKainosDock()
        {
            if (_kainosDock == null || !_kainosLayout) return;
            bool show = (panelPower.Visible || panelOptions.Visible) && !collapsedDisplay;
            if (show)
            {
                int left = panelPower.Left;
                int width = KainosUI.S(80);
                // from the menu down (the VFO boxes' row is gone), clear of anything still in the way
                int top = menuStrip1.Bottom + 4;
                int bottom = (statusStripMain.Visible ? statusStripMain.Top : ClientSize.Height) - 4;
                int mid = (top + bottom) / 2;
                foreach (Control c in Controls)
                {
                    if (c == _kainosDock || c == panelDisplay || c == statusStripMain || c == menuStrip1 || c is KainosColumn) continue;
                    if (c == panelPower || c == panelOptions) continue;      // collapsed under the dock
                    if (c is ucMeter || c == grpVFOBetween || Array.IndexOf(kainosModePanels, c) >= 0 || _kainosCollapsed.ContainsKey(c)) continue;   // meters (the column's is pinned there, and sits at the top left until it is), the column's, collapsed
                    if (!c.Visible || c.Width == 0 || c.Height == 0 || c.Top < -10000) continue;     // parked controls don't count
                    if (c.Right <= left || c.Left >= left + width) continue;
                    if (c.Bottom < mid) top = Math.Max(top, c.Bottom + 4);
                    else bottom = Math.Min(bottom, c.Top - 4);
                }
                _kainosDock.SetBounds(left, top, width, Math.Max(40, bottom - top));
                _kainosDock.ShowPower = panelPower.Visible;
                _kainosDock.ShowOptions = panelOptions.Visible;
            }
            if (_kainosDock.Visible != show) _kainosDock.Visible = show;
            if (show) _kainosDock.BringToFront();
            _kainosDock.Invalidate();
            positionKainosColumn();     // the panadapter starts beside the dock
        }

        #region Status bar: forward power, SWR, ALC

        private void kainosStatusOn()
        {
            if (_kainosFwd == null)
            {
                _kainosFwd = kainosStatusLabel("Fwd");
                _kainosSwr = kainosStatusLabel("SWR");
                _kainosAlc = kainosStatusLabel("ALC");
                _kainosTxTimer = new Timer { Interval = 250 };
                _kainosTxTimer.Tick += (s, e) => updateKainosStatus();
            }
            int at = statusStripMain.Items.IndexOf(toolStripStatusLabel_Fill);
            if (at < 0) at = statusStripMain.Items.Count;
            foreach (ToolStripStatusLabel l in new[] { _kainosAlc, _kainosSwr, _kainosFwd })
                if (!statusStripMain.Items.Contains(l)) statusStripMain.Items.Insert(at, l);
            updateKainosStatus();
            _kainosTxTimer.Start();
        }

        private void kainosStatusOff()
        {
            if (_kainosFwd == null) return;
            _kainosTxTimer.Stop();
            foreach (ToolStripStatusLabel l in new[] { _kainosFwd, _kainosSwr, _kainosAlc })
                if (statusStripMain.Items.Contains(l)) statusStripMain.Items.Remove(l);
        }

        private ToolStripStatusLabel kainosStatusLabel(string name)
        {
            return new ToolStripStatusLabel(name)
            {
                Name = "kainosStatus" + name,
                Font = new Font("Consolas", 9f),
                Margin = new Padding(10, 3, 4, 2),
                ToolTipText = name == "Fwd" ? "Forward power while transmitting" :
                              name == "SWR" ? "SWR while transmitting" : "ALC gain reduction while transmitting"
            };
        }

        private void updateKainosStatus()
        {
            if (_kainosFwd == null) return;
            if (_mox)
            {
                float alc = 0;
                try { alc = (float)Math.Max(0, -WDSP.CalculateTXMeter(1, WDSP.MeterType.ALC_G)); } catch { }
                _kainosFwd.Text = "Fwd " + calfwdpower.ToString(calfwdpower < 10 ? "0.0" : "0") + " W";
                _kainosSwr.Text = "SWR " + Math.Max(1f, alex_swr).ToString("0.0");
                _kainosAlc.Text = "ALC " + alc.ToString("0.0") + " dB";
                _kainosSwr.ForeColor = alex_swr >= 2f ? KainosUI.Tx : KainosUI.Text;
            }
            else
            {
                _kainosFwd.Text = "Fwd -- W";
                _kainosSwr.Text = "SWR --";
                _kainosAlc.Text = "ALC --";
                _kainosSwr.ForeColor = KainosUI.Text;
            }
        }

        #endregion
    }

    // The dock itself: one control that draws the buttons and hands clicks to the Thetis buttons behind them
    internal class KainosDock : Control
    {
        internal class Item
        {
            public ButtonBase Target;       // a check box (on / off) or a plain button
            public bool On { get { CheckBox c = Target as CheckBox; return c != null && c.Checked; } }
            public KainosUI.Tone Tone;
            public string Label;            // null: the Thetis button's own text
            public RectangleF Rect;         // where it was drawn last
            public bool Shown;
        }

        private readonly Console _console;
        private readonly Item[][] _groups;  // 0: POWER / RX2, 1: transmit and options, 2: VFO, 3: quick record / play, 4: VAC
        private Item _hover;
        public bool ShowPower = true, ShowOptions = true;

        private static readonly MethodInfo _onClick = typeof(Control).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo _onMouseDown = typeof(Control).GetMethod("OnMouseDown", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo _onMouseUp = typeof(Control).GetMethod("OnMouseUp", BindingFlags.Instance | BindingFlags.NonPublic);

        public KainosDock(Console console, Item[][] groups)
        {
            _console = console;
            _groups = groups;
            Name = "kainosDock";
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = KainosUI.Bg;
            Cursor = Cursors.Hand;
            foreach (Item[] g in groups)
                foreach (Item it in g)
                {
                    if (it.Target == null) continue;
                    if (it.Target is CheckBox) ((CheckBox)it.Target).CheckedChanged += (s, e) => invalidateSafe();
                    it.Target.EnabledChanged += (s, e) => invalidateSafe();
                    it.Target.VisibleChanged += (s, e) => invalidateSafe();
                    it.Target.TextChanged += (s, e) => invalidateSafe();
                }
        }

        private void invalidateSafe()
        {
            if (IsDisposed || !IsHandleCreated) return;
            if (InvokeRequired) BeginInvoke(new Action(Invalidate));
            else Invalidate();
        }

        private static bool targetShown(Item it)
        {
            // Visible is false while the target's own Visible is off; the Thetis panels themselves stay visible
            return it.Target != null && it.Target.Visible;
        }

        // one column of buttons (two if the dock is wide); the gap between groups is larger than between rows
        private void layout()
        {
            int cols = Width >= KainosUI.S(110) ? 2 : 1;
            float pad = KainosUI.S(4), gap = KainosUI.S(4), groupGap = KainosUI.S(12);
            float colW = (Width - pad * 2 - gap * (cols - 1)) / cols;
            List<List<Item>> shown = new List<List<Item>>();
            for (int gi = 0; gi < _groups.Length; gi++)
            {
                List<Item> l = new List<Item>();
                bool groupOn = gi == 0 ? ShowPower : (gi == 1 || gi == 3) ? ShowOptions : true;     // record / play are on the options panel
                foreach (Item it in _groups[gi])
                {
                    it.Shown = groupOn && targetShown(it);
                    if (it.Shown) l.Add(it);
                }
                shown.Add(l);
            }
            int rows = 0;
            foreach (List<Item> l in shown) rows += (l.Count + cols - 1) / cols;
            int groups = 0;
            foreach (List<Item> l in shown) if (l.Count > 0) groups++;
            float avail = Height - pad * 2 - Math.Max(0, groups - 1) * groupGap - Math.Max(0, rows - groups) * gap;
            float rowH = rows > 0 ? Math.Min(KainosUI.S(32), avail / rows) : 0;
            float y = pad;
            foreach (List<Item> l in shown)
            {
                if (l.Count == 0) continue;
                for (int i = 0; i < l.Count; i++)
                {
                    float x = pad + (i % cols) * (colW + gap);
                    l[i].Rect = new RectangleF(x, y + (i / cols) * (rowH + gap), colW, rowH);
                }
                y += ((l.Count + cols - 1) / cols) * (rowH + gap) - gap + groupGap;
            }
        }

        private Item hit(Point p)
        {
            foreach (Item[] g in _groups)
                foreach (Item it in g)
                    if (it.Shown && it.Rect.Contains(p)) return it;
            return null;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            layout();
            Graphics g = e.Graphics;
            float font = Math.Max(9f, Math.Min(KainosUI.S(12), (_groups[0][0].Rect.Height > 0 ? _groups[0][0].Rect.Height : 24) * 0.46f));
            foreach (Item[] grp in _groups)
                foreach (Item it in grp)
                {
                    if (!it.Shown) continue;
                    string text = it.Label ?? it.Target.Text.Replace("&&", "&");
                    KainosUI.DrawButton(g, it.Rect, text, it.On, it.Target.Enabled, it == _hover, it.Tone, font);
                }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Item h = hit(e.Location);
            if (h != _hover) { _hover = h; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = null;
            Invalidate();
        }

        // Left click: a real click on the Thetis button (it toggles and runs its Click handler). Right click: the
        // Thetis button's right-click (VOX, TUN, 2TON, PS-A... open their settings).
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Item it = hit(e.Location);
            if (it == null || !it.Target.Enabled) return;
            if (e.Button == MouseButtons.Left)
                KainosUI.Press(it.Target);
            else if (e.Button == MouseButtons.Right)
            {
                MouseEventArgs m = new MouseEventArgs(MouseButtons.Right, 1, 1, 1, 0);
                _onMouseDown.Invoke(it.Target, new object[] { m });
                _onMouseUp.Invoke(it.Target, new object[] { m });
            }
            Invalidate();
        }
    }
}
