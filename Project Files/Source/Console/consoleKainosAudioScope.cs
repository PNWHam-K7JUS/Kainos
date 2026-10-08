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
using System.Windows.Forms;

namespace Thetis
{
    // The right column's AUDIO SCOPE tab (off by default): RX1's audio, or the transmit signal while transmitting, as
    // a waveform (KainosAudioScope.cs). The tap runs only while the tab is on screen.
    public partial class Console
    {
        private KainosActionGrid _scopeButtons;
        private KainosScopeView _scopeView;
        private Timer _scopeTimer;

        // saved with the options (hidden Setup box txtKainosScope): "tb=10"
        public string KainosScopeSettings = "";

        private void kainosAddScopeSection()
        {
            _scopeView = new KainosScopeView { RxRate = () => Audio.OutRate, TxRate = () => Audio.OutRateTX };
            _scopeButtons = new KainosActionGrid(KainosScopeView.Timebases.Length) { Name = "kainosScopeButtons" };
            foreach (int tb in KainosScopeView.Timebases)
            {
                int t = tb;
                _scopeButtons.Add(t + " ms", () => _scopeView.Timebase == t, () => { _scopeView.Timebase = t; scopeSave(); }, KainosUI.Tone.Ice);
            }
            _kainosColumn.Viewport.Controls.Add(_scopeButtons);
            _kainosColumn.Viewport.Controls.Add(_scopeView);
            int gap = KainosUI.S(6), view = KainosUI.S(150);
            _kainosColumn.AddSection("scope", "AUDIO SCOPE", w => _scopeButtons.PreferredHeight(w) + gap + view, r =>
            {
                int bh = _scopeButtons.PreferredHeight(r.Width);
                _scopeButtons.SetBounds(r.Left, r.Top, r.Width, bh);
                _scopeView.SetBounds(r.Left, r.Top + bh + gap, r.Width, view);
            }, false);
            KainosScopeLoad();

            // the tap and the drawing run only while the tab is on screen
            _scopeTimer = new Timer { Interval = 250 };
            _scopeTimer.Tick += (s, e) => scopeCheckRunning();
            _scopeTimer.Start();
            FormClosing += (s, e) => { _scopeTimer.Stop(); KainosScopeTap.Wanted = false; cmaster.KainosScopeRefresh(); };
        }

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

        internal void KainosScopeLoad()
        {
            if (_scopeView == null) return;
            foreach (string kv in (KainosScopeSettings ?? "").Split(';'))
            {
                int eq = kv.IndexOf('='), n;
                if (eq <= 0) continue;
                string k = kv.Substring(0, eq), v = kv.Substring(eq + 1);
                if (k == "tb" && int.TryParse(v, out n)) _scopeView.Timebase = n;
            }
            _scopeButtons.Invalidate();
        }

        private void scopeSave()
        {
            KainosScopeSettings = "tb=" + _scopeView.Timebase;
            _scopeButtons.Invalidate();
            KainosSettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
