/*  setupKainos.cs

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
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace Thetis
{
    // One side (receive or transmit) of the AetherVoice settings on Setup > DSP > AetherVoice.
    internal class AetherVoiceSetupControls
    {
        public GroupBoxTS Group;
        public CheckBoxTS Enable;
        public ComboBoxTS Mode;
        public NumericUpDownTS BodyDrive, BodyTune, BodyMix;              // dB, Hz, %
        public NumericUpDownTS ClarityTune, ClarityHarmonics, ClarityMix; // Hz, dB, %

        public NumericUpDownTS[] UpDowns
        {
            get { return new[] { BodyDrive, BodyTune, BodyMix, ClarityTune, ClarityHarmonics, ClarityMix }; }
        }

        public Control[] All
        {
            get { return new Control[] { Enable, Mode, BodyDrive, BodyTune, BodyMix, ClarityTune, ClarityHarmonics, ClarityMix }; }
        }
    }

    // Kainos additions to the Setup window. The controls are built in code rather than in
    // setup.designer.cs so that merging new Thetis releases doesn't conflict with them.
    // They use the Thetis 'TS' control types, so getOptions/saveOptions persist them by name.
    public partial class Setup
    {
        private TabPage tpDSPAetherVoice;
        private AetherVoiceSetupControls _aetherVoiceRX;
        private AetherVoiceSetupControls _aetherVoiceTX;
        // AetherRX strip settings (AetherStrip.Serialize text), kept in a hidden TextBoxTS so the
        // Setup options save and restore them like every other setting
        private TextBoxTS txtAetherStripRX;
        private bool _syncingStripRX;

        // The AetherVoice window (frmAetherVoice) reads and writes these controls directly, so the
        // window, this tab and the console AV button always agree and save the same way.
        internal AetherVoiceSetupControls AetherVoiceRX { get { return _aetherVoiceRX; } }
        internal AetherVoiceSetupControls AetherVoiceTX { get { return _aetherVoiceTX; } }

        // called from the constructor straight after InitializeComponent, before saved options are restored
        private void addKainosControls()
        {
            addAetherVoiceTab();
        }

        #region AetherVoice

        private void addAetherVoiceTab()
        {
            tpDSPAetherVoice = new TabPage("AetherVoice")
            {
                Name = "tpDSPAetherVoice",
                BackColor = SystemColors.Control,
                Padding = new Padding(3)
            };

            // receive: names are unchanged from the first release so saved settings carry over
            _aetherVoiceRX = buildAetherVoiceGroup("RX", "AetherVoice exciter - Receive", "Enable on receive (voice modes only)", 8);
            toolTip1.SetToolTip(_aetherVoiceRX.Enable, "Adds low-end body and high-end clarity to received voice, on every receiver.\r\n" +
                "It is bypassed automatically in CW, digital, DRM and SPEC modes so it never affects decoders.");
            _aetherVoiceTX = buildAetherVoiceGroup("TX", "AetherVoice exciter - Transmit (saved in each TX profile)", "Enable on transmit (voice modes only)", 208);
            toolTip1.SetToolTip(_aetherVoiceTX.Enable, "Adds low-end body and high-end clarity to your transmitted voice. It runs after the TX EQ and\r\n" +
                "before the leveler, CFC, compressor, TX filter and ALC, so the TX filter removes anything outside your bandwidth.\r\n" +
                "It is bypassed automatically in CW and digital modes. These settings are saved in each TX profile.");

            LabelTS note = avLabel("AetherVoice is ported from AetherSDR.\r\n\r\n" +
                "For knobs, open the AetherVoice window from the menu bar, or right-click the AV button on the console.\r\n\r\n" +
                "High Drive, Harmonics or Mix settings raise the level. On receive, lower the Mix or the volume if the audio distorts. " +
                "On transmit, the leveler and ALC follow it; test into a dummy load and check your signal on a second receiver first.", 450, 16);
            note.AutoSize = false;
            note.Size = new Size(264, 220);
            tpDSPAetherVoice.Controls.Add(note);

            txtAetherStripRX = new TextBoxTS { Name = "txtAetherStripRX", Visible = false, Text = AetherStrip.DefaultsSerialized() };
            txtAetherStripRX.TextChanged += (s, e) =>
            {
                hookAetherStripRX();
                if (!_syncingStripRX) console.AetherStripRX.Deserialize(txtAetherStripRX.Text);
            };
            tpDSPAetherVoice.Controls.Add(txtAetherStripRX);

            foreach (Control c in _aetherVoiceRX.All) hookChanged(c, aetherVoiceRX_Changed);
            foreach (Control c in _aetherVoiceTX.All) hookChanged(c, aetherVoiceTX_Changed);

            tcDSP.Controls.Add(tpDSPAetherVoice);
        }

        private AetherVoiceSetupControls buildAetherVoiceGroup(string side, string title, string enableText, int y)
        {
            AetherVoiceSetupControls s = new AetherVoiceSetupControls();
            s.Group = new GroupBoxTS
            {
                Name = "grpAetherVoice" + side,
                Text = title,
                Location = new Point(8, y),
                Size = new Size(430, 190)
            };
            s.Enable = new CheckBoxTS
            {
                Name = "chkAetherVoice" + side,
                Text = enableText,
                Location = new Point(14, 20),
                AutoSize = true
            };
            s.Mode = new ComboBoxTS
            {
                Name = "comboAetherVoice" + side + "Mode",
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(120, 44),
                Size = new Size(110, 21)
            };
            s.Mode.Items.AddRange(new object[] { "Aphex", "Behringer" });
            s.Mode.SelectedIndex = 0;
            toolTip1.SetToolTip(s.Mode, "Aphex: warm, asymmetric shaping with even and odd harmonics, and Big Bottom low-end saturation.\r\n" +
                "Behringer: brighter, symmetric shaping with odd harmonics, and a low-end compressor instead of saturation.");

            s.Group.Controls.Add(s.Enable);
            s.Group.Controls.Add(avLabel("Mode", 14, 47));
            s.Group.Controls.Add(s.Mode);

            // Body: the low band ("Poo" in AetherSDR); Clarity: the high band ("Doo")
            s.Group.Controls.Add(avLabel("Body (low band)", 14, 78, true));
            s.Group.Controls.Add(avLabel("Clarity (high band)", 224, 78, true));
            s.BodyDrive = avUpDown("udAetherVoice" + side + "BodyDrive", 0, 24, 0, 0.5m, 1, 120, 100,
                "How hard the low band is driven: saturation depth in Aphex mode, compression in Behringer mode.");
            s.BodyTune = avUpDown("udAetherVoice" + side + "BodyTune", 50, 160, 100, 5, 0, 120, 126,
                "Corner frequency of the low band.");
            s.BodyMix = avUpDown("udAetherVoice" + side + "BodyMix", 0, 100, 50, 5, 0, 120, 152,
                "How much of the processed low band is added to the original audio.");
            s.ClarityTune = avUpDown("udAetherVoice" + side + "ClarityTune", 1000, 10000, 5000, 100, 0, 340, 100,
                "Corner frequency of the high band: harmonics are generated above this frequency.");
            s.ClarityHarmonics = avUpDown("udAetherVoice" + side + "ClarityHarmonics", 0, 24, 6, 0.5m, 1, 340, 126,
                "How hard the high band is driven into the harmonic generator ('Air' in the AetherVoice window).");
            s.ClarityMix = avUpDown("udAetherVoice" + side + "ClarityMix", 0, 100, 50, 5, 0, 340, 152,
                "How much of the generated harmonics is added to the original audio.");
            s.Group.Controls.Add(avLabel("Drive (dB)", 26, 102));
            s.Group.Controls.Add(avLabel("Tune (Hz)", 26, 128));
            s.Group.Controls.Add(avLabel("Mix (%)", 26, 154));
            s.Group.Controls.Add(avLabel("Tune (Hz)", 236, 102));
            s.Group.Controls.Add(avLabel("Harmonics (dB)", 236, 128));
            s.Group.Controls.Add(avLabel("Mix (%)", 236, 154));
            foreach (NumericUpDownTS ud in s.UpDowns) s.Group.Controls.Add(ud);

            tpDSPAetherVoice.Controls.Add(s.Group);
            return s;
        }

        // the tab is built before Setup has its console, so the RX strip is connected on first use
        private bool _aetherStripRXHooked;
        internal void hookAetherStripRX()
        {
            if (_aetherStripRXHooked || console == null) return;
            _aetherStripRXHooked = true;
            console.AetherStripRX.Changed += (s, e) =>
            {
                _syncingStripRX = true;
                txtAetherStripRX.Text = console.AetherStripRX.Serialize();
                _syncingStripRX = false;
            };
        }

        private static void hookChanged(Control c, EventHandler h)
        {
            if (c is CheckBoxTS) ((CheckBoxTS)c).CheckedChanged += h;
            else if (c is ComboBoxTS) ((ComboBoxTS)c).SelectedIndexChanged += h;
            else if (c is NumericUpDownTS) ((NumericUpDownTS)c).ValueChanged += h;
        }

        private LabelTS avLabel(string text, int x, int y, bool bold = false)
        {
            LabelTS l = new LabelTS
            {
                Text = text,
                Location = new Point(x, y),
                AutoSize = true
            };
            if (bold) l.Font = new Font(l.Font, FontStyle.Bold);
            return l;
        }

        private NumericUpDownTS avUpDown(string name, decimal min, decimal max, decimal value, decimal increment, int decimals,
            int x, int y, string tooltip)
        {
            NumericUpDownTS ud = new NumericUpDownTS
            {
                Name = name,
                Minimum = min,
                Maximum = max,
                Value = value,
                Increment = increment,
                DecimalPlaces = decimals,
                TinyStep = false,
                Location = new Point(x, y),
                Size = new Size(64, 20)
            };
            toolTip1.SetToolTip(ud, tooltip);
            return ud;
        }

        private void aetherVoiceRX_Changed(object sender, EventArgs e)
        {
            if (initializing) return;
            applyAetherVoiceRX();
        }

        private void aetherVoiceTX_Changed(object sender, EventArgs e)
        {
            if (initializing) return;
            applyAetherVoiceTX();
        }

        // called from ForceAllEvents at startup, and whenever a setting changes
        private void applyAetherVoice()
        {
            hookAetherStripRX();
            applyAetherVoiceRX();
            applyAetherVoiceTX();
            console.AetherStripTX.ApplyAll();
            console.AetherStripRX.ApplyAll();
        }

        // the AetherRX BYPASS button also bypasses AetherVoice RX
        internal void ApplyAetherVoiceRXFromStrip()
        {
            applyAetherVoiceRX();
        }

        // the AetherTX BYPASS button also bypasses AetherVoice TX
        internal void ApplyAetherVoiceTXFromStrip()
        {
            applyAetherVoiceTX();
        }

        // pushes the receive settings to every receiver (RX1, RX1 sub, RX2, RX2 sub)
        private void applyAetherVoiceRX()
        {
            AetherVoiceSetupControls s = _aetherVoiceRX;
            int mode = Math.Max(0, s.Mode.SelectedIndex);
            for (int thread = 0; thread < 2; thread++)
            {
                for (int subrx = 0; subrx < 2; subrx++)
                {
                    RadioDSPRX rx = console.radio.GetDSPRX(thread, subrx);
                    rx.RXAetherVoiceMode = mode;
                    rx.SetRXAetherVoiceBody((double)s.BodyDrive.Value, (double)s.BodyTune.Value, (double)s.BodyMix.Value / 100.0);
                    rx.SetRXAetherVoiceClarity((double)s.ClarityTune.Value, (double)s.ClarityHarmonics.Value, (double)s.ClarityMix.Value / 100.0);
                    rx.RXAetherVoiceOn = s.Enable.Checked && !console.AetherStripRX.Bypass;
                }
            }
            console.AetherVoiceRXChanged(s.Enable.Checked);
        }

        private void applyAetherVoiceTX()
        {
            AetherVoiceSetupControls s = _aetherVoiceTX;
            RadioDSPTX tx = console.radio.GetDSPTX(0);
            tx.TXAetherVoiceMode = Math.Max(0, s.Mode.SelectedIndex);
            tx.SetTXAetherVoiceBody((double)s.BodyDrive.Value, (double)s.BodyTune.Value, (double)s.BodyMix.Value / 100.0);
            tx.SetTXAetherVoiceClarity((double)s.ClarityTune.Value, (double)s.ClarityHarmonics.Value, (double)s.ClarityMix.Value / 100.0);
            tx.TXAetherVoiceOn = s.Enable.Checked && !console.AetherStripTX.Bypass;
        }

        #endregion

        #region AetherVoice TX profile

        // TX profile columns. They are added to the TX profile table the first time they are needed,
        // so databases created by Thetis or earlier Kainos builds keep working; a profile without
        // values loads with AetherVoice off and the default settings.
        private const string AV_TX_ENABLED = "AetherVoiceTXEnabled";
        private const string AV_TX_MODE = "AetherVoiceTXMode";
        private static readonly string[] AV_TX_VALUES = { "AetherVoiceTXBodyDrive", "AetherVoiceTXBodyTune", "AetherVoiceTXBodyMix",
            "AetherVoiceTXClarityTune", "AetherVoiceTXClarityHarmonics", "AetherVoiceTXClarityMix" };
        private static readonly decimal[] AV_TX_DEFAULTS = { 0, 100, 50, 5000, 6, 50 };
        private static readonly string[] AV_TX_LABELS = { "Body Drive", "Body Tune", "Body Mix", "Clarity Tune", "Clarity Harmonics", "Clarity Mix" };

        // the whole AetherTX channel strip, as AetherStripTX.Serialize() text
        private const string AV_TX_STRIP = "AetherStripTX";

        private static string profileStrip(DataRow dr)
        {
            return profileHas(dr, AV_TX_STRIP) ? Convert.ToString(dr[AV_TX_STRIP]) : null;
        }

        private static void ensureAetherVoiceTXColumns(DataTable t)
        {
            if (!t.Columns.Contains(AV_TX_STRIP)) t.Columns.Add(AV_TX_STRIP, typeof(string));
            if (!t.Columns.Contains(AV_TX_ENABLED)) t.Columns.Add(AV_TX_ENABLED, typeof(bool));
            if (!t.Columns.Contains(AV_TX_MODE)) t.Columns.Add(AV_TX_MODE, typeof(int));
            foreach (string col in AV_TX_VALUES)
                if (!t.Columns.Contains(col)) t.Columns.Add(col, typeof(double));
        }

        private static bool profileHas(DataRow dr, string col)
        {
            return dr.Table.Columns.Contains(col) && dr[col] != DBNull.Value;
        }

        private static bool profileEnabled(DataRow dr) { return profileHas(dr, AV_TX_ENABLED) && Convert.ToBoolean(dr[AV_TX_ENABLED]); }
        private static int profileMode(DataRow dr) { return profileHas(dr, AV_TX_MODE) ? Convert.ToInt32(dr[AV_TX_MODE]) : 0; }
        private static decimal profileValue(DataRow dr, int i)
        {
            return profileHas(dr, AV_TX_VALUES[i]) ? (decimal)Convert.ToDouble(dr[AV_TX_VALUES[i]]) : AV_TX_DEFAULTS[i];
        }

        // updateTXProfileInDB
        private void saveAetherVoiceTXProfile(DataRow dr)
        {
            ensureAetherVoiceTXColumns(dr.Table);
            AetherVoiceSetupControls s = _aetherVoiceTX;
            dr[AV_TX_ENABLED] = s.Enable.Checked;
            dr[AV_TX_MODE] = Math.Max(0, s.Mode.SelectedIndex);
            NumericUpDownTS[] uds = s.UpDowns;
            for (int i = 0; i < uds.Length; i++) dr[AV_TX_VALUES[i]] = (double)uds[i].Value;
            dr[AV_TX_STRIP] = console.AetherStripTX.Serialize();
        }

        // loadTXProfile
        private void loadAetherVoiceTXProfile(DataRow dr)
        {
            AetherVoiceSetupControls s = _aetherVoiceTX;
            NumericUpDownTS[] uds = s.UpDowns;
            for (int i = 0; i < uds.Length; i++)
                uds[i].Value = Math.Max(uds[i].Minimum, Math.Min(uds[i].Maximum, profileValue(dr, i)));
            s.Mode.SelectedIndex = profileMode(dr) == 1 ? 1 : 0;
            s.Enable.Checked = profileEnabled(dr);
            console.AetherStripTX.Deserialize(profileStrip(dr));   // a profile without it loads with the strip off
        }

        // checkTXProfileChanged2
        private bool aetherVoiceTXProfileDiffers(DataRow dr)
        {
            AetherVoiceSetupControls s = _aetherVoiceTX;
            if (profileEnabled(dr) != s.Enable.Checked) return true;
            if (profileMode(dr) != Math.Max(0, s.Mode.SelectedIndex)) return true;
            NumericUpDownTS[] uds = s.UpDowns;
            for (int i = 0; i < uds.Length; i++)
                if (profileValue(dr, i) != uds[i].Value) return true;
            return console.AetherStripTX.Differs(profileStrip(dr));
        }

        // getTXProfileChangeReport
        private string aetherVoiceTXProfileReport(DataRow dr)
        {
            AetherVoiceSetupControls s = _aetherVoiceTX;
            string report = "";
            if (profileEnabled(dr) != s.Enable.Checked)
                report += "AetherVoice TX enabled: " + profileEnabled(dr) + " -> " + s.Enable.Checked + Environment.NewLine;
            if (profileMode(dr) != Math.Max(0, s.Mode.SelectedIndex))
                report += "AetherVoice TX mode changed" + Environment.NewLine;
            NumericUpDownTS[] uds = s.UpDowns;
            for (int i = 0; i < uds.Length; i++)
                if (profileValue(dr, i) != uds[i].Value)
                    report += "AetherVoice TX " + AV_TX_LABELS[i] + ": " + profileValue(dr, i) + " -> " + uds[i].Value + Environment.NewLine;
            if (console.AetherStripTX.Differs(profileStrip(dr)))
                report += "AetherTX channel strip changed" + Environment.NewLine;
            return report;
        }

        // highlightTXProfileSaveItems
        private void highlightAetherVoiceTXProfileItems(bool bHighlight)
        {
            foreach (Control c in _aetherVoiceTX.All)
                Common.HightlightControl(c, bHighlight);
        }

        #endregion
    }
}
