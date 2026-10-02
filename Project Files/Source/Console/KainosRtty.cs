/*  KainosRtty.cs

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
using System.Runtime.InteropServices;
using System.Text;

namespace Thetis
{
    // Kainos's native RTTY: amateur Baudot (ITA2 with the US figures), 5 data bits, 1 start and 1.5 stop bits, sent
    // and received as AFSK audio (mark and space tones either side of a centre frequency, 170 Hz apart by default,
    // mark the lower tone on LSB / DIGL as usual, Reverse swaps them). The audio comes from and goes to ChannelMaster
    // (kdigi.c): a copy of the receiver's demodulated audio, and a feed that stands in for the mic while sending.

    internal static class KDigi
    {
        [DllImport("ChannelMaster.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void KDigiRxTap(int rx, int on);
        [DllImport("ChannelMaster.dll", CallingConvention = CallingConvention.Cdecl)] public static extern int KDigiRxRate(int rx);
        [DllImport("ChannelMaster.dll", CallingConvention = CallingConvention.Cdecl)] public static extern int KDigiRxRead(int rx, float[] dst, int max);
        [DllImport("ChannelMaster.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void KDigiTxEnable(int on);
        [DllImport("ChannelMaster.dll", CallingConvention = CallingConvention.Cdecl)] public static extern int KDigiTxRate();
        [DllImport("ChannelMaster.dll", CallingConvention = CallingConvention.Cdecl)] public static extern int KDigiTxWrite(float[] src, int n);
        [DllImport("ChannelMaster.dll", CallingConvention = CallingConvention.Cdecl)] public static extern int KDigiTxQueued();
        [DllImport("ChannelMaster.dll", CallingConvention = CallingConvention.Cdecl)] public static extern void KDigiTxClear();
    }

    internal static class Baudot
    {
        public const int LTRS = 0x1F, FIGS = 0x1B, SPACE = 0x04, CR = 0x08, LF = 0x02;

        public static readonly char[] Letters =
        {
            '\0', 'E', '\n', 'A', ' ', 'S', 'I', 'U', '\r', 'D', 'R', 'J', 'N', 'F', 'C', 'K',
            'T', 'Z', 'L', 'W', 'H', 'Y', 'P', 'Q', 'O', 'B', 'G', '\0', 'M', 'X', 'V', '\0',
        };

        public static readonly char[] Figures =
        {
            '\0', '3', '\n', '-', ' ', '\a', '8', '7', '\r', '$', '4', '\'', ',', '!', ':', '(',
            '5', '"', ')', '2', '#', '6', '0', '1', '9', '?', '&', '\0', '.', '/', ';', '\0',
        };

        // the code for a character and whether it needs letters (false: figures; null: either, e.g. space)
        public static bool Encode(char c, out int code, out bool? letters)
        {
            c = char.ToUpperInvariant(c);
            code = 0;
            letters = null;
            if (c == ' ') { code = SPACE; return true; }
            if (c == '\r') { code = CR; return true; }
            if (c == '\n') { code = LF; return true; }
            for (int i = 0; i < 32; i++)
            {
                if (i == FIGS || i == LTRS || Letters[i] == '\0') continue;
                if (Letters[i] == c) { code = i; letters = true; return true; }
            }
            for (int i = 0; i < 32; i++)
            {
                if (i == FIGS || i == LTRS || Figures[i] == '\0' || Figures[i] == '\a') continue;
                if (Figures[i] == c) { code = i; letters = false; return true; }
            }
            return false;
        }
    }

    // The receiver: each tone is mixed to zero and averaged over one bit (a matched filter), the two magnitudes
    // compared ((mark - space) / (mark + space)), and characters framed from the start bit's edge, sampling each bit
    // at its middle. A quality figure (the average strength of that comparison) squelches noise.
    internal class RttyDemod
    {
        public double Center = 2210, Shift = 170, Baud = 45.45;
        public bool Reverse, UnshiftOnSpace = true;
        public double Squelch = 0.15;       // quality below this decodes nothing
        public double MinConfidence = 0.4;  // a character's bits must average at least this clear
        public event Action<char> Decoded;

        public double Quality { get; private set; }          // 0 (noise) .. 1 (a clean signal)
        public double MarkLevel { get; private set; }
        public double SpaceLevel { get; private set; }

        private readonly int _rate;
        private int _n;                                     // samples per bit
        private double _markPh, _spacePh, _markInc, _spaceInc;
        private double[] _mi, _mq, _si, _sq;
        private double _smi, _smq, _ssi, _ssq;
        private int _pos;
        private double _prev = 1;
        private int _state, _count, _bits, _bitNo;
        private bool _figs;
        private double _bitLen;
        private double _avgAbs;
        private double _conf;                               // the character's bits' strength, summed
        private string _config = "";

        public RttyDemod(int rate) { _rate = rate; configure(); }

        private void configure()
        {
            string cfg = Center + "/" + Shift + "/" + Baud + "/" + Reverse;
            if (cfg == _config) return;
            _config = cfg;
            double mark = Center - Shift / 2, space = Center + Shift / 2;
            if (Reverse) { double t = mark; mark = space; space = t; }
            _markInc = 2 * Math.PI * mark / _rate;
            _spaceInc = 2 * Math.PI * space / _rate;
            _bitLen = _rate / Baud;
            _n = Math.Max(4, (int)Math.Round(_bitLen));
            _mi = new double[_n]; _mq = new double[_n]; _si = new double[_n]; _sq = new double[_n];
            _smi = _smq = _ssi = _ssq = 0;
            _pos = 0;
            _state = 0;
        }

        public void Process(float[] x, int count)
        {
            configure();
            for (int k = 0; k < count; k++)
            {
                double s = x[k];
                double a = s * Math.Cos(_markPh), b = s * Math.Sin(_markPh);
                double c = s * Math.Cos(_spacePh), d = s * Math.Sin(_spacePh);
                _markPh += _markInc; if (_markPh > Math.PI * 2) _markPh -= Math.PI * 2;
                _spacePh += _spaceInc; if (_spacePh > Math.PI * 2) _spacePh -= Math.PI * 2;

                _smi += a - _mi[_pos]; _mi[_pos] = a;
                _smq += b - _mq[_pos]; _mq[_pos] = b;
                _ssi += c - _si[_pos]; _si[_pos] = c;
                _ssq += d - _sq[_pos]; _sq[_pos] = d;
                if (++_pos == _n) _pos = 0;

                double m = Math.Sqrt(_smi * _smi + _smq * _smq), sp = Math.Sqrt(_ssi * _ssi + _ssq * _ssq);
                double v = (m - sp) / (m + sp + 1e-12);         // +1 mark .. -1 space
                MarkLevel = m / _n; SpaceLevel = sp / _n;
                _avgAbs += (Math.Abs(v) - _avgAbs) * (1.0 / (_rate * 0.12));
                Quality = _avgAbs;
                step(v);
                _prev = v;
            }
        }

        // 0 idle (wait for mark then a fall to space), 1 start bit (check it at its middle), 2 data bits, 3 stop bit
        private void step(double v)
        {
            switch (_state)
            {
                case 0:
                    if (_prev >= 0 && v < 0 && Quality >= Squelch) { _state = 1; _count = 0; }
                    break;
                case 1:
                    if (++_count >= (int)(_bitLen * 0.5))
                    {
                        if (v < 0) { _state = 2; _count = 0; _bits = 0; _bitNo = 0; _conf = -v; }
                        else _state = 0;                         // a glitch, not a start bit
                    }
                    break;
                case 2:
                    if (++_count >= (int)_bitLen)
                    {
                        _count = 0;
                        if (v > 0) _bits |= 1 << _bitNo;
                        _conf += Math.Abs(v);
                        if (++_bitNo == 5) _state = 3;
                    }
                    break;
                case 3:
                    if (++_count >= (int)_bitLen)
                    {
                        // a mark stop bit and every bit clearly mark or space: a good character (noise makes
                        // characters too, but weak ones)
                        _conf += Math.Max(0, v);
                        if (v > 0 && _conf / 7 >= MinConfidence) emit(_bits);
                        _state = 0;
                        _prev = v;
                    }
                    break;
            }
        }

        private void emit(int code)
        {
            if (code == Baudot.LTRS) { _figs = false; return; }
            if (code == Baudot.FIGS) { _figs = true; return; }
            char c = _figs ? Baudot.Figures[code] : Baudot.Letters[code];
            if (code == Baudot.SPACE && UnshiftOnSpace) _figs = false;
            if (c == '\0' || c == '\a' || c == '\r') return;
            Decoded?.Invoke(c);
        }
    }

    // The sender: phase-continuous AFSK, a character at a time (with the letters / figures shifts it needs), a
    // run of mark to start, and LTRS while idle so the receiver stays locked between words.
    internal class RttyMod
    {
        public double Center = 2210, Shift = 170, Baud = 45.45;
        public bool Reverse;
        public float Amplitude = 0.5f;

        private readonly int _rate;
        private double _phase, _carry;
        private bool _figs, _shiftKnown;

        public RttyMod(int rate) { _rate = rate; }

        private void tone(List<float> o, bool mark, double bits)
        {
            double mf = Center - Shift / 2, sf = Center + Shift / 2;
            if (Reverse) { double t = mf; mf = sf; sf = t; }
            double inc = 2 * Math.PI * (mark ? mf : sf) / _rate;
            double exact = bits * _rate / Baud + _carry;
            int n = (int)exact;
            _carry = exact - n;
            for (int i = 0; i < n; i++)
            {
                o.Add((float)(Amplitude * Math.Sin(_phase)));
                _phase += inc;
                if (_phase > Math.PI * 2) _phase -= Math.PI * 2;
            }
        }

        private void code(List<float> o, int c)
        {
            tone(o, false, 1);                                       // start
            for (int i = 0; i < 5; i++) tone(o, ((c >> i) & 1) != 0, 1);
            tone(o, true, 1.5);                                      // stop
        }

        // the opening: mark, then LTRS so the receiver knows the shift
        public void Begin(List<float> o)
        {
            tone(o, true, 8);
            code(o, Baudot.LTRS);
            _figs = false;
            _shiftKnown = true;
        }

        public void Idle(List<float> o) { code(o, _figs ? Baudot.FIGS : Baudot.LTRS); }

        public void Char(List<float> o, char ch)
        {
            if (ch == '\n') { code(o, Baudot.CR); code(o, Baudot.LF); return; }
            int c; bool? letters;
            if (!Baudot.Encode(ch, out c, out letters)) return;
            if (letters.HasValue && (!_shiftKnown || letters.Value == _figs))
            {
                code(o, letters.Value ? Baudot.LTRS : Baudot.FIGS);
                _figs = !letters.Value;
                _shiftKnown = true;
            }
            code(o, c);
            if (c == Baudot.SPACE) _figs = false;                     // the receiver unshifts on space too
        }

        // the closing: a little mark so the last character's stop bit is clean
        public void End(List<float> o) { tone(o, true, 3); }
    }
}
