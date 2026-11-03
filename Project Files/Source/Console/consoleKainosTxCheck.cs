/*  consoleKainosTxCheck.cs

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
using System.Threading.Tasks;

namespace Thetis
{
    // The first-transmit check (KainosTxCheck.cs is the window): a few seconds of Thetis's own TUN carrier at the
    // user's tune power (power out and SWR), then a few seconds of MOX at low drive while they talk (the microphone's
    // level and the ALC), each with a plain verdict. Thetis's keying and protection stay in charge; the drive is put back
    // afterwards.
    public partial class Console
    {
        private KainosTxCheck _kainosTxCheckForm;
        internal const int KainosMicTestDrive = 10;      // drive (%) for the talk test: low, the dummy load still sees a signal

        internal void KainosShowTxCheck()
        {
            if (_kainosTxCheckForm == null || _kainosTxCheckForm.IsDisposed) _kainosTxCheckForm = new KainosTxCheck(this);
            if (!_kainosTxCheckForm.Visible) _kainosTxCheckForm.Show(this);
            _kainosTxCheckForm.Activate();
        }

        internal class TxCheckResult { public bool Ok; public string Verdict; }

        // why a test can't start here, or null
        private string kainosTxCheckBlocked()
        {
            if (!PowerOn) return "Turn the radio on (POWER) first.";
            if (MOX || TUN) return "Stop transmitting first.";
            if (chkVFOSplit.Checked || !KainosIsTxVfo(1)) return "Turn split off, with VFO A as the transmit VFO.";
            if (!kainosSweepInBand(VFOAFreq)) return "VFO A is outside the band for your region. Tune into a band first.";
            return null;
        }

        internal static bool KainosVoiceMode(DSPMode m)
        {
            return m == DSPMode.LSB || m == DSPMode.USB || m == DSPMode.DSB || m == DSPMode.AM || m == DSPMode.SAM || m == DSPMode.FM;
        }

        // TUN for a few seconds at the user's own tune power: the highest forward power and its SWR
        internal async Task<TxCheckResult> KainosTxCarrierTest(int seconds, Func<bool> cancelled, Action<string> live)
        {
            string why = kainosTxCheckBlocked();
            if (why != null) return new TxCheckResult { Verdict = why };
            float best = 0, swrAtBest = 1;
            try
            {
                TUN = true;
                await Task.Delay(300);
                if (!TUN) return new TxCheckResult { Verdict = "The radio wouldn't transmit here (out of band, or TX inhibited)." };
                DateTime end = DateTime.UtcNow.AddSeconds(seconds);
                while (DateTime.UtcNow < end)
                {
                    if (cancelled()) return new TxCheckResult { Verdict = "Stopped." };
                    if (!TUN || !PowerOn) return new TxCheckResult { Verdict = "Transmit stopped (TUN off, or the radio's protection)." };
                    float fwd = calfwdpower, swr = Math.Max(1f, alex_swr);
                    if (fwd > best) { best = fwd; swrAtBest = swr; }
                    live(string.Format("Fwd {0:0.0} W   SWR {1:0.0}", fwd, swr));
                    // our own protection: a bad match stops it (as the SWR sweep does)
                    if (fwd > 0.2f && swr > 5f)
                        return new TxCheckResult { Verdict = string.Format("Stopped: SWR {0:0.0}:1. The antenna or the cable isn't matched; check the dummy load or antenna is connected to the transmit port.", swr) };
                    await Task.Delay(100);
                }
            }
            finally { TUN = false; }

            if (best < 0.05f)
                return new TxCheckResult { Verdict = "No power out. Check: the tune power (TUN slider) isn't at zero; " +
                    (HardwareSpecific.Model == HPSDRModel.HERMESLITE ? "the HL2's built-in amplifier is on (the setup wizard's Hardware page); " : "the PA is enabled (Setup > PA); ") +
                    "the right antenna port is selected for transmit; and an external amplifier (xPA) isn't bypassing the output." };
            string power = string.Format("{0:0.0} W at tune power {1} %", best, TunePower);
            if (swrAtBest >= 2f)
                return new TxCheckResult { Verdict = "Power out: " + power + ", but the SWR is " + swrAtBest.ToString("0.0") + ":1. Check the load or antenna before transmitting at full power." };
            return new TxCheckResult { Ok = true, Verdict = "Your transmitter works: " + power + ", SWR " + swrAtBest.ToString("0.0") + ":1." };
        }

        // MOX for a few seconds at low drive while the user talks: the microphone's peak and the ALC's most
        internal async Task<TxCheckResult> KainosTxMicTest(int seconds, Func<bool> cancelled, Action<string> live)
        {
            string why = kainosTxCheckBlocked();
            if (why != null) return new TxCheckResult { Verdict = why };
            DSPMode mode = radio.GetDSPTX(0).CurrentDSPMode;
            if (!KainosVoiceMode(mode)) return new TxCheckResult { Verdict = "Switch to a voice mode (LSB or USB) first: in " + mode + " the microphone isn't used." };

            int oldDrive = PWR;
            float micPeak = -200, alcMost = 0, fwdMost = 0;
            try
            {
                PWR = Math.Min(oldDrive, KainosMicTestDrive);
                MOX = true;
                await Task.Delay(300);
                if (!MOX) return new TxCheckResult { Verdict = "The radio wouldn't transmit here (out of band, or TX inhibited)." };
                DateTime end = DateTime.UtcNow.AddSeconds(seconds);
                while (DateTime.UtcNow < end)
                {
                    if (cancelled()) return new TxCheckResult { Verdict = "Stopped." };
                    if (!MOX || !PowerOn) return new TxCheckResult { Verdict = "Transmit stopped (MOX off, or the radio's protection)." };
                    float mic = -200, alc = 0;
                    try
                    {
                        mic = (float)Math.Max(-200, -WDSP.CalculateTXMeter(1, WDSP.MeterType.MIC));
                        alc = (float)Math.Max(0, -WDSP.CalculateTXMeter(1, WDSP.MeterType.ALC_G));
                    }
                    catch { }
                    micPeak = Math.Max(micPeak, mic);
                    alcMost = Math.Max(alcMost, alc);
                    fwdMost = Math.Max(fwdMost, calfwdpower);
                    live(string.Format("Mic {0:0} dB   ALC {1:0.0} dB   Fwd {2:0.0} W", mic, alc, calfwdpower));
                    await Task.Delay(100);
                }
            }
            finally
            {
                MOX = false;
                await Task.Delay(100);
                PWR = oldDrive;
            }

            if (micPeak < -40)
                return new TxCheckResult { Verdict = string.Format("Kainos hardly hears your microphone (peak {0:0} dB). Check the microphone is the one chosen " +
                    "(Setup > Audio; VAC 1 for a PC microphone), that Windows isn't muting it, and raise MIC gain.", micPeak) };
            if (alcMost > 12)
                return new TxCheckResult { Verdict = string.Format("Too loud: the ALC pulled {0:0} dB, which distorts. Lower MIC gain until the ALC peaks around 3 - 6 dB " +
                    "on your loudest words.", alcMost) };
            if (micPeak < -20 && alcMost < 1)
                return new TxCheckResult { Ok = true, Verdict = string.Format("Your microphone works but is quiet (peak {0:0} dB, ALC {1:0.0} dB). Raise MIC gain a little " +
                    "for a fuller signal.", micPeak, alcMost) };
            return new TxCheckResult { Ok = true, Verdict = string.Format("Your microphone sounds right: peak {0:0} dB, ALC {1:0.0} dB{2}.", micPeak, alcMost,
                fwdMost < 0.05f ? " (no power out was measured at the low test drive: run the carrier test)" : "") };
        }
    }
}
