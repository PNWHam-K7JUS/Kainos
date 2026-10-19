/*  consoleKainosPtt.cs

This file is part of Kainos, a program that implements a Software Defined Radio.

Copyright (C) 2026 Justin Cron, K7JUS

This program is free software; you can redistribute it and/or modify it under the terms of the GNU General Public
License as published by the Free Software Foundation; either version 2 of the License, or (at your option) any later
version.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied
warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License along with this program; if not, write to the Free
Software Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
*/

using System;
using System.Diagnostics;
using System.IO;

namespace Thetis
{
    // A diagnostic for external PTT that stays keyed after it's released (GitHub #23). PollPTT un-keys within a
    // millisecond or two of the radio reporting PTT up, unless something deliberately holds it: MOX or the 2-tone
    // test (_manual_mox), a RADE end-of-over, or one of PollPTT's other guards. Which one it was can't be told from a
    // report, so when the radio says PTT is up but Kainos is still keyed from it, this writes one line to
    // KainosErrors.txt (which the error report includes) with the radio's PTT bit and every flag that can hold it.
    // It only watches; keying is unchanged.
    public partial class Console
    {
        private const int KainosPttStuckMs = 500;           // well past PollPTT's normal release
        private const int KainosPttStuckRadeMs = 3500;      // a RADE end-of-over may hold it up to 3 s (consoleRade.cs)
        private readonly Stopwatch _kainosPttStuck = new Stopwatch();
        private bool _kainosPttLogged;

        // called from PollPTT each tick, before its guards
        private void kainosPttWatch(int dotdashptt)
        {
            bool stuck = _mox && _current_ptt_mode == PTTMode.MIC && (dotdashptt & 0x01) == 0;
            if (!stuck)
            {
                _kainosPttStuck.Reset();
                _kainosPttLogged = false;
                return;
            }
            if (!_kainosPttStuck.IsRunning) _kainosPttStuck.Start();
            int limit = _rade_ptt_state == RadePttState.Idle ? KainosPttStuckMs : KainosPttStuckRadeMs;
            if (_kainosPttLogged || _kainosPttStuck.ElapsedMilliseconds < limit) return;
            _kainosPttLogged = true;        // once per stuck over

            try
            {
                string line = DateTime.Now.ToString("u") + "  ptt still keyed " + _kainosPttStuck.ElapsedMilliseconds + " ms after the radio's PTT went up:" +
                    " dotdashptt=0x" + dotdashptt.ToString("X2") +
                    " mode=" + _current_ptt_mode +
                    " manual_mox=" + _manual_mox +
                    " two_tone=" + chk2TONE.Checked +
                    " tune=" + chkTUN.Checked +
                    " ps=" + (psform != null && psform.PSEnabled) +
                    " rade=" + _rade_ptt_state +
                    " disable_ptt=" + _disable_ptt +
                    " rx_only=" + _rx_only +
                    " tx_inhibit=" + _tx_inhibit +
                    " qsk=" + QSKEnabled +
                    " ganymede=" + _ganymede_pa_issue +
                    " model=" + HardwareSpecific.Model +
                    Environment.NewLine;
                File.AppendAllText(Path.Combine(AppDataPath, "KainosErrors.txt"), line);
            }
            catch { }
        }
    }
}
