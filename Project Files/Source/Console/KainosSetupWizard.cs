/*  KainosSetupWizard.cs

This file is part of Kainos, a program that implements a Software Defined Radio.

Copyright (C) 2026 Justin Cron, K7JUS

This program is free software; you can redistribute it and/or modify it under the terms of the GNU General Public
License as published by the Free Software Foundation; either version 2 of the License, or (at your option) any later
version.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied
warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License along with this program; if not, write to the Free
Software Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Thetis
{
    // What the setup wizard asks, read from Setup and written back to it (setupKainosWizard.cs)
    internal class KainosWizardAnswers
    {
        public string Callsign = "", Grid = "", Country = "", Licence = "";
        public bool N2adr, IoBoard, Pa, BandVolts;
        public bool Ext10MHz, Cl2;
        public decimal Cl2Freq, TxLatency, PttHang;
        // audio: VAC1 (the HL2 has no audio output of its own: receive audio and the mic go through the PC)
        public bool AudioOn;
        public string AudioHost = "", AudioOut = "", AudioIn = "";
        // look
        public int Layout = 1;                      // 0 Classic, 1 Kainos
        public string UIScale = "100%";
        public Dictionary<string, bool> Tabs = new Dictionary<string, bool>();     // right-hand column tab key: open
        public List<KeyValuePair<string, string>> TabTitles = new List<KeyValuePair<string, string>>();

        public KainosWizardAnswers Copy()
        {
            KainosWizardAnswers a = (KainosWizardAnswers)MemberwiseClone();
            a.Tabs = new Dictionary<string, bool>(Tabs);
            return a;
        }
    }

    // Countries and their licence classes, for the station page (the band plans will use them)
    internal static class KainosLicences
    {
        public static readonly KeyValuePair<string, string[]>[] Countries =
        {
            new KeyValuePair<string, string[]>("United States", new[] { "Technician", "General", "Amateur Extra" }),
            new KeyValuePair<string, string[]>("Canada", new[] { "Basic", "Basic with Honours", "Advanced" }),
            new KeyValuePair<string, string[]>("United Kingdom", new[] { "Foundation", "Intermediate", "Full" }),
            new KeyValuePair<string, string[]>("Australia", new[] { "Foundation", "Standard", "Advanced" }),
            new KeyValuePair<string, string[]>("Germany", new[] { "Class N", "Class E", "Class A" }),
            new KeyValuePair<string, string[]>("Other", new[] { "Full" }),
        };

        public static string[] ClassesFor(string country)
        {
            KeyValuePair<string, string[]> c = Countries.FirstOrDefault(k => k.Key == country);
            return c.Value ?? new string[0];
        }
    }

    // The setup wizard: a welcome (offered once to existing users, or straight to the questions after the first-run
    // choice), the station, the HL2's hardware, and a summary of what will change. Nothing is set until Apply.
    internal class KainosSetupWizard : Form
    {
        private readonly KainosWizardAnswers _was, _now;
        private readonly bool _offer;
        private readonly List<Panel> _pages = new List<Panel>();
        private readonly string[] _titles;
        private int _page;
        private readonly Label _title, _steps;
        private readonly Panel _body;
        private readonly KainosWizardButton _back, _next, _cancel;

        // the station page
        private TextBox _call, _grid;
        private ComboBox _country, _licence;
        // the hardware page
        private CheckBox _n2adr, _io, _pa, _bandVolts, _ext10, _cl2;
        private NumericUpDown _cl2Freq, _txLat, _pttHang;
        private Panel _advanced;
        // the audio page
        private CheckBox _audioOn;
        private ComboBox _host, _out, _in;
        // the look page
        private ComboBox _layout, _scale;
        private readonly Dictionary<string, CheckBox> _tabs = new Dictionary<string, CheckBox>();
        private Label _summary;

        public KainosWizardAnswers Answers { get { return _now; } }
        public bool NotNow { get; private set; }

        public KainosSetupWizard(KainosWizardAnswers current, bool offer)
        {
            _was = current;
            _now = current.Copy();
            _offer = offer;

            Text = "Kainos setup";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = KainosWindowTheme.WindowBg;
            ForeColor = KainosWindowTheme.Text;
            Font = new Font("Segoe UI", 9.5f);
            AutoScaleDimensions = new SizeF(96f, 96f);     // laid out at 96 dpi, scaled for the screen
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(620, 470);

            _title = new Label { Location = new Point(24, 16), Size = new Size(572, 30), Font = new Font("Segoe UI", 15f, FontStyle.Bold), ForeColor = KainosUI.GoldHi };
            _steps = new Label { Location = new Point(26, 48), Size = new Size(570, 20), ForeColor = KainosWindowTheme.TextDim };
            _body = new Panel { Location = new Point(24, 78), Size = new Size(572, 330), BackColor = KainosWindowTheme.WindowBg };
            _back = new KainosWizardButton("Back") { Location = new Point(24, 422), Size = new Size(100, 32) };
            _cancel = new KainosWizardButton(offer ? "Not now" : "Cancel") { Location = new Point(386, 422), Size = new Size(100, 32) };
            _next = new KainosWizardButton("Next") { Location = new Point(496, 422), Size = new Size(100, 32), Accent = true };
            _back.Click += (s, e) => show(_page - 1);
            _next.Click += (s, e) => { if (_page == _pages.Count - 1) { DialogResult = DialogResult.OK; Close(); } else show(_page + 1); };
            _cancel.Click += (s, e) => { NotNow = _offer; DialogResult = DialogResult.Cancel; Close(); };
            Controls.AddRange(new Control[] { _title, _steps, _body, _back, _cancel, _next });
            KeyPreview = true;
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) { NotNow = _offer; DialogResult = DialogResult.Cancel; Close(); } };

            if (offer) _pages.Add(welcomePage());
            _pages.Add(stationPage());
            _pages.Add(hardwarePage());
            _pages.Add(audioPage());
            _pages.Add(lookPage());
            _pages.Add(summaryPage());
            _titles = (offer ? new[] { "Welcome" } : new string[0]).Concat(new[] { "Station", "Hardware", "Audio", "Look", "Summary" }).ToArray();
            show(0);
        }

        private void show(int page)
        {
            if (page < 0 || page >= _pages.Count) return;
            read();
            _page = page;
            _body.Controls.Clear();
            _body.Controls.Add(_pages[page]);
            _title.Text = (string)_pages[page].Tag;
            _steps.Text = string.Join("   ·   ", _titles.Select((t, i) => i == page ? "● " + t : t));
            _back.Visible = page > 0;
            _next.Text = page == _pages.Count - 1 ? "Apply" : _offer && page == 0 ? "Set up" : "Next";
            if (_pages[page] == _summaryPage) _summary.Text = summaryText();
            _back.Invalidate(); _next.Invalidate();
        }

        // ---- pages ----

        private Panel newPage(string title)
        {
            return new Panel { Dock = DockStyle.Fill, Tag = title, BackColor = KainosWindowTheme.WindowBg };
        }

        // w: a field's caption is narrow, so it doesn't cover the field beside it
        private Label text(Panel p, string s, int y, int h, bool dim = false, int w = 560)
        {
            Label l = new Label { Text = s, Location = new Point(0, y), Size = new Size(w, h), ForeColor = dim ? KainosWindowTheme.TextMid : KainosWindowTheme.Text };
            p.Controls.Add(l);
            return l;
        }

        private Panel welcomePage()
        {
            Panel p = newPage("Set up Kainos for your Hermes Lite 2");
            text(p, "Kainos can set itself up for your Hermes Lite 2 in a few steps: your callsign, which boards your HL2 has, " +
                    "and a check of what will change before anything is set.", 0, 60);
            text(p, "Nothing changes until you press Apply on the last page, and you can run this again any time from " +
                    "Setup > Appearance > Kainos > Run setup wizard.", 70, 50, true);
            text(p, "Choose Not now to keep your settings as they are.", 130, 24, true);
            return p;
        }

        private Panel stationPage()
        {
            Panel p = newPage("Your station");
            text(p, "Your callsign and grid square are used for DX cluster spots, FreeDV RADE (sent at the end of each over) " +
                    "and the FreeDV Reporter.", 0, 44, true);
            text(p, "Callsign", 58, 20, false, 130);
            _call = field(new TextBox { Location = new Point(140, 54), Size = new Size(140, 24), CharacterCasing = CharacterCasing.Upper, MaxLength = 8, Text = _now.Callsign });
            p.Controls.Add(_call);
            text(p, "Grid square", 94, 20, false, 130);
            _grid = field(new TextBox { Location = new Point(140, 90), Size = new Size(90, 24), CharacterCasing = CharacterCasing.Upper, MaxLength = 6, Text = _now.Grid });
            p.Controls.Add(_grid);

            text(p, "Where you operate and your licence class. Kainos will use these to show where you may transmit on the " +
                    "panadapter (coming in a later version).", 140, 44, true);
            text(p, "Country", 196, 20, false, 130);
            // the lists are Kainos drop-downs (as Band / Mode / Filter) over combo boxes that aren't shown
            _country = hidden(p);
            _country.Items.Add("");
            _country.Items.AddRange(KainosLicences.Countries.Select(c => (object)c.Key).ToArray());
            text(p, "Licence class", 232, 20, false, 130);
            _licence = hidden(p);
            _country.SelectedIndexChanged += (s, e) =>
            {
                _licence.Items.Clear();
                _licence.Items.AddRange(KainosLicences.ClassesFor(_country.Text));
                _licence.Enabled = _licence.Items.Count > 0;
                if (_licence.Items.Count > 0) _licence.SelectedIndex = Math.Max(0, _licence.Items.IndexOf(_now.Licence));
            };
            _country.SelectedIndex = Math.Max(0, _country.Items.IndexOf(_now.Country));
            p.Controls.Add(new KainosDropDown(() => _country, null) { Location = new Point(140, 188), Size = new Size(220, 28) });
            p.Controls.Add(new KainosDropDown(() => _licence, null) { Location = new Point(140, 224), Size = new Size(220, 28) });
            return p;
        }

        private Panel hardwarePage()
        {
            Panel p = newPage("Your Hermes Lite 2");
            text(p, "Tick what your HL2 has. Kainos sets the matching options for you.", 0, 22, true);
            int y = 30;
            _n2adr = check(p, "N2ADR filter board", "Sets the filter switching for each band to suit the N2ADR board.", ref y, _now.N2adr);
            _io = check(p, "HL2 I/O board", "Turns on the I/O board's band outputs and controls.", ref y, _now.IoBoard);
            _pa = check(p, "Use the HL2's built-in power amplifier", "Leave off if you only want the low-level output (eg into a transverter).", ref y, _now.Pa);
            _bandVolts = check(p, "My external amplifier needs band data (Band Volts)", "A band voltage on the HL2's output for amplifiers that switch band by voltage.", ref y, _now.BandVolts);

            CheckBox more = new CheckBox { Text = "Show advanced options", Location = new Point(0, y + 4), AutoSize = true, ForeColor = KainosWindowTheme.TextMid };
            p.Controls.Add(more);
            _advanced = new Panel { Location = new Point(0, y + 32), Size = new Size(560, 100), Visible = false, BackColor = KainosWindowTheme.WindowBg };
            more.CheckedChanged += (s, e) => _advanced.Visible = more.Checked;
            p.Controls.Add(_advanced);
            _ext10 = new CheckBox { Text = "External 10 MHz reference on CL1", Location = new Point(0, 0), AutoSize = true, Checked = _now.Ext10MHz, ForeColor = KainosWindowTheme.Text };
            _cl2 = new CheckBox { Text = "CL2 clock output, MHz", Location = new Point(0, 28), AutoSize = true, Checked = _now.Cl2, ForeColor = KainosWindowTheme.Text };
            _cl2Freq = updown(200, 26, _now.Cl2Freq, 0, 200, 3);
            Label lat = new Label { Text = "TX latency (ms)", Location = new Point(0, 62), AutoSize = true };
            _txLat = updown(110, 60, _now.TxLatency, 0, 1000, 0);
            Label hang = new Label { Text = "PTT hang (ms)", Location = new Point(220, 62), AutoSize = true };
            _pttHang = updown(320, 60, _now.PttHang, 0, 1000, 0);
            _advanced.Controls.AddRange(new Control[] { _ext10, _cl2, _cl2Freq, lat, _txLat, hang, _pttHang });
            return p;
        }

        private Panel audioPage()
        {
            Panel p = newPage("Audio");
            text(p, "The HL2 has no speaker or headphone output of its own, so receive audio and your microphone go through this " +
                    "PC's sound devices (Thetis's VAC 1).", 0, 44, true);
            _audioOn = new CheckBox { Text = "Play receive audio on this PC, and use a PC microphone  (recommended)", Location = new Point(0, 50), AutoSize = true, Checked = _now.AudioOn, ForeColor = KainosWindowTheme.Text, Font = new Font(Font, FontStyle.Bold) };
            p.Controls.Add(_audioOn);

            _host = hidden(p); _out = hidden(p); _in = hidden(p);
            text(p, "Audio system", 92, 20, false, 130);
            p.Controls.Add(new KainosDropDown(() => _host, null) { Location = new Point(140, 88), Size = new Size(300, 28) });
            text(p, "Speakers", 128, 20, false, 130);
            p.Controls.Add(new KainosDropDown(() => _out, null) { Location = new Point(140, 124), Size = new Size(420, 28) });
            text(p, "Microphone", 164, 20, false, 130);
            p.Controls.Add(new KainosDropDown(() => _in, null) { Location = new Point(140, 160), Size = new Size(420, 28) });

            foreach (string h in hosts()) _host.Items.Add(h);
            _host.SelectedIndexChanged += (s, e) =>
            {
                fill(_out, false, _now.AudioOut);
                fill(_in, true, _now.AudioIn);
            };
            int hi = _host.Items.IndexOf(_now.AudioHost);
            if (hi < 0) hi = _host.Items.IndexOf("MME");
            if (_host.Items.Count > 0) _host.SelectedIndex = Math.Max(0, hi);

            KainosWizardButton tone = new KainosWizardButton("Play a test tone") { Location = new Point(140, 202), Size = new Size(160, 30) };
            tone.Click += (s, e) => playTone(_out.Text);
            p.Controls.Add(tone);
            text(p, "Using a sound card interface or a virtual cable for digital modes? Pick it here, or set it later in " +
                    "Setup > Audio > VAC 1.", 248, 44, true);
            return p;
        }

        private ComboBox hidden(Panel p)
        {
            ComboBox c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Visible = false };
            p.Controls.Add(c);
            return c;
        }

        // the audio systems (PortAudio host APIs) that have any devices, as Setup lists them
        private static List<string> hosts()
        {
            List<string> list = new List<string>();
            try
            {
                int i = 0;
                foreach (string name in Audio.GetPAHosts())
                {
                    if (Audio.GetPAInputDevices(i).Count > 0 || Audio.GetPAOutputDevices(i).Count > 0) list.Add(name);
                    i++;
                }
            }
            catch { }
            return list;
        }

        private void fill(ComboBox c, bool input, string want)
        {
            c.Items.Clear();
            try
            {
                int host = Audio.GetPAHosts().Cast<string>().ToList().IndexOf(_host.Text);
                if (host >= 0)
                    foreach (PADeviceInfo d in input ? Audio.GetPAInputDevices(host) : Audio.GetPAOutputDevices(host)) c.Items.Add(d.Name);
            }
            catch { }
            if (c.Items.Count > 0) c.SelectedIndex = Math.Max(0, c.Items.IndexOf(want));
        }

        // a second of 700 Hz on the chosen speakers (by name: Windows's device list shortens names to 31 characters)
        private static void playTone(string device)
        {
            try
            {
                int dev = -1;
                for (int i = 0; i < WaveOut.DeviceCount; i++)
                {
                    string n = WaveOut.GetCapabilities(i).ProductName;
                    if (n.Length > 0 && (device.StartsWith(n) || n.StartsWith(device))) { dev = i; break; }
                }
                SignalGenerator tone = new SignalGenerator(48000, 1) { Frequency = 700, Gain = 0.2, Type = SignalGeneratorType.Sin };
                WaveOutEvent wo = new WaveOutEvent { DeviceNumber = dev };
                wo.Init(tone.Take(TimeSpan.FromSeconds(1)));
                wo.PlaybackStopped += (s, e) => wo.Dispose();
                wo.Play();
            }
            catch (Exception ex)
            {
                MessageBox.Show("The test tone couldn't be played on that device.\r\n\r\n" + ex.Message, "Kainos setup", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private Panel lookPage()
        {
            Panel p = newPage("Look and feel");
            _layout = hidden(p); _scale = hidden(p);
            text(p, "Layout", 4, 20, false, 130);
            _layout.Items.AddRange(new object[] { "Classic (Thetis)", "Kainos" });
            _layout.SelectedIndex = _now.Layout == 0 ? 0 : 1;
            p.Controls.Add(new KainosDropDown(() => _layout, null) { Location = new Point(140, 0), Size = new Size(220, 28) });
            text(p, "UI scale", 40, 20, false, 130);
            _scale.Items.AddRange(new object[] { "75%", "90%", "100%", "110%", "125%", "150%", "175%", "200%" });
            _scale.SelectedIndex = Math.Max(0, _scale.Items.IndexOf(_now.UIScale));
            p.Controls.Add(new KainosDropDown(() => _scale, null) { Location = new Point(140, 36), Size = new Size(120, 28) });
            text(p, "On top of Windows's own display scaling. 100% suits most screens; choose a larger size if the text is hard to read.", 72, 40, true);

            if (_now.TabTitles.Count > 0)
            {
                text(p, "Tabs open in the right-hand column (any can be opened or closed later by clicking it):", 120, 22);
                int i = 0;
                foreach (KeyValuePair<string, string> t in _now.TabTitles)
                {
                    bool on;
                    _now.Tabs.TryGetValue(t.Key, out on);
                    CheckBox c = new CheckBox { Text = t.Value, Checked = on, AutoSize = true, ForeColor = KainosWindowTheme.Text, Location = new Point(i % 3 * 190, 148 + i / 3 * 28) };
                    _tabs[t.Key] = c;
                    p.Controls.Add(c);
                    i++;
                }
            }
            return p;
        }

        private Panel _summaryPage;
        private Panel summaryPage()
        {
            Panel p = newPage("Ready to apply");
            _summary = text(p, "", 0, 320);
            _summaryPage = p;
            return p;
        }

        // ---- reading the pages, and what changes ----

        private void read()
        {
            if (_call == null) return;
            _now.Callsign = _call.Text.Trim().ToUpperInvariant();
            _now.Grid = _grid.Text.Trim().ToUpperInvariant();
            _now.Country = _country.Text;
            _now.Licence = _licence.Enabled ? _licence.Text : "";
            if (_n2adr == null) return;
            _now.N2adr = _n2adr.Checked; _now.IoBoard = _io.Checked; _now.Pa = _pa.Checked; _now.BandVolts = _bandVolts.Checked;
            _now.Ext10MHz = _ext10.Checked; _now.Cl2 = _cl2.Checked;
            _now.Cl2Freq = _cl2Freq.Value; _now.TxLatency = _txLat.Value; _now.PttHang = _pttHang.Value;
            if (_audioOn == null) return;
            _now.AudioOn = _audioOn.Checked;
            _now.AudioHost = _host.Text; _now.AudioOut = _out.Text; _now.AudioIn = _in.Text;
            if (_layout == null) return;
            _now.Layout = _layout.SelectedIndex;
            _now.UIScale = _scale.Text;
            foreach (KeyValuePair<string, CheckBox> t in _tabs) _now.Tabs[t.Key] = t.Value.Checked;
        }

        private string summaryText()
        {
            List<string> lines = new List<string>();
            Action<string, string, string> diff = (what, a, b) => { if (a != b) lines.Add("•  " + what + ":  " + (a.Length > 0 ? a : "(none)") + "  →  " + (b.Length > 0 ? b : "(none)")); };
            Func<bool, string> onOff = v => v ? "on" : "off";
            diff("Callsign", _was.Callsign, _now.Callsign);
            diff("Grid square", _was.Grid, _now.Grid);
            diff("Country", _was.Country, _now.Country);
            diff("Licence class", _was.Licence, _now.Licence);
            diff("N2ADR filter board", onOff(_was.N2adr), onOff(_now.N2adr));
            diff("HL2 I/O board", onOff(_was.IoBoard), onOff(_now.IoBoard));
            diff("Built-in power amplifier", onOff(_was.Pa), onOff(_now.Pa));
            diff("Band Volts", onOff(_was.BandVolts), onOff(_now.BandVolts));
            diff("External 10 MHz on CL1", onOff(_was.Ext10MHz), onOff(_now.Ext10MHz));
            diff("CL2 output", onOff(_was.Cl2), onOff(_now.Cl2));
            diff("CL2 frequency (MHz)", _was.Cl2Freq.ToString(), _now.Cl2Freq.ToString());
            diff("TX latency (ms)", _was.TxLatency.ToString(), _now.TxLatency.ToString());
            diff("PTT hang (ms)", _was.PttHang.ToString(), _now.PttHang.ToString());
            diff("Audio through this PC (VAC 1)", onOff(_was.AudioOn), onOff(_now.AudioOn));
            if (_now.AudioOn)
            {
                diff("Audio system", _was.AudioHost, _now.AudioHost);
                diff("Speakers", _was.AudioOut, _now.AudioOut);
                diff("Microphone", _was.AudioIn, _now.AudioIn);
            }
            diff("Layout", _was.Layout == 0 ? "Classic" : "Kainos", _now.Layout == 0 ? "Classic" : "Kainos");
            diff("UI scale", _was.UIScale, _now.UIScale);
            Func<string, string> title = k => _now.TabTitles.Where(t => t.Key == k).Select(t => t.Value).FirstOrDefault() ?? k;
            string opened = string.Join(", ", _now.Tabs.Where(t => t.Value && !(_was.Tabs.ContainsKey(t.Key) && _was.Tabs[t.Key])).Select(t => title(t.Key)));
            string closed = string.Join(", ", _now.Tabs.Where(t => !t.Value && _was.Tabs.ContainsKey(t.Key) && _was.Tabs[t.Key]).Select(t => title(t.Key)));
            if (opened.Length > 0) lines.Add("•  Tabs to open:  " + opened);
            if (closed.Length > 0) lines.Add("•  Tabs to close:  " + closed);
            if (lines.Count == 0) return "Nothing to change: your settings already match your answers.\r\n\r\nPress Apply to finish.";
            if (_now.N2adr && !_was.N2adr)
                lines.Add("\r\nThe N2ADR preset replaces the band filter pins in Setup > General > Ant/Filters.");
            if (!_now.N2adr && _was.N2adr)
                lines.Add("\r\nTurning the N2ADR preset off clears its band filter pins.");
            return "These settings will change:\r\n\r\n" + string.Join("\r\n", lines) + "\r\n\r\nPress Apply to set them, or Back to change your answers.";
        }

        // ---- controls in the Kainos colours ----

        private static T field<T>(T c) where T : Control
        {
            c.BackColor = KainosWindowTheme.FieldBg;
            c.ForeColor = KainosWindowTheme.Text;
            return c;
        }

        private CheckBox check(Panel p, string label, string hint, ref int y, bool on)
        {
            CheckBox c = new CheckBox { Text = label, Location = new Point(0, y), AutoSize = true, Checked = on, ForeColor = KainosWindowTheme.Text, Font = new Font(Font, FontStyle.Bold) };
            p.Controls.Add(c);
            p.Controls.Add(new Label { Text = hint, Location = new Point(20, y + 24), Size = new Size(540, 20), ForeColor = KainosWindowTheme.TextMid });
            y += 54;
            return c;
        }

        private static NumericUpDown updown(int x, int y, decimal value, decimal min, decimal max, int decimals)
        {
            NumericUpDown u = field(new NumericUpDown { Location = new Point(x, y), Size = new Size(90, 24), Minimum = min, Maximum = max, DecimalPlaces = decimals });
            u.Value = Math.Max(min, Math.Min(max, value));
            return u;
        }
    }

    // The first run, with a Thetis install found and no Kainos settings yet (before the settings load): set up for the
    // HL2 with fresh settings, import the Thetis settings (then the same questions), or skip. Choice: "hl2", "import",
    // "skip".
    internal class KainosFirstRunChoice : Form
    {
        public string Choice = "skip";

        public KainosFirstRunChoice(string thetis_data_path)
        {
            Text = "Welcome to Kainos";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            BackColor = KainosWindowTheme.WindowBg;
            ForeColor = KainosWindowTheme.Text;
            Font = new Font("Segoe UI", 9.5f);
            AutoScaleDimensions = new SizeF(96f, 96f);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(560, 400);

            Controls.Add(new Label { Text = "Welcome to Kainos", Location = new Point(24, 16), Size = new Size(510, 30), Font = new Font("Segoe UI", 15f, FontStyle.Bold), ForeColor = KainosUI.GoldHi });
            Controls.Add(new Label
            {
                Text = "Kainos keeps its settings separately from Thetis, and a Thetis install was found. How would you like to start?",
                Location = new Point(24, 52), Size = new Size(510, 40),
            });
            int y = 100;
            option("Set up for my Hermes Lite 2  (recommended)", "Start with fresh settings. A few questions (callsign, which boards your HL2 has) and Kainos sets the rest.", "hl2", ref y, true);
            option("Import my Thetis settings", "Copy your Thetis databases, meters, skins and cmASIO settings (Thetis is not changed), then the same questions. Close Thetis first.", "import", ref y, false);
            option("Skip", "Start with Kainos's default settings and set everything up yourself.", "skip", ref y, false);
            Controls.Add(new Label { Text = thetis_data_path, Location = new Point(24, 370), Size = new Size(510, 20), ForeColor = KainosWindowTheme.TextDim });
        }

        private void option(string title, string hint, string choice, ref int y, bool accent)
        {
            KainosWizardButton b = new KainosWizardButton(title) { Location = new Point(24, y), Size = new Size(510, 34), Accent = accent };
            b.Click += (s, e) => { Choice = choice; DialogResult = DialogResult.OK; Close(); };
            Controls.Add(b);
            Controls.Add(new Label { Text = hint, Location = new Point(28, y + 38), Size = new Size(506, 36), ForeColor = KainosWindowTheme.TextMid });
            y += 86;
        }
    }

    // A wizard button drawn like the rest of Kainos's (rounded; the main one gold)
    internal class KainosWizardButton : Control
    {
        public bool Accent;
        private bool _hover;

        public KainosWizardButton(string text)
        {
            Text = text;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent != null ? Parent.BackColor : KainosWindowTheme.WindowBg);
            KainosUI.DrawButton(e.Graphics, new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), Text, Accent, Enabled, _hover, KainosUI.Tone.Gold, Height * 0.4f);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; Invalidate(); }
        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }
    }
}
