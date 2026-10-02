/*  frmAetherStrip.cs

This file is part of Kainos, a program that implements a Software-Defined Radio.

The AetherTX channel strip window, after AetherSDR's Aetherial Audio Channel Strip
(src/gui/AetherialAudioStrip and the Strip*Panel classes, https://github.com/aethersdr/AetherSDR,
commit 20d022d5, by the AetherSDR contributors), rebuilt in Windows Forms by Justin Cron K7JUS, 2026.

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
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Thetis
{
    #region parameter definitions

    // One AetherSDR strip parameter. Stage/param numbers match wdsp/aetherstrip.h; names, ranges and
    // defaults are AetherSDR's (the final limiter starts off in Kainos).
    internal class AetherStripParam
    {
        public int Stage, Param;
        public string Name, Tip;
        public double Min, Max, Default;
        public bool Log, Integer;
        public Func<double, string> Format;
    }

    internal static class AetherStripDefs
    {
        public const int Gate = 0, DeEss = 1, Comp = 2, Tube = 3, Reverb = 4, Limiter = 5, Stages = 6, MaxParams = 11;
        public static readonly string[] StageNames = { "Gate", "De-Esser", "Compressor", "Tube", "Reverb", "Final Output" };

        public static readonly List<AetherStripParam> Params = new List<AetherStripParam>();
        public static readonly double[,] Defaults = new double[Stages, MaxParams];

        static Func<double, string> dB = v => v.ToString("0.0") + " dB";
        static Func<double, string> ms = v => v < 10 ? v.ToString("0.0") + " ms" : v.ToString("0") + " ms";
        static Func<double, string> hz = v => v >= 1000 ? (v / 1000).ToString("0.0") + " kHz" : v.ToString("0") + " Hz";
        static Func<double, string> pct = v => ((int)Math.Round(v * 100)).ToString() + " %";
        static Func<double, string> plain = v => v.ToString("0.00");

        static AetherStripDefs()
        {
            // enables (param 0): everything off
            for (int s = 0; s < Stages; s++) add(s, 0, null, 0, 1, 0, null, false, true, null, false);
            // gate: mode (1) and the two preset values it snaps are handled by the mode buttons
            add(Gate, 1, null, 0, 1, 0, null, false, true, null, false);
            add(Gate, 2, "Thresh", -80, 0, -40, dB, false, false, "Level below which the gate starts to close.");
            add(Gate, 3, "Ratio", 1, 10, 2, v => v.ToString("0.0") + ":1", false, false, "Expansion ratio below the threshold (10:1 is a hard gate).");
            add(Gate, 4, "Attack", 0.1, 100, 1, ms, true, false, "How fast the gate opens.");
            add(Gate, 6, "Hold", 0, 500, 20, ms, false, false, "How long the gate stays open after the level drops.");
            add(Gate, 5, "Release", 5, 2000, 100, ms, true, false, "How fast the gate closes.");
            add(Gate, 7, "Floor", -80, 0, -15, dB, false, false, "Maximum attenuation when closed.");
            add(Gate, 8, "Return", 0, 20, 2, dB, false, false, "Hysteresis: how far below the threshold the gate closes again.");
            add(Gate, 9, "Look", 0, 5, 0, ms, false, false, "Lookahead: delays the audio so the gate opens before word onsets.");
            // de-esser
            add(DeEss, 1, "Freq", 1000, 12000, 6000, hz, true, false, "Centre of the sibilance band.");
            add(DeEss, 2, "Q", 0.5, 5, 2, v => v.ToString("0.0"), false, false, "Width of the sibilance band (higher is narrower).");
            add(DeEss, 3, "Thresh", -60, 0, -30, dB, false, false, "Sibilance level where reduction starts.");
            add(DeEss, 4, "Amount", -24, 0, -6, dB, false, false, "Maximum reduction.");
            add(DeEss, 5, "Attack", 0.1, 30, 1, ms, true, false, "How fast it reacts to sibilance.");
            add(DeEss, 6, "Release", 10, 500, 100, ms, true, false, "How fast it lets go.");
            add(DeEss, 7, "Slope", 1, 4, 2, v => ((int)Math.Round(v) * 12) + " dB/oct", false, true, "Steepness of the sibilance filter.");
            // compressor
            add(Comp, 9, "Drive", 0, 18, 0, dB, false, false, "Gain before the compressor: pushes more of the voice over the threshold.");
            add(Comp, 1, "Thresh", -60, 0, -18, dB, false, false, "Level where compression starts.");
            add(Comp, 2, "Ratio", 1, 20, 3, v => v.ToString("0.0") + ":1", false, false, "Compression ratio (20:1 limits).");
            add(Comp, 5, "Knee", 0, 24, 6, dB, false, false, "Softness of the compression onset.");
            add(Comp, 3, "Attack", 0.1, 300, 20, ms, true, false, "How fast it compresses.");
            add(Comp, 4, "Release", 5, 2000, 200, ms, true, false, "How fast it recovers.");
            add(Comp, 6, "Makeup", -12, 24, 0, dB, false, false, "Gain after compression.");
            add(Comp, 8, "Ceiling", -24, 0, -1, dB, false, false, "Ceiling of the compressor's output limiter.");
            add(Comp, 10, "Phase", 0, 6, 0, v => ((int)Math.Round(v)) == 0 ? "Off" : ((int)Math.Round(v)) + " st", false, true,
                "Phase rotator stages before compression: evens out lopsided voice peaks (4 is the broadcast setting).");
            add(Comp, 7, null, 0, 1, 1, null, false, true, null, false);      // limiter on
            // tube
            add(Tube, 1, null, 0, 2, 0, null, false, true, null, false);      // model
            add(Tube, 2, "Drive", 0, 24, 0, dB, false, false, "How hard the voice is pushed into the tube.");
            add(Tube, 3, "Bias", 0, 1, 0, pct, false, false, "Asymmetry: more bias gives more even harmonics.");
            add(Tube, 4, "Tone", -1, 1, 0, v => v == 0 ? "Flat" : (v < 0 ? "Dark " : "Bright ") + ((int)Math.Round(Math.Abs(v) * 100)) + "%", false, false,
                "Tilts which part of the spectrum is pushed into the saturation.");
            add(Tube, 7, "Env", -1, 1, 0, v => ((int)Math.Round(v * 100)) + " %", false, false,
                "Dynamic drive: positive adds drive on loud passages, negative keeps loud passages clean.");
            add(Tube, 8, "Release", 10, 500, 35, ms, true, false, "Release of the dynamic-drive envelope.");
            add(Tube, 5, "Output", -24, 12, 0, dB, false, false, "Output level of the tube stage.");
            add(Tube, 6, "Dry/Wet", 0, 1, 1, pct, false, false, "Blend of the original and saturated audio.");
            // reverb
            add(Reverb, 1, "Size", 0, 1, 0.5, pct, false, false, "Room size.");
            add(Reverb, 2, "Decay", 0.3, 5, 1.2, v => v.ToString("0.0") + " s", false, false, "Reverb time.");
            add(Reverb, 3, "Damping", 0, 1, 0.5, pct, false, false, "High-frequency damping of the tail.");
            add(Reverb, 4, "Pre-dly", 0, 100, 20, ms, false, false, "Delay before the reverb starts.");
            add(Reverb, 5, "Mix", 0, 1, 0.15, pct, false, false, "How much reverb is added.");
            // final limiter
            add(Limiter, 1, "Ceiling", -12, 0, -1, dB, false, false, "Highest level the strip lets through.");
            add(Limiter, 2, "Trim", -12, 12, 0, dB, false, false, "Output level of the strip.");
            add(Limiter, 3, null, 0, 1, 1, null, false, true, null, false);   // DC block
        }

        static void add(int stage, int param, string name, double min, double max, double def, Func<double, string> fmt,
            bool log, bool integer, string tip, bool knob = true)
        {
            Defaults[stage, param] = def;
            if (knob) Params.Add(new AetherStripParam { Stage = stage, Param = param, Name = name, Min = min, Max = max, Default = def,
                Format = fmt, Log = log, Integer = integer, Tip = tip });
        }
    }

    #endregion

    #region settings model

    // Channel strip settings for one side. AetherTX (console.AetherStripTX) is saved in each TX profile
    // (Setup: saveAetherVoiceTXProfile/loadAetherVoiceTXProfile) and pushed to RadioDSPTX; AetherRX
    // (console.AetherStripRX) is saved with the Setup options and pushed to all four receivers.
    public class AetherStrip
    {
        private readonly Console _console;
        private readonly bool _rx;
        private readonly double[,] _values = (double[,])AetherStripDefs.Defaults.Clone();
        private bool _bypass;

        public event EventHandler Changed;

        public AetherStrip(Console console, bool rx)
        {
            _console = console;
            _rx = rx;
        }

        public bool IsRX { get { return _rx; } }

        public double Get(int stage, int param) { return _values[stage, param]; }
        public bool Enabled(int stage) { return _values[stage, 0] != 0; }

        public void Set(int stage, int param, double value)
        {
            if (_values[stage, param] == value) return;
            _values[stage, param] = value;
            push(stage, param);
            Changed?.Invoke(this, EventArgs.Empty);
        }

        // AetherSDR's gate mode buttons snap ratio and floor to preset pairs
        public void SetGateMode(int mode)
        {
            _values[AetherStripDefs.Gate, 1] = mode;
            _values[AetherStripDefs.Gate, 3] = mode == 1 ? 10 : 2;
            _values[AetherStripDefs.Gate, 7] = mode == 1 ? -40 : -15;
            push(AetherStripDefs.Gate, 1);
            push(AetherStripDefs.Gate, 3);
            push(AetherStripDefs.Gate, 7);
            Changed?.Invoke(this, EventArgs.Empty);
        }

        // BYPASS: the whole chain (strip and AetherVoice) off, without losing the settings
        public bool Bypass
        {
            get { return _bypass; }
            set
            {
                if (_bypass == value) return;
                _bypass = value;
                for (int s = 0; s < AetherStripDefs.Stages; s++) push(s, 0);
                if (!_console.IsSetupFormNull)
                {
                    if (_rx) _console.SetupForm.ApplyAetherVoiceRXFromStrip();
                    else _console.SetupForm.ApplyAetherVoiceTXFromStrip();
                }
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        public void ApplyAll()
        {
            for (int s = 0; s < AetherStripDefs.Stages; s++)
                for (int p = 1; p < AetherStripDefs.MaxParams; p++) push(s, p);
            for (int s = 0; s < AetherStripDefs.Stages; s++) push(s, 0);
        }

        private void push(int stage, int param)
        {
            if (_console == null || _console.radio == null) return;
            double v = _values[stage, param];
            if (param == 0 && _bypass) v = 0;
            if (_rx)
            {
                for (int t = 0; t < 2; t++)
                    for (int s = 0; s < 2; s++) _console.radio.GetDSPRX(t, s).SetRXStripParam(stage, param, v);
            }
            else _console.radio.GetDSPTX(0).SetTXStripParam(stage, param, v);
        }

        // "stage.param=value;..." for the TX profile column
        public string Serialize()
        {
            StringBuilder sb = new StringBuilder();
            for (int s = 0; s < AetherStripDefs.Stages; s++)
                for (int p = 0; p < AetherStripDefs.MaxParams; p++)
                    sb.Append(s).Append('.').Append(p).Append('=').Append(_values[s, p].ToString("R", CultureInfo.InvariantCulture)).Append(';');
            return sb.ToString();
        }

        // null or empty (a profile saved before the strip existed) loads the defaults: everything off
        public void Deserialize(string data)
        {
            double[,] v = (double[,])AetherStripDefs.Defaults.Clone();
            if (!string.IsNullOrEmpty(data))
            {
                foreach (string item in data.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string[] kv = item.Split('=');
                    string[] sp = kv[0].Split('.');
                    int s, p; double val;
                    if (kv.Length == 2 && sp.Length == 2 && int.TryParse(sp[0], out s) && int.TryParse(sp[1], out p) &&
                        double.TryParse(kv[1], NumberStyles.Float, CultureInfo.InvariantCulture, out val) &&
                        s >= 0 && s < AetherStripDefs.Stages && p >= 0 && p < AetherStripDefs.MaxParams)
                        v[s, p] = val;
                }
            }
            for (int s = 0; s < AetherStripDefs.Stages; s++)
                for (int p = 0; p < AetherStripDefs.MaxParams; p++) _values[s, p] = v[s, p];
            ApplyAll();
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public static string DefaultsSerialized()
        {
            return new AetherStrip(null, false).Serialize();
        }

        public bool Differs(string profileData)
        {
            AetherStrip other = new AetherStrip(null, false);
            other.load(profileData);
            for (int s = 0; s < AetherStripDefs.Stages; s++)
                for (int p = 0; p < AetherStripDefs.MaxParams; p++)
                    if (other._values[s, p] != _values[s, p]) return true;
            return false;
        }

        private void load(string data)
        {
            if (string.IsNullOrEmpty(data)) return;
            foreach (string item in data.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] kv = item.Split('=');
                string[] sp = kv[0].Split('.');
                int s, p; double val;
                if (kv.Length == 2 && sp.Length == 2 && int.TryParse(sp[0], out s) && int.TryParse(sp[1], out p) &&
                    double.TryParse(kv[1], NumberStyles.Float, CultureInfo.InvariantCulture, out val) &&
                    s >= 0 && s < AetherStripDefs.Stages && p >= 0 && p < AetherStripDefs.MaxParams)
                    _values[s, p] = val;
            }
        }
    }

    #endregion

    #region window

    // The Aether channel-strip window: RX and TX tabs and the stage list on the left, the selected stage's
    // page on the right. The AetherRX and AetherTX menu items open it on the matching tab.
    public class frmAetherStrip : Form
    {
        private static readonly Color kWindowBg = Color.FromArgb(0x08, 0x12, 0x1d);
        private static readonly Color kTitleBg = Color.FromArgb(0x0f, 0x0f, 0x1a);
        private static readonly Color kBorder = Color.FromArgb(0x2a, 0x3a, 0x4d);
        private static readonly Color kPanel = Color.FromArgb(0x0b, 0x17, 0x24);
        private static readonly Color kGrid = Color.FromArgb(0x1a, 0x2a, 0x3a);
        private static readonly Color kText = Color.FromArgb(0xc8, 0xd8, 0xe8);
        private static readonly Color kTextDim = Color.FromArgb(0x5a, 0x6a, 0x7a);
        private static readonly Color kTextMid = Color.FromArgb(0x8a, 0xa8, 0xc0);
        private static readonly Color kAmber = Color.FromArgb(0xf2, 0xc1, 0x4e);
        private static readonly Color kGreen = Color.FromArgb(0x4d, 0xd8, 0x7a);
        private static readonly Color kBlue = Color.FromArgb(0x00, 0x70, 0xc0);

        // pages in AetherSDR's chain order; Exciter is AetherVoice TX
        private const int PageExciter = 100;
        private static readonly int[] PageOrderTX = { AetherStripDefs.Gate, AetherStripDefs.DeEss, AetherStripDefs.Comp, AetherStripDefs.Tube,
            PageExciter, AetherStripDefs.Reverb, AetherStripDefs.Limiter };
        // AetherSDR's receive chain: gate, compressor, tube, exciter (de-essing and reverb are transmit tools)
        private static readonly int[] PageOrderRX = { AetherStripDefs.Gate, AetherStripDefs.Comp, AetherStripDefs.Tube, PageExciter };

        private readonly Console _console;
        private bool _rx;                                   // the tab shown: receive or transmit
        private int[] _pageIds;
        private AetherStrip _strip;
        private readonly Setup _setup;
        private AetherVoiceSetupControls _av;               // the AetherVoice controls for this side
        private readonly int[] _lastPage = { AetherStripDefs.Gate, AetherStripDefs.Gate };   // per tab: [0] TX, [1] RX
        private readonly AetherToggleButton _tabRX, _tabTX;
        private readonly StageList _list;
        private readonly Panel _page;
        private readonly Label _pageTitle, _pageNote, _status;
        private readonly AetherToggleButton _btnOn, _btnBypass;
        private readonly List<AetherToggleButton> _extraButtons = new List<AetherToggleButton>();
        private readonly List<KeyValuePair<AetherStripParam, AetherKnob>> _knobs = new List<KeyValuePair<AetherStripParam, AetherKnob>>();
        private readonly StripViz _viz;
        private readonly AetherVoiceLogo _logo;
        private readonly AetherBracketLabel _bodyLbl, _clarityLbl;
        private readonly ToolTip _tips = new ToolTip();
        private readonly Timer _timer;
        private int _pageId = AetherStripDefs.Gate;
        private bool _syncing;
        private AetherKnob[] _exciterKnobs;
        private AetherToggleButton _even, _odd;

        public frmAetherStrip(Console console, bool rx)
        {
            _console = console;
            _setup = console.SetupForm;

            Text = "Kainos Audio";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            BackColor = kWindowBg;
            ClientSize = new Size(900, 600);
            StartPosition = FormStartPosition.Manual;
            DoubleBuffered = true;
            KeyPreview = true;
            Icon = console.Icon;

            Label title = new Label
            {
                Text = "Kainos Audio Processing",
                ForeColor = kText, BackColor = kTitleBg,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold, GraphicsUnit.Pixel),
                TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0),
                Location = new Point(1, 1), Size = new Size(ClientSize.Width - 32, 22)
            };
            title.MouseDown += dragWindow;
            Label close = new Label
            {
                Text = "✕", ForeColor = kTextMid, BackColor = kTitleBg,
                Font = new Font("Segoe UI", 12f, GraphicsUnit.Pixel), TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(ClientSize.Width - 31, 1), Size = new Size(30, 22), Cursor = Cursors.Hand
            };
            close.MouseEnter += (s, e) => { close.BackColor = Color.FromArgb(0xcc, 0x20, 0x30); close.ForeColor = Color.White; };
            close.MouseLeave += (s, e) => { close.BackColor = kTitleBg; close.ForeColor = kTextMid; };
            close.Click += (s, e) => Hide();
            Controls.Add(title);
            Controls.Add(close);

            // RX / TX tabs, then the stage list for the tab shown
            _tabRX = new AetherToggleButton { Text = "RX", Location = new Point(10, 34), Size = new Size(82, 28) };
            _tabTX = new AetherToggleButton { Text = "TX", Location = new Point(98, 34), Size = new Size(82, 28) };
            _tabRX.Click += (s, e) => SetSide(true);
            _tabTX.Click += (s, e) => SetSide(false);
            _tips.SetToolTip(_tabRX, "Receive: the audio chain on every receiver, saved with your settings.");
            _tips.SetToolTip(_tabTX, "Transmit: your voice's audio chain, saved in each TX profile.");
            Controls.Add(_tabRX);
            Controls.Add(_tabTX);
            _list = new StageList(this) { Location = new Point(10, 70), Size = new Size(170, PageOrderTX.Length * 38 + 4) };
            Controls.Add(_list);

            // bottom-left: TX indicator and BYPASS
            _btnBypass = new AetherToggleButton { Text = "BYPASS", Bypass = true, Location = new Point(10, 520), Size = new Size(170, 28) };
            _btnBypass.Click += (s, e) => _strip.Bypass = !_strip.Bypass;
            Controls.Add(_btnBypass);
            _status = new Label
            {
                ForeColor = kTextMid, Font = new Font("Segoe UI", 11f, FontStyle.Bold, GraphicsUnit.Pixel),
                Location = new Point(10, 556), Size = new Size(170, 36)
            };
            Controls.Add(_status);

            // page
            _page = new Panel { Location = new Point(194, 34), Size = new Size(696, 556), BackColor = kWindowBg };
            Controls.Add(_page);
            _pageTitle = new Label
            {
                ForeColor = kText, Font = new Font("Segoe UI", 18f, FontStyle.Bold, GraphicsUnit.Pixel),
                Location = new Point(0, 0), AutoSize = true
            };
            _pageNote = new Label
            {
                ForeColor = kTextDim, Font = new Font("Segoe UI", 11f, GraphicsUnit.Pixel),
                Location = new Point(2, 28), Size = new Size(520, 16)
            };
            _btnOn = new AetherToggleButton { Text = "ON", Bypass = true, Location = new Point(632, 2), Size = new Size(60, 26) };
            _btnOn.Click += (s, e) => toggleStage();
            _viz = new StripViz(this) { Location = new Point(0, 56), Size = new Size(692, 230) };
            _logo = new AetherVoiceLogo { Location = new Point(40, 100), Size = new Size(612, 110), Visible = false };
            _page.Controls.Add(_pageTitle);
            _page.Controls.Add(_pageNote);
            _page.Controls.Add(_btnOn);
            _page.Controls.Add(_logo);
            _bodyLbl = new AetherBracketLabel { Text = "BODY", Location = new Point(0, 270), Size = new Size(260, 20), Visible = false };
            _clarityLbl = new AetherBracketLabel { Text = "CLARITY", Location = new Point(368, 270), Size = new Size(260, 20), Visible = false };
            _page.Controls.Add(_bodyLbl);
            _page.Controls.Add(_clarityLbl);
            _page.Controls.Add(_viz);

            _timer = new Timer { Interval = 33 };
            _timer.Tick += (s, e) => tick();

            SetSide(rx);
        }

        // switch the window between the receive and transmit chains
        public void SetSide(bool rx)
        {
            if (_strip != null)
            {
                if (_rx == rx) return;
                _lastPage[_rx ? 1 : 0] = _pageId;
                _strip.Changed -= stripChanged;
                hookSetupTX(false);
            }
            _rx = rx;
            _pageIds = rx ? PageOrderRX : PageOrderTX;
            _strip = rx ? _console.AetherStripRX : _console.AetherStripTX;
            _av = rx ? _setup.AetherVoiceRX : _setup.AetherVoiceTX;
            _strip.Changed += stripChanged;
            hookSetupTX(true);

            _tabRX.Checked = rx;
            _tabTX.Checked = !rx;
            _tips.SetToolTip(_btnBypass, "Bypass the whole " + (rx ? "receive" : "transmit") + " chain (strip and AetherVoice) without changing any settings.");
            _list.Invalidate();
            ShowPage(_lastPage[rx ? 1 : 0]);
        }

        #region pages

        internal int PageId { get { return _pageId; } }
        internal int[] Pages { get { return _pageIds; } }
        internal bool IsRX { get { return _rx; } }

        internal string PageName(int id)
        {
            return id == PageExciter ? "Exciter" : AetherStripDefs.StageNames[id];
        }

        internal bool PageEnabled(int id)
        {
            return id == PageExciter ? _av.Enable.Checked : _strip.Enabled(id);
        }

        internal bool Bypassed { get { return _strip.Bypass; } }

        internal void ShowPage(int id)
        {
            _pageId = id;
            foreach (KeyValuePair<AetherStripParam, AetherKnob> kv in _knobs) { _page.Controls.Remove(kv.Value); kv.Value.Dispose(); }
            _knobs.Clear();
            foreach (AetherToggleButton b in _extraButtons) { _page.Controls.Remove(b); b.Dispose(); }
            _extraButtons.Clear();
            if (_exciterKnobs != null) { foreach (AetherKnob k in _exciterKnobs) { _page.Controls.Remove(k); k.Dispose(); } _exciterKnobs = null; }
            _even = _odd = null;

            _pageTitle.Text = PageName(id);
            _logo.Visible = _bodyLbl.Visible = _clarityLbl.Visible = id == PageExciter;
            _viz.Visible = id != PageExciter;

            const int knobY = 300, step = 92;
            if (id == PageExciter)
            {
                _pageNote.Text = _rx ? "AetherVoice on receive: the same settings as the AetherVoice window (RX) and Setup > DSP > AetherVoice."
                                     : "AetherVoice on transmit: the same settings as the AetherVoice window (TX) and Setup > DSP > AetherVoice.";
                buildExciterPage(knobY, step);
            }
            else
            {
                switch (id)
                {
                    case AetherStripDefs.Gate:
                        _pageNote.Text = "Downward expander / noise gate: quietens the gaps between words.";
                        addModeButtons(new[] { "Expander", "Gate" }, AetherStripDefs.Gate, 1,
                            new[] { "Gentle expansion: 2:1 ratio, -15 dB floor.", "Hard gate: 10:1 ratio, -40 dB floor." });
                        break;
                    case AetherStripDefs.DeEss: _pageNote.Text = "Turns down sibilance (s, sh, t) without dulling the voice."; break;
                    case AetherStripDefs.Comp:
                        _pageNote.Text = "Compressor with drive, phase rotator and an output limiter.";
                        addToggle("LIMIT", AetherStripDefs.Comp, 7, "The compressor's own output limiter.", 560);
                        break;
                    case AetherStripDefs.Tube:
                        _pageNote.Text = "Tube saturation: adds harmonic warmth.";
                        addModeButtons(new[] { "A", "B", "C" }, AetherStripDefs.Tube, 1,
                            new[] { "Model A: soft tanh, broad and gentle.", "Model B: hard clip and tanh hybrid, odd harmonics, aggressive.",
                                    "Model C: asymmetric, even harmonics, warm." });
                        break;
                    case AetherStripDefs.Reverb: _pageNote.Text = "Reverb (Freeverb). Use sparingly on the air."; break;
                    case AetherStripDefs.Limiter:
                        _pageNote.Text = "Final brickwall limiter on the strip's output.";
                        addToggle("DC BLOCK", AetherStripDefs.Limiter, 3, "25 Hz high-pass before the limiter.", 540);
                        break;
                }
                int n = 0;
                foreach (AetherStripParam p in AetherStripDefs.Params)
                {
                    if (p.Stage != id || p.Name == null) continue;
                    AetherStripParam pp = p;
                    AetherKnob k = new AetherKnob(p.Name, p.Min, p.Max, p.Default, p.Format);
                    if (p.Log)
                    {
                        k.ToNorm = v => Math.Log(Math.Max(pp.Min, v) / pp.Min) / Math.Log(pp.Max / pp.Min);
                        k.FromNorm = x => pp.Min * Math.Pow(pp.Max / pp.Min, x);
                    }
                    int row = n / 7, col = n % 7;
                    k.Location = new Point(col * step, knobY + row * 100);
                    k.Size = new Size(76, 76);
                    k.SetValue(_strip.Get(p.Stage, p.Param));
                    k.ValueChanged += (s, e) =>
                    {
                        if (_syncing) return;
                        double v = k.Value;
                        if (pp.Integer) v = Math.Round(v);
                        _strip.Set(pp.Stage, pp.Param, v);
                    };
                    if (p.Tip != null) _tips.SetToolTip(k, p.Tip);
                    _page.Controls.Add(k);
                    _knobs.Add(new KeyValuePair<AetherStripParam, AetherKnob>(p, k));
                    n++;
                }
            }
            syncControls();
            _list.Invalidate();
            _viz.Invalidate();
        }

        private void addModeButtons(string[] names, int stage, int param, string[] tips)
        {
            int x = 300;
            for (int i = 0; i < names.Length; i++)
            {
                int idx = i;
                AetherToggleButton b = new AetherToggleButton { Text = names[i], Location = new Point(x, 2), Size = new Size(names[i].Length > 2 ? 84 : 40, 26) };
                x += b.Width + 6;
                b.Tag = new[] { stage, param, idx };
                b.Click += (s, e) =>
                {
                    if (stage == AetherStripDefs.Gate) _strip.SetGateMode(idx);
                    else _strip.Set(stage, param, idx);
                };
                _tips.SetToolTip(b, tips[i]);
                _page.Controls.Add(b);
                _extraButtons.Add(b);
            }
        }

        private void addToggle(string text, int stage, int param, string tip, int x)
        {
            AetherToggleButton b = new AetherToggleButton { Text = text, Bypass = true, Location = new Point(x, 2), Size = new Size(text.Length > 5 ? 84 : 64, 26) };
            b.Tag = new[] { stage, param, -1 };
            b.Click += (s, e) => _strip.Set(stage, param, _strip.Get(stage, param) != 0 ? 0 : 1);
            _tips.SetToolTip(b, tip);
            _page.Controls.Add(b);
            _extraButtons.Add(b);
        }

        private void buildExciterPage(int knobY, int step)
        {
            AetherVoiceSetupControls s = _av;
            _even = new AetherToggleButton { Text = "Even", Location = new Point(300, 2), Size = new Size(62, 26) };
            _odd = new AetherToggleButton { Text = "Odd", Location = new Point(368, 2), Size = new Size(62, 26) };
            _even.Click += (o, e) => s.Mode.SelectedIndex = 0;
            _odd.Click += (o, e) => s.Mode.SelectedIndex = 1;
            _tips.SetToolTip(_even, "Aphex-style asymmetric shaping: warmer, with Big Bottom low-end saturation.");
            _tips.SetToolTip(_odd, "Behringer-style symmetric shaping: brighter, with a low-end compressor.");
            _page.Controls.Add(_even);
            _page.Controls.Add(_odd);
            _extraButtons.Add(_even);
            _extraButtons.Add(_odd);

            Func<double, string> db = v => v.ToString("0.0") + " dB", pct = v => ((int)(v * 100 + 0.5)) + " %";
            AetherKnob[] k =
            {
                new AetherKnob("Drive", 0, 24, 6, db), new AetherKnob("Tune", 50, 160, 100, v => v.ToString("0") + " Hz"),
                new AetherKnob("Mix", 0, 1, 0.3, pct),
                new AetherKnob("Tune", 1000, 10000, 5000, v => (v / 1000).ToString("0.0") + " kHz")
                    { ToNorm = v => Math.Log10(Math.Max(1000, v) / 1000.0), FromNorm = n => 1000.0 * Math.Pow(10, n) },
                new AetherKnob("Air", 0, 24, 6, db), new AetherKnob("Mix", 0, 1, 0.3, pct)
            };
            NumericUpDownTS[] uds = s.UpDowns;
            double[] scale = { 1, 1, 100, 1, 1, 100 };
            int[] dec = { 1, 0, 0, 0, 1, 0 };
            for (int i = 0; i < k.Length; i++)
            {
                int idx = i;
                k[i].Location = new Point((i < 3 ? i : i + 1) * step, knobY);
                k[i].Size = new Size(76, 76);
                k[i].ValueChanged += (o, e) =>
                {
                    if (_syncing) return;
                    decimal v = (decimal)Math.Round(k[idx].Value * scale[idx], dec[idx]);
                    v = Math.Max(uds[idx].Minimum, Math.Min(uds[idx].Maximum, v));
                    if (uds[idx].Value != v) uds[idx].Value = v;
                };
                _page.Controls.Add(k[i]);
            }
            _exciterKnobs = k;
        }

        #endregion

        #region sync

        private void toggleStage()
        {
            if (_pageId == PageExciter) _av.Enable.Checked = !_av.Enable.Checked;
            else _strip.Set(_pageId, 0, _strip.Enabled(_pageId) ? 0 : 1);
        }

        private void stripChanged(object sender, EventArgs e) { syncControls(); }
        private void setupTXChanged(object sender, EventArgs e) { syncControls(); }

        private void hookSetupTX(bool add)
        {
            AetherVoiceSetupControls s = _av;
            EventHandler h = setupTXChanged;
            if (add) { s.Enable.CheckedChanged += h; s.Mode.SelectedIndexChanged += h; foreach (NumericUpDownTS ud in s.UpDowns) ud.ValueChanged += h; }
            else { s.Enable.CheckedChanged -= h; s.Mode.SelectedIndexChanged -= h; foreach (NumericUpDownTS ud in s.UpDowns) ud.ValueChanged -= h; }
        }

        private void syncControls()
        {
            _syncing = true;
            try
            {
                _btnOn.Checked = PageEnabled(_pageId);
                _btnBypass.Checked = _strip.Bypass;
                foreach (KeyValuePair<AetherStripParam, AetherKnob> kv in _knobs)
                    kv.Value.SetValue(_strip.Get(kv.Key.Stage, kv.Key.Param));
                foreach (AetherToggleButton b in _extraButtons)
                {
                    int[] t = b.Tag as int[];
                    if (t == null) continue;
                    double v = _strip.Get(t[0], t[1]);
                    b.Checked = t[2] < 0 ? v != 0 : (int)Math.Round(v) == t[2];
                }
                if (_exciterKnobs != null)
                {
                    AetherVoiceSetupControls s = _av;
                    NumericUpDownTS[] uds = s.UpDowns;
                    double[] scale = { 1, 1, 100, 1, 1, 100 };
                    for (int i = 0; i < _exciterKnobs.Length; i++) _exciterKnobs[i].SetValue((double)uds[i].Value / scale[i]);
                    _even.Checked = s.Mode.SelectedIndex != 1;
                    _odd.Checked = s.Mode.SelectedIndex == 1;
                }
            }
            finally { _syncing = false; }
            _list.Invalidate();
            _viz.Invalidate();
        }

        private void tick()
        {
            DSPMode mode = _rx ? _console.RX1DSPMode : _console.radio.GetDSPTX(0).CurrentDSPMode;
            // meters are live while transmitting (AetherTX) or receiving on RX1 (AetherRX)
            bool live = _console.PowerOn && (_rx ? !_console.MOX : _console.MOX);
            bool voice = RadioDSPRX.IsAetherVoiceMode(mode);
            if (_strip.Bypass) { _status.Text = "Bypassed"; _status.ForeColor = kAmber; }
            else if (!voice) { _status.Text = "Paused in " + mode + "\r\n(voice modes only)"; _status.ForeColor = kTextMid; }
            else if (live && !_rx) { _status.Text = "● TX"; _status.ForeColor = Color.FromArgb(0xff, 0x50, 0x50); }
            else if (live) { _status.Text = "● RX"; _status.ForeColor = kGreen; }
            else { _status.Text = "Ready"; _status.ForeColor = kTextDim; }
            live = live && voice && !_strip.Bypass;

            if (_pageId == PageExciter)
            {
                double wet = -120;
                if (live && _av.Enable.Checked)
                    try { wet = _rx ? WDSP.GetRXAAetherVoiceWetRms(WDSP.id(0, 0)) : WDSP.GetTXAAetherVoiceWetRms(WDSP.id(1, 0)); } catch { }
                _logo.Update(wet);
            }
            else _viz.UpdateMeters(live && _strip.Enabled(_pageId));
        }

        internal double Meter(int stage, int meter)
        {
            try { return _rx ? _console.radio.GetDSPRX(0, 0).GetRXStripMeter(stage, meter) : _console.radio.GetDSPTX(0).GetTXStripMeter(stage, meter); }
            catch { return -120; }
        }

        internal AetherStrip Strip { get { return _strip; } }

        #endregion

        #region form

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible) { syncControls(); _timer.Start(); }
            else _timer.Stop();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); return; }
            base.OnFormClosing(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) Hide();
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen p = new Pen(kBorder)) e.Graphics.DrawRectangle(p, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer.Dispose();
                if (_strip != null) _strip.Changed -= stripChanged;
                hookSetupTX(false);
            }
            base.Dispose(disposing);
        }

        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private void dragWindow(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
        }

        #endregion

        #region stage list

        // AetherSDR's vertical stage tabs: a power light per stage, the selected one highlighted
        private class StageList : Control
        {
            private readonly frmAetherStrip _f;
            private int _hover = -1;

            public StageList(frmAetherStrip f)
            {
                _f = f;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                BackColor = kWindowBg;
                Cursor = Cursors.Hand;
            }

            protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); int h = e.Y / 38; if (h != _hover) { _hover = h; Invalidate(); } }
            protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = -1; Invalidate(); }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                int i = e.Y / 38;
                if (i >= 0 && i < _f.Pages.Length) _f.ShowPage(_f.Pages[i]);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                using (Font f = new Font("Segoe UI", 13f, FontStyle.Bold, GraphicsUnit.Pixel))
                    for (int i = 0; i < _f.Pages.Length; i++)
                    {
                        int id = _f.Pages[i];
                        Rectangle r = new Rectangle(0, i * 38, Width - 1, 34);
                        bool sel = id == _f.PageId;
                        using (Brush b = new SolidBrush(sel ? Color.FromArgb(0x1a, 0x2a, 0x3a) : i == _hover ? Color.FromArgb(0x10, 0x1e, 0x2c) : kPanel))
                            g.FillRectangle(b, r);
                        if (sel) using (Brush b = new SolidBrush(kAmber)) g.FillRectangle(b, 0, r.Y, 3, r.Height);
                        bool on = _f.PageEnabled(id) && !_f.Bypassed;
                        using (Brush b = new SolidBrush(on ? kGreen : Color.FromArgb(0x24, 0x34, 0x44)))
                            g.FillEllipse(b, 14, r.Y + 12, 10, 10);
                        using (Brush b = new SolidBrush(sel ? kText : kTextMid))
                            g.DrawString(_f.PageName(id), f, b, 34, r.Y + 8);
                    }
            }
        }

        #endregion

        #region visualisation

        // The picture above each stage's knobs: transfer curves for the dynamics stages, the band for the
        // de-esser, the tail for the reverb, and level / gain-reduction meters fed from the DSP.
        private class StripViz : Control
        {
            private readonly frmAetherStrip _f;
            private double _inDb = -120, _outDb = -120, _grDb = 0, _gr2Db = 0, _extraDb = -120;
            private bool _live;

            public StripViz(frmAetherStrip f)
            {
                _f = f;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                BackColor = kPanel;
            }

            public void UpdateMeters(bool live)
            {
                _live = live;
                int s = _f.PageId;
                if (live)
                {
                    _inDb = smooth(_inDb, _f.Meter(s, 10));
                    _outDb = smooth(_outDb, s == AetherStripDefs.Limiter ? _f.Meter(s, 1) : _f.Meter(s, 11));
                    _grDb = smooth(_grDb, s == AetherStripDefs.Tube || s == AetherStripDefs.Reverb ? 0 : _f.Meter(s, 0));
                    _gr2Db = smooth(_gr2Db, s == AetherStripDefs.Comp ? _f.Meter(s, 1) : 0);
                    _extraDb = smooth(_extraDb, s == AetherStripDefs.Tube || s == AetherStripDefs.Reverb ? _f.Meter(s, 0) :
                                                s == AetherStripDefs.Limiter ? _f.Meter(s, 2) : -120);
                }
                else { _inDb = _outDb = _extraDb = -120; _grDb = _gr2Db = 0; }
                Invalidate();
            }

            private static double smooth(double old, double v)
            {
                if (double.IsNaN(v) || double.IsInfinity(v)) v = -120;
                return v > old ? v : old + 0.3 * (v - old);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                using (Pen p = new Pen(kBorder)) g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
                AetherStrip st = _f.Strip;
                Rectangle plot = new Rectangle(14, 14, Height - 28, Height - 28);       // square curve plot
                Rectangle meters = new Rectangle(plot.Right + 30, 14, Width - plot.Right - 44, Height - 28);
                switch (_f.PageId)
                {
                    case AetherStripDefs.Gate:
                        drawCurve(g, plot, -80, x => x + gateGain(st, x));
                        drawMeters(g, meters, new[] { "In", "Out" }, new[] { _inDb, _outDb }, "Gain reduction", _grDb, 0, double.NaN);
                        break;
                    case AetherStripDefs.Comp:
                        drawCurve(g, plot, -60, x => compOut(st, x));
                        drawMeters(g, meters, new[] { "In", "Out" }, new[] { _inDb, _outDb }, "Comp / limiter GR", _grDb, _gr2Db, double.NaN);
                        break;
                    case AetherStripDefs.DeEss:
                        drawBand(g, plot, st);
                        drawMeters(g, meters, new[] { "In" }, new[] { _inDb }, "Sibilance reduction", _grDb, 0, double.NaN);
                        break;
                    case AetherStripDefs.Tube:
                        drawShaper(g, plot, st);
                        drawMeters(g, meters, new[] { "In", "Out", "Drive" }, new[] { _inDb, _outDb, _extraDb - 24 }, null, 0, 0, double.NaN);
                        break;
                    case AetherStripDefs.Reverb:
                        drawTail(g, plot, st);
                        drawMeters(g, meters, new[] { "In", "Out", "Wet" }, new[] { _inDb, _outDb, _extraDb }, null, 0, 0, double.NaN);
                        break;
                    case AetherStripDefs.Limiter:
                        drawCurve(g, plot, -40, x => Math.Min(x + st.Get(AetherStripDefs.Limiter, 2), st.Get(AetherStripDefs.Limiter, 1)));
                        drawMeters(g, meters, new[] { "Peak", "RMS" }, new[] { _outDb, _extraDb }, "Gain reduction", _grDb, 0, st.Get(AetherStripDefs.Limiter, 1));
                        break;
                }
                if (!_live)
                    using (Font f = new Font("Segoe UI", 11f, GraphicsUnit.Pixel))
                    using (Brush b = new SolidBrush(kTextDim))
                        g.DrawString(_f.IsRX ? "Meters show while receiving on RX1 with this stage on" : "Meters show while transmitting with this stage on",
                            f, b, meters.X, meters.Bottom - 12);
            }

            // AetherSDR ClientGate::staticCurveGainDb
            private static double gateGain(AetherStrip st, double x)
            {
                double T = st.Get(AetherStripDefs.Gate, 2), slope = st.Get(AetherStripDefs.Gate, 3) - 1, floor = st.Get(AetherStripDefs.Gate, 7);
                double shortfall = T - x;
                return shortfall <= 0 ? 0 : Math.Max(-shortfall * slope, floor);
            }

            // drive, AetherSDR ClientComp::staticCurveGainDb (soft knee), makeup, then the output limiter
            private static double compOut(AetherStrip st, double x)
            {
                const int C = AetherStripDefs.Comp;
                double env = x + st.Get(C, 9), T = st.Get(C, 1), W = st.Get(C, 5), slope = 1 - 1 / st.Get(C, 2), over = env - T, gain;
                if (W <= 0) gain = over > 0 ? -over * slope : 0;
                else if (over <= -0.5 * W) gain = 0;
                else if (over >= 0.5 * W) gain = -over * slope;
                else { double q = over + 0.5 * W; gain = -slope * q * q / (2 * W); }
                double y = env + gain + st.Get(C, 6);
                return st.Get(C, 7) != 0 ? Math.Min(y, st.Get(C, 8)) : y;
            }

            private void drawGrid(Graphics g, Rectangle r, double lo, string xLabel, string yLabel)
            {
                using (Brush b = new SolidBrush(Color.FromArgb(0x06, 0x0e, 0x17))) g.FillRectangle(b, r);
                using (Pen p = new Pen(kGrid))
                    for (int i = 0; i <= 4; i++)
                    {
                        float x = r.X + r.Width * i / 4f, y = r.Y + r.Height * i / 4f;
                        g.DrawLine(p, x, r.Y, x, r.Bottom);
                        g.DrawLine(p, r.X, y, r.Right, y);
                    }
                using (Font f = new Font("Segoe UI", 10f, GraphicsUnit.Pixel))
                using (Brush b = new SolidBrush(kTextDim))
                {
                    if (xLabel != null) g.DrawString(xLabel, f, b, r.X + 2, r.Bottom - 14);
                    if (yLabel != null) g.DrawString(yLabel, f, b, r.X + 2, r.Y + 2);
                }
            }

            // input dB -> output dB transfer curve, with the 1:1 line and the live input level
            private void drawCurve(Graphics g, Rectangle r, double lo, Func<double, double> fn)
            {
                drawGrid(g, r, lo, "in " + lo + "..0 dB", "out");
                Func<double, float> X = v => (float)(r.X + (v - lo) / -lo * r.Width);
                Func<double, float> Y = v => (float)(r.Bottom - (Math.Max(lo, Math.Min(0, v)) - lo) / -lo * r.Height);
                using (Pen p = new Pen(Color.FromArgb(0x30, 0x40, 0x50)) { DashStyle = DashStyle.Dash }) g.DrawLine(p, X(lo), Y(lo), X(0), Y(0));
                List<PointF> pts = new List<PointF>();
                for (int i = 0; i <= 200; i++) { double x = lo + -lo * i / 200.0; pts.Add(new PointF(X(x), Y(fn(x)))); }
                using (Pen p = new Pen(kAmber, 2f)) g.DrawLines(p, pts.ToArray());
                if (_live && _inDb > lo)
                    using (Brush b = new SolidBrush(kText)) g.FillEllipse(b, X(_inDb) - 4, Y(fn(_inDb)) - 4, 8, 8);
            }

            // de-esser sidechain band: |H| of N cascaded RBJ band-pass sections, 200 Hz .. 16 kHz
            private void drawBand(Graphics g, Rectangle r, AetherStrip st)
            {
                drawGrid(g, r, 0, "200 Hz .. 16 kHz", "band");
                double f0 = st.Get(AetherStripDefs.DeEss, 1), q = st.Get(AetherStripDefs.DeEss, 2);
                int n = (int)Math.Round(st.Get(AetherStripDefs.DeEss, 7));
                List<PointF> pts = new List<PointF>();
                for (int i = 0; i <= 200; i++)
                {
                    double f = 200 * Math.Pow(80, i / 200.0), u = f / f0 - f0 / f;
                    double mag = Math.Pow(1 / Math.Sqrt(1 + q * q * u * u), n);       // 0 dB at the centre
                    double db = Math.Max(-48, 20 * Math.Log10(mag));
                    pts.Add(new PointF(r.X + r.Width * i / 200f, (float)(r.Y + r.Height * (-db / 48))));
                }
                using (Pen p = new Pen(kAmber, 2f)) g.DrawLines(p, pts.ToArray());
            }

            // tube: soft-clip transfer shape for the current drive and bias (illustrative)
            private void drawShaper(Graphics g, Rectangle r, AetherStrip st)
            {
                drawGrid(g, r, 0, "in", "out");
                double drive = Math.Pow(10, st.Get(AetherStripDefs.Tube, 2) / 20), bias = st.Get(AetherStripDefs.Tube, 3);
                int model = (int)Math.Round(st.Get(AetherStripDefs.Tube, 1));
                List<PointF> pts = new List<PointF>();
                for (int i = 0; i <= 200; i++)
                {
                    double x = -1 + 2 * i / 200.0, d = x * drive, y;
                    if (model == 1) y = Math.Tanh(Math.Max(-1.2, Math.Min(1.2, d)));
                    else if (model == 2) y = d >= 0 ? Math.Tanh(d * (1 + bias)) : Math.Tanh(d * (1 - 0.5 * bias)) * 0.8;
                    else y = Math.Tanh(d + bias * 0.3) - Math.Tanh(bias * 0.3);
                    y = Math.Max(-1, Math.Min(1, y));
                    pts.Add(new PointF(r.X + r.Width * i / 200f, (float)(r.Y + r.Height * (1 - y) / 2)));
                }
                using (Pen p = new Pen(Color.FromArgb(0x30, 0x40, 0x50)) { DashStyle = DashStyle.Dash }) g.DrawLine(p, r.X, r.Bottom, r.Right, r.Y);
                using (Pen p = new Pen(kAmber, 2f)) g.DrawLines(p, pts.ToArray());
            }

            // reverb: pre-delay gap, then the decay envelope over 3 seconds
            private void drawTail(Graphics g, Rectangle r, AetherStrip st)
            {
                drawGrid(g, r, 0, "0 .. 3 s", "tail");
                double pre = st.Get(AetherStripDefs.Reverb, 4) / 1000, decay = st.Get(AetherStripDefs.Reverb, 2), mix = st.Get(AetherStripDefs.Reverb, 5);
                double damp = st.Get(AetherStripDefs.Reverb, 3);
                List<PointF> pts = new List<PointF> { new PointF(r.X, r.Bottom) };
                for (int i = 0; i <= 300; i++)
                {
                    double t = 3.0 * i / 300, a = t < pre ? 0 : Math.Pow(10, -3 * (t - pre) / decay) * (0.35 + 0.65 * mix);  // -60 dB at the decay time
                    pts.Add(new PointF(r.X + r.Width * i / 300f, (float)(r.Bottom - r.Height * a)));
                }
                pts.Add(new PointF(r.Right, r.Bottom));
                using (Brush b = new SolidBrush(Color.FromArgb(70 + (int)(60 * (1 - damp)), 0xf2, 0xc1, 0x4e))) g.FillPolygon(b, pts.ToArray());
                using (Pen p = new Pen(kAmber, 1.5f)) g.DrawLines(p, pts.GetRange(1, pts.Count - 2).ToArray());
            }

            // vertical level bars (-60..0 dB) and an optional gain-reduction bar (0..-30 dB)
            private void drawMeters(Graphics g, Rectangle r, string[] names, double[] levels, string grName, double gr, double gr2, double ceiling)
            {
                using (Font f = new Font("Segoe UI", 11f, FontStyle.Bold, GraphicsUnit.Pixel))
                using (Font small = new Font("Segoe UI", 10f, GraphicsUnit.Pixel))
                using (Brush text = new SolidBrush(kTextMid))
                {
                    int barW = 34, gap = 26, top = r.Y + 4, h = r.Height - 60, x = r.X;
                    for (int i = 0; i < names.Length; i++, x += barW + gap)
                    {
                        Rectangle bar = new Rectangle(x, top, barW, h);
                        using (Brush b = new SolidBrush(Color.FromArgb(0x06, 0x0e, 0x17))) g.FillRectangle(b, bar);
                        double frac = Math.Max(0, Math.Min(1, (levels[i] + 60) / 60));
                        int fh = (int)(bar.Height * frac);
                        using (Brush b = new LinearGradientBrush(bar, kAmber, kBlue, LinearGradientMode.Vertical))
                            if (fh > 0) g.FillRectangle(b, bar.X, bar.Bottom - fh, bar.Width, fh);
                        if (!double.IsNaN(ceiling))
                        {
                            int cy = bar.Bottom - (int)(bar.Height * Math.Max(0, Math.Min(1, (ceiling + 60) / 60)));
                            using (Pen p = new Pen(kAmber, 2f)) g.DrawLine(p, bar.X - 4, cy, bar.Right + 4, cy);
                        }
                        g.DrawString(names[i], f, text, x, bar.Bottom + 4);
                        g.DrawString(levels[i] <= -119 ? "-" : levels[i].ToString("0"), small, text, x, bar.Bottom + 18);
                    }
                    if (grName != null)
                    {
                        x += 20;
                        double[] grs = gr2 != 0 ? new[] { gr, gr2 } : new[] { gr };
                        for (int i = 0; i < grs.Length; i++, x += barW + 8)
                        {
                            Rectangle bar = new Rectangle(x, top, barW, h);
                            using (Brush b = new SolidBrush(Color.FromArgb(0x06, 0x0e, 0x17))) g.FillRectangle(b, bar);
                            int fh = (int)(bar.Height * Math.Max(0, Math.Min(1, -grs[i] / 30)));
                            using (Brush b = new SolidBrush(Color.FromArgb(0xe0, 0x60, 0x40))) if (fh > 0) g.FillRectangle(b, bar.X, bar.Y, bar.Width, fh);
                            g.DrawString(grs[i].ToString("0.0"), small, text, x, bar.Bottom + 18);
                        }
                        g.DrawString(grName, f, text, x - (barW + 8) * grs.Length, top + h + 4);
                    }
                }
            }

        }

        #endregion
    }

    #endregion
}
