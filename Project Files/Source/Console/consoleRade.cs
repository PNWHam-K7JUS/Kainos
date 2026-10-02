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
using System.Collections.Generic;
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

        // FreeDV Reporter options (Setup > DSP > FreeDV (RADE)), read by the reporter window
        public bool RadeIgnoreQsyRequest { get; set; }
        public bool RadeReporterTimesUtc { get; set; } = true;

        // the callsign sent in each over's end-of-over frame (from Setup > DSP > FreeDV (RADE))
        private volatile string _radeCallsign = "";
        public string RadeCallsign
        {
            get { return _radeCallsign; }
            set { _radeCallsign = (value ?? "").Trim().ToUpperInvariant(); }
        }

        // per receiver (0 = RX1, 1 = RX2): the mode before RADE switched it to DIGU/DIGL, and the mode it chose
        private readonly DSPMode[] _radeSavedMode = { DSPMode.FIRST, DSPMode.FIRST };
        private readonly DSPMode[] _radeForcedMode = { DSPMode.FIRST, DSPMode.FIRST };
        private bool _radeRx2Hooked;

        public event EventHandler RadeEnabledChanged;

        // RADE on RX1
        public bool RadeEnabled
        {
            get { return RadeEnabledOn(0); }
            set { SetRadeEnabled(0, value); }
        }

        // RADE on RX2
        public bool RadeRx2Enabled
        {
            get { return RadeEnabledOn(1); }
            set { SetRadeEnabled(1, value); }
        }

        public bool RadeEnabledOn(int rx)
        {
            try { return Rade.GetRadaeRxEnabled(rx) != 0; }
            catch { return false; }
        }

        public bool RadeAnyEnabled { get { return RadeEnabledOn(0) || RadeEnabledOn(1); } }

        public bool SetRadeEnabled(bool on) { return SetRadeEnabled(0, on); }

        // RADE is the operating mode for a receiver while it is on: it goes to DIGL below 10 MHz and DIGU above
        // (DIGU on 60 m), noise reduction, NB2 and the auto-notch are switched off because they damage the modem
        // signal, and Kainos Audio is bypassed (it runs in voice modes only). Switching RADE off puts the receiver
        // back in the mode it was in. RX2 needs RX2 to be on. The encoder serves whichever receiver is transmitting:
        // RX2's on a VFO B over with RX2 on, RX1's otherwise.
        // Returns false if it can't change now (transmitting, or RX2 off).
        public bool SetRadeEnabled(int rx, bool on)
        {
            if (rx < 0 || rx > 1) return false;
            if (on == RadeEnabledOn(rx)) return true;
            if (_mox || _rade_ptt_state != RadePttState.Idle) return false;
            if (on && rx == 1 && !RX2Enabled) return false;
            hookRadeRx2();

            if (on)
            {
                _radeSavedMode[rx] = rxMode(rx);
                Rade.SetRadaeRxScale(rx, 1.0);
                Rade.SetRadaeRxEnabled(rx, 1);
                Rade.SetRadaeTxEnabled(1);

                // MI0BOT's HL2 code locks the receiver's AF at a fixed level in the digital modes; with RADE the
                // AF slider sets the level of the decoded speech, so give it back
                if (HardwareSpecific.Model == HPSDRModel.HERMESLITE && isDigiMode(rxMode(rx))) hl2DigiAudio(rx, false);
                reapplyAF(rx);

                double f = rx == 0 ? VFOAFreq : VFOBFreq;
                DSPMode want = (f >= 5.0 && f < 5.5) || f >= 10.0 ? DSPMode.DIGU : DSPMode.DIGL;
                _radeForcedMode[rx] = want;
                if (rxMode(rx) != want)
                {
                    if (rx == 0) RX1DSPMode = want;
                    else RX2DSPMode = want;
                }

                forceRadeCleanAudio(rx);
            }
            else
            {
                Rade.SetRadaeRxEnabled(rx, 0);
                Rade.SetRadaeTxEnabled(RadeAnyEnabled ? 1 : 0);
                reapplyAF(rx);

                DSPMode saved = _radeSavedMode[rx];
                if (saved != DSPMode.FIRST && rxMode(rx) == _radeForcedMode[rx] && saved != rxMode(rx))
                {
                    if (rx == 0) RX1DSPMode = saved;
                    else RX2DSPMode = saved;
                }
                else if (HardwareSpecific.Model == HPSDRModel.HERMESLITE && isDigiMode(rxMode(rx)))
                    hl2DigiAudio(rx, true);
                _radeSavedMode[rx] = _radeForcedMode[rx] = DSPMode.FIRST;
            }

            RadeEnabledChanged?.Invoke(this, EventArgs.Empty);
            RadaeEnabledChangedHandlers?.Invoke(rx + 1, on);       // meter containers that hide without RADE
            FreeDVReporter.FreeDVReporterManager.Update(this);     // report while RADE is on, if asked to
            return true;
        }

        // RX2 RADE goes off when RX2 is switched off
        private void hookRadeRx2()
        {
            if (_radeRx2Hooked) return;
            _radeRx2Hooked = true;
            RX2EnabledChangedHandlers += enabled =>
            {
                if (!enabled && RadeEnabledOn(1)) SetRadeEnabled(1, false);
            };
        }

        private DSPMode rxMode(int rx) { return rx == 0 ? _rx1_dsp_mode : _rx2_dsp_mode; }

        private static bool isDigiMode(DSPMode m) { return m == DSPMode.DIGU || m == DSPMode.DIGL; }

        // The AF slider goes through RadioDSPRX.RXOutputGain, which sends it after the decoder while RADE is on
        // and to WDSP otherwise; setting it again moves it to the right place
        private void reapplyAF(int rx)
        {
            RadioDSPRX d = radio.GetDSPRX(rx, 0);
            d.RXOutputGain = d.RXOutputGain;
        }

        // the same steps as MI0BOT's HL2 digital-mode code in SetRX1Mode / SetRX2Mode: lock AF at a fixed
        // level, or unlock it
        private void hl2DigiAudio(int rx, bool locked)
        {
            PrettyTrackBar af = rx == 0 ? ptbRX1AF : ptbRX2AF;
            PrettyTrackBar gain = rx == 0 ? ptbRX0Gain : ptbRX2Gain;
            af.Enabled = !locked;
            af.SmallChange = locked ? 0 : 1;
            af.LargeChange = locked ? 0 : 1;
            gain.Enabled = !locked;
            gain.SmallChange = locked ? 0 : 1;
            gain.LargeChange = locked ? 0 : 1;
            if (locked) radio.GetDSPRX(rx, 0).RXOutputGain = 0.1;
            else if (rx == 0) chkMUT_CheckedChanged(this, EventArgs.Empty);
            else chkRX2Mute_CheckedChanged(this, EventArgs.Empty);
        }

        // NR1-4, NB2 and the auto-notch distort the OFDM signal; NB1 and SNB are fine and left alone.
        // The buttons are set first (their handlers write WDSP), then WDSP's ANF flag directly.
        private void forceRadeCleanAudio(int rx)
        {
            try
            {
                SelectNR(rx + 1, true, 1);   // step to plain NR, then off, so the button shows "NR"
                SelectNR(rx + 1, true, 0);
                CheckBoxTS nb = rx == 0 ? chkNB : chkRX2NB;
                CheckBoxTS anf = rx == 0 ? chkANF : chkRX2ANF;
                if (nb.CheckState == CheckState.Indeterminate) nb.CheckState = CheckState.Checked;    // NB2 -> NB1
                anf.Checked = false;
                radio.GetDSPRX(rx, 0).AutoNotchFilter = false;
            }
            catch { }
        }

        #endregion

        #region Status (for the FreeDV window)

        public bool RadeSyncOn(int rx) { return Rade.GetRadaeSync(rx) != 0; }
        public int RadeSnrDbOn(int rx) { return Rade.GetRadaeSnrDb(rx); }
        public int RadeRxLevelDbOn(int rx) { return Rade.GetRadaeRxLevelDb(rx); }
        public bool RadeRxClipOn(int rx) { return Rade.GetRadaeClip(rx) != 0; }
        public float RadeFreqOffsetHzOn(int rx) { return Rade.GetRadaeFreqOffset(rx); }
        public int RadeCallsignSeqOn(int rx) { return Rade.GetRadaeRemoteCallsignSeq(rx); }
        public int RadeMicLevelDb { get { return Rade.GetRadaeTxMicLevelDb(); } }
        public bool RadeMicClip { get { return Rade.GetRadaeTxMicClip() != 0; } }
        public bool RadeTransmitting { get { return _mox && Rade.OverWouldBeRade(this) || _rade_ptt_state != RadePttState.Idle; } }
        public bool RadeSendingEoo { get { return _rade_ptt_state == RadePttState.EmitEOO || _rade_ptt_state == RadePttState.Flushing; } }

        // the receiver a VFO B over with RX2 on transmits from; RX1 otherwise
        public int RadeTxReceiver { get { return RX2Enabled && VFOBTX ? 1 : 0; } }

        public string RadeRemoteCallsignOn(int rx)
        {
            StringBuilder sb = new StringBuilder(32);
            int n = Rade.GetRadaeRemoteCallsign(rx, sb, sb.Capacity);
            return n > 0 ? sb.ToString().Trim() : "";
        }

        #endregion

        #region Meters and panadapter overlay (from Thetis-RADE)

        // Thetis-RADE's meter code (MeterManager, frmMeterDisplay) uses these names. rx is 1 or 2.
        public delegate void RadaeEnabledChanged(int rx, bool enabled);
        public RadaeEnabledChanged RadaeEnabledChangedHandlers;
        public bool RadaeRx1Enabled { get { return RadeEnabledOn(0); } }
        public bool RadaeRx2Enabled { get { return RadeEnabledOn(1); } }

        // The RADE status overlay at the top right of each panadapter (display.cs), per receiver and on
        // transmit; set from Setup > DSP > FreeDV (RADE). It shows only while that receiver has RADE on.
        public bool RadeMeasureRx1 { get; set; } = true;
        public bool RadeMeasureRx2 { get; set; } = true;
        public bool RadeMeasureTx { get; set; } = true;

        // the RADE readings for meter containers, polled with the other receiver meters (rx is 1 or 2)
        private void radeMeterReadings(int rx)
        {
            int i = rx - 1;
            Dictionary<Reading, float> values = rx == 1 ? _RX1MeterValues : _RX2MeterValues;
            if (!RadeEnabledOn(i))
            {
                // idle values while RADE is off, so nothing freezes at its last reading
                values[Reading.RADAE_SYNC] = 0;
                values[Reading.RADAE_SNR_DB] = 0;
                values[Reading.RADAE_RX_LEVEL_DB] = -120;
                values[Reading.RADAE_CLIP] = 0;
                values[Reading.RADAE_EOO_DECODE] = 0;
                return;
            }
            if (MeterManager.RequiresUpdate(rx, Reading.RADAE_SYNC)) values[Reading.RADAE_SYNC] = Rade.GetRadaeSync(i);
            if (MeterManager.RequiresUpdate(rx, Reading.RADAE_SNR_DB)) values[Reading.RADAE_SNR_DB] = Rade.GetRadaeSnrDb(i);
            if (MeterManager.RequiresUpdate(rx, Reading.RADAE_RX_LEVEL_DB)) values[Reading.RADAE_RX_LEVEL_DB] = Rade.GetRadaeRxLevelDb(i);
            if (MeterManager.RequiresUpdate(rx, Reading.RADAE_CLIP)) values[Reading.RADAE_CLIP] = Rade.GetRadaeClip(i);
            if (MeterManager.RequiresUpdate(rx, Reading.RADAE_EOO_DECODE)) values[Reading.RADAE_EOO_DECODE] = Rade.GetRadaeEooDecodePulse(i);
        }

        // the encoder's mic level and clip, while transmitting
        private void radeMeterReadingsTX()
        {
            updateMetersReading(Reading.RADAE_TX_MIC_LEVEL_DB, Rade.GetRadaeTxMicLevelDb(), 0);
            updateMetersReading(Reading.RADAE_TX_MIC_CLIP, Rade.GetRadaeTxMicClip(), 0);
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
                        _rade_over_was_rade = Rade.OverWouldBeRade(this);
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
