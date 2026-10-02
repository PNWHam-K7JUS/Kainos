/*  KainosCw.cs

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
using System.Text;

namespace Thetis
{
    // Kainos's CW decoder. The receiver's audio (from ChannelMaster, kdigi.c) is mixed down from the CW pitch and
    // averaged over a few milliseconds, giving the tone's envelope every millisecond. The envelope is keyed against
    // a threshold between the tracked signal peak and noise floor (with hysteresis), and the marks and gaps are timed:
    // a dot length tracked from both dots and dashes sets the speed, marks shorter than two dots are dots, gaps of
    // two to five dots end a character and longer ones a word.
    internal class CwDecoder
    {
        public double Pitch = 600;
        public double Squelch = 0.2;        // 0..1: how far above the noise a signal must be to be keyed
        public event Action<char> Decoded;

        public double Level { get; private set; }       // the envelope, 0..1 of the tracked peak
        public bool KeyDown { get; private set; }
        public double Wpm { get { return 1200.0 / _dot; } }
        public double Snr { get { return _peak / Math.Max(1e-12, _noise); } }

        private static readonly Dictionary<string, char> Morse = new Dictionary<string, char>
        {
            { ".-", 'A' }, { "-...", 'B' }, { "-.-.", 'C' }, { "-..", 'D' }, { ".", 'E' }, { "..-.", 'F' }, { "--.", 'G' },
            { "....", 'H' }, { "..", 'I' }, { ".---", 'J' }, { "-.-", 'K' }, { ".-..", 'L' }, { "--", 'M' }, { "-.", 'N' },
            { "---", 'O' }, { ".--.", 'P' }, { "--.-", 'Q' }, { ".-.", 'R' }, { "...", 'S' }, { "-", 'T' }, { "..-", 'U' },
            { "...-", 'V' }, { ".--", 'W' }, { "-..-", 'X' }, { "-.--", 'Y' }, { "--..", 'Z' },
            { "-----", '0' }, { ".----", '1' }, { "..---", '2' }, { "...--", '3' }, { "....-", '4' }, { ".....", '5' },
            { "-....", '6' }, { "--...", '7' }, { "---..", '8' }, { "----.", '9' },
            { ".-.-.-", '.' }, { "--..--", ',' }, { "..--..", '?' }, { "-..-.", '/' }, { "-...-", '=' }, { ".-.-.", '+' },
            { "-....-", '-' }, { ".----.", '\'' }, { "-.--.", '(' }, { "-.--.-", ')' }, { "---...", ':' }, { ".-..-.", '"' },
            { ".--.-.", '@' }, { "-.-.--", '!' }, { "...-.-", '*' },          // * : SK
        };

        private readonly int _rate;
        private readonly int _decim;        // samples per envelope step (1 ms)
        private double _ph, _inc;
        private double[] _bi, _bq;
        private double _si, _sq;
        private int _pos, _n, _step;
        private double _peak = 1e-9, _noise = 1e-9, _env;
        private double _dot = 60;           // ms (20 WPM)
        private int _markMs, _gapMs;
        private readonly StringBuilder _sym = new StringBuilder();
        private bool _wordSpaced = true;
        private double _pitchUsed = -1;
        private int _ms;                    // milliseconds since start (the noise floor is learnt quickly at first)

        public CwDecoder(int rate)
        {
            _rate = rate;
            _decim = Math.Max(1, rate / 1000);
            _n = Math.Max(8, rate / 125);           // 8 ms average: ~125 Hz wide around the pitch
            _bi = new double[_n]; _bq = new double[_n];
        }

        public void Process(float[] x, int count)
        {
            if (Pitch != _pitchUsed) { _pitchUsed = Pitch; _inc = 2 * Math.PI * Pitch / _rate; }
            for (int k = 0; k < count; k++)
            {
                double s = x[k];
                double a = s * Math.Cos(_ph), b = s * Math.Sin(_ph);
                _ph += _inc; if (_ph > Math.PI * 2) _ph -= Math.PI * 2;
                _si += a - _bi[_pos]; _bi[_pos] = a;
                _sq += b - _bq[_pos]; _bq[_pos] = b;
                if (++_pos == _n) _pos = 0;
                if (++_step >= _decim)
                {
                    _step = 0;
                    envelope(Math.Sqrt(_si * _si + _sq * _sq) / _n);
                }
            }
        }

        // every millisecond
        private void envelope(double e)
        {
            // smoothing that follows the speed (a fifth of a dot): narrower, so quieter, for slower code
            double a = 1.0 / Math.Max(2, _dot / 5);
            _env += (e - _env) * a;
            _ms++;
            // peak: quick up, slow down (about 4 s); noise: slow up, quicker down (learnt fast in the first 0.3 s)
            _peak = _env > _peak ? _peak + (_env - _peak) * 0.1 : _peak * 0.99985;
            double up = _ms < 300 ? 0.02 : 0.0004;
            _noise = _env < _noise ? _noise + (_env - _noise) * 0.05 : _noise + (_env - _noise) * up;
            if (_peak < _noise) _peak = _noise;
            Level = Math.Min(1, _env / Math.Max(1e-12, _peak));

            // a signal clearly above the noise, then keyed with hysteresis between the floor and the peak
            bool signal = _ms > 300 && _peak > _noise * (2.0 + Squelch * 6);     // not while the floor is first learnt
            double hi = _noise + (_peak - _noise) * 0.55, lo = _noise + (_peak - _noise) * 0.4;
            bool down = signal && (KeyDown ? _env > lo : _env > hi);

            if (down)
            {
                if (!KeyDown) { endGap(); _markMs = 0; }
                _markMs++;
            }
            else
            {
                if (KeyDown) endMark();
                _gapMs++;
                // nothing more is coming: finish the character, and then the word
                if (_sym.Length > 0 && _gapMs > _dot * 2.5) flushChar();
                if (!_wordSpaced && _gapMs > _dot * 6) { Decoded?.Invoke(' '); _wordSpaced = true; }
            }
            KeyDown = down;
        }

        private void endMark()
        {
            int ms = _markMs;
            if (ms < Math.Max(8, _dot * 0.3)) return;                   // a click
            bool dash = ms > _dot * 2;
            _sym.Append(dash ? '-' : '.');
            // the speed follows both kinds of element (a dash is three dots), bounded to 5..60 WPM; only from a
            // clear signal, and only so far at a time, so noise can't run it away
            if (Snr > 4)
            {
                double est = dash ? ms / 3.0 : ms;
                est = Math.Max(_dot * 0.6, Math.Min(_dot * 1.6, est));
                _dot = Math.Max(20, Math.Min(240, _dot * 0.8 + est * 0.2));
            }
            _gapMs = 0;
        }

        private void endGap()
        {
            // a gap that ended a character was handled as it grew (flushChar); here only intra-character gaps,
            // which also refine the dot length when they're clean
            if (Snr > 4 && _gapMs > 0 && _gapMs < _dot * 1.6 && _gapMs > _dot * 0.5)
                _dot = Math.Max(20, Math.Min(240, _dot * 0.9 + _gapMs * 0.1));
            _gapMs = 0;
        }

        private void flushChar()
        {
            char c;
            string s = _sym.ToString();
            _sym.Clear();
            if (s.Length > 8) return;                                      // noise
            if (!Morse.TryGetValue(s, out c)) c = '_';                    // unknown: show that something was there
            Decoded?.Invoke(c);
            _wordSpaced = false;
        }
    }
}
