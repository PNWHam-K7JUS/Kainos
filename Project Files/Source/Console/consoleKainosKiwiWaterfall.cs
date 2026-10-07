/*  consoleKainosKiwiWaterfall.cs

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
using System.Drawing;
using System.Windows.Forms;

namespace Thetis
{
    // The KiwiSDR panadapter and waterfall window (KainosKiwiWaterfall.cs) from the KIWI tab: its waterfall stream
    // runs while the window is open and a Kiwi is being listened to, joining that receiver's slot; tuning from it is
    // VFO A with Follow on (the Kiwi follows VFO A, the HL2 on or off), the Kiwi's own frequency with Follow off.
    public partial class Console
    {
        private KainosKiwiWaterfall _kiwiWfForm;
        private KiwiWaterfallClient _kiwiWf;
        private KiwiClient _kiwiWfFor;          // the sound stream the waterfall joined
        private int _kiwiWfZoom = 9;            // about 58 kHz wide
        private Timer _kiwiWfTimer;
        // the Kiwi on the main panadapter while the HL2 is off (Kainos layout; "Main view" in the KIWI tab)
        internal bool KiwiOnPanadapter = true;
        private KiwiSpectrumView _kiwiMain;

        internal void KiwiShowWaterfall()
        {
            if (_kiwiWfForm == null || _kiwiWfForm.IsDisposed)
            {
                _kiwiWfForm = new KainosKiwiWaterfall(this);
                if (_kiwiTimer == null)         // the KIWI tab's tick runs it otherwise
                {
                    _kiwiWfTimer = new Timer { Interval = 300 };
                    _kiwiWfTimer.Tick += (s, e) => kiwiWfTick();
                    _kiwiWfTimer.Start();
                }
            }
            if (!_kiwiWfForm.Visible) _kiwiWfForm.Show(this);
            _kiwiWfForm.Activate();
            kiwiWfTick();
        }

        internal void KiwiWaterfallClosed()
        {
            _kiwiWfTimer?.Stop();
            _kiwiWfTimer?.Dispose();
            _kiwiWfTimer = null;
            _kiwiWfForm = null;
            kiwiWfTick();               // the main view may still want the stream
        }

        private void kiwiWfStop()
        {
            _kiwiWf?.Dispose();
            _kiwiWf = null;
            _kiwiWfFor = null;
        }

        // a few times a second: start the stream when the Kiwi is ready (or changed), keep the view on the tuned
        // frequency, and the window's title
        private void kiwiWfTick()
        {
            bool window = _kiwiWfForm != null && !_kiwiWfForm.IsDisposed;
            bool main = kiwiMainWanted;
            kiwiMainShow(main);
            if (!window && !main) { if (_kiwiWf != null) kiwiWfStop(); return; }
            KiwiClient k = _kiwi;
            if (k == null || !k.Connected || k.WsBase == null) { if (_kiwiWf != null) kiwiWfStop(); }
            else if (_kiwiWfFor != k)
            {
                kiwiWfStop();
                _kiwiWfFor = k;
                _kiwiWf = new KiwiWaterfallClient();
                _kiwiWf.Line += (dbm, zoom, cf) =>
                {
                    KiwiWaterfallClient wf = _kiwiWf;
                    if (wf == null) return;
                    double span = wf.FullSpanKhz / Math.Pow(2, zoom);
                    KainosKiwiWaterfall form = _kiwiWfForm;
                    if (form != null && !form.IsDisposed) form.View.AddLine(dbm, cf, span);
                    KiwiSpectrumView mv = _kiwiMain;
                    if (mv != null && mv.Visible) mv.AddLine(dbm, cf, span);
                };
                _kiwiWf.View(_kiwiWfZoom, KiwiViewTunedKhz);
                _kiwiWf.Connect(k.WsBase, k.Prefix, k.Stamp, KainosMyCallsign);
            }
            else
            {
                double tuned = KiwiViewTunedKhz;
                if (Math.Abs(tuned - _kiwiWf.CentreKhz) > _kiwiWf.SpanKhz * 0.4) _kiwiWf.View(_kiwiWfZoom, tuned);
            }
            if (window)
            {
                string who = _kiwiOn != null ? (!string.IsNullOrEmpty(_kiwiOn.Loc) ? _kiwiOn.Loc : _kiwiOn.Name) : "No KiwiSDR";
                _kiwiWfForm.SetTitle(who + "   " + (KiwiViewTunedKhz / 1000).ToString("0.000000") + " MHz   " + (!_kiwiFollow ? "own frequency" : _kiwiFollowB ? "following VFO B" : "following VFO A"));
                _kiwiWfForm.View.Invalidate();
            }
            if (main) _kiwiMain.Invalidate();
        }

        // the main view: wanted in Kainos layout with the HL2 off while a Kiwi is being listened to
        private bool kiwiMainWanted
        {
            get { return KiwiOnPanadapter && _kainosLayout && !PowerOn && _kiwi != null && !collapsedDisplay && pnlDisplay.Visible; }
        }

        private void kiwiMainShow(bool show)
        {
            if (show)
            {
                if (_kiwiMain == null)
                {
                    _kiwiMain = new KiwiSpectrumView(this) { Name = "kainosKiwiMainView" };     // the console keeps its controls by name
                    Controls.Add(_kiwiMain);
                }
                // the panadapter's area in the console's coordinates (pnlDisplay sits inside panelDisplay)
                Rectangle area = RectangleToClient(pnlDisplay.RectangleToScreen(pnlDisplay.ClientRectangle));
                if (_kiwiMain.Bounds != area) _kiwiMain.Bounds = area;
                if (!_kiwiMain.Visible) _kiwiMain.Visible = true;
                _kiwiMain.BringToFront();
            }
            else if (_kiwiMain != null && _kiwiMain.Visible) _kiwiMain.Visible = false;
        }

        internal void KiwiWaterfallZoom(int step)
        {
            _kiwiWfZoom = Math.Max(0, Math.Min(14, _kiwiWfZoom + step));
            _kiwiWf?.View(_kiwiWfZoom, KiwiViewTunedKhz);
        }

        internal void KiwiWaterfallCentre() { _kiwiWf?.View(_kiwiWfZoom, KiwiViewTunedKhz); }

        // the Kiwi's dial frequency (kHz) and mode, as it's tuned now
        private string kiwiViewMode { get { return _kiwiFollow ? kiwiFollowMode : KiwiOwnMode; } }
        internal double KiwiViewTunedKhz { get { return _kiwiFollow ? kiwiFollowKhz(kiwiViewMode) : KiwiOwnKhz; } }

        // the passband around the dial, in Hz (as KiwiClient asks for it)
        internal void KiwiViewPassband(out int lo, out int hi)
        {
            switch (kiwiViewMode)
            {
                case "lsb": lo = -2700; hi = -300; break;
                case "cw": lo = Math.Max(50, cw_pitch - 250); hi = cw_pitch + 250; break;
                case "am": lo = -4900; hi = 4900; break;
                case "nbfm": lo = -6000; hi = 6000; break;
                default: lo = 300; hi = 2700; break;
            }
        }

        internal string KiwiViewWaiting
        {
            get
            {
                if (_kiwi == null) return "Choose a KiwiSDR in the KIWI tab to listen to";
                if (!_kiwi.Connected) return "Connecting to the KiwiSDR...";
                return "Waiting for the waterfall...";
            }
        }

        // tune from the view. fromDial: khz is the Kiwi's dial (the wheel); otherwise where the pointer is (a click:
        // in CW that's the signal, so the dial goes a CW pitch below it)
        internal void KiwiViewTuneTo(double khz, bool fromDial = false)
        {
            string mode = kiwiViewMode;
            double pitch = mode == "cw" ? cw_pitch / 1000.0 : 0;
            if (_kiwiFollow)
            {
                double vfoKhz = fromDial ? khz + pitch : khz;       // Thetis's VFO in CW is the signal itself
                if (_kiwiFollowB) VFOBFreq = Math.Max(0.01, vfoKhz / 1000);
                else VFOAFreq = Math.Max(0.01, vfoKhz / 1000);
            }
            else KiwiTuneOwn(fromDial ? khz : khz - pitch, null);
            kiwiWfTick();
        }
    }
}
