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
    // Kainos layout, stage 4: the slice flags (SmartSDR's). A panel on the panadapter beside each VFO line (A on
    // RX1; B on RX2 when the display is split): the VFO's letter, antenna, filter width, active DSP, TX, the mode and
    // the frequency (the mouse wheel over a digit tunes by that digit), and a row of tabs - AUDIO, DSP, MODE,
    // RIT/XIT, VAC, FREEDV - each opening a drawer of controls under the flag.
    //
    // The panadapter is drawn by DirectX into pnlDisplay's window, and in Thetis's "flip" present mode that covers
    // any control placed over it, so each flag is its own small window owned by the console (it stays with the
    // console, never takes the keyboard focus, and draws on top whatever the present mode). It follows its VFO with
    // Thetis's own frequency-to-pixel conversion (HzToPixel, which allows for CTUN, RIT, zoom and pan) on a timer.
    public partial class Console
    {
        private KainosFlagForm _kainosFlagA, _kainosFlagB;
        private Timer _kainosFlagTimer;

        private void kainosFlagOn()
        {
            if (_kainosFlagA == null)
            {
                _kainosFlagA = new KainosFlagForm(this, 1);
                _kainosFlagB = new KainosFlagForm(this, 2);
                foreach (KainosFlagForm f in new[] { _kainosFlagA, _kainosFlagB })
                {
                    int rx = f == _kainosFlagA ? 1 : 2;
                    f.View.DragMoved += dy => kainosFlagDrag(rx, dy);
                    f.View.DragEnded += () => KainosSettingsChanged?.Invoke(this, EventArgs.Empty);
                    f.View.DragReset += () => { _kainosFlagDrop[rx] = 0; placeKainosFlags(); KainosSettingsChanged?.Invoke(this, EventArgs.Empty); };
                }
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
            if (_kainosFlagB != null) _kainosFlagB.Hide();
        }

        private void placeKainosFlags()
        {
            if (_kainosFlagA == null) return;
            bool layout = _kainosLayout && _kainosPartsOn && WindowState != FormWindowState.Minimized && Visible
                          && pnlDisplay.Visible && pnlDisplay.Width > 100 && pnlDisplay.Height > 60 && !collapsedDisplay;
            bool split = RX2Enabled && Display.SplitDisplay;

            int panH = split ? pnlDisplay.Height / 2 : pnlDisplay.Height;
            placeKainosFlag(_kainosFlagA, 1, layout, () => HzToPixel((float)((VFOAFreq - CentreFrequency) * 1e6)), 0, panH);
            if (KainosSplitB)
                // split (or quick split) without RX2: VFO B is the transmit frequency, on RX1's panadapter (issue #1)
                placeKainosFlag(_kainosFlagB, 2, layout, () => HzToPixel((float)((VFOBFreq - CentreFrequency) * 1e6)), 0, panH);
            else
                placeKainosFlag(_kainosFlagB, 2, layout && split, () => HzToPixel((float)((VFOBFreq - CentreRX2Frequency) * 1e6), 2), pnlDisplay.Height / 2, panH);
            if (_kainosVfoA != null) { _kainosVfoA.Invalidate(); _kainosVfoB.Invalidate(); }
            kainosProfileDropCheck();
        }

        // How far each flag (rx 1, 2) has been dragged down from the top of its panadapter, in unscaled pixels, so the
        // spots, TCI flags and skimmer markers drawn along the top can be seen (GitHub issue #2). Double-click the flag's
        // face to put it back. Saved with the settings (KainosFlagSettings).
        private readonly float[] _kainosFlagDrop = new float[3];
        private readonly float[] _kainosFlagMaxDrop = { 0, float.MaxValue, float.MaxValue };

        // how solid the flags are while the mouse isn't over them (Setup > Appearance > Kainos); solid while it is
        internal double KainosFlagOpacity = 0.75;

        internal string KainosFlagSettings
        {
            get { return string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0};{1:0}", _kainosFlagDrop[1], _kainosFlagDrop[2]); }
            set
            {
                string[] p = (value ?? "").Split(';');
                for (int i = 0; i < 2; i++)
                {
                    float d;
                    _kainosFlagDrop[i + 1] = i < p.Length && float.TryParse(p[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d) ? Math.Max(0, d) : 0;
                }
                placeKainosFlags();
            }
        }

        private void kainosFlagDrag(int rx, int dy)
        {
            _kainosFlagDrop[rx] = Math.Max(0, Math.Min(_kainosFlagMaxDrop[rx], _kainosFlagDrop[rx] + dy / KainosUI.Scale));
            placeKainosFlags();
        }

        private void placeKainosFlag(KainosFlagForm flag, int rx, bool show, Func<int> vfoX, int panTop, int panH)
        {
            int x = 0;
            if (show)
            {
                try { x = vfoX(); } catch { show = false; }
                if (x < 0 || x > pnlDisplay.Width) show = false;      // the VFO is off its panadapter
            }
            if (!show)
            {
                if (flag.Visible) flag.Hide();
                return;
            }

            // hang the flag to the left of the line, or to the right when there's no room on the left
            Size size = flag.PreferredSize2();
            int gap = KainosUI.S(6);
            int left = x - gap - size.Width;
            if (left < 2) left = x + gap;
            left = Math.Max(2, Math.Min(left, pnlDisplay.Width - size.Width - 2));
            int top = panTop + KainosUI.S(26);                    // below the frequency scale
            int maxDrop = Math.Max(0, panH - KainosUI.S(26) - size.Height - 2);
            _kainosFlagMaxDrop[rx] = maxDrop / KainosUI.Scale;
            top += Math.Min(maxDrop, KainosUI.S((int)_kainosFlagDrop[rx]));      // dragged down by the user
            Point screen = pnlDisplay.PointToScreen(new Point(left, top));
            Rectangle want = new Rectangle(screen, size);
            if (flag.Bounds != want) flag.Bounds = want;
            if (!flag.Visible) flag.Show(this);
            // see-through while the mouse isn't over it (the spots behind it show), solid while it is, while a tab's
            // drawer is open and while typing a frequency
            double solid = KainosFlagOpacity >= 1 ? 1.0
                        : flag.Bounds.Contains(Cursor.Position) || flag.DrawerOpen || flag.ContainsFocus || flag.View.Dragging ? 0.99 : KainosFlagOpacity;
            if (Math.Abs(flag.Opacity - solid) > 0.001) flag.Opacity = solid;
            flag.View.Invalidate();
            flag.RefreshDrawer();
        }

        // ---- what the flags show (rx 1 or 2) ----

        private Control kainosFilterPanel(int rx) { return rx == 1 ? (Control)panelFilter : panelRX2Filter; }

        internal string KainosFilterText(int rx)
        {
            if (rx == 2 && KainosSplitB) return "";
            RadioButton r = kainosFilterPanel(rx).Controls.OfType<RadioButton>().FirstOrDefault(b => b.Checked);
            if (r != null && r.Text.Length > 0 && !r.Text.StartsWith("Var")) return r.Text;
            try
            {
                RadioDSPRX d = radio.GetDSPRX(rx - 1, 0);
                double w = Math.Abs(d.RXFilterHigh - d.RXFilterLow);
                return w >= 1000 ? (w / 1000).ToString("0.0") + "k" : w.ToString("0");
            }
            catch { return ""; }
        }

        // VFO B as split's transmit frequency (split or quick split on, RX2 off): it has no receiver of its own
        internal bool KainosSplitB { get { return chkVFOSplit.Checked && !RX2Enabled; } }

        internal string KainosDspText(int rx)
        {
            List<string> on = new List<string>();
            if (rx == 2 && KainosSplitB) return _quickSplitState ? "QUICK SPLIT" : "SPLIT";
            if (rx == 1)
            {
                if (chkNR.Checked) on.Add(chkNR.Text);
                if (chkNB.Checked) on.Add(chkNB.Text);
                if (chkDSPNB2.Checked) on.Add("SNB");
                if (chkANF.Checked) on.Add("ANF");
                if (chkBIN.Checked) on.Add("BIN");
            }
            else
            {
                foreach (CheckBox c in panelRX2DSP.Controls.OfType<CheckBox>().Where(c => c.Checked && c.Text.Length > 0).OrderBy(c => c.Top).ThenBy(c => c.Left))
                    on.Add(c.Text);
            }
            return string.Join(" ", on);
        }

        internal string KainosAntText(int rx)
        {
            string t = toolStripStatusLabelRXAnt.Text ?? "";      // the HL2 has the one receive antenna for both
            return t.Replace("Rx ", "").Replace("RX ", "").Trim().ToUpperInvariant().Replace(" ", "");
        }

        // the transmit VFO: A unless VFO B transmits (with RX2 on, or split)
        internal bool KainosIsTxVfo(int rx) { return rx == 1 ? !chkVFOBTX.Checked : chkVFOBTX.Checked; }
        internal bool KainosMox { get { return _mox; } }
        internal string KainosModeText(int rx)
        {
            if (RadeEnabledOn(rx - 1)) return "RADE";
            return (rx == 1 || KainosSplitB ? _rx1_dsp_mode : _rx2_dsp_mode).ToString();      // split without RX2: B transmits in RX1's mode
        }
        internal double KainosVfoMHz(int rx) { return rx == 1 ? VFOAFreq : VFOBFreq; }

        // tune by the digit under the pointer (or the console's tune step)
        internal void KainosTune(int rx, int steps, long stepHz)
        {
            if (steps == 0 || stepHz <= 0) return;
            if (rx == 1 && _vfoA_lock) return;
            if (rx == 2 && _vfoB_lock) return;
            double hz = Math.Round(KainosVfoMHz(rx) * 1e6) + steps * (double)stepHz;
            if (hz < 0) return;
            if (rx == 1) VFOAFreq = hz / 1e6; else VFOBFreq = hz / 1e6;
        }

        // a frequency typed on the flag: MHz ("7.1761", "7,1761"), or kHz when there's no decimal point ("7176.1"
        // has one; "7176" is kHz)
        internal void KainosSetFreq(int rx, string text)
        {
            if (rx == 1 && _vfoA_lock) return;
            if (rx == 2 && _vfoB_lock) return;
            string t = (text ?? "").Trim().Replace(',', '.');
            double v;
            if (!double.TryParse(t, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v) || v <= 0) return;
            if (!t.Contains(".") && v >= 1000) v /= 1000;       // kHz
            if (rx == 1) VFOAFreq = v; else VFOBFreq = v;
        }

        // click the TX badge: this VFO transmits (Thetis's TX buttons on the VFO A / B boxes)
        internal void KainosSetTxVfo(int rx)
        {
            CheckBox c = rx == 1 ? (CheckBox)chkVFOATX : chkVFOBTX;
            if (!c.Checked && c.Enabled) kainosClick(c);
        }

        // the S meter: Thetis's signal strength in dBm (calibrated), and S units (S9 = -73 dBm, -93 above 30 MHz)
        internal float KainosSignalDbm(int rx)
        {
            if (!PowerOn) return -200f;
            if (rx == 2 && !RX2Enabled) return -200f;                       // no RX2: nothing to measure
            return WDSP.CalculateRXMeter(rx == 1 ? 0u : 2u, 0u, WDSP.MeterType.SIGNAL_STRENGTH) + RXOffset(rx);
        }
        internal double KainosSUnits(int rx, float dbm) { return Common.GetSMeterUnits(dbm, KainosVfoMHz(rx) >= S9Frequency); }

        internal int KainosTuneStepHz { get { return CurrentTuneStepHz; } }

        // ---- the drawers ----

        internal Control KainosBuildDrawer(int rx, string tab, Action changed)
        {
            KainosUI.Tone tone = rx == 1 ? KainosUI.Tone.Gold : KainosUI.Tone.Violet;
            Panel p = new Panel { BackColor = Color.FromArgb(0x06, 0x0e, 0x17) };
            List<Control> rows = new List<Control>();
            switch (tab)
            {
                case "AUDIO":
                    {
                        KainosActionGrid g = new KainosActionGrid(1);
                        CheckBox mute = rx == 1 ? (CheckBox)chkMUT : chkRX2Mute;
                        g.Add("MUTE", () => mute.Checked, () => kainosClick(mute), KainosUI.Tone.Tx);
                        rows.Add(new KainosSlider(rx == 1 ? ptbRX1AF : ptbRX2AF, rx == 1 ? "RX1 AF" : "RX2 AF"));
                        rows.Add(g);
                        break;
                    }
                case "DSP":
                    {
                        // Thetis's DSP buttons (the panel at the bottom is collapsed): NR, NB, SNB, ANF, BIN, MNF, +MNF
                        KainosButtonGrid g = new KainosButtonGrid(4, KainosUI.Tone.Ice);
                        Control panel = rx == 1 ? (Control)panelDSP : panelRX2DSP;
                        g.SetTargets(panel.Controls.OfType<ButtonBase>().Where(c => c != chkMUT && c != chkRX2Mute)
                                         .OrderBy(c => c.Top).ThenBy(c => c.Left));
                        rows.Add(g);
                        break;
                    }
                case "MODE":
                    {
                        KainosButtonGrid modes = new KainosButtonGrid(4, tone);
                        modes.SetTargets(orderedButtons(rx == 1 ? (Control)panelMode : panelRX2Mode));
                        KainosButtonGrid filters = new KainosButtonGrid(4, KainosUI.Tone.Ice);
                        filters.SetTargets(orderedButtons(kainosFilterPanel(rx)).Where(b => b is RadioButton));
                        rows.Add(modes);
                        rows.Add(filters);
                        break;
                    }
                case "RIT/XIT":
                    {
                        KainosActionGrid g = new KainosActionGrid(2);
                        g.Add("RIT", () => chkRIT.Checked, () => kainosClick(chkRIT), tone);
                        g.Add("XIT", () => chkXIT.Checked, () => kainosClick(chkXIT), tone);
                        g.Add("RIT 0", () => false, () => kainosClick(btnRITReset), KainosUI.Tone.Ice);
                        g.Add("XIT 0", () => false, () => kainosClick(btnXITReset), KainosUI.Tone.Ice);
                        rows.Add(g);
                        rows.Add(new KainosUpDown(udRIT, "RIT", "Hz"));
                        rows.Add(new KainosUpDown(udXIT, "XIT", "Hz"));
                        break;
                    }
                case "VAC":
                    {
                        KainosActionGrid g = new KainosActionGrid(2);
                        g.Add("VAC1", () => chkVAC1.Checked, () => kainosClick(chkVAC1), KainosUI.Tone.Ice, () => kainosRightClick(chkVAC1));
                        g.Add("VAC2", () => chkVAC2.Checked, () => kainosClick(chkVAC2), KainosUI.Tone.Ice, () => kainosRightClick(chkVAC2));
                        rows.Add(g);
                        break;
                    }
                case "FREEDV":
                    {
                        KainosActionGrid g = new KainosActionGrid(2);
                        int i = rx - 1;
                        g.Add("RADE", () => RadeEnabledOn(i), () => SetRadeEnabled(i, !RadeEnabledOn(i)), tone);
                        g.Add("FreeDV...", () => false, ShowFreeDV, KainosUI.Tone.Ice);
                        rows.Add(new KainosTextLine(() =>
                        {
                            if (!RadeEnabledOn(i)) return "RADE is off";
                            string s = RadeSyncOn(i) ? "SYNC · SNR " + RadeSnrDbOn(i) + " dB" : "no sync";
                            string c = RadeRemoteCallsignOn(i);
                            return c.Length > 0 ? s + "  ·  last " + c : s;
                        }, () => RadeEnabledOn(i) ? KainosUI.Text : KainosUI.Faint));
                        rows.Add(g);
                        break;
                    }
            }

            // stack the rows
            int pad = KainosUI.S(8), gap = KainosUI.S(6), w = KainosUI.S(250) - pad * 2, y = pad;
            foreach (Control c in rows)
            {
                int h = c is KainosButtonGrid ? ((KainosButtonGrid)c).PreferredHeight(w)
                      : c is KainosActionGrid ? ((KainosActionGrid)c).PreferredHeight(w)
                      : c is KainosSlider ? KainosUI.S(40) : c is KainosUpDown ? KainosUI.S(26) : KainosUI.S(20);
                c.SetBounds(pad, y, w, h);
                p.Controls.Add(c);
                y += h + gap;
            }
            p.Size = new Size(KainosUI.S(250), y - gap + pad);
            return p;
        }

        // ---- the VFO tab in the right column (the same face, without tabs) ----

        private KainosFlagView _kainosVfoA, _kainosVfoB;

        private void kainosAddVfoSection()
        {
            _kainosVfoA = new KainosFlagView(this, 1, false);
            _kainosVfoB = new KainosFlagView(this, 2, false);
            _kainosColumn.Viewport.Controls.Add(_kainosVfoA);
            _kainosColumn.Viewport.Controls.Add(_kainosVfoB);
            _kainosColumn.AddSection("vfo", "VFO", w => KainosFlagView.FaceHeight + (RX2Enabled || KainosSplitB ? KainosUI.S(6) + KainosFlagView.FaceHeight : 0), r =>
            {
                _kainosVfoA.SetBounds(r.Left, r.Top, r.Width, KainosFlagView.FaceHeight);
                if (RX2Enabled || KainosSplitB) _kainosVfoB.SetBounds(r.Left, r.Top + KainosFlagView.FaceHeight + KainosUI.S(6), r.Width, KainosFlagView.FaceHeight);
                else _kainosVfoB.Top = -30000;
            });
            RX2EnabledChangedHandlers += enabled => { if (_kainosLayout) positionKainosColumn(); };
            chkVFOSplit.CheckedChanged += (s, e) => { if (_kainosLayout) positionKainosColumn(); };      // split shows VFO B
        }
    }

    // A borderless window owned by the console that never takes the focus: the flag, and under it the drawer of the
    // tab that is open
    internal class KainosFlagForm : Form
    {
        private readonly Console _console;
        private readonly int _rx;
        public readonly KainosFlagView View;
        private Control _drawer;
        private string _drawerTab;

        public KainosFlagForm(Console console, int rx)
        {
            _console = console;
            _rx = rx;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(0x06, 0x0e, 0x17);
            View = new KainosFlagView(console, rx, true);
            View.TabClicked += toggleDrawer;
            Controls.Add(View);
            layoutFlag();
        }

        public Size PreferredSize2()
        {
            Size v = View.PreferredFlagSize();
            return new Size(v.Width, v.Height + (_drawer != null ? _drawer.Height : 0));
        }

        private void layoutFlag()
        {
            Size v = View.PreferredFlagSize();
            View.SetBounds(0, 0, v.Width, v.Height);
            if (_drawer != null) _drawer.SetBounds(0, v.Height, v.Width, _drawer.Height);
        }

        private void toggleDrawer(string tab)
        {
            if (_drawer != null)
            {
                Controls.Remove(_drawer);
                _drawer.Dispose();
                _drawer = null;
            }
            _drawerTab = _drawerTab == tab ? null : tab;
            View.OpenTab = _drawerTab;
            if (_drawerTab != null)
            {
                _drawer = _console.KainosBuildDrawer(_rx, _drawerTab, () => Invalidate(true));
                Controls.Add(_drawer);
            }
            layoutFlag();
            Size = PreferredSize2();
            Invalidate(true);
        }

        public bool DrawerOpen { get { return _drawer != null; } }

        // keep the drawer's buttons current (state changes behind them come from Thetis)
        public void RefreshDrawer() { if (_drawer != null) _drawer.Invalidate(true); }

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

    // The flag's face: letter, antenna, filter, DSP, TX; mode and frequency; and (on the panadapter) the tab row
    internal class KainosFlagView : Control
    {
        private static readonly string[] Tabs = { "AUDIO", "DSP", "MODE", "RIT/XIT", "VAC", "FREEDV" };
        private readonly Console _console;
        private readonly int _rx;
        private readonly bool _showTabs;
        private readonly List<KeyValuePair<RectangleF, long>> _digits = new List<KeyValuePair<RectangleF, long>>();
        private readonly RectangleF[] _tabRects = new RectangleF[Tabs.Length];
        private int _hoverTab = -1;
        private RectangleF _txRect, _freqRect;
        private bool _hoverTx;
        public string OpenTab;
        public event Action<string> TabClicked;
        // dragging the flag up and down by its face (on the panadapter): the move in screen pixels, the end, and a
        // double-click to put it back at the top
        public event Action<int> DragMoved;
        public event Action DragEnded, DragReset;
        private int _dragY = int.MinValue;
        public bool Dragging { get { return _dragY != int.MinValue; } }

        public KainosFlagView(Console console, int rx, bool showTabs)
        {
            _console = console;
            _rx = rx;
            _showTabs = showTabs;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.FromArgb(0x06, 0x0e, 0x17);
        }

        // the face: identity row, mode and frequency, S meter (the tab row comes under it)
        public static int FaceHeight { get { return KainosUI.S(86); } }

        public Size PreferredFlagSize() { return new Size(KainosUI.S(250), FaceHeight + (_showTabs ? KainosUI.S(22) : 0)); }

        private Color tone { get { return _rx == 1 ? KainosUI.Gold : KainosUI.Violet; } }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            float s = KainosUI.Scale;
            float faceH = FaceHeight;
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
                x = drawItem(g, f, _console.KainosAntText(_rx), tone, x, y, badge);
                x = drawItem(g, f, _console.KainosFilterText(_rx), KainosUI.Dim, x, y, badge);
            }
            using (Font f = new Font("Segoe UI", 10 * s, FontStyle.Regular, GraphicsUnit.Pixel))
                drawItem(g, f, _console.KainosDspText(_rx), KainosUI.Faint, x, y, badge);

            // TX: outlined red on the transmit VFO (filled while transmitting); dim on the other, where a click makes
            // it the transmit VFO (Thetis's TX buttons on its VFO boxes)
            {
                bool isTx = _console.KainosIsTxVfo(_rx);
                RectangleF tx = new RectangleF(Width - pad - 26 * s, y + 1 * s, 26 * s, badge - 2 * s);
                _txRect = tx;
                bool keyed = isTx && _console.KainosMox;
                Color c = isTx ? KainosUI.Tx : (_hoverTx ? KainosUI.Dim : KainosUI.Line);
                using (System.Drawing.Drawing2D.GraphicsPath path = KainosUI.RoundedRect(tx, 3 * s))
                {
                    if (keyed) using (Brush b = new SolidBrush(KainosUI.Tx)) g.FillPath(b, path);
                    using (Pen p = new Pen(c)) g.DrawPath(p, path);
                }
                using (Font f = new Font("Segoe UI", 10 * s, FontStyle.Bold, GraphicsUnit.Pixel))
                using (Brush b = new SolidBrush(keyed ? Color.White : isTx ? KainosUI.Tx : (_hoverTx ? KainosUI.Dim : KainosUI.Faint)))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    g.DrawString("TX", f, b, tx, sf);
            }

            // frequency as Thetis shows it, "14.225" then "000" (Hz) smaller; each digit remembers its place value
            // for wheel tuning
            string whole = _console.KainosVfoMHz(_rx).ToString("0.000000", System.Globalization.CultureInfo.InvariantCulture);
            string head = whole.Substring(0, whole.Length - 3), tail = whole.Substring(whole.Length - 3);
            float fy = y + badge + 2 * s, fh = 37 * s;
            _digits.Clear();
            using (Font big = new Font("Consolas", 26 * s, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font small = new Font("Consolas", 20 * s, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Brush bh = new SolidBrush(KainosUI.Text))
            using (Brush bt = new SolidBrush(KainosUI.Dim))
            {
                SizeF tailSize = g.MeasureString(tail, small);
                SizeF headSize = g.MeasureString(head, big);
                float right = Width - pad + 3 * s;
                float baseY = fy + (fh - headSize.Height) / 2;
                PointF tailAt = new PointF(right - tailSize.Width, baseY + (headSize.Height - tailSize.Height) * 0.75f);
                PointF headAt = new PointF(right - tailSize.Width - headSize.Width + 6 * s, baseY);
                g.DrawString(tail, small, bt, tailAt);
                g.DrawString(head, big, bh, headAt);
                recordDigits(g, head, big, headAt, 1000);           // head's last digit is kHz
                recordDigits(g, tail, small, tailAt, 1);            // tail: hundreds, tens, units of Hz
                _freqRect = new RectangleF(headAt.X, baseY, right - headAt.X, headSize.Height);
            }
            using (Font f = new Font("Segoe UI", 12 * s, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Brush b = new SolidBrush(KainosUI.Ice))
            using (StringFormat sf = new StringFormat { LineAlignment = StringAlignment.Center })
                g.DrawString(_console.KainosModeText(_rx), f, b, new RectangleF(pad, fy, 70 * s, fh), sf);

            drawSMeter(g, s, pad, fy + fh + 1 * s);

            // tab row
            if (_showTabs)
            {
                float ty = faceH, tw = (Width - 2) / (float)Tabs.Length, th = KainosUI.S(22);
                using (Pen line = new Pen(KainosUI.Line)) g.DrawLine(line, 1, ty, Width - 1, ty);
                using (Font f = new Font("Segoe UI", 9 * s, FontStyle.Bold, GraphicsUnit.Pixel))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    for (int i = 0; i < Tabs.Length; i++)
                    {
                        RectangleF r = new RectangleF(1 + i * tw, ty, tw, th);
                        _tabRects[i] = r;
                        bool open = OpenTab == Tabs[i];
                        if (open) using (Brush b = new SolidBrush(KainosUI.Selected)) g.FillRectangle(b, r);
                        if (i > 0) using (Pen line = new Pen(KainosUI.Line)) g.DrawLine(line, r.Left, r.Top, r.Left, r.Bottom);
                        Color c = open ? (_rx == 1 ? KainosUI.GoldHi : KainosUI.Violet) : (i == _hoverTab ? KainosUI.Text : KainosUI.Dim);
                        using (Brush b = new SolidBrush(c)) g.DrawString(Tabs[i], f, b, r, sf);
                    }
            }
        }

        // A bar S meter under the frequency: the reading at the left ("S7", "S9+12"), the bar (S1-S9 over the first
        // 60%, S9 to +60 dB the rest, gold / violet to S9 and red over it) with its scale under it. Fast attack,
        // slow decay. Empty while this receiver's VFO transmits or the radio is off.
        private float _sm = -1;
        private void drawSMeter(Graphics g, float s, float pad, float top)
        {
            float dbm = _console.KainosSignalDbm(_rx);
            bool live = dbm > -190f && !(_console.KainosMox && _console.KainosIsTxVfo(_rx));
            double su = live ? _console.KainosSUnits(_rx, dbm) : 0;
            float target = live ? (float)(su <= 9 ? Math.Max(0, su) / 9 * 0.6 : 0.6 + Math.Min(60, (su - 9) * 6) / 60 * 0.4) : 0;
            _sm = _sm < 0 || target > _sm ? target : _sm * 0.8f + target * 0.2f;

            string reading = !live ? "S-" : su <= 9 ? "S" + Math.Max(0, (int)Math.Floor(su)) : "S9+" + (int)Math.Round((su - 9) * 6);
            float lw = 40 * s;
            using (Font f = new Font("Consolas", 11 * s, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Brush b = new SolidBrush(live ? KainosUI.Text : KainosUI.Faint))
                g.DrawString(reading, f, b, pad - 2 * s, top);
            // the KiwiSDR being listened to (KIWI tab), under VFO A's own reading: here vs. there
            string kiwi = _rx == 1 ? _console.KiwiSignalText : "";
            if (kiwi.Length > 0)
                using (Font f = new Font("Segoe UI", 8 * s, FontStyle.Bold, GraphicsUnit.Pixel))
                using (Brush b = new SolidBrush(KainosUI.Ice))
                    g.DrawString("K " + kiwi, f, b, pad - 2 * s, top + 11 * s);

            RectangleF bar = new RectangleF(pad + lw, top + 2 * s, Width - pad * 2 - lw, 5 * s);
            using (Brush b = new SolidBrush(KainosUI.Line)) g.FillRectangle(b, bar);
            float w9 = bar.Width * 0.6f, wv = bar.Width * _sm;
            using (Brush b = new SolidBrush(tone)) g.FillRectangle(b, bar.X, bar.Y, Math.Min(wv, w9), bar.Height);
            if (wv > w9) using (Brush b = new SolidBrush(KainosUI.Tx)) g.FillRectangle(b, bar.X + w9, bar.Y, wv - w9, bar.Height);

            // scale: 1 3 5 7 9 +20 +40 +60
            using (Font f = new Font("Segoe UI", 8 * s, FontStyle.Regular, GraphicsUnit.Pixel))
            using (Brush b = new SolidBrush(KainosUI.Faint))
            using (Pen tick = new Pen(KainosUI.Steel))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center })
            {
                string[] labels = { "1", "3", "5", "7", "9", "+20", "+40", "+60" };
                float[] at = { 1 / 9f * 0.6f, 3 / 9f * 0.6f, 5 / 9f * 0.6f, 7 / 9f * 0.6f, 0.6f, 0.6f + 0.4f / 3, 0.6f + 0.8f / 3, 1f };
                for (int i = 0; i < labels.Length; i++)
                {
                    float x = bar.X + bar.Width * at[i];
                    g.DrawLine(tick, x, bar.Bottom, x, bar.Bottom + 2 * s);
                    float lx = Math.Min(x, bar.Right - 8 * s);
                    g.DrawString(labels[i], f, b, new RectangleF(lx - 15 * s, bar.Bottom + 1.5f * s, 30 * s, 11 * s), sf);
                }
            }
        }

        // click the frequency to type one (Enter sets it, Esc or clicking away cancels)
        private TextBox _edit;
        private void beginEdit()
        {
            if (_edit != null || _freqRect.IsEmpty) return;
            _edit = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = KainosUI.Bg,
                ForeColor = KainosUI.Text,
                Font = new Font("Consolas", 20 * KainosUI.Scale, FontStyle.Bold, GraphicsUnit.Pixel),
                TextAlign = HorizontalAlignment.Right,
                Text = _console.KainosVfoMHz(_rx).ToString("0.000000", System.Globalization.CultureInfo.InvariantCulture),
            };
            int w = (int)Math.Max(_freqRect.Width, 140 * KainosUI.Scale);
            _edit.SetBounds((int)(_freqRect.Right - w), (int)(_freqRect.Top + (_freqRect.Height - _edit.PreferredHeight) / 2), w, _edit.PreferredHeight);
            _edit.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { string t = _edit.Text; endEdit(); _console.KainosSetFreq(_rx, t); e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.Escape) { endEdit(); e.SuppressKeyPress = true; }
            };
            _edit.LostFocus += (s, e) => BeginInvoke(new Action(endEdit));
            Controls.Add(_edit);
            Form f = FindForm();
            if (f is KainosFlagForm) f.Activate();        // the flag never takes the focus unless typing in it
            _edit.Focus();
            _edit.SelectAll();
        }

        private void endEdit()
        {
            if (_edit == null) return;
            TextBox t = _edit;
            _edit = null;
            Controls.Remove(t);
            t.Dispose();
            if (FindForm() is KainosFlagForm) _console.Activate();
            Invalidate();
        }

        // the place value of each digit (lowest at the right), skipping the decimal point
        private void recordDigits(Graphics g, string text, Font f, PointF at, long lowest)
        {
            CharacterRange[] ranges = Enumerable.Range(0, text.Length).Select(i => new CharacterRange(i, 1)).ToArray();
            using (StringFormat sf = new StringFormat(StringFormat.GenericDefault))
            {
                sf.SetMeasurableCharacterRanges(ranges.Take(32).ToArray());
                Region[] regs = g.MeasureCharacterRanges(text, f, new RectangleF(at.X, at.Y, 1000, 200), sf);
                long place = lowest;
                for (int i = text.Length - 1; i >= 0; i--)
                {
                    if (text[i] == '.') continue;
                    RectangleF r = regs[i].GetBounds(g);
                    _digits.Add(new KeyValuePair<RectangleF, long>(r, place));
                    place *= 10;
                }
            }
        }

        private static float drawItem(Graphics g, Font f, string text, Color c, float x, float y, float h)
        {
            if (string.IsNullOrEmpty(text)) return x;
            using (Brush b = new SolidBrush(c))
            using (StringFormat sf = new StringFormat { LineAlignment = StringAlignment.Center })
                g.DrawString(text, f, b, new RectangleF(x, y, 200, h), sf);
            return x + g.MeasureString(text, f).Width + 4;
        }

        // the wheel over a digit tunes by that digit; elsewhere by the console's tune step
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            long step = _console.KainosTuneStepHz;
            foreach (KeyValuePair<RectangleF, long> d in _digits)
            {
                RectangleF r = d.Key;
                if (e.X >= r.Left - 1 && e.X <= r.Right + 1 && e.Y >= r.Top && e.Y <= r.Bottom) { step = d.Value; break; }
            }
            _console.KainosTune(_rx, Math.Sign(e.Delta), step);
            if (e is HandledMouseEventArgs) ((HandledMouseEventArgs)e).Handled = true;     // not on to the console's own wheel tuning
            Invalidate();
        }

        // the face, other than the TX button and the frequency, moves the flag
        private bool inDragArea(Point p) { return _showTabs && p.Y < FaceHeight && !_txRect.Contains(p) && !_freqRect.Contains(p); }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (Dragging)
            {
                int dy = Cursor.Position.Y - _dragY;
                if (dy != 0) { _dragY += dy; DragMoved?.Invoke(dy); }
                return;
            }
            int h = -1;
            if (_showTabs) for (int i = 0; i < Tabs.Length; i++) if (_tabRects[i].Contains(e.Location)) h = i;
            bool overDigit = _digits.Any(d => d.Key.Contains(e.Location));
            bool overTx = _txRect.Contains(e.Location) && !_console.KainosIsTxVfo(_rx);
            Cursor = h >= 0 || overTx ? Cursors.Hand : overDigit ? Cursors.IBeam : inDragArea(e.Location) ? Cursors.SizeNS : Cursors.Default;
            if (h != _hoverTab || overTx != _hoverTx) { _hoverTab = h; _hoverTx = overTx; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hoverTab = -1; _hoverTx = false; Invalidate(); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            if (_txRect.Contains(e.Location)) { _console.KainosSetTxVfo(_rx); Invalidate(); return; }
            if (_freqRect.Contains(e.Location)) { beginEdit(); return; }
            if (inDragArea(e.Location)) { _dragY = Cursor.Position.Y; Capture = true; return; }
            if (!_showTabs) return;
            for (int i = 0; i < Tabs.Length; i++)
                if (_tabRects[i].Contains(e.Location)) { TabClicked?.Invoke(Tabs[i]); return; }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!Dragging) return;
            _dragY = int.MinValue;
            Capture = false;
            DragEnded?.Invoke();
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (Dragging && !Capture) { _dragY = int.MinValue; DragEnded?.Invoke(); }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button == MouseButtons.Left && inDragArea(e.Location)) DragReset?.Invoke();
        }
    }

    // A Kainos number bound to a Thetis NumericUpDown (RIT, XIT): its label and value, with - and + (its increment),
    // the mouse wheel over it the same; Thetis's ValueChanged does the rest
    internal class KainosUpDown : Control
    {
        private readonly NumericUpDown _target;
        private readonly string _label, _unit;
        private readonly bool _signed;     // +120 / -120 (offsets) or plain numbers
        private RectangleF _minus, _plus;
        private int _hover;     // -1 minus, +1 plus

        public KainosUpDown(NumericUpDown target, string label, string unit, bool signed = true)
        {
            _target = target;
            _signed = signed;
            _label = label;
            _unit = unit;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.FromArgb(0x06, 0x0e, 0x17);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            float b = Height, gap = KainosUI.S(4);
            _plus = new RectangleF(Width - b, 0, b, b);
            _minus = new RectangleF(Width - b * 2 - gap, 0, b, b);
            float fs = Math.Max(9f, KainosUI.S(12));
            KainosUI.DrawButton(g, _minus, "-", false, _target.Enabled, _hover == -1, KainosUI.Tone.Ice, fs);
            KainosUI.DrawButton(g, _plus, "+", false, _target.Enabled, _hover == 1, KainosUI.Tone.Ice, fs);
            using (Font f = new Font("Segoe UI", fs, FontStyle.Regular, GraphicsUnit.Pixel))
            using (Font v = new Font("Consolas", fs, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Brush dim = new SolidBrush(KainosUI.Dim))
            using (Brush txt = new SolidBrush(_target.Value != 0 ? KainosUI.Text : KainosUI.Faint))
            using (StringFormat sf = new StringFormat { LineAlignment = StringAlignment.Center })
            using (StringFormat sr = new StringFormat { LineAlignment = StringAlignment.Center, Alignment = StringAlignment.Far })
            {
                g.DrawString(_label, f, dim, new RectangleF(2, 0, KainosUI.S(40), Height), sf);
                g.DrawString(_target.Value.ToString(_signed ? "+0;-0;0" : "0") + " " + _unit, v, txt, new RectangleF(0, 0, _minus.Left - KainosUI.S(8), Height), sr);
            }
        }

        private void step(int dir)
        {
            if (!_target.Enabled || dir == 0) return;
            decimal v = Math.Max(_target.Minimum, Math.Min(_target.Maximum, _target.Value + dir * _target.Increment));
            if (v != _target.Value) _target.Value = v;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int h = _minus.Contains(e.Location) ? -1 : _plus.Contains(e.Location) ? 1 : 0;
            Cursor = h != 0 ? Cursors.Hand : Cursors.Default;
            if (h != _hover) { _hover = h; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = 0; Invalidate(); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            if (_minus.Contains(e.Location)) step(-1); else if (_plus.Contains(e.Location)) step(1);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            step(Math.Sign(e.Delta));
            if (e is HandledMouseEventArgs) ((HandledMouseEventArgs)e).Handled = true;
        }
    }

    // A Kainos slider bound to a Thetis PrettyTrackBar: dragging or the wheel sets its value and raises its Scroll,
    // exactly as dragging Thetis's own slider would
    internal class KainosSlider : Control
    {
        private readonly PrettyTrackBar _target;
        private readonly string _label;
        private readonly bool _compact;     // one line: label, then the track (the panadapter's bar)
        private readonly Timer _follow;
        private int _shownValue = int.MinValue;

        protected override void Dispose(bool disposing) { if (disposing) _follow.Dispose(); base.Dispose(disposing); }
        private static readonly System.Reflection.MethodInfo _onScroll = typeof(PrettyTrackBar).GetMethod("OnScroll",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, new[] { typeof(object), typeof(EventArgs) }, null);
        private bool _drag;

        public KainosSlider(PrettyTrackBar target, string label, bool compact = false)
        {
            _target = target;
            _label = label;
            _compact = compact;
            // PrettyTrackBar has no value-changed event, and Thetis moves pan / zoom from the panadapter too: follow it
            _follow = new Timer { Interval = 150 };
            _follow.Tick += (s, e) => { if (_target.Value != _shownValue && IsHandleCreated && Visible) Invalidate(); };
            _follow.Start();
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.FromArgb(0x06, 0x0e, 0x17);
            Cursor = Cursors.Hand;
        }

        private float labelWidth { get { return _compact ? KainosUI.S(_label.Length > 3 ? 40 : 30) : 0; } }

        private RectangleF track
        {
            get
            {
                if (_compact) return new RectangleF(labelWidth + KainosUI.S(6), Height / 2f - KainosUI.S(2), Width - labelWidth - KainosUI.S(12), KainosUI.S(4));
                return new RectangleF(KainosUI.S(6), Height - KainosUI.S(14), Width - KainosUI.S(12), KainosUI.S(4));
            }
        }

        private float norm { get { int span = Math.Max(1, _target.Maximum - _target.Minimum); return (_target.Value - _target.Minimum) / (float)span; } }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            if (_compact)
                using (Font f = new Font("Segoe UI", Math.Max(7f, KainosUI.S(10)), FontStyle.Bold, GraphicsUnit.Pixel))
                using (Brush b = new SolidBrush(KainosUI.Faint))
                using (StringFormat sf = new StringFormat { LineAlignment = StringAlignment.Center })
                    g.DrawString(_label, f, b, new RectangleF(2, 0, labelWidth + KainosUI.S(4), Height), sf);
            else
            using (Font f = new Font("Segoe UI", Math.Max(8f, KainosUI.S(11)), FontStyle.Regular, GraphicsUnit.Pixel))
            {
                using (Brush b = new SolidBrush(KainosUI.Dim)) g.DrawString(_label, f, b, 2, 0);
                using (Brush b = new SolidBrush(KainosUI.Text))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Far })
                    g.DrawString(_target.Value.ToString(), f, b, new RectangleF(0, 0, Width - 2, KainosUI.S(16)), sf);
            }
            _shownValue = _target.Value;
            RectangleF t = track;
            using (Brush b = new SolidBrush(KainosUI.Line)) g.FillRectangle(b, t);
            using (Brush b = new SolidBrush(KainosUI.Ice)) g.FillRectangle(b, t.X, t.Y, t.Width * norm, t.Height);
            float kx = t.X + t.Width * norm;
            RectangleF knob = new RectangleF(kx - KainosUI.S(5), t.Y - KainosUI.S(5), KainosUI.S(10), KainosUI.S(14));
            using (System.Drawing.Drawing2D.GraphicsPath p = KainosUI.RoundedRect(knob, KainosUI.S(2)))
            {
                using (Brush b = new SolidBrush(KainosUI.Text)) g.FillPath(b, p);
                using (Pen pen = new Pen(KainosUI.Steel)) g.DrawPath(pen, p);
            }
        }

        private void setFromX(int x)
        {
            RectangleF t = track;
            float n = Math.Max(0, Math.Min(1, (x - t.X) / t.Width));
            setValue(_target.Minimum + (int)Math.Round(n * (_target.Maximum - _target.Minimum)));
        }

        private void setValue(int v)
        {
            v = Math.Max(_target.Minimum, Math.Min(_target.Maximum, v));
            if (v == _target.Value) return;
            _target.Value = v;
            _onScroll?.Invoke(_target, new object[] { _target, EventArgs.Empty });
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) { _drag = true; setFromX(e.X); } }
        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (_drag) setFromX(e.X); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); _drag = false; }
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            setValue(_target.Value + Math.Sign(e.Delta));
            if (e is HandledMouseEventArgs) ((HandledMouseEventArgs)e).Handled = true;
        }
    }
}
