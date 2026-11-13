/*  consoleKainosBandRange.cs

A band the radio can't tune is refused, with the reason, instead of half done. Thetis would set the panadapter to
the band (with click-tune on, its centre goes first) and then pin VFO A at the radio's top frequency: on a Hermes
Lite 2, picking 6 m showed 50 MHz on the panadapter with VFO A stuck at 38.400 MHz in CW, and nothing to hear.

The radio's range is Thetis's own: Setup > General > Hardware Config, Max Freq (38.4 MHz on a Hermes Lite 2, whose
converter samples at 76.8 MHz), and the lowest frequency it tunes. A transverter (Setup > XVTRs) covering the band
makes it reachable.

This file is part of Kainos. Copyright (C) 2026 Justin Cron K7JUS. GPLv3 or later.
*/

using System;
using System.Globalization;
using System.Windows.Forms;

namespace Thetis
{
    partial class Console
    {
        // true when VFO A can go to this frequency (MHz): within the radio's range, or on a transverter's band
        internal bool KainosFreqReachable(double mhz)
        {
            if (XVTRForm != null && XVTRForm.XVTRFreq(mhz) >= 0) return true;
            return mhz >= min_freq && mhz <= max_freq;
        }

        // called instead of the band change: the band button lit again for the band we're still on, and why
        private void kainosBandOutOfRange(double mhz)
        {
            BeginInvoke((Action)(() =>
            {
                try { SetRX1BandButton(rx1_band); } catch { }
                string f = mhz.ToString("0.###", CultureInfo.InvariantCulture);
                string top = max_freq.ToString("0.0##", CultureInfo.InvariantCulture);
                string msg = HardwareSpecific.Model == HPSDRModel.HERMESLITE
                    ? "That band (" + f + " MHz) is above the Hermes Lite 2's range: it receives and transmits up to " + top + " MHz.\r\n\r\n" +
                      "6 m and above need a transverter (Setup > XVTRs)."
                    : mhz > max_freq
                        ? "That band (" + f + " MHz) is above this radio's range, which Kainos has as up to " + top + " MHz (Setup > General > Hardware Config, Max Freq).\r\n\r\n" +
                          "If your radio does cover it, raise Max Freq there; if not, it needs a transverter (Setup > XVTRs)."
                        : "That band (" + f + " MHz) is below the lowest frequency this radio tunes.";
                MessageBox.Show(this, msg, "Band", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }));
        }
    }
}
