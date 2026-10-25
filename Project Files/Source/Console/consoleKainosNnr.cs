/*  consoleKainosNnr.cs

Kainos NNR: WDSP 2.10's neural noise reduction (Warren Pratt, NR0V), moved into Kainos's WDSP (wdsp/nnr.c,
nnet.c, nnio.c and the two built-in models). It has its own button on each VFO flag's DSP tab, separate from
the NR button, so it can run on its own or together with NR, NR2, NR3 or NR4.

This program is free software; you can redistribute it and/or modify it under the terms of the GNU General
Public License as published by the Free Software Foundation; either version 2 of the License, or (at your
option) any later version.

*/

using System;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Thetis
{
    internal static class KNnr
    {
        [DllImport("wdsp.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void SetRXANNRRun(int channel, int run);
        [DllImport("wdsp.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void SetRXANNRPosition(int channel, int position);
        [DllImport("wdsp.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void SetRXANNRMaskFloor(int channel, double floor_db);
        [DllImport("wdsp.dll", CallingConvention = CallingConvention.Cdecl)] public static extern int SetRXANNRModel(int channel, int slot);
        [DllImport("wdsp.dll", CallingConvention = CallingConvention.Cdecl)] public static extern int GetRXANNRModel(int channel);
    }

    // The model is trained on speech: steady tones are treated as noise and taken down by about 28 dB, so by
    // default it only runs in the voice modes (LSB, USB, AM, SAM, DSB, FM) and stands by in CW and the digital modes.
    public partial class Console
    {
        // saved with the options (a hidden Setup box): "rx1=1;rx2=0"
        public string KainosNnrSettings = "";

        // set from Setup > DSP > NNR (setupKainosNnr.cs)
        internal int KainosNnrModel = 0;            // 0 standard, 1 large
        internal int KainosNnrDepth = 25;           // dB: the most it takes a bin down (the mask floor)
        internal bool KainosNnrVoiceOnly = true;

        private readonly bool[] _nnrOn = new bool[2];
        private System.Windows.Forms.Timer _nnrTimer;

        // what each WDSP channel was last given: RX1, RX1's sub-receiver, RX2
        private readonly int[] _nnrChannel = { -1, -1, -1 };
        private readonly int[] _nnrRun = { -1, -1, -1 }, _nnrPos = { -1, -1, -1 }, _nnrDepth = { -1, -1, -1 };

        internal bool KainosNnrOn(int rx) { return _nnrOn[rx - 1]; }

        // on, but standing by because the mode isn't a voice mode
        internal bool KainosNnrStandby(int rx) { return _nnrOn[rx - 1] && !nnrModeOk(rx - 1); }

        internal void KainosToggleNnr(int rx)
        {
            _nnrOn[rx - 1] = !_nnrOn[rx - 1];
            nnrSave();
            nnrTick();
        }

        internal void KainosNnrLoad()
        {
            foreach (string kv in (KainosNnrSettings ?? "").Split(';'))
            {
                string[] p = kv.Split('=');
                if (p.Length != 2) continue;
                if (p[0] == "rx1") _nnrOn[0] = p[1] == "1";
                else if (p[0] == "rx2") _nnrOn[1] = p[1] == "1";
            }
            if (_nnrTimer == null)
            {
                _nnrTimer = new System.Windows.Forms.Timer { Interval = 250 };
                _nnrTimer.Tick += (s, e) => nnrTick();
                _nnrTimer.Start();
            }
        }

        // a Setup change: send it on the next tick
        internal void KainosNnrSettingsChanged() { nnrTick(); }

        private void nnrSave()
        {
            KainosNnrSettings = string.Format(CultureInfo.InvariantCulture, "rx1={0};rx2={1}", _nnrOn[0] ? 1 : 0, _nnrOn[1] ? 1 : 0);
            KainosSettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        private bool nnrModeOk(int i)
        {
            if (!KainosNnrVoiceOnly) return true;
            DSPMode m = i == 0 ? _rx1_dsp_mode : _rx2_dsp_mode;
            return m == DSPMode.LSB || m == DSPMode.USB || m == DSPMode.AM || m == DSPMode.SAM || m == DSPMode.DSB || m == DSPMode.FM;
        }

        // Only what has changed is sent. The model is checked every tick because WDSP goes back to the standard
        // model when the DSP sample rate or buffer size changes (the block is rebuilt).
        private void nnrTick()
        {
            if (!PowerOn) return;
            for (int k = 0; k < 3; k++)
            {
                int rx = k < 2 ? 0 : 1;
                int ch = k == 0 ? WDSP.id(0, 0) : k == 1 ? WDSP.id(0, 1) : WDSP.id(2, 0);
                // before or after the AGC, with the other NRs (Setup > DSP > NR, "NRs/ANF Position"): WDSP moves its
                // post-NR bandpass to match, so NNR must be where they are
                int pos = radio.GetDSPRX(rx, k == 1 ? 1 : 0).RXANR2Position;
                if (_nnrChannel[k] != ch) { _nnrChannel[k] = ch; _nnrRun[k] = _nnrPos[k] = _nnrDepth[k] = -1; }
                try
                {
                    if (_nnrPos[k] != pos) { KNnr.SetRXANNRPosition(ch, pos); _nnrPos[k] = pos; }
                    if (_nnrDepth[k] != KainosNnrDepth) { KNnr.SetRXANNRMaskFloor(ch, -KainosNnrDepth); _nnrDepth[k] = KainosNnrDepth; }
                    int run = _nnrOn[rx] && nnrModeOk(rx) && (rx == 0 || RX2Enabled) ? 1 : 0;
                    if (run == 1 && KNnr.GetRXANNRModel(ch) != KainosNnrModel) KNnr.SetRXANNRModel(ch, KainosNnrModel);
                    if (_nnrRun[k] != run) { KNnr.SetRXANNRRun(ch, run); _nnrRun[k] = run; }
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine("nnr: " + ex.Message); }
            }
        }
    }
}
