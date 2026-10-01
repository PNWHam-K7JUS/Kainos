/*  frmAetherVoice.cs

This file is part of Kainos, a program that implements a Software-Defined Radio.

The AetherVoice window recreates the AetherSDR AetherVoice/PooDoo editor (src/gui/ClientPuduEditor,
PooDooLogo and ClientCompKnob, https://github.com/aethersdr/AetherSDR, commit 20d022d5) in
Windows Forms, by the AetherSDR contributors and Justin Cron K7JUS, 2026.

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
    // The AetherVoice window. It has no settings of its own: every control reads and writes the
    // matching control on Setup > DSP > AetherVoice, so the two always agree and save the same way.
    public class frmAetherVoice : Form
    {
        private static readonly Color kWindowBg = Color.FromArgb(0x08, 0x12, 0x1d);
        private static readonly Color kTitleBg = Color.FromArgb(0x0f, 0x0f, 0x1a);
        private static readonly Color kBorder = Color.FromArgb(0x2a, 0x3a, 0x4d);
        private static readonly Color kTextPrimary = Color.FromArgb(0xc8, 0xd8, 0xe8);
        private static readonly Color kTextDim = Color.FromArgb(0x5a, 0x6a, 0x7a);
        private static readonly Color kAmber = Color.FromArgb(0xf2, 0xc1, 0x4e);

        private readonly Console _console;
        private readonly Setup _setup;
        private readonly AetherVoiceLogo _logo;
        private readonly AetherToggleButton _btnOn, _btnEven, _btnOdd;
        private readonly AetherKnob _bodyDrive, _bodyTune, _bodyMix, _clarityTune, _clarityAir, _clarityMix;
        private readonly Label _status;
        private readonly Timer _timer;
        private bool _syncing;

        public frmAetherVoice(Console console)
        {
            _console = console;
            _setup = console.SetupForm;

            Text = "AetherVoice - RX";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            BackColor = kWindowBg;
            ClientSize = new Size(640, 300);
            StartPosition = FormStartPosition.Manual;
            DoubleBuffered = true;
            KeyPreview = true;
            Icon = console.Icon;

            // title bar: drag to move, x to close
            Label title = new Label
            {
                Text = "AetherVoice — RX",
                ForeColor = kTextPrimary,
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
                ForeColor = Color.FromArgb(0x8a, 0xa8, 0xc0),
                BackColor = kTitleBg,
                Font = new Font("Segoe UI", 12f, GraphicsUnit.Pixel),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(ClientSize.Width - 1 - 30, 1),
                Size = new Size(30, 20),
                Cursor = Cursors.Hand
            };
            close.MouseEnter += (s, e) => { close.BackColor = Color.FromArgb(0xcc, 0x20, 0x30); close.ForeColor = Color.White; };
            close.MouseLeave += (s, e) => { close.BackColor = kTitleBg; close.ForeColor = Color.FromArgb(0x8a, 0xa8, 0xc0); };
            close.Click += (s, e) => Hide();
            Controls.Add(title);
            Controls.Add(close);

            _logo = new AetherVoiceLogo { Location = new Point(12, 28), Size = new Size(616, 80) };
            _logo.MouseDown += dragWindow;
            Controls.Add(_logo);

            // ON at the left; Even / Odd centred like AetherSDR's mode row
            _btnOn = new AetherToggleButton { Text = "ON", Bypass = true, Location = new Point(12, 118), Size = new Size(54, 24) };
            _btnEven = new AetherToggleButton { Text = "Even", Location = new Point(256, 118), Size = new Size(62, 24) };
            _btnOdd = new AetherToggleButton { Text = "Odd", Location = new Point(324, 118), Size = new Size(62, 24) };
            ToolTip tips = new ToolTip();
            tips.SetToolTip(_btnOn, "Turn AetherVoice on or off for every receiver (voice modes only).");
            tips.SetToolTip(_btnEven, "Aphex-style asymmetric shaping: mostly even harmonics, warmer, with Big Bottom low-end saturation.");
            tips.SetToolTip(_btnOdd, "Behringer-style symmetric shaping: odd harmonics, brighter and edgier, with a low-end compressor.");
            _btnOn.Click += (s, e) => { if (!_syncing) _setup.AetherVoiceRXEnable.Checked = !_setup.AetherVoiceRXEnable.Checked; };
            _btnEven.Click += (s, e) => { if (!_syncing) _setup.AetherVoiceRXMode.SelectedIndex = 0; };
            _btnOdd.Click += (s, e) => { if (!_syncing) _setup.AetherVoiceRXMode.SelectedIndex = 1; };
            Controls.Add(_btnOn);
            Controls.Add(_btnEven);
            Controls.Add(_btnOdd);

            // bracket labels and knobs: Body | gap | Clarity (AetherSDR's Poo | Doo)
            const int knobY = 176, k = 76, gap = 12;
            int x0 = 40, x4 = 40 + 3 * (k + gap) + 32;
            Controls.Add(new AetherBracketLabel { Text = "BODY", Location = new Point(x0, 150), Size = new Size(3 * k + 2 * gap, 20) });
            Controls.Add(new AetherBracketLabel { Text = "CLARITY", Location = new Point(x4, 150), Size = new Size(3 * k + 2 * gap, 20) });

            _bodyDrive = new AetherKnob("Drive", 0, 24, 6, v => v.ToString("0.0") + " dB");
            _bodyTune = new AetherKnob("Tune", 50, 160, 100, v => v.ToString("0") + " Hz");
            _bodyMix = new AetherKnob("Mix", 0, 1, 0.3, v => ((int)(v * 100 + 0.5)).ToString() + " %");
            _clarityTune = new AetherKnob("Tune", 1000, 10000, 5000, v => v >= 1000 ? (v / 1000).ToString("0.0") + " kHz" : v.ToString("0") + " Hz")
            {
                // logarithmic, as in AetherSDR: 1 kHz .. 10 kHz across the sweep
                ToNorm = v => Math.Log10(Math.Max(1000, v) / 1000.0),
                FromNorm = n => 1000.0 * Math.Pow(10, n)
            };
            _clarityAir = new AetherKnob("Air", 0, 24, 6, v => v.ToString("0.0") + " dB");
            _clarityMix = new AetherKnob("Mix", 0, 1, 0.3, v => ((int)(v * 100 + 0.5)).ToString() + " %");
            AetherKnob[] knobs = { _bodyDrive, _bodyTune, _bodyMix, _clarityTune, _clarityAir, _clarityMix };
            int[] xs = { x0, x0 + k + gap, x0 + 2 * (k + gap), x4, x4 + k + gap, x4 + 2 * (k + gap) };
            for (int i = 0; i < knobs.Length; i++)
            {
                knobs[i].Location = new Point(xs[i], knobY);
                knobs[i].Size = new Size(k, k);
                Controls.Add(knobs[i]);
            }
            tips.SetToolTip(_bodyDrive, "Body drive: saturation depth (Even) or compression (Odd) of the low band.");
            tips.SetToolTip(_bodyTune, "Body tune: corner frequency of the low band.");
            tips.SetToolTip(_bodyMix, "Body mix: how much of the processed low band is added.");
            tips.SetToolTip(_clarityTune, "Clarity tune: harmonics are generated above this frequency.");
            tips.SetToolTip(_clarityAir, "Air: how hard the high band is driven into the harmonic generator (Harmonics in Setup).");
            tips.SetToolTip(_clarityMix, "Clarity mix: how much of the generated harmonics is added.");

            _bodyDrive.ValueChanged += (s, e) => setUpDown(_setup.AetherVoiceRXBodyDrive, _bodyDrive.Value, 1);
            _bodyTune.ValueChanged += (s, e) => setUpDown(_setup.AetherVoiceRXBodyTune, _bodyTune.Value, 0);
            _bodyMix.ValueChanged += (s, e) => setUpDown(_setup.AetherVoiceRXBodyMix, _bodyMix.Value * 100, 0);
            _clarityTune.ValueChanged += (s, e) => setUpDown(_setup.AetherVoiceRXClarityTune, _clarityTune.Value, 0);
            _clarityAir.ValueChanged += (s, e) => setUpDown(_setup.AetherVoiceRXClarityHarmonics, _clarityAir.Value, 1);
            _clarityMix.ValueChanged += (s, e) => setUpDown(_setup.AetherVoiceRXClarityMix, _clarityMix.Value * 100, 0);

            Label hint = new Label
            {
                Text = "Drag or scroll a knob to adjust  ·  Shift for fine control  ·  Double-click to reset",
                ForeColor = kTextDim,
                Font = new Font("Segoe UI", 11f, GraphicsUnit.Pixel),
                AutoSize = true,
                Location = new Point(12, 272)
            };
            _status = new Label
            {
                ForeColor = kTextDim,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold, GraphicsUnit.Pixel),
                TextAlign = ContentAlignment.MiddleRight,
                Location = new Point(400, 270),
                Size = new Size(228, 18)
            };
            Controls.Add(hint);
            Controls.Add(_status);

            // follow Setup, which may also be changed from Setup itself or the console AV button
            _setup.AetherVoiceRXEnable.CheckedChanged += setupChanged;
            _setup.AetherVoiceRXMode.SelectedIndexChanged += setupChanged;
            foreach (NumericUpDownTS ud in new[] { _setup.AetherVoiceRXBodyDrive, _setup.AetherVoiceRXBodyTune, _setup.AetherVoiceRXBodyMix,
                _setup.AetherVoiceRXClarityTune, _setup.AetherVoiceRXClarityHarmonics, _setup.AetherVoiceRXClarityMix })
                ud.ValueChanged += setupChanged;

            _timer = new Timer { Interval = 16 };     // AetherSDR's panel tick
            _timer.Tick += (s, e) => tick();

            syncFromSetup();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _timer.Start();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible) { syncFromSetup(); _timer.Start(); }
            else _timer.Stop();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // keep the window (and its position) for next time
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
                _setup.AetherVoiceRXEnable.CheckedChanged -= setupChanged;
                _setup.AetherVoiceRXMode.SelectedIndexChanged -= setupChanged;
                foreach (NumericUpDownTS ud in new[] { _setup.AetherVoiceRXBodyDrive, _setup.AetherVoiceRXBodyTune, _setup.AetherVoiceRXBodyMix,
                    _setup.AetherVoiceRXClarityTune, _setup.AetherVoiceRXClarityHarmonics, _setup.AetherVoiceRXClarityMix })
                    ud.ValueChanged -= setupChanged;
            }
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen p = new Pen(kBorder))
                e.Graphics.DrawRectangle(p, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
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
                _btnOn.Checked = _setup.AetherVoiceRXEnable.Checked;
                _btnEven.Checked = _setup.AetherVoiceRXMode.SelectedIndex != 1;
                _btnOdd.Checked = _setup.AetherVoiceRXMode.SelectedIndex == 1;
                _bodyDrive.SetValue((double)_setup.AetherVoiceRXBodyDrive.Value);
                _bodyTune.SetValue((double)_setup.AetherVoiceRXBodyTune.Value);
                _bodyMix.SetValue((double)_setup.AetherVoiceRXBodyMix.Value / 100.0);
                _clarityTune.SetValue((double)_setup.AetherVoiceRXClarityTune.Value);
                _clarityAir.SetValue((double)_setup.AetherVoiceRXClarityHarmonics.Value);
                _clarityMix.SetValue((double)_setup.AetherVoiceRXClarityMix.Value / 100.0);
            }
            finally
            {
                _syncing = false;
            }
            updateStatus();
        }

        private void setUpDown(NumericUpDownTS ud, double value, int decimals)
        {
            if (_syncing) return;
            decimal v = (decimal)Math.Round(value, decimals);
            v = Math.Max(ud.Minimum, Math.Min(ud.Maximum, v));
            if (ud.Value != v) ud.Value = v;       // Setup's handler applies it to the DSP
        }

        private void updateStatus()
        {
            if (!_setup.AetherVoiceRXEnable.Checked)
            {
                _status.Text = "Off";
                _status.ForeColor = kTextDim;
            }
            else if (!RadioDSPRX.IsAetherVoiceMode(_console.RX1DSPMode))
            {
                _status.Text = "Paused in " + _console.RX1DSPMode + " (voice modes only)";
                _status.ForeColor = Color.FromArgb(0x8a, 0xa8, 0xc0);
            }
            else
            {
                _status.Text = "Active";
                _status.ForeColor = kAmber;
            }
        }

        private void tick()
        {
            double wet = -120.0;
            if (_console.PowerOn && _setup.AetherVoiceRXEnable.Checked)
            {
                try { wet = WDSP.GetRXAAetherVoiceWetRms(WDSP.id(0, 0)); }
                catch { }
            }
            _logo.Update(wet);
            updateStatus();
        }

        #region window dragging

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        private const int WM_NCLBUTTONDOWN = 0xA1, HTCAPTION = 2;

        private void dragWindow(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
        }

        #endregion
    }

    // AetherSDR's ClientCompKnob in centre-label mode: a 270 degree ring from 7:30 to 4:30, the
    // value arc, a pointer, the name inside the ring and the value in the gap at the bottom.
    public class AetherKnob : Control
    {
        private static readonly Color kRingBg = Color.FromArgb(0x1a, 0x2a, 0x3a);
        private static readonly Color kRingArc = Color.FromArgb(0x00, 0x70, 0xc0);
        private static readonly Color kPointer = Color.FromArgb(0xc8, 0xd8, 0xe8);
        private static readonly Color kLabel = Color.FromArgb(0x8e, 0xa8, 0xc0);
        private static readonly Color kValue = Color.FromArgb(0xc8, 0xd8, 0xe8);
        private const double kDragPxPerFullRange = 200.0, kWheelStep = 0.01, kFine = 0.25;

        private readonly string _label;
        private readonly double _min, _max, _default;
        private readonly Func<double, string> _format;
        private double _value;
        private bool _dragging;
        private int _dragStartY;
        private double _dragStartNorm;

        public Func<double, double> ToNorm;
        public Func<double, double> FromNorm;
        public event EventHandler ValueChanged;

        public AetherKnob(string label, double min, double max, double defaultValue, Func<double, string> format)
        {
            _label = label;
            _min = min;
            _max = max;
            _default = defaultValue;
            _format = format;
            _value = defaultValue;
            ToNorm = v => (v - _min) / (_max - _min);
            FromNorm = n => _min + n * (_max - _min);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.SizeNS;
        }

        public double Value { get { return _value; } }

        // programmatic update: no ValueChanged
        public void SetValue(double v)
        {
            v = Math.Max(_min, Math.Min(_max, v));
            if (v == _value) return;
            _value = v;
            Invalidate();
        }

        private double norm { get { return Math.Max(0, Math.Min(1, ToNorm(_value))); } }

        private void setFromUser(double n)
        {
            n = Math.Max(0, Math.Min(1, n));
            double v = Math.Max(_min, Math.Min(_max, FromNorm(n)));
            if (v == _value) return;
            _value = v;
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            _dragging = true;
            _dragStartY = e.Y;
            _dragStartNorm = norm;
            Focus();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_dragging) return;
            double scale = (ModifierKeys & Keys.Shift) != 0 ? kFine : 1.0;
            setFromUser(_dragStartNorm + (_dragStartY - e.Y) / kDragPxPerFullRange * scale);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _dragging = false;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            double step = kWheelStep * ((ModifierKeys & Keys.Shift) != 0 ? kFine : 1.0);
            setFromUser(norm + Math.Sign(e.Delta) * step);
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            setFromUser(ToNorm(_default));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

            float d = Math.Min(Width - 4, Height - 4);
            RectangleF ring = new RectangleF((Width - d) / 2f, 0, d, d);
            float thick = Math.Max(2f, d * 0.10f);
            RectangleF arc = RectangleF.Inflate(ring, -thick / 2f, -thick / 2f);
            double n = norm;

            // 7:30 clockwise to 4:30 (GDI+ angles run clockwise from 3 o'clock)
            using (Pen bg = new Pen(kRingBg, thick))
                g.DrawArc(bg, arc, 135f, 270f);
            if (n > 0.0005)
                using (Pen fg = new Pen(kRingArc, thick))
                    g.DrawArc(fg, arc, 135f, (float)(270.0 * n));

            double angle = (225.0 - 270.0 * n) * Math.PI / 180.0;
            PointF c = new PointF(ring.X + d / 2f, ring.Y + d / 2f);
            float rOut = d / 2f - thick / 2f, rIn = d / 2f - thick * 1.6f;
            using (Pen p = new Pen(kPointer, thick * 0.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawLine(p, c.X + rIn * (float)Math.Cos(angle), c.Y - rIn * (float)Math.Sin(angle),
                              c.X + rOut * (float)Math.Cos(angle), c.Y - rOut * (float)Math.Sin(angle));

            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                using (Font f = new Font("Segoe UI", Math.Max(8f, d / 6f), FontStyle.Bold, GraphicsUnit.Pixel))
                using (Brush b = new SolidBrush(kLabel))
                    g.DrawString(_label, f, b, ring, sf);
                using (Font f = new Font("Segoe UI", 11f, FontStyle.Bold, GraphicsUnit.Pixel))
                using (Brush b = new SolidBrush(kValue))
                    g.DrawString(_format(_value), f, b, new RectangleF(0, Height - 17, Width, 16), sf);
            }
        }
    }

    // AetherSDR's PooDooLogo: amber wordmark whose glow follows how much the exciter is adding
    public class AetherVoiceLogo : Control
    {
        private const double kSmoothAlpha = 0.25, kMinDb = -60.0, kMaxDb = 0.0;
        private double _smoothedDb = -120.0;

        public AetherVoiceLogo()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        public void Update(double wetRmsDb)
        {
            double before = _smoothedDb;
            _smoothedDb += kSmoothAlpha * (wetRmsDb - _smoothedDb);
            if (Math.Abs(before - _smoothedDb) > 0.01) Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            RectangleF r = ClientRectangle;

            float pulse = (float)Math.Max(0, Math.Min(1, (_smoothedDb - kMinDb) / (kMaxDb - kMinDb)));
            int textAlpha = Math.Min(255, 180 + (int)(pulse * 75));
            Color text = Color.FromArgb(textAlpha, 0xf2, 0xc1, 0x4e);

            if (pulse > 0.02f)
            {
                PointF c = new PointF(r.X + r.Width / 2f, r.Y + r.Height / 2f);
                float radius = r.Width * 0.55f;
                using (GraphicsPath path = new GraphicsPath())
                {
                    path.AddEllipse(c.X - radius, c.Y - radius * 0.6f, radius * 2f, radius * 1.2f);
                    using (PathGradientBrush glow = new PathGradientBrush(path))
                    {
                        glow.CenterPoint = c;
                        glow.CenterColor = Color.FromArgb((int)(pulse * 120), 0xf2, 0xc1, 0x4e);
                        glow.SurroundColors = new[] { Color.FromArgb(0, 0xf2, 0xc1, 0x4e) };
                        g.FillPath(glow, path);
                    }
                }
            }

            // Arial Black sized off the height, then reduced until it fits the width
            const string mark = "AetherVoice";
            float px = Math.Max(8f, r.Height * 0.55f);
            float room = r.Width * 0.92f;
            Font font = new Font("Arial Black", px, FontStyle.Regular, GraphicsUnit.Pixel);
            SizeF size = g.MeasureString(mark, font);
            if (size.Width > room)
            {
                font.Dispose();
                px = Math.Max(8f, px * room / size.Width);
                font = new Font("Arial Black", px, FontStyle.Regular, GraphicsUnit.Pixel);
            }
            using (font)
            using (Brush b = new SolidBrush(text))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(mark, font, b, r, sf);

            using (Pen underline = new Pen(Color.FromArgb(Math.Min(255, 80 + (int)(pulse * 120)), 0x5a, 0x3a, 0x0e), 1.2f))
                g.DrawLine(underline, r.Left + r.Width * 0.15f, r.Bottom - 3f, r.Right - r.Width * 0.15f, r.Bottom - 3f);
        }
    }

    // AetherSDR's checkable push buttons: kModeStyle (Even/Odd) and kBypassStyle (ON)
    public class AetherToggleButton : Control
    {
        private bool _checked, _hover;

        public bool Bypass { get; set; }

        public bool Checked
        {
            get { return _checked; }
            set { if (_checked != value) { _checked = value; Invalidate(); } }
        }

        public AetherToggleButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; Invalidate(); }
        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            Color back, border, text;
            if (_checked)
            {
                back = _hover ? Color.FromArgb(0x4a, 0x3a, 0x1e) : Color.FromArgb(0x3a, 0x2a, 0x0e);
                border = text = Color.FromArgb(0xf2, 0xc1, 0x4e);
            }
            else if (Bypass)
            {
                back = _hover ? Color.FromArgb(0x1a, 0x2a, 0x3a) : Color.FromArgb(0x0e, 0x1b, 0x28);
                border = Color.FromArgb(0x24, 0x3a, 0x4e);
                text = Color.FromArgb(0x8a, 0xa8, 0xc0);
            }
            else
            {
                back = _hover ? Color.FromArgb(0x24, 0x38, 0x4e) : Color.FromArgb(0x1a, 0x2a, 0x3a);
                border = Color.FromArgb(0x2a, 0x44, 0x58);
                text = Color.FromArgb(0x8a, 0xa8, 0xc0);
            }
            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (GraphicsPath path = roundedRect(r, 3f))
            {
                using (Brush b = new SolidBrush(back)) g.FillPath(b, path);
                using (Pen p = new Pen(border)) g.DrawPath(p, path);
            }
            using (Font f = new Font("Segoe UI", Bypass ? 11f : 12f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Brush b = new SolidBrush(text))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(Text, f, b, new RectangleF(0, 0, Width, Height), sf);
        }

        private static GraphicsPath roundedRect(RectangleF r, float radius)
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
    }

    // AetherSDR's "|--- text ---|" group label
    public class AetherBracketLabel : Control
    {
        public AetherBracketLabel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using (Font f = new Font("Segoe UI", 13f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Pen line = new Pen(Color.FromArgb(0x5a, 0x6a, 0x7a)))
            {
                // 2 px letter spacing, as in AetherSDR
                const float spacing = 2f;
                float[] widths = new float[Text.Length];
                float total = 0;
                for (int i = 0; i < Text.Length; i++)
                {
                    widths[i] = TextRenderer.MeasureText(g, Text[i].ToString(), f, Size.Empty, TextFormatFlags.NoPadding).Width;
                    total += widths[i] + (i > 0 ? spacing : 0);
                }
                float x = (Width - total) / 2f, y = (Height - f.Height) / 2f, mid = Height / 2f;
                g.DrawLine(line, 0, mid, x - 6, mid);
                g.DrawLine(line, x + total + 6, mid, Width, mid);
                for (int i = 0; i < Text.Length; i++)
                {
                    TextRenderer.DrawText(g, Text[i].ToString(), f, new Point((int)x, (int)y), Color.FromArgb(0xc8, 0xd8, 0xe8), TextFormatFlags.NoPadding);
                    x += widths[i] + spacing;
                }
            }
        }
    }
}
