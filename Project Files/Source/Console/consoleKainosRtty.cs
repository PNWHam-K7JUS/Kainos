/*  consoleKainosRtty.cs

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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace Thetis
{
    // The RTTY terminal (the RTTY menu item, like CWX): received text, a type-ahead line and macros, with the decoder
    // (KainosRtty.cs) on a copy of the chosen receiver's audio and the sender in place of the mic while transmitting.
    // In Kainos layout it opens under the panadapter (split screen) and can pop out to its own window; in Classic it
    // is always a window.
    public partial class Console
    {
        private ToolStripMenuItem rttyToolStripMenuItem;
        private KainosRttyPane _rttyPane;
        private Form _rttyWindow;
        private bool _rttyOpen;

        // saved with the options (a hidden Setup box): "rx=0;shift=170;baud=45.45;rev=0;usos=1;sql=15;lvl=-10;pop=0"
        public string KainosRttySettings = "";

        // engine state (the worker thread and the UI)
        private Thread _rttyThread;
        private volatile bool _rttyRun;
        private RttyDemod _rttyDemod;
        private RttyMod _rttyMod;
        private readonly ConcurrentQueue<KeyValuePair<char, bool>> _rttyOut = new ConcurrentQueue<KeyValuePair<char, bool>>();     // char, sent
        private readonly ConcurrentQueue<char> _rttyTxQueue = new ConcurrentQueue<char>();
        private volatile bool _rttyTx, _rttyTxBegun, _rttyTxEnding, _rttyTxDrained;
        private System.Windows.Forms.Timer _rttyUiTimer;

        internal int RttyRx = 0;
        internal double RttyShift = 170, RttyBaud = 45.45;
        internal bool RttyReverse, RttyUnshiftOnSpace = true, RttyAfc = true;
        internal readonly NumericUpDown RttySql = new NumericUpDown { Minimum = 0, Maximum = 60, Increment = 5, Value = 15 };
        internal readonly NumericUpDown RttyLevel = new NumericUpDown { Minimum = -40, Maximum = 0, Increment = 1, Value = -10 };
        private bool _rttyPopped;

        private void addRttyControls()
        {
            rttyToolStripMenuItem = new ToolStripMenuItem("RTTY") { Name = "rttyToolStripMenuItem" };
            rttyToolStripMenuItem.Click += (s, e) => { if (_rttyOpen) RttyClose(); else RttyOpen(); };
            menuStrip1.Items.Insert(menuStrip1.Items.IndexOf(cWXToolStripMenuItem) + 1, rttyToolStripMenuItem);
            RttySql.ValueChanged += (s, e) => rttySave();
            RttyLevel.ValueChanged += (s, e) => rttySave();
        }

        internal void RttyLoadSettings()
        {
            foreach (string kv in (KainosRttySettings ?? "").Split(';'))
            {
                string[] p = kv.Split('=');
                if (p.Length != 2) continue;
                double d;
                bool num = double.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out d);
                switch (p[0])
                {
                    case "rx": if (num) RttyRx = d >= 1 ? 1 : 0; break;
                    case "shift": if (num && d > 0) RttyShift = d; break;
                    case "baud": if (num && d > 0) RttyBaud = d; break;
                    case "rev": RttyReverse = p[1] == "1"; break;
                    case "usos": RttyUnshiftOnSpace = p[1] != "0"; break;
                    case "afc": RttyAfc = p[1] != "0"; break;
                    case "sql": if (num) RttySql.Value = (decimal)Math.Max(0, Math.Min(60, d)); break;
                    case "lvl": if (num) RttyLevel.Value = (decimal)Math.Max(-40, Math.Min(0, d)); break;
                    case "pop": _rttyPopped = p[1] == "1"; break;
                }
            }
        }

        private void rttySave()
        {
            KainosRttySettings = string.Format(CultureInfo.InvariantCulture, "rx={0};shift={1};baud={2};rev={3};usos={4};sql={5};lvl={6};pop={7};afc={8}",
                RttyRx, RttyShift, RttyBaud, RttyReverse ? 1 : 0, RttyUnshiftOnSpace ? 1 : 0, RttySql.Value, RttyLevel.Value, _rttyPopped ? 1 : 0, RttyAfc ? 1 : 0);
            KainosSettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        internal void RttyChanged() { rttySave(); if (_rttyPane != null) _rttyPane.Invalidate(true); }

        // ---- opening, docking, popping out ----

        internal void RttyOpen()
        {
            if (_rttyPane == null)
            {
                _rttyPane = new KainosRttyPane(this);
                _rttyUiTimer = new System.Windows.Forms.Timer { Interval = 80 };
                _rttyUiTimer.Tick += (s, e) => rttyUiTick();
            }
            if (_cwOpen) CwClose();         // one terminal at a time (they share the receive tap)
            _rttyOpen = true;
            rttyToolStripMenuItem.Checked = true;
            rttyPlace();
            rttyStartEngine();
            _rttyUiTimer.Start();
            _rttyPane.FocusTyping();
        }

        internal void RttyClose()
        {
            if (_rttyTx) RttyAbort();
            _rttyOpen = false;
            rttyToolStripMenuItem.Checked = false;
            rttyStopEngine();
            if (_rttyUiTimer != null) _rttyUiTimer.Stop();
            rttyPlace();
        }

        internal bool RttyPopped { get { return _rttyPopped || !_kainosLayout; } }

        internal void RttyTogglePop()
        {
            _rttyPopped = !_rttyPopped;
            rttySave();
            rttyPlace();
        }

        // where the pane lives (consoleKainosTerminal.cs)
        private void rttyPlace() { _rttyWindow = kainosTermPlace(_rttyPane, _rttyWindow, "Kainos RTTY", _rttyOpen, RttyPopped, RttyClose); }

        // ---- the engine ----

        private int rttyRxThread { get { return RttyRx; } }

        // USB / DIGU put mark above space in the audio; LSB / DIGL (RTTY's usual) below
        private bool rttyUpperSideband
        {
            get
            {
                DSPMode m = RttyRx == 0 ? _rx1_dsp_mode : _rx2_dsp_mode;
                return m == DSPMode.USB || m == DSPMode.DIGU;
            }
        }

        internal bool RttyModeOk
        {
            get
            {
                DSPMode m = RttyRx == 0 ? _rx1_dsp_mode : _rx2_dsp_mode;
                return m == DSPMode.LSB || m == DSPMode.USB || m == DSPMode.DIGL || m == DSPMode.DIGU;
            }
        }

        // the tones' centre in the audio: DIGL / DIGU use Thetis's digital offset (the passband's centre), SSB 2210
        internal double RttyCenter { get { return 2210; } }

        private void rttyApplyParams()
        {
            bool rev = RttyReverse ^ rttyUpperSideband;
            if (_rttyDemod != null)
            {
                _rttyDemod.Nominal = RttyCenter; _rttyDemod.Afc = RttyAfc; _rttyDemod.Shift = RttyShift; _rttyDemod.Baud = RttyBaud;
                _rttyDemod.Reverse = rev; _rttyDemod.UnshiftOnSpace = RttyUnshiftOnSpace;
                _rttyDemod.Squelch = (double)RttySql.Value / 100.0;
                _rttyDemod.MinConfidence = 0.3 + (double)RttySql.Value / 200.0;
            }
            if (_rttyMod != null)
            {
                _rttyMod.Center = _rttyDemod != null ? _rttyDemod.Center : RttyCenter; _rttyMod.Shift = RttyShift; _rttyMod.Baud = RttyBaud; _rttyMod.Reverse = rev;
                _rttyMod.Amplitude = (float)Math.Pow(10, (double)RttyLevel.Value / 20.0);
            }
        }

        internal RttyDemod RttyDemodulator { get { return _rttyDemod; } }

        // The RTTY tones as offsets from the receiver's VFO in Hz (mark first; negative below the VFO on LSB / DIGL),
        // for the markers on that receiver's panadapter (displayKainos.cs); null when the terminal isn't open on it
        internal double[] KainosDigiMarkers(int rx)
        {
            if (!_rttyOpen || RttyRx != rx - 1 || !RttyModeOk) return null;
            RttyDemod d = _rttyDemod;
            double c = d != null ? d.Center : RttyCenter, h = RttyShift / 2;
            bool upper = rttyUpperSideband;
            double mark = RttyReverse ^ upper ? c + h : c - h, space = RttyReverse ^ upper ? c - h : c + h;
            return upper ? new[] { mark, space } : new[] { -mark, -space };
        }

        private int _rttyTapRx = -1;
        private void rttyStartEngine()
        {
            if (_rttyRun) return;
            _rttyRun = true;
            _rttyThread = new Thread(rttyWork) { IsBackground = true, Name = "Kainos RTTY", Priority = ThreadPriority.AboveNormal };
            _rttyThread.Start();
        }

        private void rttyStopEngine()
        {
            _rttyRun = false;
            if (_rttyThread != null) _rttyThread.Join(500);
            _rttyThread = null;
            try { if (_rttyTapRx >= 0) KDigi.KDigiRxTap(_rttyTapRx, 0); } catch { }
            _rttyTapRx = -1;
        }

        private void rttyWork()
        {
            float[] buf = new float[16384];
            List<float> tx = new List<float>(8192);
            int demodRate = 0;
            while (_rttyRun)
            {
                try
                {
                    // receive: follow the chosen receiver and its rate
                    int rx = RttyRx;
                    if (rx != _rttyTapRx)
                    {
                        if (_rttyTapRx >= 0) KDigi.KDigiRxTap(_rttyTapRx, 0);
                        KDigi.KDigiRxTap(rx, 1);
                        _rttyTapRx = rx;
                    }
                    int rate = KDigi.KDigiRxRate(rx);
                    if (rate > 0 && rate != demodRate)
                    {
                        demodRate = rate;
                        _rttyDemod = new RttyDemod(rate);
                        _rttyDemod.Decoded += c => _rttyOut.Enqueue(new KeyValuePair<char, bool>(c, false));
                    }
                    rttyApplyParams();
                    int n;
                    while ((n = KDigi.KDigiRxRead(rx, buf, buf.Length)) > 0)
                        if (_rttyDemod != null && !_mox) _rttyDemod.Process(buf, n);

                    // transmit: keep ~0.2 s of audio queued ahead of the radio
                    if (_rttyTx && !_rttyTxDrained)
                    {
                        int txRate = KDigi.KDigiTxRate();
                        if (txRate > 0)
                        {
                            if (_rttyMod == null) { _rttyMod = new RttyMod(txRate); rttyApplyParams(); }
                            while (KDigi.KDigiTxQueued() < txRate / 5 && !_rttyTxDrained)
                            {
                                tx.Clear();
                                char c;
                                if (!_rttyTxBegun) { _rttyMod.Begin(tx); _rttyTxBegun = true; }
                                else if (_rttyTxQueue.TryDequeue(out c)) { _rttyMod.Char(tx, c); _rttyOut.Enqueue(new KeyValuePair<char, bool>(c, true)); }
                                else if (_rttyTxEnding) { _rttyMod.End(tx); _rttyTxDrained = true; }
                                else _rttyMod.Idle(tx);
                                float[] a = tx.ToArray();
                                int off = 0;
                                while (off < a.Length && _rttyRun)
                                {
                                    float[] part = off == 0 ? a : a.Skip(off).ToArray();
                                    int w = KDigi.KDigiTxWrite(part, part.Length);
                                    off += w;
                                    if (w == 0) Thread.Sleep(5);
                                }
                            }
                        }
                    }
                }
                catch (Exception) { }
                Thread.Sleep(20);
            }
        }

        // ---- transmitting ----

        internal bool RttyTransmitting { get { return _rttyTx; } }
        internal int RttyQueued { get { return _rttyTxQueue.Count; } }

        internal string RttyStartProblem
        {
            get
            {
                if (!PowerOn) return "The radio is off";
                if (!RttyModeOk) return "Use DIGL or LSB (or DIGU / USB) for RTTY";
                if (RttyRx == 1 && !chkVFOBTX.Checked) return "RX2 is selected but VFO A transmits";
                return null;
            }
        }

        internal void RttySend(string text, bool thenReceive)
        {
            foreach (char c in text) _rttyTxQueue.Enqueue(c);
            if (!_rttyTx)
            {
                if (RttyStartProblem != null) { while (_rttyTxQueue.TryDequeue(out char _)) { } return; }
                KDigi.KDigiTxClear();
                _rttyMod = null;
                _rttyTxBegun = false;
                _rttyTxEnding = false;
                _rttyTxDrained = false;
                KDigi.KDigiTxEnable(1);
                _rttyTx = true;
                MOX = true;
            }
            if (thenReceive) _rttyTxEnding = true;
        }

        internal void RttyType(char c) { if (_rttyTx) _rttyTxQueue.Enqueue(c); }

        internal void RttyReceive()
        {
            if (_rttyTx) _rttyTxEnding = true;
        }

        internal void RttyAbort()
        {
            while (_rttyTxQueue.TryDequeue(out char _)) { }
            rttyStopTx();
        }

        private void rttyStopTx()
        {
            _rttyTx = false;
            KDigi.KDigiTxClear();
            if (_mox) MOX = false;
            KDigi.KDigiTxEnable(0);
            _rttyOut.Enqueue(new KeyValuePair<char, bool>('\n', true));
        }

        private void rttyUiTick()
        {
            // the sent text and decoded text into the terminal
            if (_rttyPane != null)
            {
                KeyValuePair<char, bool> kv;
                int guard = 0;
                while (guard++ < 2000 && _rttyOut.TryDequeue(out kv)) _rttyPane.Append(kv.Key, kv.Value);
                _rttyPane.Tick();
            }
            if (_rttyTx)
            {
                if (!_mox) { RttyAbort(); return; }                          // un-keyed elsewhere (the MOX button, a foot switch...)
                if (_rttyTxDrained && KDigi.KDigiTxQueued() == 0)
                    BeginInvoke(new Action(() => { if (_rttyTx && _rttyTxDrained) rttyStopTx(); }));
            }
        }

        // the macros (KainosMacros.cs), saved with the options (a hidden Setup box)
        public string KainosRttyMacros = "";
        private List<KainosMacro> _rttyMacroList;
        internal List<KainosMacro> RttyMacroList { get { return _rttyMacroList ?? (_rttyMacroList = KainosMacros.Parse(KainosRttyMacros, KainosMacros.RttyDefaults())); } }
        internal void RttyMacrosLoaded() { _rttyMacroList = null; if (_rttyPane != null) _rttyPane.Invalidate(true); }
        internal void RttyMacrosChanged()
        {
            KainosRttyMacros = KainosMacros.Serialize(RttyMacroList);
            KainosSettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        internal string RttyMacroText(string m, string theirCall)
        {
            string my = (RadeCallsign ?? "").Trim().ToUpperInvariant();
            string call = (theirCall ?? "").Trim().ToUpperInvariant();
            return m.Replace("{MY}", my).Replace("{CALL}", call);
        }
    }

    // The terminal: settings row, received text, the type-ahead line with TX / RX / ABORT, and the macro row
    internal class KainosRttyPane : Panel, IKainosTerminal
    {
        private readonly Console _console;
        private readonly RichTextBox _rx;
        private readonly TextBox _type, _call;
        private readonly KainosActionGrid _top, _txButtons, _macros, _winButtons;
        private readonly KainosRttyTune _tune;
        private readonly KainosUpDown _sql, _lvl;
        private readonly Label _status;

        private static readonly double[] Shifts = { 170, 200, 425, 850 };
        private static readonly double[] Bauds = { 45.45, 50, 75 };

        public KainosRttyPane(Console console)
        {
            _console = console;
            BackColor = KainosUI.Surface;
            Name = "kainosRttyPane";

            _top = new KainosActionGrid(7);
            _top.Add("RX1", () => _console.RttyRx == 0, () => { _console.RttyRx = 0; _console.RttyChanged(); }, KainosUI.Tone.Gold);
            _top.Add("RX2", () => _console.RttyRx == 1, () => { _console.RttyRx = 1; _console.RttyChanged(); }, KainosUI.Tone.Violet);
            _top.Add("170 Hz", () => false, () => { _console.RttyShift = next(Shifts, _console.RttyShift); _console.RttyChanged(); }, KainosUI.Tone.Ice);
            _top.Add("45.45", () => false, () => { _console.RttyBaud = next(Bauds, _console.RttyBaud); _console.RttyChanged(); }, KainosUI.Tone.Ice);
            _top.Add("REV", () => _console.RttyReverse, () => { _console.RttyReverse = !_console.RttyReverse; _console.RttyChanged(); }, KainosUI.Tone.Ice);
            _top.Add("USOS", () => _console.RttyUnshiftOnSpace, () => { _console.RttyUnshiftOnSpace = !_console.RttyUnshiftOnSpace; _console.RttyChanged(); }, KainosUI.Tone.Ice);
            _top.Add("AFC", () => _console.RttyAfc, () => { _console.RttyAfc = !_console.RttyAfc; _console.RttyChanged(); }, KainosUI.Tone.Ice);
            _top.LabelFor = (i, l) => i == 2 ? _console.RttyShift.ToString(CultureInfo.InvariantCulture) + " Hz" : i == 3 ? _console.RttyBaud.ToString(CultureInfo.InvariantCulture) + " Bd" : l;

            _tune = new KainosRttyTune(console);
            _sql = new KainosUpDown(console.RttySql, "SQL", "", false);
            _lvl = new KainosUpDown(console.RttyLevel, "TX", "dB", false);

            _winButtons = new KainosActionGrid(3);
            _winButtons.Add("Clear", () => false, () => _rx.Clear(), KainosUI.Tone.Ice);
            _winButtons.Add("Pop out", () => false, () => _console.RttyTogglePop(), KainosUI.Tone.Ice);
            _winButtons.Add("Close", () => false, () => _console.RttyClose(), KainosUI.Tone.Ice);
            _winButtons.LabelFor = (i, l) => i == 1 ? (_console.RttyPopped ? "Dock" : "Pop out") : l;

            _rx = new RichTextBox
            {
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = KainosUI.Bg,
                ForeColor = KainosUI.Text,
                Font = new Font("Consolas", Math.Max(9f, KainosUI.S(14)), FontStyle.Regular, GraphicsUnit.Pixel),
                ScrollBars = RichTextBoxScrollBars.Vertical,
                DetectUrls = false,
                HideSelection = false,
                TabStop = false,
            };
            // double-click a word: their call
            _rx.DoubleClick += (s, e) => { string w = _rx.SelectedText.Trim(); if (w.Length >= 3 && w.Length <= 12) _call.Text = w.ToUpperInvariant(); };

            _type = new TextBox
            {
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = KainosUI.Bg,
                ForeColor = KainosUI.GoldHi,
                Font = new Font("Consolas", Math.Max(9f, KainosUI.S(14)), FontStyle.Regular, GraphicsUnit.Pixel),
                CharacterCasing = CharacterCasing.Upper,
            };
            _type.KeyPress += typeKeyPress;
            _type.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape) { _console.RttyReceive(); e.SuppressKeyPress = true; }
                else if (e.KeyCode >= Keys.F1 && e.KeyCode < Keys.F1 + KainosMacros.Count) { runMacro(e.KeyCode - Keys.F1); e.Handled = e.SuppressKeyPress = true; }
            };

            _call = new TextBox
            {
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = KainosUI.Bg,
                ForeColor = KainosUI.Text,
                Font = new Font("Consolas", Math.Max(9f, KainosUI.S(13)), FontStyle.Bold, GraphicsUnit.Pixel),
                CharacterCasing = CharacterCasing.Upper,
                MaxLength = 12,
            };

            _txButtons = new KainosActionGrid(3);
            _txButtons.Add("TX", () => _console.RttyTransmitting, startTx, KainosUI.Tone.Tx);
            _txButtons.Add("RX", () => false, () => _console.RttyReceive(), KainosUI.Tone.Ice);
            _txButtons.Add("ABORT", () => false, () => _console.RttyAbort(), KainosUI.Tone.Tx);

            // the macros: a click runs one, a right click edits it, F1-F6 in the typing line run them
            _macros = new KainosActionGrid(KainosMacros.Count);
            for (int i = 0; i < KainosMacros.Count; i++)
            {
                int k = i;
                _macros.Add("", () => false, () => runMacro(k), KainosUI.Tone.Gold, () => editMacro(k));
            }
            _macros.LabelFor = (i, l) => i < _console.RttyMacroList.Count ? _console.RttyMacroList[i].Label : l;

            _status = new Label { AutoSize = false, ForeColor = KainosUI.Faint, BackColor = KainosUI.Surface, TextAlign = ContentAlignment.MiddleLeft,
                                  Font = new Font("Segoe UI", Math.Max(8f, KainosUI.S(11)), FontStyle.Regular, GraphicsUnit.Pixel) };
            _status.Text = rttyHint;

            Controls.AddRange(new Control[] { _top, _tune, _sql, _lvl, _winButtons, _rx, _type, _txButtons, _call, _macros, _status });
        }

        private void runMacro(int k)
        {
            if (k < 0 || k >= _console.RttyMacroList.Count) return;
            KainosMacro m = _console.RttyMacroList[k];
            if (m.Text.Length == 0) return;
            if (m.Text.Contains("{CALL}") && _call.Text.Trim().Length == 0) { _status.Text = "Enter their call first (or double-click it in the text)"; return; }
            string t = _console.RttyMacroText(m.Text, _call.Text);
            if (m.Action == KainosMacroAction.Insert)
            {
                if (_console.RttyTransmitting) send(t, false);          // already sending: straight out, like typing it
                else { _type.AppendText(t.Replace("\n", " ")); _type.Focus(); }
                return;
            }
            send(t, m.Action == KainosMacroAction.SendThenReceive);
        }

        private void editMacro(int k)
        {
            if (KainosMacros.Edit(FindForm(), _console.RttyMacroList[k], KainosMacros.RttyDefaults()[k], "RTTY", k))
            {
                _console.RttyMacrosChanged();
                _macros.Invalidate();
            }
        }

        private static double next(double[] list, double v)
        {
            int i = Array.FindIndex(list, x => Math.Abs(x - v) < 0.01);
            return list[(i + 1) % list.Length];
        }

        private void send(string text, bool thenReceive)
        {
            string problem = _console.RttyTransmitting ? null : _console.RttyStartProblem;
            if (problem != null) { _status.Text = problem; return; }
            _console.RttySend(text, thenReceive);
        }

        // TX: send what's typed so far and keep sending what's typed (Esc or RX ends it)
        private void startTx()
        {
            if (_console.RttyTransmitting) return;
            string t = _type.Text;
            _type.Clear();
            send(t, false);
            _type.Focus();
        }

        // Enter starts sending what's typed; while sending each key goes straight out (Enter is a new line)
        private void typeKeyPress(object sender, KeyPressEventArgs e)
        {
            if (_console.RttyTransmitting)
            {
                if (e.KeyChar == '\r') { _console.RttyType('\n'); e.Handled = true; return; }
                if (e.KeyChar == '\b') { e.Handled = true; return; }            // already sent
                _console.RttyType(char.ToUpperInvariant(e.KeyChar));
                e.Handled = true;
                _type.AppendText(char.ToUpperInvariant(e.KeyChar).ToString());
                if (_type.TextLength > 200) _type.Text = _type.Text.Substring(_type.TextLength - 100);
                _type.SelectionStart = _type.TextLength;
                return;
            }
            if (e.KeyChar == '\r') { e.Handled = true; startTx(); }
        }

        private string rttyHint
        {
            get
            {
                double c = _console.RttyCenter, h = _console.RttyShift / 2;
                return string.Format(CultureInfo.InvariantCulture, "Tune so M and S light evenly: the tones {0:0} / {1:0} Hz from the VFO. Double-click a call to copy it; right-click a macro to edit it (F1-F6 send them).", c - h, c + h);
            }
        }

        public void FocusTyping() { if (_type.CanFocus) _type.Focus(); }

        private bool _lastSent;
        public void Append(char c, bool sent)
        {
            if (sent != _lastSent && !(c == '\n' && _rx.TextLength == 0))
            {
                _lastSent = sent;
            }
            _rx.SelectionStart = _rx.TextLength;
            _rx.SelectionLength = 0;
            _rx.SelectionColor = sent ? KainosUI.Tx : KainosUI.Text;
            _rx.AppendText(c.ToString());
            if (_rx.TextLength > 60000) { _rx.Select(0, 20000); _rx.ReadOnly = false; _rx.SelectedText = ""; _rx.ReadOnly = true; }
            _rx.SelectionStart = _rx.TextLength;
            _rx.ScrollToCaret();
        }

        public void Tick()
        {
            _tune.Invalidate();
            _top.Invalidate();
            _txButtons.Invalidate();
            _winButtons.Invalidate();
            if (_console.RttyTransmitting) _status.Text = "Sending" + (_console.RttyQueued > 0 ? " (" + _console.RttyQueued + " to go)" : "") + "   Esc or RX: back to receive when sent";
            else if (!_console.RttyModeOk) _status.Text = "Use DIGL or LSB (or DIGU / USB) for RTTY";
            else if (_status.Text.StartsWith("Sending") || _status.Text.StartsWith("Use DIGL") || _status.Text.StartsWith("Tune so")) _status.Text = rttyHint;
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            if (_top == null) return;
            int pad = KainosUI.S(6), gap = KainosUI.S(6), row = KainosUI.S(26), y = pad, w = ClientSize.Width - pad * 2;

            // settings row
            int topW = KainosUI.S(390), tuneW = KainosUI.S(200), udW = KainosUI.S(150), winW = KainosUI.S(210);
            int x = pad;
            _top.SetBounds(x, y, topW, row); x += topW + gap;
            _tune.SetBounds(x, y, tuneW, row); x += tuneW + gap;
            _sql.SetBounds(x, y, udW, row); x += udW + gap;
            _lvl.SetBounds(x, y, udW, row);
            _winButtons.SetBounds(ClientSize.Width - pad - winW, y, winW, row);
            y += row + gap;

            // bottom rows: status, macros (their call + macro buttons), type-ahead + TX / RX / ABORT
            int statusH = KainosUI.S(18);
            int bottom = ClientSize.Height - pad;
            _status.SetBounds(pad, bottom - statusH, w, statusH); bottom -= statusH + KainosUI.S(2);
            int callW = KainosUI.S(110);
            _call.SetBounds(pad, bottom - row + (row - _call.PreferredHeight) / 2, callW, _call.PreferredHeight);
            _macros.SetBounds(pad + callW + gap, bottom - row, Math.Min(KainosUI.S(504), w - callW - gap), row);
            bottom -= row + gap;
            int txW = KainosUI.S(220);
            _type.SetBounds(pad, bottom - row + (row - _type.PreferredHeight) / 2, w - txW - gap, _type.PreferredHeight);
            _txButtons.SetBounds(ClientSize.Width - pad - txW, bottom - row, txW, row);
            bottom -= row + gap;

            _rx.SetBounds(pad, y, w, Math.Max(KainosUI.S(30), bottom - y));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen p = new Pen(KainosUI.Line)) e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }
    }

    // The tuning indicator: the mark and space levels as two bars, and the decoder's quality; the bars are even and
    // the quality high when the signal sits on the two tones
    internal class KainosRttyTune : Control
    {
        private readonly Console _console;
        private double _m, _s, _q;

        public KainosRttyTune(Console console)
        {
            _console = console;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = KainosUI.Surface;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            RttyDemod d = _console.RttyDemodulator;
            double m = d != null ? d.MarkLevel : 0, s = d != null ? d.SpaceLevel : 0, q = d != null ? d.Quality : 0;
            double peak = Math.Max(1e-9, Math.Max(m, s));
            _m = _m * 0.6 + (m / peak) * 0.4; _s = _s * 0.6 + (s / peak) * 0.4; _q = _q * 0.7 + q * 0.3;
            float pad = KainosUI.S(3), lw = KainosUI.S(14), barH = (Height - pad * 3) / 2f, bw = Width - lw - pad * 2 - KainosUI.S(48);
            using (Font f = new Font("Segoe UI", Math.Max(7f, KainosUI.S(9)), FontStyle.Bold, GraphicsUnit.Pixel))
            using (Brush faint = new SolidBrush(KainosUI.Faint))
            using (Brush line = new SolidBrush(KainosUI.Line))
            using (Brush mark = new SolidBrush(KainosUI.Gold))
            using (Brush space = new SolidBrush(KainosUI.Ice))
            {
                g.DrawString("M", f, faint, 0, pad - 1);
                g.DrawString("S", f, faint, 0, pad * 2 + barH - 1);
                g.FillRectangle(line, lw, pad, bw, barH);
                g.FillRectangle(line, lw, pad * 2 + barH, bw, barH);
                g.FillRectangle(mark, lw, pad, (float)(bw * _m), barH);
                g.FillRectangle(space, lw, pad * 2 + barH, (float)(bw * _s), barH);
            }
            // AFC: how far the tones have been moved from the nominal centre
            if (d != null && d.Afc)
                using (Font f = new Font("Segoe UI", Math.Max(7f, KainosUI.S(9)), FontStyle.Bold, GraphicsUnit.Pixel))
                using (Brush b = new SolidBrush(KainosUI.Dim))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center })
                    g.DrawString((d.Center - d.Nominal).ToString("+0;-0;0", CultureInfo.InvariantCulture) + " Hz", f, b, new RectangleF(0, 0, Width - 2, Height), sf);
            // quality: a thin line under, green-ish gold when good
            Color qc = _q > 0.5 ? KainosUI.GoldHi : _q > 0.25 ? KainosUI.Dim : KainosUI.Faint;
            using (Pen p = new Pen(qc, Math.Max(1f, KainosUI.S(2)))) g.DrawLine(p, lw, Height - 1, lw + (float)(bw * Math.Min(1, _q)), Height - 1);
        }
    }
}
