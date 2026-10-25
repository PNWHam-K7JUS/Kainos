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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace Thetis
{
    // External PTT that stays keyed after it's released (GitHub #23). PollPTT un-keys within a millisecond or two of
    // the radio reporting PTT up, unless something deliberately holds it: MOX or the 2-tone test (_manual_mox), a RADE
    // end-of-over, or one of PollPTT's other guards.
    //
    // 1.0.12's log showed none of those holding it, with the PTT mode still MIC. The MOX handler sets the mode to NONE
    // first thing when it un-keys, so it never ran: chkMOX was already unchecked while _mox stayed true, and PollPTT's
    // "chkMOX.Checked = false" then changes nothing. How the two came apart isn't known yet, so:
    //  - every MOX change is recorded (time, thread, caller, whether the handler finished), and the last few go into
    //    the KainosErrors.txt line, which the error report includes;
    //  - when it happens, the un-key is finished here, so transmit drops instead of hanging.
    public partial class Console
    {
        private const int KainosPttStuckMs = 500;           // well past PollPTT's normal release
        private const int KainosPttStuckRadeMs = 3500;      // a RADE end-of-over may hold it up to 3 s (consoleRade.cs)
        private const int KainosPttRecoverMs = 250;         // chkMOX off but _mox on: finish the un-key after this
        private readonly Stopwatch _kainosPttStuck = new Stopwatch();
        private bool _kainosPttLogged, _kainosPttRecovered;

        private int _kainosMoxDepth;                        // > 0 while the MOX handler is running
        private readonly Queue<string> _kainosMoxTrail = new Queue<string>();
        private const int KainosMoxTrailLength = 4;

        // the MOX check box's handler (hooked in the designer, and unhooked/rehooked by the RADE and Ganymede code):
        // runs Thetis's handler and records the change
        private void chkMOX_CheckedChanged2(object sender, System.EventArgs e)
        {
            bool to = chkMOX.Checked, moxBefore = _mox;
            int depth = _kainosMoxDepth++;
            bool finished = false;
            try
            {
                chkMOX_CheckedChanged2_core(sender, e);
                finished = true;
            }
            finally
            {
                _kainosMoxDepth--;
                kainosMoxRecord(to, moxBefore, depth, finished);
            }
        }

        private void kainosMoxRecord(bool to, bool moxBefore, int depth, bool finished)
        {
            try
            {
                // the code that changed chkMOX: the first frames outside WinForms and this file
                string caller = string.Join("<", new StackTrace(2, false).GetFrames()
                    .Select(f => f.GetMethod())
                    .Where(m => m != null && m.DeclaringType != null && !m.DeclaringType.FullName.StartsWith("System."))
                    .Select(m => m.DeclaringType.Name + "." + m.Name)
                    .Take(3));
                string entry = DateTime.Now.ToString("HH:mm:ss.fff") + " chk=" + (to ? 1 : 0) + " mox " + (moxBefore ? 1 : 0) + ">" + (_mox ? 1 : 0) +
                               " ptt=" + _current_ptt_mode + " thread=" + Thread.CurrentThread.ManagedThreadId +
                               (depth > 0 ? " nested=" + depth : "") + (finished ? "" : " THREW") + " from " + caller;
                lock (_kainosMoxTrail)
                {
                    _kainosMoxTrail.Enqueue(entry);
                    while (_kainosMoxTrail.Count > KainosMoxTrailLength) _kainosMoxTrail.Dequeue();
                }
            }
            catch { }
        }

        // called from PollPTT each tick, before its guards
        private void kainosPttWatch(int dotdashptt)
        {
            bool stuck = _mox && _current_ptt_mode == PTTMode.MIC && (dotdashptt & 0x01) == 0;
            if (!stuck)
            {
                _kainosPttStuck.Reset();
                _kainosPttLogged = false;
                _kainosPttRecovered = false;
                return;
            }
            if (!_kainosPttStuck.IsRunning) _kainosPttStuck.Start();
            long ms = _kainosPttStuck.ElapsedMilliseconds;

            // chkMOX is already off, so PollPTT can't un-key: run the un-key half of the MOX handler, when nothing
            // that PollPTT respects is holding transmit and the handler isn't already part-way through
            if (!_kainosPttRecovered && ms >= KainosPttRecoverMs && !chkMOX.Checked && _kainosMoxDepth == 0 &&
                _rade_ptt_state == RadePttState.Idle && !_manual_mox && !_disable_ptt && !_rx_only && !_tx_inhibit &&
                !QSKEnabled && !_ganymede_pa_issue)
            {
                _kainosPttRecovered = true;
                kainosPttLog(dotdashptt, ms, "chkMOX was already off; un-keyed by Kainos");
                try { chkMOX_CheckedChanged2(chkMOX, EventArgs.Empty); }
                catch (Exception ex) { kainosPttLog(dotdashptt, ms, "the un-key threw " + ex.GetType().Name + ": " + ex.Message); }
                return;
            }

            int limit = _rade_ptt_state == RadePttState.Idle ? KainosPttStuckMs : KainosPttStuckRadeMs;
            if (_kainosPttLogged || ms < limit) return;
            _kainosPttLogged = true;        // once per stuck over
            kainosPttLog(dotdashptt, ms, null);
        }

        private void kainosPttLog(int dotdashptt, long ms, string what)
        {
            try
            {
                string trail;
                lock (_kainosMoxTrail) trail = string.Join(Environment.NewLine, _kainosMoxTrail.Select(t => "    mox change: " + t));
                string line = DateTime.Now.ToString("u") + "  ptt still keyed " + ms + " ms after the radio's PTT went up" +
                    (what != null ? " (" + what + ")" : "") + ":" +
                    " dotdashptt=0x" + dotdashptt.ToString("X2") +
                    " mode=" + _current_ptt_mode +
                    " chk_mox=" + chkMOX.Checked +
                    " mox_handler=" + _kainosMoxDepth +
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
                    Environment.NewLine + trail + Environment.NewLine;
                File.AppendAllText(Path.Combine(AppDataPath, "KainosErrors.txt"), line);
            }
            catch { }
        }
    }
}
