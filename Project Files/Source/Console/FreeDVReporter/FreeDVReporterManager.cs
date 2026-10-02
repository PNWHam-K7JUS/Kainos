/*  FreeDVReporterManager.cs
 *
 *  Copyright (C) 2026  Christos Nikolaou (SV1EIA) <sv1eia@gmail.com>
 *  Kainos version (RX1, connection rules and settings) Copyright (C) 2026  Justin Cron K7JUS
 *
 *  This program is free software; you can redistribute it and/or
 *  modify it under the terms of the GNU General Public License
 *  as published by the Free Software Foundation; either version 2
 *  of the License, or (at your option) any later version.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with this program; if not, write to the Free Software
 *  Foundation, Inc., 51 Franklin Street, Fifth Floor,
 *  Boston, MA  02110-1301  USA
 */

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Thetis.FreeDVReporter
{
    // Connects Kainos to the FreeDV Reporter at qso.freedv.org (from Thetis-RADE by SV1EIA, reduced to RX1).
    //
    // The connection is open while the Reporter window is open, and also while RADE is on with
    // "Report my station" ticked. Kainos publishes its own station (callsign, grid square, frequency,
    // transmitting, the callsigns it decodes and their SNR) only while RADE is on and "Report my station"
    // is ticked; otherwise it connects as a viewer and just shows the station list.
    public static class FreeDVReporterManager
    {
        private static FreeDVReporterClient _client;
        private static FreeDVReporterForm _form;
        private static Console _console;
        private static bool _windowWanted;

        // settings, from Setup > DSP > FreeDV (RADE)
        private static string _callsign = "", _grid = "", _message = "";
        private static bool _reporting;

        private static Console.MoxChanged _moxHandler;
        private static Console.VFOAFrequencyChanged _vfoaHandler;
        private static Console.VFOBFrequencyChanged _vfobHandler;
        private static Console.TuneChanged _tuneHandler;
        private static Console.TwoToneChanged _twoToneHandler;
        private static System.Windows.Forms.Timer _pollTimer;

        private static bool _lastReportedTransmitting;
        private static int _lastSync;
        private static int _lastCallsignSeq;
        private static string _lastDecodedCall = "";

        public static bool IsConnected { get { return _client != null; } }
        public static FreeDVReporterClient Client { get { return _client; } }

        // the reporter window's "Track RX1" / "Track RX2" follow these
        public static ulong CurrentFrequencyHz { get; private set; }
        public static ulong CurrentFrequencyRx2Hz { get; private set; }

        private static readonly string CLIENT_NAME = buildClientName();

        private static string buildClientName()
        {
            try
            {
                Version v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                return "Kainos " + v.Major + "." + v.Minor + "." + v.Build + "." + v.Revision;
            }
            catch { return "Kainos"; }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern void OutputDebugStringW([MarshalAs(UnmanagedType.LPWStr)] string lpOutputString);

        internal static void LogReporter(string s)
        {
            try { OutputDebugStringW("[FreeDVReporter] " + s + "\n"); } catch { }
        }

        private static string modeTag()
        {
            try { return Rade.GetRadaeProtocolV2(0) == 1 ? "RADEV2" : "RADEV1"; }
            catch { return "RADEV1"; }
        }

        // publish our own station only while RADE is on and the user asked for it
        private static bool publishing(Console c)
        {
            return _reporting && c != null && c.RadeEnabled && _callsign.Length > 0 && _grid.Length > 0;
        }

        #region Called by Kainos

        private static System.Windows.Forms.Timer _settle;

        // Setup pushes the settings here at startup and whenever they change. While connected, changes are
        // applied once typing has stopped for a second, so a callsign or grid being typed doesn't reconnect
        // on every keystroke.
        public static void Configure(Console console, string callsign, string grid, string message, bool reporting)
        {
            _callsign = (callsign ?? "").Trim().ToUpperInvariant();
            _grid = (grid ?? "").Trim().ToUpperInvariant();
            _message = message ?? "";
            bool reportingChanged = reporting != _reporting;
            _reporting = reporting;
            if (_client == null || reportingChanged) { Update(console); return; }
            if (_settle == null)
            {
                _settle = new System.Windows.Forms.Timer { Interval = 1000 };
                _settle.Tick += (s, e) => { _settle.Stop(); Update(_console); };
            }
            _settle.Stop();
            _settle.Start();
        }

        // the Reporter button in the FreeDV window
        public static void ShowWindow(Console console)
        {
            _windowWanted = true;
            Update(console);
            if (_form == null || _form.IsDisposed)
            {
                _form = new FreeDVReporterForm(_client, console);
                _form.OnUserClose = () => { _windowWanted = false; Update(_console); };
            }
            if (!_form.Visible) _form.Show();
            if (_form.WindowState == System.Windows.Forms.FormWindowState.Minimized)
                _form.WindowState = System.Windows.Forms.FormWindowState.Normal;
            _form.BringToFront();
            _form.Activate();
        }

        // Re-evaluates the connection: open or close it, and switch between reporting and viewing.
        // Called when RADE is switched on or off, the settings change, or the window closes.
        public static void Update(Console console)
        {
            if (console == null) return;
            bool want = _windowWanted || (_reporting && console.RadeEnabled);
            if (!want)
            {
                if (_client != null) disconnect();
                return;
            }
            if (_client == null)
            {
                connect(console);
                return;
            }

            string role = publishing(console) ? "report" : "view";
            bool identity = _client.Callsign != _callsign || _client.GridSquare != _grid;
            if (role != _client.Role || identity)
            {
                // the server fixes the role and identity for a session, so reconnect
                FreeDVReporterClient c = _client;
                c.Role = role;
                c.Callsign = _callsign;
                c.GridSquare = _grid;
                primeState(console);    // cached by the client and sent again once it reconnects as a reporter
                // Stop waits for the socket to close (up to 3 s), so do it off the UI thread
                System.Threading.Tasks.Task.Run(() =>
                {
                    try { c.Stop(); if (_client == c) c.Start(); } catch { }
                });
            }
            else
            {
                _client.EmitMessageUpdate(_message);
            }
        }

        // double-click a station in the reporter window: tune VFO A there (and the RADE mode, if RADE is on)
        public static void TuneToFrequency(ulong hz)
        {
            if (_console == null || hz == 0) return;
            try
            {
                double mhz = hz / 1e6;
                _console.VFOAFreq = mhz;
                if (_console.RadeEnabled)
                {
                    DSPMode want = (mhz >= 5.0 && mhz < 5.5) || mhz >= 10.0 ? DSPMode.DIGU : DSPMode.DIGL;
                    if (_console.RX1DSPMode != want) _console.RX1DSPMode = want;
                }
            }
            catch (Exception ex) { LogReporter("TuneToFrequency failed: " + ex.Message); }
        }

        // V1 / V2 changed: report the new mode now
        public static void NotifyProtocolChanged()
        {
            if (_client == null || _console == null) return;
            try
            {
                _lastReportedTransmitting = computeRealTx(_console);
                _client.EmitTxReport(modeTag(), _lastReportedTransmitting);
            }
            catch { }
        }

        #endregion

        private static void connect(Console console)
        {
            _console = console;
            _client = new FreeDVReporterClient
            {
                Callsign = _callsign,
                GridSquare = _grid,
                ClientName = CLIENT_NAME,
                RxOnly = false,
                Role = publishing(console) ? "report" : "view",
            };
            _client.Start();
            primeState(console);

            _moxHandler = (rx, oldMox, newMox) => recomputeTx();
            _tuneHandler = (rx, oldTune, newTune) => recomputeTx();
            _twoToneHandler = (rx, oldState, newState) => recomputeTx();
            _vfoaHandler = (oldBand, newBand, oldMode, newMode, oldFilter, newFilter, oldFreq, newFreq,
                            oldCentreF, newCentreF, oldCTUN, newCTUN, oldZoom, newZoom, offset, rx) =>
            {
                CurrentFrequencyHz = (ulong)Math.Round(newFreq * 1e6);
                try { _client?.EmitFreqChange(CurrentFrequencyHz); } catch { }
            };
            _vfobHandler = (oldBand, newBand, oldMode, newMode, oldFilter, newFilter, oldFreq, newFreq,
                            oldCentreF, newCentreF, oldCTUN, newCTUN, oldZoom, newZoom, offset, rx) =>
            {
                CurrentFrequencyRx2Hz = (ulong)Math.Round(newFreq * 1e6);
            };
            console.MoxChangeHandlers += _moxHandler;
            console.TuneChangedHandlers += _tuneHandler;
            console.TwoToneChangedHandlers += _twoToneHandler;
            console.VFOAFrequencyChangeHandlers += _vfoaHandler;
            console.VFOBFrequencyChangeHandlers += _vfobHandler;

            _pollTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _pollTimer.Tick += (s, e) => poll();
            _pollTimer.Start();
        }

        // The client caches frequency, transmit state and message even as a viewer, and sends them again
        // whenever it (re)connects as a reporter.
        private static void primeState(Console console)
        {
            try
            {
                CurrentFrequencyHz = (ulong)Math.Round(console.VFOAFreq * 1e6);
                CurrentFrequencyRx2Hz = (ulong)Math.Round(console.VFOBFreq * 1e6);
                _client.EmitFreqChange(CurrentFrequencyHz);
                _lastReportedTransmitting = computeRealTx(console);
                _client.EmitTxReport(modeTag(), _lastReportedTransmitting);
                _client.EmitMessageUpdate(_message);
            }
            catch { }
        }

        private static void disconnect()
        {
            try
            {
                if (_console != null)
                {
                    _console.MoxChangeHandlers -= _moxHandler;
                    _console.TuneChangedHandlers -= _tuneHandler;
                    _console.TwoToneChangedHandlers -= _twoToneHandler;
                    _console.VFOAFrequencyChangeHandlers -= _vfoaHandler;
                    _console.VFOBFrequencyChangeHandlers -= _vfobHandler;
                }
            }
            catch { }
            _moxHandler = null; _tuneHandler = null; _twoToneHandler = null; _vfoaHandler = null; _vfobHandler = null;
            try { _pollTimer?.Stop(); _pollTimer?.Dispose(); } catch { }
            _pollTimer = null;
            FreeDVReporterClient c = _client;
            _client = null;                     // first, so a reconnect in progress doesn't restart it
            try { c?.Stop(); c?.Dispose(); } catch { }
            try
            {
                if (_form != null && !_form.IsDisposed)
                {
                    _form.OnUserClose = null;
                    _form.Close();
                    _form.Dispose();
                }
            }
            catch { }
            _form = null;
        }

        // A real RADE over: MOX on, not tuning or two-tone, and the encoder is in use
        private static bool computeRealTx(Console c)
        {
            try { return c.MOX && Rade.OverWouldBeRade(c); }
            catch { return false; }
        }

        private static void recomputeTx()
        {
            try
            {
                bool now = computeRealTx(_console);
                if (now == _lastReportedTransmitting) return;
                _lastReportedTransmitting = now;
                _client?.EmitTxReport(modeTag(), now);
            }
            catch { }
        }

        // Once a second: while in sync, report what we hear; and report each callsign the decoder validates
        // (the end-of-over frame arrives just before sync drops, so that is reported even out of sync).
        // The last callsign is cleared when a new sync starts, so it stays shown between overs.
        private static void poll()
        {
            if (_client == null || _console == null) return;
            try
            {
                if (!_console.RadeEnabled) { _lastSync = 0; return; }
                int sync = Rade.GetRadaeSync(0);
                int snr = Rade.GetRadaeSnrDb(0);
                int seq = Rade.GetRadaeRemoteCallsignSeq(0);
                bool fresh = seq != _lastCallsignSeq;
                if (fresh)
                {
                    StringBuilder sb = new StringBuilder(16);
                    Rade.GetRadaeRemoteCallsign(0, sb, sb.Capacity);
                    _lastDecodedCall = sb.ToString().Trim().ToUpperInvariant();
                    _lastCallsignSeq = seq;
                }
                if (sync != 0 && _lastSync == 0) _lastDecodedCall = "";
                _lastSync = sync;
                if (sync != 0 || fresh)
                {
                    _client.EmitRxReport(_lastDecodedCall, modeTag(), snr);
                    if (publishing(_console)) _client.LocalMirrorRxReport(_lastDecodedCall, modeTag(), snr);
                }
            }
            catch { }
        }
    }
}
