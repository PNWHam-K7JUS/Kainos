/*  consoleKainosSwr.cs

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
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Thetis
{
    // The SWR sweep (KainosSwrSweep.cs is the window): Thetis's own TUN carrier, held at no more than 1 W, stepped
    // across a band while the HL2's forward and reflected power are read, so everything that protects the radio
    // (PA, filters, antenna selection, TX inhibit) stays in charge. Everything it changes (frequency, tune power and
    // its source, pulsed tune) is put back afterwards.
    public partial class Console
    {
        internal const float KainosSweepMaxWatts = 1.0f;
        private Button _kainosSwrButton;
        private KainosSwrSweep _kainosSwrForm;
        internal bool KainosSweeping { get; private set; }

        // the dock's SWR button: a button of Kainos's own (the dock presses Thetis buttons)
        private Button kainosSwrButton
        {
            get
            {
                if (_kainosSwrButton == null)
                {
                    _kainosSwrButton = new Button { Text = "SWR", Name = "btnKainosSwr" };
                    _kainosSwrButton.Click += (s, e) => KainosShowSwrSweep();
                }
                return _kainosSwrButton;
            }
        }

        internal void KainosShowSwrSweep()
        {
            if (_kainosSwrForm == null || _kainosSwrForm.IsDisposed) _kainosSwrForm = new KainosSwrSweep(this);
            if (!_kainosSwrForm.Visible) _kainosSwrForm.Show(this);
            _kainosSwrForm.Activate();
        }

        internal string KainosCountry
        {
            get
            {
                if (IsSetupFormNull) return "";
                try { return SetupForm.KainosWizardRead().Country; } catch { return ""; }
            }
        }

        internal class SweepPoint { public double MHz; public float Swr, Fwd, Rev; }

        // Runs the sweep; point is called for each step, status with what's happening. Returns null when it finished,
        // or why it stopped.
        internal async Task<string> KainosRunSweep(double startMHz, double stopMHz, int steps, Func<bool> cancelled,
                                                    Action<SweepPoint> point, Action<string> status)
        {
            if (!PowerOn) return "Turn the radio on (POWER) first.";
            if (MOX || TUN) return "Stop transmitting first.";
            if (chkVFOSplit.Checked || !KainosIsTxVfo(1)) return "Turn split off, with VFO A as the transmit VFO: the sweep transmits on VFO A.";

            double oldFreq = VFOAFreq;
            DrivePowerSource oldSource = TuneDrivePowerOrigin;
            int oldTunePower = TunePower;
            bool oldPulse = TunePulseEnabled;
            string why = null;
            KainosSweeping = true;
            try
            {
                TunePulseEnabled = false;                           // a steady carrier, for steady readings
                TuneDrivePowerOrigin = DrivePowerSource.FIXED;
                int power = 10;
                TunePower = power;
                // start on the first frequency inside the band
                double first = startMHz;
                for (int i = 0; i <= steps && !kainosSweepInBand(first); i++) first = startMHz + (stopMHz - startMHz) * i / steps;
                if (!kainosSweepInBand(first)) return "That range is outside the band for your region, so nothing was sent.";
                VFOAFreq = first;
                await Task.Delay(150);
                TUN = true;
                await Task.Delay(400);
                if (!TUN) return "The radio wouldn't transmit here (out of band, or TX inhibited).";

                // find the tune power that gives about 0.5 to 1 W
                status("Setting the sweep power...");
                for (int tries = 0; tries < 40; tries++)
                {
                    if (cancelled() || !TUN) { why = "Stopped."; return why; }
                    float fwd = (await kainosSweepRead(150)).Key;
                    if (fwd > KainosSweepMaxWatts) { if (power <= 0) break; power = Math.Max(0, power - 5); }
                    else if (fwd < 0.5f && power < 100) power = Math.Min(100, power + 5);
                    else break;
                    TunePower = power;
                    await Task.Delay(100);
                }
                float check = (await kainosSweepRead(150)).Key;
                if (check < 0.05f) { why = "No output was measured. Is the HL2's built-in power amplifier on (the setup wizard's Hardware page), and is transmitting allowed here?"; return why; }
                if (check > KainosSweepMaxWatts * 1.2f) { why = "The output couldn't be brought down to 1 W, so the sweep was stopped."; return why; }

                int high = 0, dead = 0;
                for (int i = 0; i <= steps; i++)
                {
                    if (cancelled()) { why = "Stopped."; break; }
                    if (!TUN || !PowerOn) { why = "Transmit stopped (TUN off, or the radio's protection)."; break; }
                    double f = startMHz + (stopMHz - startMHz) * i / steps;
                    // never step outside the band: Thetis would stop with an error box. The carrier sits a CW pitch
                    // from the dial, so check a margin either side.
                    if (!kainosSweepInBand(f)) continue;
                    VFOAFreq = f;
                    status(string.Format("Sweeping  {0:0.000} MHz", f));
                    await Task.Delay(110);                          // filters, relays and the readings settle
                    KeyValuePair<float, float> r = await kainosSweepRead(60);
                    float fwd = r.Key, rev = r.Value;
                    if (fwd > KainosSweepMaxWatts * 1.2f) { why = "The output rose above 1 W, so the sweep was stopped."; break; }
                    if (fwd < 0.05f) { if (++dead >= 3) { why = "The output dropped away (out of band, or TX inhibited)."; break; } continue; }
                    dead = 0;
                    float rho = (float)Math.Sqrt(Math.Max(0, Math.Min(0.999f, rev / fwd)));
                    float swr = Math.Min(99f, (1 + rho) / (1 - rho));
                    point(new SweepPoint { MHz = f, Swr = swr, Fwd = fwd, Rev = rev });
                    // our own protection (Thetis ignores SWR at low tune power): a bad match stops it
                    if (swr > 5f) { if (++high >= 3) { why = "SWR above 5:1, so the sweep was stopped. Check the antenna."; break; } }
                    else high = 0;
                }
                return why;
            }
            finally
            {
                TUN = false;
                await Task.Delay(100);
                TunePower = oldTunePower;
                TuneDrivePowerOrigin = oldSource;
                TunePulseEnabled = oldPulse;
                VFOAFreq = oldFreq;
                KainosSweeping = false;
            }
        }

        private bool kainosSweepInBand(double mhz)
        {
            try
            {
                DSPMode m = radio.GetDSPTX(0).CurrentDSPMode;
                return CheckValidTXFreq(current_region, mhz - 0.0015, m, true) && CheckValidTXFreq(current_region, mhz + 0.0015, m, true);
            }
            catch { return true; }
        }

        // the forward and reflected power, averaged over a few readings (Thetis updates them every millisecond while
        // transmitting)
        private async Task<KeyValuePair<float, float>> kainosSweepRead(int ms)
        {
            float f = 0, r = 0;
            int n = 0;
            for (int t = 0; t < ms; t += 10)
            {
                f += alex_fwd; r += alex_rev; n++;
                await Task.Delay(10);
            }
            return new KeyValuePair<float, float>(n > 0 ? f / n : 0, n > 0 ? r / n : 0);
        }
    }
}
