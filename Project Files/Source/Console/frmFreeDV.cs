/*  frmFreeDV.cs

This file is part of Kainos, a program that implements a Software-Defined Radio.
Kainos is based on Thetis : https://github.com/ramdor/Thetis

RADE (FreeDV Radio Autoencoder) support is ported from Thetis-RADE by
Christos Nikolaou (SV1EIA) : https://github.com/sv1eia/Thetis-RADE
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
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Thetis
{
    // The FreeDV window: RADE on/off, sync, SNR, the last callsign heard, levels and the mic processing.
    // Its settings live on Setup > DSP > FreeDV (RADE); the window reads and writes those controls.
    public class frmFreeDV : Form
    {
        internal static readonly Color kWindowBg = Color.FromArgb(0x08, 0x12, 0x1d);
        internal static readonly Color kTitleBg = Color.FromArgb(0x0f, 0x0f, 0x1a);
        internal static readonly Color kBorder = Color.FromArgb(0x2a, 0x3a, 0x4d);
        internal static readonly Color kPanel = Color.FromArgb(0x0b, 0x17, 0x24);
        internal static readonly Color kGrid = Color.FromArgb(0x1a, 0x2a, 0x3a);
        internal static readonly Color kText = Color.FromArgb(0xc8, 0xd8, 0xe8);
        internal static readonly Color kTextDim = Color.FromArgb(0x5a, 0x6a, 0x7a);
        internal static readonly Color kTextMid = Color.FromArgb(0x8a, 0xa8, 0xc0);
        internal static readonly Color kAmber = Color.FromArgb(0xf2, 0xc1, 0x4e);
        internal static readonly Color kGreen = Color.FromArgb(0x4d, 0xd8, 0x7a);
        internal static readonly Color kRed = Color.FromArgb(0xe0, 0x50, 0x40);
        internal static readonly Color kOrange = Color.FromArgb(0xf0, 0x90, 0x30);
        internal static readonly Color kBlue = Color.FromArgb(0x40, 0xa8, 0xf0);

        private readonly Console _console;
        private readonly Setup _setup;
        private readonly RadeSetupControls _r;
        private readonly AetherToggleButton _btnOn, _btnV1, _btnV2, _btnNoise, _btnAGC, _btnEQ, _btnSettings, _btnReporter, _btnRX1, _btnRX2;
        private readonly Label _status, _footer;
        private readonly RadeStatusPanel _statusPanel;
        private readonly RadeHeardPanel _heard;
        private readonly RadeLevelBar _rxBar, _micBar;
        private readonly AetherKnob _micKnob, _rxKnob;
        private readonly ToolTip _tips;
        private readonly Timer _timer;
        private bool _syncing;
        // the receiver shown (0 = RX1, 1 = RX2), and the last callsign heard on each
        private int _rx;
        private readonly int[] _callSeq = { -1, -1 };
        private readonly string[] _heardCall = { "", "" };
        private readonly DateTime[] _heardAt = new DateTime[2];
        private int _holdStatus;        // ticks to keep a message in the status line

        private ComboBoxTS versionCombo { get { return _rx == 0 ? _r.Version : _r.VersionRX2; } }
        private NumericUpDownTS rxLevel { get { return _rx == 0 ? _r.RxLevel : _r.RxLevelRX2; } }

        public frmFreeDV(Console console)
        {
            _console = console;
            _setup = console.SetupForm;
            _r = _setup.RadeSettings;

            Text = "FreeDV";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            BackColor = kWindowBg;
            ClientSize = new Size(560, 420);
            StartPosition = FormStartPosition.Manual;
            DoubleBuffered = true;
            KeyPreview = true;
            Icon = console.Icon;
            _tips = new ToolTip();

            Label title = new Label
            {
                Text = "FreeDV — RADE",
                ForeColor = kText,
                BackColor = kTitleBg,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold, GraphicsUnit.Pixel),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0),
                Location = new Point(1, 1),
                Size = new Size(ClientSize.Width - 2 - 30, 20)
            };
            title.MouseDown += dragWindow;
            Label close = new Label
            {
                Text = "✕",
                ForeColor = kTextMid,
                BackColor = kTitleBg,
                Font = new Font("Segoe UI", 12f, GraphicsUnit.Pixel),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(ClientSize.Width - 1 - 30, 1),
                Size = new Size(30, 20),
                Cursor = Cursors.Hand
            };
            close.MouseEnter += (s, e) => { close.BackColor = Color.FromArgb(0xcc, 0x20, 0x30); close.ForeColor = Color.White; };
            close.MouseLeave += (s, e) => { close.BackColor = kTitleBg; close.ForeColor = kTextMid; };
            close.Click += (s, e) => Hide();
            Controls.Add(title);
            Controls.Add(close);

            // RADE on/off, protocol, and what it is doing
            _btnOn = new AetherToggleButton { Text = "RADE", Bypass = true, Location = new Point(12, 32), Size = new Size(76, 28) };
            _btnV1 = new AetherToggleButton { Text = "V1", Location = new Point(98, 32), Size = new Size(40, 28) };
            _btnV2 = new AetherToggleButton { Text = "V2", Location = new Point(142, 32), Size = new Size(40, 28) };
            _btnOn.Click += (s, e) => toggleRade();
            _btnV1.Click += (s, e) => { if (!_syncing) versionCombo.SelectedIndex = 0; };
            _btnV2.Click += (s, e) => { if (!_syncing) versionCombo.SelectedIndex = 1; };
            _btnRX1 = new AetherToggleButton { Text = "RX1", Location = new Point(196, 32), Size = new Size(44, 28) };
            _btnRX2 = new AetherToggleButton { Text = "RX2", Location = new Point(244, 32), Size = new Size(44, 28) };
            _btnRX1.Click += (s, e) => showReceiver(0);
            _btnRX2.Click += (s, e) => showReceiver(1);
            _tips.SetToolTip(_btnRX1, "Show and control RADE on RX1.");
            _tips.SetToolTip(_btnRX2, "Show and control RADE on RX2 (RX2 must be on). A VFO B over with RX2 on is sent from RX2's RADE.");
            Controls.Add(_btnRX1);
            Controls.Add(_btnRX2);
            _tips.SetToolTip(_btnOn, "Turn RADE on or off for the receiver shown. On: it changes to DIGU or DIGL, received RADE is decoded to speech,\r\n" +
                                     "and your overs from it are sent as RADE. Off: it goes back to the mode it was in.");
            _tips.SetToolTip(_btnV1, "RADE V1: what most stations use. Both ends must use the same version.");
            _tips.SetToolTip(_btnV2, "RADE V2. Both ends must use the same version. V2 does not send or receive callsigns.");
            _status = new Label
            {
                ForeColor = kTextDim,
                Font = new Font("Segoe UI", 13f, FontStyle.Bold, GraphicsUnit.Pixel),
                TextAlign = ContentAlignment.MiddleRight,
                Location = new Point(296, 34),
                Size = new Size(252, 24)
            };
            Controls.Add(_btnOn);
            Controls.Add(_btnV1);
            Controls.Add(_btnV2);
            Controls.Add(_status);

            _statusPanel = new RadeStatusPanel { Location = new Point(12, 70), Size = new Size(262, 150) };
            _heard = new RadeHeardPanel { Location = new Point(286, 70), Size = new Size(262, 150) };
            _tips.SetToolTip(_statusPanel, "Sync: the decoder has locked on to a RADE signal. SNR: the decoder's estimate of the signal to noise ratio.\r\n" +
                                           "Offset: how far the signal is from your dial frequency.");
            _tips.SetToolTip(_heard, "The callsign in the last end-of-over frame decoded.");
            Controls.Add(_statusPanel);
            Controls.Add(_heard);

            _rxBar = new RadeLevelBar { Label = "RX", Location = new Point(12, 232), Size = new Size(536, 22) };
            _micBar = new RadeLevelBar { Label = "Mic", Location = new Point(12, 260), Size = new Size(536, 22) };
            _tips.SetToolTip(_rxBar, "Level of the received signal into the decoder. Red means it is clipping: turn RX level down.");
            _tips.SetToolTip(_micBar, "Level of your mic into the encoder, while transmitting RADE. Red means it is clipping: turn Mic level down.");
            Controls.Add(_rxBar);
            Controls.Add(_micBar);

            _micKnob = new AetherKnob("Mic", -40, 40, 0, v => (v > 0 ? "+" : "") + v.ToString("0") + " dB") { Location = new Point(12, 296), Size = new Size(76, 76) };
            _rxKnob = new AetherKnob("RX", -40, 40, 0, v => (v > 0 ? "+" : "") + v.ToString("0") + " dB") { Location = new Point(98, 296), Size = new Size(76, 76) };
            _micKnob.ValueChanged += (s, e) => setUpDown(_r.MicLevel, _micKnob.Value);
            _rxKnob.ValueChanged += (s, e) => setUpDown(rxLevel, _rxKnob.Value);
            _tips.SetToolTip(_micKnob, "Mic level into the encoder. Drag or scroll; double-click for 0 dB.");
            _tips.SetToolTip(_rxKnob, "Received signal level into the decoder (the RX1 AF slider sets the speech volume).\r\nDrag or scroll; double-click for 0 dB.");
            Controls.Add(_micKnob);
            Controls.Add(_rxKnob);

            _btnReporter = new AetherToggleButton { Text = "Reporter", Bypass = true, Location = new Point(12, 380), Size = new Size(162, 28) };
            _btnReporter.Click += (s, e) => FreeDVReporter.FreeDVReporterManager.ShowWindow(_console);
            _tips.SetToolTip(_btnReporter, "Open the FreeDV Reporter: who is on RADE right now (qso.freedv.org). Double-click a station to tune to it.\r\n" +
                                           "Lit while connected. Your own station is reported only while RADE is on with\r\n" +
                                           "\"Report my station\" ticked in Settings.");
            Controls.Add(_btnReporter);

            Controls.Add(new AetherBracketLabel { Text = "MIC PROCESSING", Location = new Point(196, 296), Size = new Size(352, 20) });
            _btnNoise = new AetherToggleButton { Text = "Noise", Bypass = true, Location = new Point(196, 326), Size = new Size(84, 28) };
            _btnAGC = new AetherToggleButton { Text = "AGC", Bypass = true, Location = new Point(286, 326), Size = new Size(84, 28) };
            _btnEQ = new AetherToggleButton { Text = "EQ", Bypass = true, Location = new Point(376, 326), Size = new Size(84, 28) };
            _btnSettings = new AetherToggleButton { Text = "Settings", Location = new Point(466, 326), Size = new Size(82, 28) };
            _btnNoise.Click += (s, e) => { if (!_syncing) _r.MicRNNoise.Checked = !_r.MicRNNoise.Checked; };
            _btnAGC.Click += (s, e) => { if (!_syncing) _r.MicAGC.Checked = !_r.MicAGC.Checked; };
            _btnEQ.Click += (s, e) => { if (!_syncing) _r.MicEQ.Checked = !_r.MicEQ.Checked; };
            _btnSettings.Click += (s, e) => _setup.ShowRadeTab();
            _tips.SetToolTip(_btnNoise, "RNNoise noise reduction on your mic before it is encoded.");
            _tips.SetToolTip(_btnAGC, "Loudness AGC with a peak limiter on your mic (target set in Settings).");
            _tips.SetToolTip(_btnEQ, "Three-band mic EQ (set in Settings).");
            _tips.SetToolTip(_btnSettings, "Open Setup > DSP > FreeDV (RADE): callsign, levels, mic AGC target and EQ.");
            Controls.Add(_btnNoise);
            Controls.Add(_btnAGC);
            Controls.Add(_btnEQ);
            Controls.Add(_btnSettings);

            _footer = new Label
            {
                ForeColor = kTextDim,
                Font = new Font("Segoe UI", 11f, GraphicsUnit.Pixel),
                Location = new Point(196, 362),
                Size = new Size(352, 48)
            };
            Controls.Add(_footer);

            foreach (Control c in _r.All)
            {
                if (c is TextBoxTS) ((TextBoxTS)c).TextChanged += setupChanged;
                else if (c is CheckBoxTS) ((CheckBoxTS)c).CheckedChanged += setupChanged;
                else if (c is ComboBoxTS) ((ComboBoxTS)c).SelectedIndexChanged += setupChanged;
                else if (c is NumericUpDownTS) ((NumericUpDownTS)c).ValueChanged += setupChanged;
            }
            _console.RadeEnabledChanged += setupChanged;

            _timer = new Timer { Interval = 50 };
            _timer.Tick += (s, e) => tick();
            showReceiver(!_console.RadeEnabled && _console.RadeRx2Enabled ? 1 : 0);
        }

        // switch the window between RX1 and RX2
        private void showReceiver(int rx)
        {
            _rx = rx;
            _btnRX1.Checked = rx == 0;
            _btnRX2.Checked = rx == 1;
            _heard.SetHeard(_heardCall[rx], _heardAt[rx]);
            _statusPanel.SetState(false, false, 0, 0);
            syncFromSetup();
        }

        private void toggleRade()
        {
            bool on = !_console.RadeEnabledOn(_rx);
            if (!_console.SetRadeEnabled(_rx, on))
            {
                _status.Text = on && _rx == 1 && !_console.RX2Enabled ? "Turn RX2 on first" : "Can't switch while transmitting";
                _status.ForeColor = kOrange;
                _holdStatus = 40;
            }
            syncFromSetup();
        }

        private void setupChanged(object sender, EventArgs e)
        {
            syncFromSetup();
        }

        private void syncFromSetup()
        {
            _syncing = true;
            try
            {
                _btnOn.Checked = _console.RadeEnabledOn(_rx);
                _btnV1.Checked = versionCombo.SelectedIndex != 1;
                _btnV2.Checked = versionCombo.SelectedIndex == 1;
                _btnNoise.Checked = _r.MicRNNoise.Checked;
                _btnAGC.Checked = _r.MicAGC.Checked;
                _btnEQ.Checked = _r.MicEQ.Checked;
                _micKnob.SetValue((double)_r.MicLevel.Value);
                _rxKnob.SetValue((double)rxLevel.Value);
                string call = _r.Callsign.Text.Trim();
                if (call.Length == 0)
                {
                    _footer.Text = "Your callsign isn't set, so other stations can't see who you are. Add it in Settings.";
                    _footer.ForeColor = kAmber;
                }
                else
                {
                    _footer.Text = "Sending " + call + " at the end of each over.\r\nRADE ported from Thetis-RADE by SV1EIA.";
                    _footer.ForeColor = kTextDim;
                }
            }
            finally
            {
                _syncing = false;
            }
        }

        private void setUpDown(NumericUpDownTS ud, double value)
        {
            if (_syncing) return;
            decimal v = (decimal)Math.Round(value);
            v = Math.Max(ud.Minimum, Math.Min(ud.Maximum, v));
            if (ud.Value != v) ud.Value = v;       // Setup's handler applies it
        }

        private void tick()
        {
            int r = _rx;
            bool on = _console.RadeEnabledOn(r), power = _console.PowerOn;
            bool sync = false;
            int snr = 0, rx = -120, mic = -120;
            bool rxClip = false, micClip = false;
            float offset = 0;
            try
            {
                if (on && power)
                {
                    sync = _console.RadeSyncOn(r);
                    snr = _console.RadeSnrDbOn(r);
                    rx = _console.RadeRxLevelDbOn(r);
                    rxClip = _console.RadeRxClipOn(r);
                    offset = _console.RadeFreqOffsetHzOn(r);
                    if (_console.MOX && _console.RadeTxReceiver == r)
                    {
                        mic = _console.RadeMicLevelDb;
                        micClip = _console.RadeMicClip;
                    }
                }
                // callsigns are collected on both receivers, whichever is shown
                for (int i = 0; i < 2; i++)
                {
                    int seq = _console.RadeCallsignSeqOn(i);
                    if (seq == _callSeq[i]) continue;
                    if (_callSeq[i] != -1)
                    {
                        string call = _console.RadeRemoteCallsignOn(i);
                        if (call.Length > 0)
                        {
                            _heardCall[i] = call;
                            _heardAt[i] = DateTime.Now;
                            if (i == r) _heard.SetHeard(call, _heardAt[i]);
                        }
                    }
                    _callSeq[i] = seq;
                }
            }
            catch { }

            _statusPanel.SetState(on && power, sync, snr, offset);
            _rxBar.SetLevel(on && power && !(_console.MOX && _console.RadeTxReceiver == r) ? rx : -120, rxClip);
            _micBar.SetLevel(mic, micClip);
            _heard.Invalidate();

            string text;
            Color color;
            if (!on) { text = "Off"; color = kTextDim; }
            else if (!power) { text = "Radio off"; color = kTextDim; }
            else if (_console.MOX && _console.RadeTxReceiver != r) { text = "Transmitting from RX" + (_console.RadeTxReceiver + 1); color = kTextMid; }
            else if (_console.RadeSendingEoo) { text = "Sending end-of-over"; color = kOrange; }
            else if (_console.MOX) { text = _console.RadeTransmitting ? "Transmitting RADE" : "Transmitting voice"; color = kRed; }
            else if (sync) { text = "Receiving RADE"; color = kGreen; }
            else { text = "Listening"; color = kTextMid; }
            if (_holdStatus > 0) _holdStatus--;
            else
            {
                if (_status.Text != text) _status.Text = text;
                _status.ForeColor = color;
            }
            if (_btnOn.Checked != on) _btnOn.Checked = on;
            bool connected = FreeDVReporter.FreeDVReporterManager.IsConnected;
            if (_btnReporter.Checked != connected) _btnReporter.Checked = connected;
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible) { syncFromSetup(); _timer.Start(); }
            else _timer.Stop();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            base.OnFormClosing(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) Hide();
            base.OnKeyDown(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer.Dispose();
                _console.RadeEnabledChanged -= setupChanged;
                foreach (Control c in _r.All)
                {
                    if (c is TextBoxTS) ((TextBoxTS)c).TextChanged -= setupChanged;
                    else if (c is CheckBoxTS) ((CheckBoxTS)c).CheckedChanged -= setupChanged;
                    else if (c is ComboBoxTS) ((ComboBoxTS)c).SelectedIndexChanged -= setupChanged;
                    else if (c is NumericUpDownTS) ((NumericUpDownTS)c).ValueChanged -= setupChanged;
                }
            }
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen p = new Pen(kBorder))
                e.Graphics.DrawRectangle(p, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        }

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private void dragWindow(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
        }

        internal static GraphicsPath RoundedRect(RectangleF r, float radius)
        {
            float dd = radius * 2f;
            GraphicsPath p = new GraphicsPath();
            p.AddArc(r.X, r.Y, dd, dd, 180, 90);
            p.AddArc(r.Right - dd, r.Y, dd, dd, 270, 90);
            p.AddArc(r.Right - dd, r.Bottom - dd, dd, dd, 0, 90);
            p.AddArc(r.X, r.Bottom - dd, dd, dd, 90, 90);
            p.CloseFigure();
            return p;
        }

        internal static void DrawPanel(Graphics g, Rectangle r)
        {
            using (GraphicsPath path = RoundedRect(new RectangleF(0.5f, 0.5f, r.Width - 1.5f, r.Height - 1.5f), 4f))
            {
                using (Brush b = new SolidBrush(kPanel)) g.FillPath(b, path);
                using (Pen p = new Pen(kBorder)) g.DrawPath(p, path);
            }
        }
    }

    // sync lamp, SNR (big figure and a -10..+40 dB bar) and frequency offset
    internal class RadeStatusPanel : Control
    {
        private bool _active, _sync;
        private double _snr;                 // smoothed for display
        private float _offset;

        public RadeStatusPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = frmFreeDV.kWindowBg;
        }

        public void SetState(bool active, bool sync, int snr, float offset)
        {
            _snr = sync && _active == active ? _snr + 0.3 * (snr - _snr) : snr;
            _active = active;
            _sync = sync;
            _offset = offset;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            frmFreeDV.DrawPanel(g, ClientRectangle);

            // sync lamp
            Color lamp = !_active ? frmFreeDV.kGrid : _sync ? frmFreeDV.kGreen : Color.FromArgb(0x5a, 0x24, 0x20);
            using (Brush b = new SolidBrush(lamp)) g.FillEllipse(b, 14, 14, 16, 16);
            if (_active && _sync)
                using (Pen glow = new Pen(Color.FromArgb(90, frmFreeDV.kGreen), 3f)) g.DrawEllipse(glow, 12, 12, 20, 20);
            using (Font f = new Font("Segoe UI", 13f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Brush b = new SolidBrush(_active && _sync ? frmFreeDV.kGreen : frmFreeDV.kTextMid))
                g.DrawString(_sync && _active ? "SYNC" : "NO SYNC", f, b, 38, 13);

            // SNR figure
            bool show = _active && _sync;
            using (Font f = new Font("Segoe UI", 11f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Brush b = new SolidBrush(frmFreeDV.kTextDim))
                g.DrawString("SNR", f, b, 14, 46);
            using (Font f = new Font("Segoe UI", 34f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Brush b = new SolidBrush(show ? frmFreeDV.kText : frmFreeDV.kTextDim))
                g.DrawString(show ? ((int)Math.Round(_snr)).ToString() : "--", f, b, 10, 58);
            using (Font f = new Font("Segoe UI", 13f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Brush b = new SolidBrush(frmFreeDV.kTextMid))
                g.DrawString("dB", f, b, 92, 78);

            // offset
            using (Font f = new Font("Segoe UI", 11f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Brush b = new SolidBrush(frmFreeDV.kTextDim))
                g.DrawString("OFFSET", f, b, 150, 46);
            using (Font f = new Font("Segoe UI", 18f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Brush b = new SolidBrush(show ? frmFreeDV.kText : frmFreeDV.kTextDim))
                g.DrawString(show ? (_offset >= 0 ? "+" : "") + _offset.ToString("0.0") + " Hz" : "--", f, b, 148, 66);

            // SNR bar, -10 .. +40 dB: red below 0, amber to 6, green above
            Rectangle bar = new Rectangle(14, 112, Width - 28, 10);
            using (Brush b = new SolidBrush(frmFreeDV.kGrid)) g.FillRectangle(b, bar);
            if (show)
            {
                double n = Math.Max(0, Math.Min(1, (_snr + 10) / 50.0));
                Color c = _snr < 0 ? frmFreeDV.kRed : _snr < 6 ? frmFreeDV.kAmber : frmFreeDV.kGreen;
                using (Brush b = new SolidBrush(c)) g.FillRectangle(b, bar.X, bar.Y, (float)(bar.Width * n), bar.Height);
            }
            using (Font f = new Font("Segoe UI", 9f, GraphicsUnit.Pixel))
            using (Brush b = new SolidBrush(frmFreeDV.kTextDim))
            using (Pen tick = new Pen(frmFreeDV.kTextDim))
            {
                for (int db = -10; db <= 40; db += 10)
                {
                    float x = bar.X + bar.Width * (db + 10) / 50f;
                    g.DrawLine(tick, x, bar.Bottom, x, bar.Bottom + 3);
                    string t = db.ToString();
                    SizeF sz = g.MeasureString(t, f);
                    g.DrawString(t, f, b, Math.Max(bar.X - 2, Math.Min(bar.Right - sz.Width + 2, x - sz.Width / 2)), bar.Bottom + 4);
                }
            }
        }
    }

    // the callsign from the last end-of-over frame decoded, and when it was heard
    internal class RadeHeardPanel : Control
    {
        private string _call = "";
        private DateTime _at;

        public RadeHeardPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = frmFreeDV.kWindowBg;
        }

        public void SetHeard(string call, DateTime at)
        {
            _call = call;
            _at = at;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            frmFreeDV.DrawPanel(g, ClientRectangle);
            using (Font f = new Font("Segoe UI", 11f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Brush b = new SolidBrush(frmFreeDV.kTextDim))
                g.DrawString("LAST HEARD", f, b, 14, 14);

            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                if (_call.Length == 0)
                {
                    using (Font f = new Font("Segoe UI", 13f, GraphicsUnit.Pixel))
                    using (Brush b = new SolidBrush(frmFreeDV.kTextDim))
                        g.DrawString("No callsign decoded yet", f, b, new RectangleF(0, 40, Width, 60), sf);
                    return;
                }
                float px = 40f;
                Font big = new Font("Arial Black", px, FontStyle.Regular, GraphicsUnit.Pixel);
                SizeF size = g.MeasureString(_call, big);
                if (size.Width > Width - 24)
                {
                    big.Dispose();
                    big = new Font("Arial Black", Math.Max(12f, px * (Width - 24) / size.Width), FontStyle.Regular, GraphicsUnit.Pixel);
                }
                using (big)
                using (Brush b = new SolidBrush(frmFreeDV.kAmber))
                    g.DrawString(_call, big, b, new RectangleF(0, 36, Width, 66), sf);

                TimeSpan ago = DateTime.Now - _at;
                string when = _at.ToString("HH:mm") + "  ·  " +
                    (ago.TotalSeconds < 60 ? "just now" : ago.TotalMinutes < 60 ? (int)ago.TotalMinutes + " min ago" : (int)ago.TotalHours + " h ago");
                using (Font f = new Font("Segoe UI", 12f, GraphicsUnit.Pixel))
                using (Brush b = new SolidBrush(frmFreeDV.kTextMid))
                    g.DrawString(when, f, b, new RectangleF(0, 104, Width, 24), sf);
            }
        }
    }

    // a horizontal level meter, -60 .. 0 dBFS, with a held peak; red while the source reports clipping
    internal class RadeLevelBar : Control
    {
        public string Label = "";
        private double _db = -120, _peak = -120;
        private bool _clip;

        public RadeLevelBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = frmFreeDV.kWindowBg;
        }

        public void SetLevel(int db, bool clip)
        {
            _db = db;
            _peak = db >= _peak ? db : Math.Max(db, _peak - 0.6);
            _clip = clip;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using (Font f = new Font("Segoe UI", 11f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Brush b = new SolidBrush(frmFreeDV.kTextMid))
                g.DrawString(Label, f, b, 0, 3);
            Rectangle bar = new Rectangle(36, 4, Width - 36 - 64, Height - 8);
            using (Brush b = new SolidBrush(frmFreeDV.kGrid)) g.FillRectangle(b, bar);
            Func<double, float> X = db => bar.X + bar.Width * (float)Math.Max(0, Math.Min(1, (db + 60) / 60.0));
            Color c = _clip ? frmFreeDV.kRed : frmFreeDV.kBlue;
            if (_db > -60)
                using (Brush b = new SolidBrush(c)) g.FillRectangle(b, bar.X, bar.Y, X(_db) - bar.X, bar.Height);
            if (_peak > -60)
                using (Pen p = new Pen(_clip ? frmFreeDV.kRed : frmFreeDV.kText, 2f)) g.DrawLine(p, X(_peak), bar.Y, X(_peak), bar.Bottom);
            using (Pen tick = new Pen(frmFreeDV.kWindowBg))
                for (int db = -50; db < 0; db += 10) g.DrawLine(tick, X(db), bar.Y, X(db), bar.Bottom);
            string text = _clip ? "CLIP" : _db > -120 ? ((int)Math.Round(_db)).ToString() + " dB" : "--";
            using (Font f = new Font("Segoe UI", 11f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Brush b = new SolidBrush(_clip ? frmFreeDV.kRed : frmFreeDV.kTextMid))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Far })
                g.DrawString(text, f, b, new RectangleF(bar.Right, 3, 62, Height), sf);
        }
    }
}
