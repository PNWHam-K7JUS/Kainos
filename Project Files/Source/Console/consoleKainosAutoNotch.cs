/*  consoleKainosAutoNotch.cs

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
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;

namespace Thetis
{
    // WDSP's auto-notch tap and auto-notch list (wdsp/kntap.c, wdsp/nbp.c)
    internal static class KNotch
    {
        [DllImport("wdsp.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void KainosNotchTapRun(int channel, int on);
        [DllImport("wdsp.dll", CallingConvention = CallingConvention.Cdecl)] public static extern int KainosNotchTapRead(int channel, float[] dst, int n);
        [DllImport("wdsp.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void KainosNotchTapInfo(int channel, double[] info);
        [DllImport("wdsp.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void KainosNBPSetAutoNotches(int channel, int n, double[] fcenter, double[] fwidth);
    }

    // AUTO NOTCH (the flags' DSP tab): finds steady carriers in the receiver's passband and notches them, several at
    // once, the way SmartSDR's ANFT does. Thetis's ANF follows one or two tones and works on the audio; this uses the
    // notch filter the manual notches use (MNF), so a notched carrier is gone before the AGC and stays notched while
    // you tune (the notches are at RF frequencies). The manual notches aren't touched and needn't be on.
    // Voice only (LSB, USB, AM, SAM, DSB): in CW and the digital modes the steady tones are the signal.
    public partial class Console
    {
        // saved with the options (a hidden Setup box): "rx1=1;rx2=0"
        public string KainosAutoNotchSettings = "";

        private readonly bool[] _autoNotchOn = new bool[2];
        private readonly KainosAutoNotcher[] _autoNotcher = new KainosAutoNotcher[2];
        private System.Windows.Forms.Timer _autoNotchTimer;

        internal bool KainosAutoNotchOn(int rx) { return _autoNotchOn[rx - 1]; }

        // how many carriers are notched now (0 while it's off or the mode isn't a voice mode)
        internal int KainosAutoNotchCount(int rx)
        {
            KainosAutoNotcher n = _autoNotcher[rx - 1];
            return n != null ? n.Count : 0;
        }

        internal void KainosToggleAutoNotch(int rx)
        {
            _autoNotchOn[rx - 1] = !_autoNotchOn[rx - 1];
            autoNotchSave();
            autoNotchTick();
        }

        internal void KainosAutoNotchLoad()
        {
            foreach (string kv in (KainosAutoNotchSettings ?? "").Split(';'))
            {
                string[] p = kv.Split('=');
                if (p.Length != 2) continue;
                if (p[0] == "rx1") _autoNotchOn[0] = p[1] == "1";
                else if (p[0] == "rx2") _autoNotchOn[1] = p[1] == "1";
            }
            if (_autoNotchTimer == null)
            {
                _autoNotchTimer = new System.Windows.Forms.Timer { Interval = 100 };
                _autoNotchTimer.Tick += (s, e) => autoNotchTick();
                _autoNotchTimer.Start();
            }
        }

        private void autoNotchSave()
        {
            KainosAutoNotchSettings = string.Format(CultureInfo.InvariantCulture, "rx1={0};rx2={1}", _autoNotchOn[0] ? 1 : 0, _autoNotchOn[1] ? 1 : 0);
            KainosSettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        private bool autoNotchModeOk(int i)
        {
            if (!PowerOn) return false;
            if (i == 1 && !RX2Enabled) return false;
            DSPMode m = i == 0 ? _rx1_dsp_mode : _rx2_dsp_mode;
            return m == DSPMode.LSB || m == DSPMode.USB || m == DSPMode.AM || m == DSPMode.SAM || m == DSPMode.DSB;
        }

        // on the UI thread, like Thetis's notch changes, so they never cross
        private void autoNotchTick()
        {
            for (int i = 0; i < 2; i++)
            {
                bool want = _autoNotchOn[i] && autoNotchModeOk(i);
                if (_autoNotcher[i] == null)
                {
                    if (!want) continue;
                    // RX1's sub-receiver gets RX1's notches too: they're at RF frequencies, so they only bite in its passband
                    _autoNotcher[i] = i == 0 ? new KainosAutoNotcher(WDSP.id(0, 0), WDSP.id(0, 1)) : new KainosAutoNotcher(WDSP.id(2, 0));
                }
                try { _autoNotcher[i].Step(want); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine("auto notch: " + ex.Message); }
            }
        }
    }

    // One receiver's carrier finder. Every tick: a 4096-point FFT (about 12 Hz bins at 48 kHz) of the I/Q just before
    // the notch filter, so a carrier stays visible after it has been notched. Narrow peaks well above the passband's
    // median are followed at their RF frequency; one that holds still for about 1.5 s is notched, and the notch is
    // lifted about 1 s after it goes. Speech harmonics move with the pitch and come and go with the syllables, so
    // they never stay long enough.
    internal sealed class KainosAutoNotcher
    {
        private const int N = 4096;
        private const int MaxNotches = 8;
        private const double NotchWidth = 50;        // Hz
        private const double AboveFloor = 15;        // dB above the passband's median
        private const double Narrow = 12;            // dB down 3 bins either side (a steady tone's window lobe is ±2 bins)
        private const double Match = 15;             // Hz: the same carrier from one frame to the next
        private const double Follow = 0.2;           // how fast a followed carrier's frequency moves to the new reading
        private const double Decay = 0.9;            // presence: 0.9 * last + 0.1 when seen
        private const double NotchAt = 0.8, LiftAt = 0.3;

        private readonly int _channel;
        private readonly int[] _also;
        private readonly float[] _buf = new float[2 * N];
        private readonly double[] _re = new double[N], _im = new double[N], _db = new double[N], _win = new double[N];
        private readonly double[] _info = new double[4];
        private readonly List<Track> _tracks = new List<Track>();
        private double[] _applied = new double[0];
        private bool _running;
        private int _lastW;
        private bool _haveW;

        private sealed class Track
        {
            public double Rf, Level, Presence, NotchRf;
            public bool Hit, Notched;
        }

        public KainosAutoNotcher(int channel, params int[] also)
        {
            _channel = channel;
            _also = also ?? new int[0];
            for (int k = 0; k < N; k++) _win[k] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * k / N);
        }

        public int Count { get { return _applied.Length; } }

        public void Step(bool want)
        {
            if (!want)
            {
                if (_running)
                {
                    KNotch.KainosNotchTapRun(_channel, 0);
                    _running = false;
                    _tracks.Clear();
                    apply(new double[0]);
                }
                return;
            }
            if (!_running)
            {
                KNotch.KainosNotchTapRun(_channel, 1);
                _running = true;
                _haveW = false;
            }

            KNotch.KainosNotchTapInfo(_channel, _info);
            double rate = _info[0], offset = _info[1], flow = _info[2], fhigh = _info[3];
            if (rate <= 0 || rate > 96000) return;

            int w = KNotch.KainosNotchTapRead(_channel, _buf, N);
            if (w < 0) return;
            if (!_haveW) { _haveW = true; _lastW = w; return; }      // the ring may still hold audio from before
            if (unchecked(w - _lastW) < N / 2) return;              // not enough new audio yet
            _lastW = w;

            // the spectrum, in dB. Q is negated: in WDSP's frequencies (the passband edges, the notches) a USB tone at
            // +1 kHz is at -1 kHz in a plain FFT of I + jQ, so without it the passband searched was the mirror image of
            // the one heard, and no carrier was ever found on the air
            for (int k = 0; k < N; k++) { _re[k] = _buf[2 * k] * _win[k]; _im[k] = -_buf[2 * k + 1] * _win[k]; }
            fft(_re, _im);
            for (int k = 0; k < N; k++) _db[k] = 10 * Math.Log10(_re[k] * _re[k] + _im[k] * _im[k] + 1e-30);

            // the bins inside the passband, away from its skirts and from 0 Hz (an AM carrier, or zero beat)
            double binHz = rate / N, lo = Math.Min(flow, fhigh) + 60, hi = Math.Max(flow, fhigh) - 60;
            List<int> bins = new List<int>();
            for (int k = 0; k < N; k++)
            {
                double f = (k < N / 2 ? k : k - N) * binHz;
                if (f >= lo && f <= hi && Math.Abs(f) >= 80) bins.Add(k);
            }
            List<KeyValuePair<double, double>> peaks = new List<KeyValuePair<double, double>>();     // RF Hz, dB
            if (bins.Count >= 16)
            {
                double[] sorted = bins.Select(k => _db[k]).OrderBy(v => v).ToArray();
                double floor = sorted[sorted.Length / 2];
                foreach (int k in bins)
                {
                    double a = _db[(k - 1 + N) % N], b = _db[k], c = _db[(k + 1) % N];
                    if (!(b > a && b >= c) || b < floor + AboveFloor) continue;
                    if (b - Math.Max(_db[(k - 3 + N) % N], _db[(k + 3) % N]) < Narrow) continue;
                    double den = a - 2 * b + c, d = den < 0 ? 0.5 * (a - c) / den : 0;      // parabolic: the peak between bins
                    double f = ((k < N / 2 ? k : k - N) + d) * binHz;
                    peaks.Add(new KeyValuePair<double, double>(offset + f, b));
                }
            }

            // follow them
            foreach (Track t in _tracks) t.Hit = false;
            foreach (var p in peaks.OrderByDescending(p => p.Value).Take(16))
            {
                Track best = null;
                foreach (Track t in _tracks)
                    if (!t.Hit && Math.Abs(t.Rf - p.Key) < Match && (best == null || Math.Abs(t.Rf - p.Key) < Math.Abs(best.Rf - p.Key))) best = t;
                if (best == null) { best = new Track { Rf = p.Key }; _tracks.Add(best); }
                best.Hit = true;
                best.Rf += Follow * (p.Key - best.Rf);
                best.Level = p.Value;
            }
            foreach (Track t in _tracks)
            {
                t.Presence = Decay * t.Presence + (t.Hit ? 1 - Decay : 0);
                if (!t.Notched && t.Presence > NotchAt) { t.Notched = true; t.NotchRf = t.Rf; }
                else if (t.Notched && t.Presence < LiftAt) t.Notched = false;
                if (t.Notched && Math.Abs(t.Rf - t.NotchRf) > 8) t.NotchRf = t.Rf;     // a drifting carrier
            }
            _tracks.RemoveAll(t => !t.Notched && t.Presence < 0.02);
            if (_tracks.Count > 64)
            {
                _tracks.Sort((x, y) => y.Presence.CompareTo(x.Presence));
                _tracks.RemoveRange(64, _tracks.Count - 64);
            }

            double[] want2 = _tracks.Where(t => t.Notched).OrderByDescending(t => t.Level).Take(MaxNotches)
                                    .Select(t => t.NotchRf).OrderBy(f => f).ToArray();
            bool same = want2.Length == _applied.Length;
            for (int i = 0; same && i < want2.Length; i++) same = Math.Abs(want2[i] - _applied[i]) < 0.5;
            if (!same) apply(want2);
        }

        private void apply(double[] centers)
        {
            double[] widths = centers.Select(c => NotchWidth).ToArray();
            KNotch.KainosNBPSetAutoNotches(_channel, centers.Length, centers, widths);
            foreach (int ch in _also) KNotch.KainosNBPSetAutoNotches(ch, centers.Length, centers, widths);
            _applied = centers;
        }

        // in-place radix-2 FFT (as KainosRtty.cs)
        private static void fft(double[] re, double[] im)
        {
            int n = re.Length;
            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1) j ^= bit;
                j ^= bit;
                if (i < j) { double t = re[i]; re[i] = re[j]; re[j] = t; t = im[i]; im[i] = im[j]; im[j] = t; }
            }
            for (int len = 2; len <= n; len <<= 1)
            {
                double ang = -2 * Math.PI / len, wr = Math.Cos(ang), wi = Math.Sin(ang);
                for (int i = 0; i < n; i += len)
                {
                    double cr = 1, ci = 0;
                    for (int k = 0; k < len / 2; k++)
                    {
                        int u = i + k, v = i + k + len / 2;
                        double tr = re[v] * cr - im[v] * ci, ti = re[v] * ci + im[v] * cr;
                        re[v] = re[u] - tr; im[v] = im[u] - ti;
                        re[u] += tr; im[u] += ti;
                        double ncr = cr * wr - ci * wi; ci = cr * wi + ci * wr; cr = ncr;
                    }
                }
            }
        }
    }
}
