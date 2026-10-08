/*  consoleKainosAudioScope.cs

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
    // The right column's AUDIO SCOPE tab (off by default): RX1's audio, or the transmit signal while transmitting, as
    // a waveform, an audio spectrum or both (KainosAudioScope.cs), with its options on a right click. The tap runs
    // only while the tab is on screen.
    public partial class Console
    {
        private KainosActionGrid _scopeButtons;
        private KainosScopeView _scopeView;
        private Timer _scopeTimer;

        // saved with the options (hidden Setup box txtKainosScope): "tb=10;show=0;fs=1;trig=1;ch=0;pk=1;sm=1"
        public string KainosScopeSettings = "";

        private void kainosAddScopeSection()
        {
            _scopeView = new KainosScopeView { RxRate = () => Audio.OutRate, TxRate = () => Audio.OutRateTX, AfInfo = scopeAfInfo };
            _scopeView.MenuWanted += p => scopeMenu(p);
            _scopeButtons = new KainosActionGrid(4) { Name = "kainosScopeButtons" };
            for (int i = 0; i < 4; i++)
            {
                int t = KainosScopeView.Timebases[i];
                _scopeButtons.Add(t + " ms", () => _scopeView.Timebase == t, () => { _scopeView.Timebase = t; scopeSave(); }, KainosUI.Tone.Ice);
            }
            _kainosColumn.Viewport.Controls.Add(_scopeButtons);
            _kainosColumn.Viewport.Controls.Add(_scopeView);
            int gap = KainosUI.S(6);
            // the timebase buttons only while the waveform shows
            _kainosColumn.AddSection("scope", "AUDIO SCOPE", w => (scopeButtonsShown ? _scopeButtons.PreferredHeight(w) + gap : 0) + scopeViewHeight, r =>
            {
                int y = r.Top;
                if (scopeButtonsShown)
                {
                    int bh = _scopeButtons.PreferredHeight(r.Width);
                    _scopeButtons.SetBounds(r.Left, y, r.Width, bh);
                    y += bh + gap;
                }
                else _scopeButtons.SetBounds(r.Left, -30000, r.Width, 0);
                _scopeView.SetBounds(r.Left, y, r.Width, scopeViewHeight);
            }, false);
            KainosScopeLoad();

            // the tap and the drawing run only while the tab is on screen
            _scopeTimer = new Timer { Interval = 250 };
            _scopeTimer.Tick += (s, e) => scopeCheckRunning();
            _scopeTimer.Start();
            FormClosing += (s, e) => { _scopeTimer.Stop(); KainosScopeTap.Wanted = false; cmaster.KainosScopeRefresh(); };
        }

        private bool scopeButtonsShown { get { return _scopeView.Show != KainosScopeView.Shows.Af; } }
        private int scopeViewHeight { get { return KainosUI.S(_scopeView.Show == KainosScopeView.Shows.Both ? 250 : 150); } }

        private void scopeCheckRunning()
        {
            bool on = _kainosLayout && _kainosColumn != null && _kainosColumn.Visible && _kainosColumn.IsOn("scope")
                      && WindowState != FormWindowState.Minimized
                      && _scopeView.Bottom > 0 && _scopeView.Top < _kainosColumn.Viewport.Height;     // scrolled into view
            _scopeView.SetRunning(on, KainosLightMode);
            if (KainosScopeTap.Wanted != on)
            {
                KainosScopeTap.Wanted = on;
                cmaster.KainosScopeRefresh();
            }
        }

        // the AF spectrum's span for the mode (around the pitch in CW), and the receive filter in audio terms
        private KainosAfInfo scopeAfInfo()
        {
            DSPMode m = RX1DSPMode;
            KainosAfInfo i = new KainosAfInfo { Lo = 0, Hi = 3000, Pitch = float.NaN };
            switch (m)
            {
                case DSPMode.CWL:
                case DSPMode.CWU:
                    i.Pitch = CWPitch;
                    i.Lo = Math.Max(0, CWPitch - 500);
                    i.Hi = i.Lo + 1000;
                    break;
                case DSPMode.AM: case DSPMode.SAM: case DSPMode.DSB: case DSPMode.AM_LSB: case DSPMode.AM_USB: case DSPMode.DRM:
                    i.Hi = 6000; break;
                case DSPMode.FM: i.Hi = 8000; break;
                case DSPMode.DIGL: case DSPMode.DIGU: i.Hi = 3500; break;
            }
            int lo = RX1FilterLow, hi = RX1FilterHigh;
            if (hi <= 0) { i.PassLo = -hi; i.PassHi = -lo; }
            else if (lo >= 0) { i.PassLo = lo; i.PassHi = hi; }
            else { i.PassLo = 0; i.PassHi = Math.Max(-lo, hi); }
            i.LowerSideband = m == DSPMode.LSB || m == DSPMode.CWL || m == DSPMode.DIGL || m == DSPMode.AM_LSB;
            return i;
        }

        private void scopeMenu(Point at)
        {
            KainosScopeView v = _scopeView;
            ContextMenuStrip menu = new ContextMenuStrip { Renderer = new KainosToolStripRenderer(), BackColor = KainosUI.Raised, ForeColor = KainosUI.Text };
            Func<string, bool, Action, ToolStripMenuItem> item = (text, check, act) =>
            {
                ToolStripMenuItem it = new ToolStripMenuItem(text) { Checked = check, ForeColor = KainosUI.Text };
                it.Click += (s, e) => { act(); scopeSave(); v.Invalidate(); };
                return it;
            };
            Action<KainosScopeView.Shows> show = s => { v.Show = s; if (_kainosLayout) positionKainosColumn(); };

            menu.Items.Add(item("Waveform", v.Show == KainosScopeView.Shows.Scope, () => show(KainosScopeView.Shows.Scope)));
            menu.Items.Add(item("AF spectrum", v.Show == KainosScopeView.Shows.Af, () => show(KainosScopeView.Shows.Af)));
            menu.Items.Add(item("Both", v.Show == KainosScopeView.Shows.Both, () => show(KainosScopeView.Shows.Both)));
            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem scale = new ToolStripMenuItem("Scale") { ForeColor = KainosUI.Text };
            scale.DropDownItems.Add(item("Auto", v.FullScale == 1, () => v.FullScale = 1));
            foreach (int fs in KainosScopeView.FullScales)
            {
                int f = fs;
                scale.DropDownItems.Add(item((f == 0 ? "0" : f.ToString()) + " dBFS full scale", v.FullScale == f, () => v.FullScale = f));
            }
            menu.Items.Add(scale);

            ToolStripMenuItem tb = new ToolStripMenuItem("Timebase") { ForeColor = KainosUI.Text };
            foreach (int t in KainosScopeView.Timebases)
            {
                int tt = t;
                tb.DropDownItems.Add(item(t + " ms", v.Timebase == t, () => { v.Timebase = tt; _scopeButtons.Invalidate(); }));
            }
            menu.Items.Add(tb);
            menu.Items.Add(item("Trigger (hold the wave still)", v.Trigger, () => v.Trigger = !v.Trigger));

            ToolStripMenuItem ch = new ToolStripMenuItem("Channels") { ForeColor = KainosUI.Text };
            ch.DropDownItems.Add(item("Left and right", v.Channel == KainosScopeView.Channels.Both, () => v.Channel = KainosScopeView.Channels.Both));
            ch.DropDownItems.Add(item("Left only", v.Channel == KainosScopeView.Channels.Left, () => v.Channel = KainosScopeView.Channels.Left));
            ch.DropDownItems.Add(item("Right only", v.Channel == KainosScopeView.Channels.Right, () => v.Channel = KainosScopeView.Channels.Right));
            menu.Items.Add(ch);
            menu.Items.Add(new ToolStripSeparator());

            menu.Items.Add(item("AF peak hold", v.PeakHold, () => { v.PeakHold = !v.PeakHold; v.ClearPeak(); }));
            menu.Items.Add(item("AF smoothing", v.Smooth, () => v.Smooth = !v.Smooth));
            menu.Items.Add(item("Clear peak", false, v.ClearPeak));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(item("Hold", v.Hold, () => v.Hold = !v.Hold));

            menu.Closed += (s, e) => BeginInvoke(new Action(menu.Dispose));
            menu.Show(v, at);
        }

        internal void KainosScopeLoad()
        {
            if (_scopeView == null) return;
            KainosScopeView v = _scopeView;
            foreach (string kv in (KainosScopeSettings ?? "").Split(';'))
            {
                int eq = kv.IndexOf('='), n;
                if (eq <= 0 || !int.TryParse(kv.Substring(eq + 1), out n)) continue;
                switch (kv.Substring(0, eq))
                {
                    case "tb": v.Timebase = n; break;
                    case "show": if (n >= 0 && n <= 2) v.Show = (KainosScopeView.Shows)n; break;
                    case "fs": v.FullScale = n == 1 || Array.IndexOf(KainosScopeView.FullScales, n) >= 0 ? n : 1; break;
                    case "trig": v.Trigger = n != 0; break;
                    case "ch": if (n >= 0 && n <= 2) v.Channel = (KainosScopeView.Channels)n; break;
                    case "pk": v.PeakHold = n != 0; break;
                    case "sm": v.Smooth = n != 0; break;
                }
            }
            _scopeButtons.Invalidate();
            if (_kainosLayout) positionKainosColumn();
        }

        private void scopeSave()
        {
            KainosScopeView v = _scopeView;
            KainosScopeSettings = "tb=" + v.Timebase + ";show=" + (int)v.Show + ";fs=" + v.FullScale + ";trig=" + (v.Trigger ? 1 : 0)
                                  + ";ch=" + (int)v.Channel + ";pk=" + (v.PeakHold ? 1 : 0) + ";sm=" + (v.Smooth ? 1 : 0);
            _scopeButtons.Invalidate();
            KainosSettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
