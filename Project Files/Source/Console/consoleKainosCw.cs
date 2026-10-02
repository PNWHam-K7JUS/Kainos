/*  consoleKainosCw.cs

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
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Thetis
{
    // The CW terminal (the CW menu item, beside CWX and RTTY), laid out like the RTTY one: received text from the
    // decoder (KainosCw.cs) on a copy of the chosen receiver's audio at Thetis's CW pitch, and sending through CWX
    // (its remote-message path, the one CAT's KY and TCI use: CWX keys the radio and times the elements), with a
    // type-ahead line and macros.
    public partial class Console
    {
        private ToolStripMenuItem cwToolStripMenuItem;
        private KainosCwPane _cwPane;
        private Form _cwWindow;
        private bool _cwOpen, _cwPopped;

        // saved with the options (a hidden Setup box): "rx=0;sql=20;pop=0"
        public string KainosCwSettings = "";

        private Thread _cwThread;
        private volatile bool _cwRun;
        private CwDecoder _cwDecoder;
        private readonly ConcurrentQueue<KeyValuePair<char, bool>> _cwOut = new ConcurrentQueue<KeyValuePair<char, bool>>();     // char, sent
        private readonly ConcurrentQueue<char> _cwEcho = new ConcurrentQueue<char>();           // sent to CWX, echoed as CWX starts each
        private System.Windows.Forms.Timer _cwUiTimer;
        private bool _cwEchoHooked;

        internal int CwRx = 0;
        internal readonly NumericUpDown CwSql = new NumericUpDown { Minimum = 0, Maximum = 100, Increment = 5, Value = 20 };
        internal readonly NumericUpDown CwWpm = new NumericUpDown { Minimum = 5, Maximum = 60, Increment = 1, Value = 20 };

        private void addCwControls()
        {
            cwToolStripMenuItem = new ToolStripMenuItem("CW") { Name = "cwToolStripMenuItem" };
            cwToolStripMenuItem.Click += (s, e) => { if (_cwOpen) CwClose(); else CwOpen(); };
            menuStrip1.Items.Insert(menuStrip1.Items.IndexOf(cWXToolStripMenuItem) + 1, cwToolStripMenuItem);
            CwSql.ValueChanged += (s, e) => cwSave();
            CwWpm.ValueChanged += (s, e) => { try { if (CWXForm.WPM != (int)CwWpm.Value) CWXForm.WPM = (int)CwWpm.Value; } catch { } };
        }

        internal void CwLoadSettings()
        {
            foreach (string kv in (KainosCwSettings ?? "").Split(';'))
            {
                string[] p = kv.Split('=');
                if (p.Length != 2) continue;
                double d;
                bool num = double.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out d);
                switch (p[0])
                {
                    case "rx": if (num) CwRx = d >= 1 ? 1 : 0; break;
                    case "sql": if (num) CwSql.Value = (decimal)Math.Max(0, Math.Min(100, d)); break;
                    case "pop": _cwPopped = p[1] == "1"; break;
                }
            }
        }

        private void cwSave()
        {
            KainosCwSettings = string.Format(CultureInfo.InvariantCulture, "rx={0};sql={1};pop={2}", CwRx, CwSql.Value, _cwPopped ? 1 : 0);
            KainosSettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        internal void CwChanged() { cwSave(); if (_cwPane != null) _cwPane.Invalidate(true); }

        // ---- opening, docking, popping out ----

        internal void CwOpen()
        {
            if (_cwPane == null)
            {
                _cwPane = new KainosCwPane(this);
                _cwUiTimer = new System.Windows.Forms.Timer { Interval = 80 };
                _cwUiTimer.Tick += (s, e) => cwUiTick();
            }
            if (_rttyOpen) RttyClose();     // one terminal at a time (they share the receive tap)
            if (!_cwEchoHooked)
            {
                _cwEchoHooked = true;
                // CWX starts a character from its remote queue: echo the next one we gave it
                CWXRemoteCharacterStartedHandlers += (remaining, pending) =>
                {
                    char c;
                    if (_cwEcho.TryDequeue(out c)) _cwOut.Enqueue(new KeyValuePair<char, bool>(c, true));
                };
            }
            try { CwWpm.Value = Math.Max(CwWpm.Minimum, Math.Min(CwWpm.Maximum, CWXForm.WPM)); } catch { }
            _cwOpen = true;
            cwToolStripMenuItem.Checked = true;
            cwPlace();
            cwStartEngine();
            _cwUiTimer.Start();
            _cwPane.FocusTyping();
        }

        internal void CwClose()
        {
            _cwOpen = false;
            cwToolStripMenuItem.Checked = false;
            cwStopEngine();
            if (_cwUiTimer != null) _cwUiTimer.Stop();
            cwPlace();
        }

        internal bool CwPopped { get { return _cwPopped || !_kainosLayout; } }

        internal void CwTogglePop()
        {
            _cwPopped = !_cwPopped;
            cwSave();
            cwPlace();
        }

        private void cwPlace() { _cwWindow = kainosTermPlace(_cwPane, _cwWindow, "Kainos CW", _cwOpen, CwPopped, CwClose); }

        // ---- receiving ----

        internal CwDecoder CwDecoderNow { get { return _cwDecoder; } }
        internal int CwPitchHz { get { return CWPitch; } }

        internal bool CwModeOk
        {
            get
            {
                DSPMode m = CwRx == 0 ? _rx1_dsp_mode : _rx2_dsp_mode;
                return m == DSPMode.CWL || m == DSPMode.CWU;
            }
        }

        private int _cwTapRx = -1;
        private void cwStartEngine()
        {
            if (_cwRun) return;
            _cwRun = true;
            _cwThread = new Thread(cwWork) { IsBackground = true, Name = "Kainos CW", Priority = ThreadPriority.AboveNormal };
            _cwThread.Start();
        }

        private void cwStopEngine()
        {
            _cwRun = false;
            if (_cwThread != null) _cwThread.Join(500);
            _cwThread = null;
            try { if (_cwTapRx >= 0) KDigi.KDigiRxTap(_cwTapRx, 0); } catch { }
            _cwTapRx = -1;
        }

        private void cwWork()
        {
            float[] buf = new float[16384];
            int rate0 = 0;
            while (_cwRun)
            {
                try
                {
                    int rx = CwRx;
                    if (rx != _cwTapRx)
                    {
                        if (_cwTapRx >= 0) KDigi.KDigiRxTap(_cwTapRx, 0);
                        KDigi.KDigiRxTap(rx, 1);
                        _cwTapRx = rx;
                    }
                    int rate = KDigi.KDigiRxRate(rx);
                    if (rate > 0 && rate != rate0)
                    {
                        rate0 = rate;
                        _cwDecoder = new CwDecoder(rate);
                        _cwDecoder.Decoded += c => _cwOut.Enqueue(new KeyValuePair<char, bool>(c, false));
                    }
                    if (_cwDecoder != null)
                    {
                        _cwDecoder.Pitch = CWPitch;
                        _cwDecoder.Squelch = (double)CwSql.Value / 100.0;
                    }
                    int n;
                    while ((n = KDigi.KDigiRxRead(rx, buf, buf.Length)) > 0)
                        if (_cwDecoder != null && !_mox) _cwDecoder.Process(buf, n);
                }
                catch (Exception) { }
                Thread.Sleep(20);
            }
        }

        // ---- sending (through CWX) ----

        internal bool CwSending
        {
            get
            {
                if (m_frmCWXForm == null || m_frmCWXForm.IsDisposed) return false;
                return m_frmCWXForm.PendingRemoteCharacters > 0 || m_frmCWXForm.Characters2Send > 0;
            }
        }

        internal int CwQueued { get { return _cwEcho.Count; } }

        internal string CwStartProblem
        {
            get
            {
                if (!PowerOn) return "The radio is off";
                if (!CwModeOk) return "Use CWL or CWU to send CW";
                return null;
            }
        }

        internal void CwSend(string text)
        {
            if (CwStartProblem != null || string.IsNullOrEmpty(text)) return;
            text = text.ToUpperInvariant();
            foreach (char c in text) _cwEcho.Enqueue(c);
            CWXForm.RemoteMessage(Encoding.ASCII.GetBytes(text));
        }

        // stop what's queued after the character being sent, or everything at once
        internal void CwStopQueue()
        {
            try { CWXForm.CWXStop(); } catch { }
            while (_cwEcho.TryDequeue(out char _)) { }
        }

        internal void CwAbort()
        {
            try { CWXForm.AbortSending(); } catch { }
            while (_cwEcho.TryDequeue(out char _)) { }
            _cwOut.Enqueue(new KeyValuePair<char, bool>('\n', true));
        }

        private bool _cwWasSending;
        private void cwUiTick()
        {
            if (_cwPane == null) return;
            KeyValuePair<char, bool> kv;
            int guard = 0;
            while (guard++ < 2000 && _cwOut.TryDequeue(out kv)) _cwPane.Append(kv.Key, kv.Value);
            bool sending = CwSending;
            if (_cwWasSending && !sending) _cwPane.Append('\n', true);      // a new line after what was sent
            _cwWasSending = sending;
            _cwPane.Tick();
        }

        internal string CwMacroText(string m, string theirCall)
        {
            string my = (RadeCallsign ?? "").Trim().ToUpperInvariant();
            string call = (theirCall ?? "").Trim().ToUpperInvariant();
            return m.Replace("{MY}", my).Replace("{CALL}", call);
        }
    }

    // The terminal: settings row, received text, the type-ahead line with TX / STOP / ABORT, and the macro row
    internal class KainosCwPane : Panel, IKainosTerminal
    {
        private readonly Console _console;
        private readonly RichTextBox _rx;
        private readonly TextBox _type, _call;
        private readonly KainosActionGrid _top, _txButtons, _macros, _winButtons;
        private readonly KainosCwTune _tune;
        private readonly KainosUpDown _sql, _wpm;
        private readonly Label _status;

        public KainosCwPane(Console console)
        {
            _console = console;
            BackColor = KainosUI.Surface;
            Name = "kainosCwPane";

            _top = new KainosActionGrid(2);
            _top.Add("RX1", () => _console.CwRx == 0, () => { _console.CwRx = 0; _console.CwChanged(); }, KainosUI.Tone.Gold);
            _top.Add("RX2", () => _console.CwRx == 1, () => { _console.CwRx = 1; _console.CwChanged(); }, KainosUI.Tone.Violet);

            _tune = new KainosCwTune(console);
            _sql = new KainosUpDown(console.CwSql, "SQL", "", false);
            _wpm = new KainosUpDown(console.CwWpm, "TX", "WPM", false);

            _winButtons = new KainosActionGrid(3);
            _winButtons.Add("Clear", () => false, () => _rx.Clear(), KainosUI.Tone.Ice);
            _winButtons.Add("Pop out", () => false, () => _console.CwTogglePop(), KainosUI.Tone.Ice);
            _winButtons.Add("Close", () => false, () => _console.CwClose(), KainosUI.Tone.Ice);
            _winButtons.LabelFor = (i, l) => i == 1 ? (_console.CwPopped ? "Dock" : "Pop out") : l;

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
            _type.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) { _console.CwStopQueue(); e.SuppressKeyPress = true; } };

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
            _txButtons.Add("TX", () => _console.CwSending, startTx, KainosUI.Tone.Tx);
            _txButtons.Add("STOP", () => false, () => _console.CwStopQueue(), KainosUI.Tone.Ice);
            _txButtons.Add("ABORT", () => false, () => _console.CwAbort(), KainosUI.Tone.Tx);

            _macros = new KainosActionGrid(5);
            macro("CQ", "CQ CQ CQ DE {MY} {MY} K ");
            macro("ANS", "{CALL} DE {MY} {MY} K ");
            macro("599", "{CALL} TU UR 5NN 5NN BK ");
            macro("73", "{CALL} TU 73 DE {MY} SK ");
            macro("MY", "{MY} ");

            _status = new Label { AutoSize = false, ForeColor = KainosUI.Faint, BackColor = KainosUI.Surface, TextAlign = ContentAlignment.MiddleLeft,
                                  Font = new Font("Segoe UI", Math.Max(8f, KainosUI.S(11)), FontStyle.Regular, GraphicsUnit.Pixel) };
            _status.Text = hint;

            Controls.AddRange(new Control[] { _top, _tune, _sql, _wpm, _winButtons, _rx, _type, _txButtons, _call, _macros, _status });
        }

        private string hint
        {
            get { return "Zero-beat the signal (0 Beat) so it sits at your CW pitch, " + _console.CwPitchHz + " Hz. Double-click a call to copy it."; }
        }

        private void macro(string label, string text)
        {
            _macros.Add(label, () => false, () =>
            {
                if (text.Contains("{CALL}") && _call.Text.Trim().Length == 0) { _status.Text = "Enter their call first (or double-click it in the text)"; return; }
                send(_console.CwMacroText(text, _call.Text));
            }, label == "MY" ? KainosUI.Tone.Ice : KainosUI.Tone.Gold);
        }

        private void send(string text)
        {
            string problem = _console.CwStartProblem;
            if (problem != null) { _status.Text = problem; return; }
            _console.CwSend(text);
        }

        // TX: send what's typed; Enter does the same. While CW is going out each key is sent as typed.
        private void startTx()
        {
            string t = _type.Text;
            _type.Clear();
            if (t.Length > 0) send(t + " ");
            _type.Focus();
        }

        private void typeKeyPress(object sender, KeyPressEventArgs e)
        {
            if (_console.CwSending)
            {
                if (e.KeyChar == '\r') { e.Handled = true; return; }
                if (e.KeyChar == '\b') { e.Handled = true; return; }            // already sent
                char c = char.ToUpperInvariant(e.KeyChar);
                send(c.ToString());
                e.Handled = true;
                _type.AppendText(c.ToString());
                if (_type.TextLength > 200) _type.Text = _type.Text.Substring(_type.TextLength - 100);
                _type.SelectionStart = _type.TextLength;
                return;
            }
            if (e.KeyChar == '\r') { e.Handled = true; startTx(); }
        }

        public void FocusTyping() { if (_type.CanFocus) _type.Focus(); }

        public void Append(char c, bool sent)
        {
            if (c == '\n' && (_rx.TextLength == 0 || _rx.Text[_rx.TextLength - 1] == '\n')) return;
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
            _wpm.Invalidate();
            if (_console.CwSending) _status.Text = "Sending" + (_console.CwQueued > 0 ? " (" + _console.CwQueued + " to go)" : "") + "   Esc or STOP: stop after this character";
            else if (_status.Text.StartsWith("Sending")) _status.Text = hint;
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            if (_top == null) return;
            int pad = KainosUI.S(6), gap = KainosUI.S(6), row = KainosUI.S(26), y = pad, w = ClientSize.Width - pad * 2;

            int topW = KainosUI.S(110), tuneW = KainosUI.S(260), udW = KainosUI.S(150), winW = KainosUI.S(210);
            int x = pad;
            _top.SetBounds(x, y, topW, row); x += topW + gap;
            _tune.SetBounds(x, y, tuneW, row); x += tuneW + gap;
            _sql.SetBounds(x, y, udW, row); x += udW + gap;
            _wpm.SetBounds(x, y, udW + KainosUI.S(20), row);
            _winButtons.SetBounds(ClientSize.Width - pad - winW, y, winW, row);
            y += row + gap;

            int statusH = KainosUI.S(18);
            int bottom = ClientSize.Height - pad;
            _status.SetBounds(pad, bottom - statusH, w, statusH); bottom -= statusH + KainosUI.S(2);
            int callW = KainosUI.S(110);
            _call.SetBounds(pad, bottom - row + (row - _call.PreferredHeight) / 2, callW, _call.PreferredHeight);
            _macros.SetBounds(pad + callW + gap, bottom - row, Math.Min(KainosUI.S(420), w - callW - gap), row);
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

    // The CW indicator: a key light, the tone's level against the decoder's threshold, the speed it hears and the pitch
    internal class KainosCwTune : Control
    {
        private readonly Console _console;
        private double _lvl;

        public KainosCwTune(Console console)
        {
            _console = console;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = KainosUI.Surface;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            CwDecoder d = _console.CwDecoderNow;
            bool key = d != null && d.KeyDown;
            _lvl = _lvl * 0.5 + (d != null ? d.Level : 0) * 0.5;
            float h = Height, led = KainosUI.S(12);
            using (Brush b = new SolidBrush(key ? KainosUI.Gold : KainosUI.Line)) g.FillEllipse(b, 2, (h - led) / 2, led, led);
            float x = led + KainosUI.S(8), textW = KainosUI.S(118), bw = Width - x - textW - KainosUI.S(4), barH = KainosUI.S(6);
            using (Brush line = new SolidBrush(KainosUI.Line)) g.FillRectangle(line, x, (h - barH) / 2, bw, barH);
            using (Brush lv = new SolidBrush(key ? KainosUI.Gold : KainosUI.Ice)) g.FillRectangle(lv, x, (h - barH) / 2, (float)(bw * Math.Min(1, _lvl)), barH);
            string t = (d != null ? d.Wpm.ToString("0") : "--") + " WPM  " + _console.CwPitchHz + " Hz";
            using (Font f = new Font("Segoe UI", Math.Max(8f, KainosUI.S(11)), FontStyle.Bold, GraphicsUnit.Pixel))
            using (Brush b = new SolidBrush(KainosUI.Dim))
            using (StringFormat sf = new StringFormat { LineAlignment = StringAlignment.Center, Alignment = StringAlignment.Far })
                g.DrawString(t, f, b, new RectangleF(Width - textW, 0, textW, h), sf);
        }
    }
}
