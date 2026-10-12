/*  consoleKainosCiv.cs

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
using System.Threading;
using System.Windows.Forms;

namespace Thetis
{
    // The amplifier band-switching link (KainosCiv.cs): the console's side. The link's thread answers the amp from a
    // snapshot: the transmit frequency, worked out on the UI thread ten times a second, and the MOX state, set the
    // moment it changes (MoxChangeHandlers, which TUN goes through too).
    public partial class Console
    {
        private KainosCivLink _kainosCiv;
        private System.Windows.Forms.Timer _kainosCivTimer;
        private long _kainosCivHz;
        private volatile bool _kainosCivTx;

        internal KainosCivLink KainosCiv
        {
            get
            {
                if (_kainosCiv == null)
                {
                    _kainosCiv = new KainosCivLink();
                    _kainosCiv.Civ.FrequencyHz = () => Interlocked.Read(ref _kainosCivHz);
                    _kainosCiv.Civ.Transmitting = () => _kainosCivTx;
                    MoxChangeHandlers += (rx, oldMox, newMox) => _kainosCivTx = newMox;
                    _kainosCivTimer = new System.Windows.Forms.Timer { Interval = 100 };
                    _kainosCivTimer.Tick += (s, e) => kainosCivSnapshot();
                    FormClosing += (s, e) => { _kainosCivTimer.Stop(); _kainosCiv.Stop(); };
                }
                return _kainosCiv;
            }
        }

        // the frequency the HL2 transmits on: VFO B in split (or with TX on VFO B), and with a transverter the HL2's
        // own frequency (the amp is on the HL2's output, not the transverter's)
        private void kainosCivSnapshot()
        {
            try
            {
                double mhz = chkVFOSplit.Checked || chkVFOBTX.Checked ? VFOBFreq : VFOAFreq;
                if (XVTRForm != null) mhz = XVTRForm.TranslateFreq(mhz);
                Interlocked.Exchange(ref _kainosCivHz, (long)Math.Round(mhz * 1e6));
                _kainosCivTx = MOX;
            }
            catch { }
        }

        // Setup > Serial / Network / Midi CAT > Amplifier (setupKainosCiv.cs)
        internal void KainosCivApply(bool on, string port, byte address, bool log)
        {
            KainosCivLink link = KainosCiv;
            bool restart = on != link.Running || port != link.PortName || address != link.Civ.Address;
            link.PortName = port;
            link.Civ.Address = address;
            link.Log = log;
            kainosCivSnapshot();
            if (on) _kainosCivTimer.Start(); else _kainosCivTimer.Stop();
            if (!restart) return;
            if (on) link.Start(); else link.Stop();
        }
    }
}
