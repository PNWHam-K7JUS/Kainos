/*  kainosRade.cs

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
using System.Runtime.InteropServices;
using System.Text;

namespace Thetis
{
    // The RADE modem lives in ChannelMaster.dll (radae.c, from Thetis-RADE). It decodes RX1's demodulated
    // audio in place, after WDSP and before the speakers, and encodes the mic audio before WDSP's transmit
    // chain, so the radio sends the modem signal as ordinary DIGU/DIGL audio.
    internal static class Rade
    {
        // radae FreeDV RADEV1 digital voice integration -- dual-RX:
        // RX-side getters/setters take an `int rx` argument (0 = RX1,
        // 1 = RX2).  TX-side and global PORTs are parameterless.
        [DllImport("ChannelMaster.dll", EntryPoint = "SetRadaeRxEnabled", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetRadaeRxEnabled(int rx, int enable);

        [DllImport("ChannelMaster.dll", EntryPoint = "SetRadaeTxEnabled", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetRadaeTxEnabled(int enable);

        [DllImport("ChannelMaster.dll", EntryPoint = "GetRadaeRxEnabled", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetRadaeRxEnabled(int rx);

        [DllImport("ChannelMaster.dll", EntryPoint = "GetRadaeTxEnabled", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetRadaeTxEnabled();

        [DllImport("ChannelMaster.dll", EntryPoint = "GetRadaeSync", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetRadaeSync(int rx);

        [DllImport("ChannelMaster.dll", EntryPoint = "GetRadaeSnrDb", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetRadaeSnrDb(int rx);
        [DllImport("ChannelMaster.dll", EntryPoint = "GetRadaeRxLevelDb", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetRadaeRxLevelDb(int rx);
        [DllImport("ChannelMaster.dll", EntryPoint = "GetRadaeClip", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetRadaeClip(int rx);
        [DllImport("ChannelMaster.dll", EntryPoint = "GetRadaeRemoteCallsign", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int GetRadaeRemoteCallsign(int rx, StringBuilder dst, int max);
        [DllImport("ChannelMaster.dll", EntryPoint = "GetRadaeRemoteCallsignSeq", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetRadaeRemoteCallsignSeq(int rx);
        [DllImport("ChannelMaster.dll", EntryPoint = "GetRadaeEooDecodePulse", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetRadaeEooDecodePulse(int rx);
        [DllImport("ChannelMaster.dll", EntryPoint = "GetRadaeTxMicLevelDb", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetRadaeTxMicLevelDb();
        [DllImport("ChannelMaster.dll", EntryPoint = "GetRadaeTxMicClip", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetRadaeTxMicClip();

        // EOO-safe un-key handshake (PTTRADE arbiter, Option B).
        [DllImport("ChannelMaster.dll", EntryPoint = "GetRadaeEooFlushed", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetRadaeEooFlushed();
        [DllImport("ChannelMaster.dll", EntryPoint = "SetRadaeTxSilenceHold", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetRadaeTxSilenceHold(int on);

        [DllImport("ChannelMaster.dll", EntryPoint = "GetRadaeFreqOffset", CallingConvention = CallingConvention.Cdecl)]
        public static extern float GetRadaeFreqOffset(int rx);

        [DllImport("ChannelMaster.dll", EntryPoint = "SetRadaeFreqOffset", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetRadaeFreqOffset(int rx, float hz);

        [DllImport("ChannelMaster.dll", EntryPoint = "RadaeNotifyEndOfOver", CallingConvention = CallingConvention.Cdecl)]
        public static extern void RadaeNotifyEndOfOver();

        [DllImport("ChannelMaster.dll", EntryPoint = "RadaeNotifyBeginOver", CallingConvention = CallingConvention.Cdecl)]
        public static extern void RadaeNotifyBeginOver();

        [DllImport("ChannelMaster.dll", EntryPoint = "SetRadaeLoopbackEnabled", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetRadaeLoopbackEnabled(int rx, int enable);
        [DllImport("ChannelMaster.dll", EntryPoint = "GetRadaeLoopbackEnabled", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetRadaeLoopbackEnabled(int rx);

        // Per-RX RADE protocol selection (0 = V1, 1 = V2).  Setting it while
        // armed live-recycles only that RX's modem handle.  TX-source selector
        // (SetRadaeTxRx) tells the single encoder which RX's handle/protocol to
        // use; pushed at the MOX 0->1 edge.
        [DllImport("ChannelMaster.dll", EntryPoint = "SetRadaeProtocolV2", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetRadaeProtocolV2(int rx, int on);
        [DllImport("ChannelMaster.dll", EntryPoint = "GetRadaeProtocolV2", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetRadaeProtocolV2(int rx);
        [DllImport("ChannelMaster.dll", EntryPoint = "SetRadaeTxRx", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetRadaeTxRx(int rx);
        [DllImport("ChannelMaster.dll", EntryPoint = "GetRadaeTxRx", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetRadaeTxRx();

        [DllImport("ChannelMaster.dll", EntryPoint = "SetRadaeMoxState", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetRadaeMoxState(int mox);

        [DllImport("ChannelMaster.dll", EntryPoint = "SetRadaeEooCallsign", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void SetRadaeEooCallsign([MarshalAs(UnmanagedType.LPStr)] string callsign);

        [DllImport("ChannelMaster.dll", EntryPoint = "SetRadaeMicScale", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetRadaeMicScale(double scale);
        [DllImport("ChannelMaster.dll", EntryPoint = "SetRadaeRxScale", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetRadaeRxScale(int rx, double scale);
        [DllImport("ChannelMaster.dll", EntryPoint = "SetRadaeRxDialScale", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetRadaeRxDialScale(int rx, double scale);

        // Per-RX AF post-decode multiplier.  Captured by the C# side
        // (DSPRX.RXOutputGain setter) whenever RADE RX is on; applied in
        // pipe.c after xradae_rx returns.
        [DllImport("ChannelMaster.dll", EntryPoint = "SetRadaeRxAFGain", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetRadaeRxAFGain(int rx, double gain);
        [DllImport("ChannelMaster.dll", EntryPoint = "GetRadaeRxAFGain", CallingConvention = CallingConvention.Cdecl)]
        public static extern float GetRadaeRxAFGain(int rx);

        // RADE pre-encoder mic conditioning (FreeDV-GUI parity):
        // RNNoise + ITU-R BS.1770 K-weighted AGC + 3-band biquad EQ.
        [DllImport("ChannelMaster.dll", EntryPoint = "SetRadaeMicRNNoiseEnabled", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetRadaeMicRNNoiseEnabled(int enable);
        [DllImport("ChannelMaster.dll", EntryPoint = "SetRadaeMicAGCEnabled", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetRadaeMicAGCEnabled(int enable);
        [DllImport("ChannelMaster.dll", EntryPoint = "SetRadaeMicAGCTargetLufs", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetRadaeMicAGCTargetLufs(double target_lufs);
        [DllImport("ChannelMaster.dll", EntryPoint = "SetRadaeMicEQEnabled", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetRadaeMicEQEnabled(int enable);
        [DllImport("ChannelMaster.dll", EntryPoint = "SetRadaeMicEQBass", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetRadaeMicEQBass(double freq_hz, double gain_db);
        [DllImport("ChannelMaster.dll", EntryPoint = "SetRadaeMicEQMid", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetRadaeMicEQMid(double freq_hz, double gain_db, double q);
        [DllImport("ChannelMaster.dll", EntryPoint = "SetRadaeMicEQTreble", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetRadaeMicEQTreble(double freq_hz, double gain_db);
        [DllImport("ChannelMaster.dll", EntryPoint = "SetRadaeMicEQVol", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetRadaeMicEQVol(double gain_db);

        // Diagnostic bypass flags -- non-persistent, see SetRadae*Bypass*
        // checkboxes under Setup -> DSP -> RADE -> Diagnostics.
        [DllImport("ChannelMaster.dll", EntryPoint = "SetRadaeBypassMicDsp", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetRadaeBypassMicDsp(int enable);
        [DllImport("ChannelMaster.dll", EntryPoint = "SetRadaeBypassEncoderCore", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetRadaeBypassEncoderCore(int enable);
        [DllImport("ChannelMaster.dll", EntryPoint = "SetRadaeBypassRmatch", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetRadaeBypassRmatch(int enable);
        [DllImport("ChannelMaster.dll", EntryPoint = "SetRadaeBypassEncoder", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetRadaeBypassEncoder(int enable);
        [DllImport("ChannelMaster.dll", EntryPoint = "SetRadaeBypassAll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetRadaeBypassAll(int enable);
        // end radae

        // ---- per-over keying, called from Audio.MOX on every MOX change ----

        // true while the current over is RADE-encoded; decided at key-down and kept until un-key
        private static bool _overIsRade;
        // set by the console's PTT arbiter when it has already sent the end-of-over frame while it held
        // the radio keyed, so the real un-key doesn't send a second one
        internal static volatile bool EooSentByArbiter;

        // The receiver an over keyed now transmits from: RX2 on a VFO B over with RX2 on, RX1 otherwise.
        internal static int OverReceiver(Console c)
        {
            return c != null && c.RX2Enabled && c.VFOBTX ? 1 : 0;
        }

        // Whether an over keyed now would be RADE: RADE is on for the transmitting receiver, and it isn't a
        // tune or two-tone test. An over from a receiver without RADE goes out as plain voice.
        internal static bool OverWouldBeRade(Console c)
        {
            if (c == null || c.TUN || c.TwoTone) return false;
            return GetRadaeRxEnabled(OverReceiver(c)) != 0;
        }

        // The modem signal must not go through the TX compressor, CFC, overshoot control or EQ: the TX profile
        // follows RX1's mode, so an RX2 RADE over while RX1 is in a voice mode would otherwise get the voice
        // profile's processing. They are switched off in WDSP for the over and restored from their settings
        // afterwards (the leveler and phase rotator are left as they are).
        private static bool _txProcessingHeld;

        private static void holdTxProcessing(Console c, bool hold)
        {
            if (hold == _txProcessingHeld) return;
            int id = WDSP.id(1, 0);
            if (hold)
            {
                WDSP.SetTXAEQRun(id, false);
                WDSP.SetTXACompressorRun(id, false);
                WDSP.SetTXAosctrlRun(id, false);
                WDSP.SetTXACFCOMPRun(id, 0);
            }
            else
            {
                RadioDSPTX tx = c.radio.GetDSPTX(0);
                WDSP.SetTXAEQRun(id, tx.TXEQOn);
                WDSP.SetTXACompressorRun(id, tx.TXCompandOn);
                WDSP.SetTXAosctrlRun(id, tx.TXOsctrlOn);
                WDSP.SetTXACFCOMPRun(id, !c.IsSetupFormNull && c.SetupForm.TXCFCOn ? 1 : 0);
            }
            _txProcessingHeld = hold;
        }

        // Order matters at un-key: the end-of-over request must be raised before the MOX state drops,
        // so the encoder's next block still passes its gate and sends the frame.
        internal static void OnMox(bool wasMox, bool mox)
        {
            try
            {
                if (wasMox && !mox)
                {
                    if (_overIsRade && !EooSentByArbiter) RadaeNotifyEndOfOver();
                    EooSentByArbiter = false;
                    _overIsRade = false;
                    holdTxProcessing(Console.getConsole(), false);
                }
                else if (!wasMox && mox)
                {
                    Console c = Console.getConsole();
                    _overIsRade = OverWouldBeRade(c);
                    SetRadaeTxRx(OverReceiver(c));        // the encoder uses this receiver's protocol (V1/V2)
                    if (_overIsRade)
                    {
                        holdTxProcessing(c, true);
                        SetRadaeEooCallsign(c.RadeCallsign);
                        RadaeNotifyBeginOver();
                    }
                }
                SetRadaeMoxState(_overIsRade ? 1 : 0);
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }
    }
}
