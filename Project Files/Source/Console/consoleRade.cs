/*  consoleRade.cs

This file is part of Kainos, a program that implements a Software-Defined Radio.
Kainos is based on Thetis : https://github.com/ramdor/Thetis

RADE (FreeDV Radio Autoencoder) support is ported from Thetis-RADE by
Christos Nikolaou (SV1EIA) : https://github.com/sv1eia/Thetis-RADE
Copyright (C) 2026 Christos Nikolaou (SV1EIA), Justin Cron K7JUS

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
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace Thetis
{
    // FreeDV RADE on RX1 and the transmitter: switching it on and off, and the PTT arbiter that keeps
    // the radio keyed at the end of an over until the end-of-over frame (which carries your callsign)
    // has actually been sent.
    public partial class Console
    {
        private ToolStripMenuItem freeDVToolStripMenuItem;
        private frmFreeDV _frmFreeDV;

        private void addRadeControls()
        {
            freeDVToolStripMenuItem = new ToolStripMenuItem("FreeDV")
            {
                Name = "freeDVToolStripMenuItem",
                ToolTipText = "Open FreeDV: RADE digital voice on RX1 and the transmitter (ported from Thetis-RADE by SV1EIA)"
            };
            freeDVToolStripMenuItem.Click += (s, e) => ShowFreeDV();
            menuStrip1.Items.Insert(menuStrip1.Items.IndexOf(kainosAudioToolStripMenuItem) + 1, freeDVToolStripMenuItem);
        }

        public void ShowFreeDV()
        {
            if (_frmFreeDV == null || _frmFreeDV.IsDisposed)
            {
                _frmFreeDV = new frmFreeDV(this) { Owner = this };
                _frmFreeDV.Location = new Point(Left + (Width - _frmFreeDV.Width) / 2, Top + (Height - _frmFreeDV.Height) / 2);
            }
            _frmFreeDV.Show();
            _frmFreeDV.BringToFront();
            _frmFreeDV.Activate();
        }

        #region Enable

        // the callsign sent in each over's end-of-over frame (from Setup > DSP > FreeDV (RADE))
        private volatile string _radeCallsign = "";
        public string RadeCallsign
        {
            get { return _radeCallsign; }
            set { _radeCallsign = (value ?? "").Trim().ToUpperInvariant(); }
        }

        private DSPMode _radeSavedMode = DSPMode.FIRST;     // RX1's mode before RADE switched it to DIGU/DIGL
        private DSPMode _radeForcedMode = DSPMode.FIRST;

        public event EventHandler RadeEnabledChanged;

        public bool RadeEnabled
        {
            get
            {
                try { return Rade.GetRadaeRxEnabled(0) != 0; }
                catch { return false; }
            }
            set { SetRadeEnabled(value); }
        }

        // RADE is the operating mode for RX1 while it is on: RX1 goes to DIGL below 10 MHz and DIGU above
        // (DIGU on 60 m), noise reduction, NB2 and the auto-notch are switched off because they damage the
        // modem signal, and Kainos Audio is bypassed (it runs in voice modes only). Switching RADE off puts
        // RX1 back in the mode it was in. Returns false if it can't change now (while transmitting).
        public bool SetRadeEnabled(bool on)
        {
            if (on == RadeEnabled) return true;
            if (_mox || _rade_ptt_state != RadePttState.Idle) return false;

            if (on)
            {
                _radeSavedMode = _rx1_dsp_mode;
                Rade.SetRadaeRxScale(0, 1.0);
                Rade.SetRadaeRxEnabled(0, 1);
                Rade.SetRadaeTxEnabled(1);

                // MI0BOT's HL2 code locks RX1's AF at a fixed level in the digital modes; with RADE the AF
                // slider sets the level of the decoded speech, so give it back
                if (HardwareSpecific.Model == HPSDRModel.HERMESLITE && isDigiMode(_rx1_dsp_mode)) hl2DigiAudio(false);
                reapplyRX1AF();

                double f = VFOAFreq;
                DSPMode want = (f >= 5.0 && f < 5.5) || f >= 10.0 ? DSPMode.DIGU : DSPMode.DIGL;
                _radeForcedMode = want;
                if (_rx1_dsp_mode != want) RX1DSPMode = want;

                forceRadeCleanAudio();
            }
            else
            {
                Rade.SetRadaeRxEnabled(0, 0);
                Rade.SetRadaeTxEnabled(0);
                reapplyRX1AF();

                if (_radeSavedMode != DSPMode.FIRST && _rx1_dsp_mode == _radeForcedMode && _radeSavedMode != _rx1_dsp_mode)
                    RX1DSPMode = _radeSavedMode;
                else if (HardwareSpecific.Model == HPSDRModel.HERMESLITE && isDigiMode(_rx1_dsp_mode))
                    hl2DigiAudio(true);
                _radeSavedMode = _radeForcedMode = DSPMode.FIRST;
            }

            RadeEnabledChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        private static bool isDigiMode(DSPMode m) { return m == DSPMode.DIGU || m == DSPMode.DIGL; }

        // RX1's AF slider goes through RadioDSPRX.RXOutputGain, which sends it after the decoder while RADE
        // is on and to WDSP otherwise; setting it again moves it to the right place
        private void reapplyRX1AF()
        {
            RadioDSPRX d = radio.GetDSPRX(0, 0);
            d.RXOutputGain = d.RXOutputGain;
        }

        // the same steps as MI0BOT's HL2 digital-mode code in SetRX1Mode: lock AF at a fixed level, or unlock it
        private void hl2DigiAudio(bool locked)
        {
            ptbRX1AF.Enabled = !locked;
            ptbRX1AF.SmallChange = locked ? 0 : 1;
            ptbRX1AF.LargeChange = locked ? 0 : 1;
            ptbRX0Gain.Enabled = !locked;
            ptbRX0Gain.SmallChange = locked ? 0 : 1;
            ptbRX0Gain.LargeChange = locked ? 0 : 1;
            if (locked) radio.GetDSPRX(0, 0).RXOutputGain = 0.1;
            else chkMUT_CheckedChanged(this, EventArgs.Empty);
        }

        // NR1-4, NB2 and the auto-notch distort the OFDM signal; NB1 and SNB are fine and left alone.
        // The buttons are set first (their handlers write WDSP), then WDSP's ANF flag directly.
        private void forceRadeCleanAudio()
        {
            try
            {
                SelectNR(1, true, 1);   // step to plain NR, then off, so the button shows "NR"
                SelectNR(1, true, 0);
                if (chkNB.CheckState == CheckState.Indeterminate) chkNB.CheckState = CheckState.Checked;    // NB2 -> NB1
                chkANF.Checked = false;
                radio.GetDSPRX(0, 0).AutoNotchFilter = false;
            }
            catch { }
        }

        #endregion

        #region Status (for the FreeDV window)

        public bool RadeSync { get { return Rade.GetRadaeSync(0) != 0; } }
        public int RadeSnrDb { get { return Rade.GetRadaeSnrDb(0); } }
        public int RadeRxLevelDb { get { return Rade.GetRadaeRxLevelDb(0); } }
        public bool RadeRxClip { get { return Rade.GetRadaeClip(0) != 0; } }
        public int RadeMicLevelDb { get { return Rade.GetRadaeTxMicLevelDb(); } }
        public bool RadeMicClip { get { return Rade.GetRadaeTxMicClip() != 0; } }
        public float RadeFreqOffsetHz { get { return Rade.GetRadaeFreqOffset(0); } }
        public int RadeCallsignSeq { get { return Rade.GetRadaeRemoteCallsignSeq(0); } }
        public bool RadeTransmitting { get { return _mox && Rade.OverWouldBeRade(this) || _rade_ptt_state != RadePttState.Idle; } }
        public bool RadeSendingEoo { get { return _rade_ptt_state == RadePttState.EmitEOO || _rade_ptt_state == RadePttState.Flushing; } }

        public string RadeRemoteCallsign
        {
            get
            {
                StringBuilder sb = new StringBuilder(32);
                int n = Rade.GetRadaeRemoteCallsign(0, sb, sb.Capacity);
                return n > 0 ? sb.ToString().Trim() : "";
            }
        }

        #endregion

        #region PTT arbiter

        // From Thetis-RADE ("Option B"). A RADE over is keyed as normal. When it is released, by the MOX
        // button or by any PTT source, the release is held off: the radio stays keyed while the encoder sends
        // the end-of-over frame and 60 ms of silence, then a 300 ms margin, and only then un-keys (3 s cap).
        // The state machine runs on the PollPTT thread, every tick.
        private enum RadePttState { Idle, Transmitting, EmitEOO, Flushing, Releasing }
        private volatile RadePttState _rade_ptt_state = RadePttState.Idle;
        private volatile bool _rade_ptt_request;
        private volatile bool _rade_arbiter_actuating;     // the arbiter is writing chkMOX itself
        private const int RadeEooMarginMs = 300;
        private const int RadeEooTimeoutMs = 3000;
        private readonly Stopwatch _rade_seq_sw = new Stopwatch();
        private long _rade_flush_ack_ms = -1;
        private bool _rade_over_was_rade;
        private Color _rade_mox_fore = Color.Empty;

        // Called at the top of each PollPTT loop. Returns true while the end-of-over is being sent, so the
        // rest of PollPTT is skipped and can't fight the held-off un-key.
        private bool radePollTick()
        {
            radePttStateMachine();
            return _rade_ptt_state == RadePttState.EmitEOO ||
                   _rade_ptt_state == RadePttState.Flushing ||
                   _rade_ptt_state == RadePttState.Releasing;
        }

        // Called at the top of chkMOX_CheckedChanged2. Key-down of a RADE over engages the arbiter and keys
        // as normal; the un-key of an over the arbiter owns is undone here (MOX re-asserted without running
        // the keying code) and done later by the arbiter. Returns true when the change was swallowed.
        private bool radeInterceptMox()
        {
            if (_rade_arbiter_actuating) return false;
            if (chkMOX.Checked)
            {
                if (chkPower.Checked && Rade.OverWouldBeRade(this)) _rade_ptt_request = true;
                return false;
            }
            if (_rade_ptt_state == RadePttState.Idle || !chkPower.Checked)
            {
                _rade_ptt_request = false;
                return false;
            }
            _rade_ptt_request = false;
            chkMOX.CheckedChanged -= chkMOX_CheckedChanged2;
            chkMOX.Checked = true;
            chkMOX.CheckedChanged += chkMOX_CheckedChanged2;
            return true;
        }

        private void radePttStateMachine()
        {
            switch (_rade_ptt_state)
            {
                case RadePttState.Idle:
                    if (_rade_ptt_request)
                    {
                        if (_rx_only || _tx_inhibit || _ganymede_pa_issue) { _rade_ptt_request = false; return; }
                        _rade_over_was_rade = RadeEnabled;
                        radeKeyRadio(true);
                        _rade_seq_sw.Restart();
                        setRadeState(RadePttState.Transmitting);
                    }
                    break;

                case RadePttState.Transmitting:
                    if (!_rade_ptt_request)
                    {
                        if (_rade_over_was_rade)
                        {
                            Rade.SetRadaeTxSilenceHold(1);       // keep writing silence while held keyed
                            Rade.EooSentByArbiter = true;        // no second end-of-over at the real un-key
                            Rade.RadaeNotifyEndOfOver();
                            Rade.SetRadaeMoxState(0);            // stop new speech; send the end-of-over
                            _rade_flush_ack_ms = -1;
                            _rade_seq_sw.Restart();
                            setRadeState(RadePttState.EmitEOO);
                        }
                        else setRadeState(RadePttState.Releasing);
                    }
                    else if (!_mox && _rade_seq_sw.ElapsedMilliseconds > 500)   // un-keyed some other way (power off)
                    {
                        _rade_ptt_request = false;
                        setRadeState(RadePttState.Idle);
                    }
                    break;

                case RadePttState.EmitEOO:
                    setRadeState(RadePttState.Flushing);        // one tick for the encoder to pick up the request
                    break;

                case RadePttState.Flushing:
                    if (_rade_ptt_request)                      // keyed again: carry on with the over
                    {
                        Rade.SetRadaeMoxState(1);
                        Rade.SetRadaeTxSilenceHold(0);
                        Rade.EooSentByArbiter = false;
                        _rade_seq_sw.Restart();
                        setRadeState(RadePttState.Transmitting);
                        break;
                    }
                    if (_rade_flush_ack_ms < 0 && Rade.GetRadaeEooFlushed() != 0)
                        _rade_flush_ack_ms = _rade_seq_sw.ElapsedMilliseconds;
                    bool marginDone = _rade_flush_ack_ms >= 0 && _rade_seq_sw.ElapsedMilliseconds - _rade_flush_ack_ms >= RadeEooMarginMs;
                    if (marginDone || _rade_seq_sw.ElapsedMilliseconds >= RadeEooTimeoutMs)
                        setRadeState(RadePttState.Releasing);
                    break;

                case RadePttState.Releasing:
                    radeKeyRadio(false);
                    _manual_mox = false;
                    Rade.SetRadaeTxSilenceHold(0);              // after the un-key
                    setRadeState(RadePttState.Idle);
                    break;
            }
        }

        // chkMOX is a thread-safe CheckBoxTS, so this marshals to the UI thread
        private void radeKeyRadio(bool on)
        {
            _rade_arbiter_actuating = true;
            try { if (chkMOX.Checked != on) chkMOX.Checked = on; }
            finally { _rade_arbiter_actuating = false; }
        }

        // the MOX button's letters turn orange while the end-of-over is being sent
        private void setRadeState(RadePttState next)
        {
            if (_rade_ptt_state == next) return;
            _rade_ptt_state = next;
            bool eoo = next == RadePttState.EmitEOO || next == RadePttState.Flushing;
            if (eoo && _rade_mox_fore == Color.Empty) { _rade_mox_fore = chkMOX.ForeColor; chkMOX.ForeColor = Color.Orange; }
            else if (!eoo && _rade_mox_fore != Color.Empty) { chkMOX.ForeColor = _rade_mox_fore; _rade_mox_fore = Color.Empty; }
        }

        #endregion
    }
}
