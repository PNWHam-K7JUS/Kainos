/*  setupKainosWizard.cs

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
using System.Drawing;
using System.Windows.Forms;

namespace Thetis
{
    // The setup wizard's side of Setup: its own two settings (whether it has run, and the country / licence class),
    // the button to run it again, and reading and applying its answers through Setup's own controls, so Thetis's
    // handlers (the N2ADR preset's filter pins, the I/O board, the PA) run exactly as if the boxes were clicked.
    public partial class Setup
    {
        private TextBoxTS txtKainosWizard, txtKainosLicence;
        // light mode (consoleKainosSupport.cs): the box, and the frame rate and 3D it replaced; the update check's
        // "last=yyyy-mm-dd;skip=version"
        private CheckBoxTS chkKainosLightMode;
        private TextBoxTS txtKainosLightSaved, txtKainosUpdate;

        // called from the Kainos appearance page's setup (setupKainosUI.cs)
        private void addKainosWizardControls(TabPage page)
        {
            txtKainosWizard = new TextBoxTS { Name = "txtKainosWizard", Visible = false, Text = "" };       // "", "done", "later", "skipped"
            txtKainosLicence = new TextBoxTS { Name = "txtKainosLicence", Visible = false, Text = "" };     // country|licence class
            page.Controls.Add(txtKainosWizard);
            page.Controls.Add(txtKainosLicence);

            ButtonTS run = new ButtonTS { Name = "btnKainosWizard", Text = "Run setup wizard...", Location = new Point(8, 316), Size = new Size(160, 26) };
            toolTip1.SetToolTip(run, "Set Kainos up for your Hermes Lite 2: callsign, grid square, licence class and which boards the HL2 has.");
            run.Click += (s, e) => console.KainosRunWizard(false);
            page.Controls.Add(run);

            chkKainosLightMode = new CheckBoxTS { Name = "chkKainosLightMode", Text = "Light mode (for slower PCs: no 3D, 20 frames a second)", Location = new Point(180, 320), AutoSize = true };
            toolTip1.SetToolTip(chkKainosLightMode, "Turns the 3D panadapter off and lowers the display to 20 frames a second, so Kainos runs well on an\r\n" +
                "older or slower PC. Turning it off puts your frame rate and 3D back.");
            chkKainosLightMode.CheckedChanged += (s, e) => { if (!initializing) console.KainosSetLightMode(chkKainosLightMode.Checked, false); };
            page.Controls.Add(chkKainosLightMode);
            txtKainosLightSaved = new TextBoxTS { Name = "txtKainosLightSaved", Visible = false, Text = "" };
            txtKainosUpdate = new TextBoxTS { Name = "txtKainosUpdate", Visible = false, Text = "" };
            page.Controls.Add(txtKainosLightSaved);
            page.Controls.Add(txtKainosUpdate);

            addKainosBandPlanControls(page);
        }

        // ---- the licence-aware band plan: on or off, and the country and licence class (shared with the wizard,
        // kept in txtKainosLicence) ----
        private CheckBoxTS chkKainosBandPlan;
        private ComboBox comboKainosCountry, comboKainosLicence;     // not saved themselves: txtKainosLicence is
        private bool _kainosLicenceSync;

        private void addKainosBandPlanControls(TabPage page)
        {
            GroupBoxTS grp = new GroupBoxTS { Name = "grpKainosBandPlan", Text = "Band plan (Kainos layout)", Location = new Point(446, 8), Size = new Size(280, 298) };
            chkKainosBandPlan = new CheckBoxTS { Name = "chkKainosBandPlan", Text = "Show where I may transmit, along the\r\ntop of the panadapter", Location = new Point(12, 22), Size = new Size(270, 34), Checked = true };
            chkKainosBandPlan.CheckedChanged += (s, e) => { if (!initializing) kainosBandPlanApply(); };
            grp.Controls.Add(chkKainosBandPlan);
            grp.Controls.Add(new LabelTS { Name = "lblKainosBPCountry", Text = "Country", Location = new Point(12, 66), AutoSize = true });
            comboKainosCountry = new ComboBox { Name = "comboKainosBPCountry", DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(96, 62), Size = new Size(180, 21) };
            comboKainosCountry.Items.Add("");
            foreach (var c in KainosLicences.Countries) comboKainosCountry.Items.Add(c.Key);
            grp.Controls.Add(new LabelTS { Name = "lblKainosBPLicence", Text = "Licence class", Location = new Point(12, 96), AutoSize = true });
            comboKainosLicence = new ComboBox { Name = "comboKainosBPLicence", DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(96, 92), Size = new Size(180, 21) };
            comboKainosCountry.SelectedIndexChanged += (s, e) =>
            {
                string keep = comboKainosLicence.Text;
                comboKainosLicence.Items.Clear();
                comboKainosLicence.Items.AddRange(KainosLicences.ClassesFor(comboKainosCountry.Text));
                if (comboKainosLicence.Items.Count > 0) comboKainosLicence.SelectedIndex = Math.Max(0, comboKainosLicence.Items.IndexOf(keep));
                kainosLicenceFromCombos();
            };
            comboKainosLicence.SelectedIndexChanged += (s, e) => kainosLicenceFromCombos();
            grp.Controls.Add(comboKainosCountry);
            grp.Controls.Add(comboKainosLicence);
            LabelTS legend = new LabelTS
            {
                Name = "lblKainosBPLegend",
                Text = "Gold: all modes (phone too)\r\nIce: CW and data\r\nViolet: CW only\r\nRed: in the band, but not your privileges\r\nGrey: band edges only\r\n\r\n" +
                       "Privileges are built in for the United States and Canada; elsewhere the band edges are shown. It's a guide: " +
                       "always check your own licence.",
                Location = new Point(12, 126), Size = new Size(268, 166),
            };
            grp.Controls.Add(legend);
            page.Controls.Add(grp);
            txtKainosLicence.TextChanged += (s, e) => kainosLicenceToCombos();
        }

        private void kainosLicenceFromCombos()
        {
            if (_kainosLicenceSync) return;
            string v = comboKainosCountry.Text + "|" + (comboKainosLicence.Items.Count > 0 ? comboKainosLicence.Text : "");
            if (txtKainosLicence.Text != v) txtKainosLicence.Text = v;      // its TextChanged applies it
        }

        private void kainosLicenceToCombos()
        {
            _kainosLicenceSync = true;
            try
            {
                string[] lic = (txtKainosLicence.Text ?? "").Split('|');
                comboKainosCountry.SelectedIndex = Math.Max(0, comboKainosCountry.Items.IndexOf(lic[0]));
                if (lic.Length > 1 && comboKainosLicence.Items.IndexOf(lic[1]) >= 0) comboKainosLicence.SelectedIndex = comboKainosLicence.Items.IndexOf(lic[1]);
            }
            finally { _kainosLicenceSync = false; }
            kainosBandPlanApply();
        }

        // to the console (also from applyKainosUI at startup)
        internal void kainosBandPlanApply()
        {
            string[] lic = (txtKainosLicence.Text ?? "").Split('|');
            console.KainosBandPlanOn = chkKainosBandPlan.Checked;
            console.KainosBandPlanSet(lic[0], lic.Length > 1 ? lic[1] : "");
        }

        internal bool KainosLight
        {
            get { return chkKainosLightMode != null && chkKainosLightMode.Checked; }
            set { if (chkKainosLightMode != null && chkKainosLightMode.Checked != value) chkKainosLightMode.Checked = value; }
        }
        internal string KainosLightSaved { get { return txtKainosLightSaved.Text; } set { txtKainosLightSaved.Text = value; } }
        internal int KainosDisplayFps
        {
            get { return (int)udDisplayFPS.Value; }
            set { udDisplayFPS.Value = Math.Max(udDisplayFPS.Minimum, Math.Min(udDisplayFPS.Maximum, value)); }
        }
        private string updateField(string key)
        {
            foreach (string kv in (txtKainosUpdate.Text ?? "").Split(';'))
                if (kv.StartsWith(key + "=")) return kv.Substring(key.Length + 1);
            return "";
        }
        private void setUpdateField(string key, string value)
        {
            string other = key == "last" ? "skip" : "last";
            txtKainosUpdate.Text = key + "=" + value + ";" + other + "=" + updateField(other);
        }
        internal string KainosUpdateLastCheck { get { return updateField("last"); } set { setUpdateField("last", value); } }
        internal string KainosUpdateSkip { get { return updateField("skip"); } set { setUpdateField("skip", value); } }

        internal string KainosWizardState
        {
            get { return txtKainosWizard != null ? txtKainosWizard.Text : "done"; }
            set { if (txtKainosWizard != null) txtKainosWizard.Text = value; }
        }

        internal KainosWizardAnswers KainosWizardRead()
        {
            string[] lic = (txtKainosLicence.Text ?? "").Split('|');
            return new KainosWizardAnswers
            {
                Callsign = _rade != null ? _rade.Callsign.Text : "",
                Grid = _rade != null ? _rade.Grid.Text : "",
                Country = lic[0],
                Licence = lic.Length > 1 ? lic[1] : "",
                N2adr = chkHERCULES.Checked,
                IoBoard = chkHL2IOBoardPresent.Checked,
                Pa = chkApolloTuner.Checked,
                BandVolts = chkHL2BandVolts.Checked,
                Ext10MHz = chkExt10MHz.Checked,
                Cl2 = chkCl2Enable.Checked,
                Cl2Freq = udCl2Freq.Value,
                TxLatency = udTxBufferLat.Value,
                PttHang = udPTTHang.Value,
                AudioOn = chkAudioEnableVAC.Checked,
                AudioHost = comboAudioDriver2.Text,
                AudioOut = comboAudioOutput2.Text,
                AudioIn = comboAudioInput2.Text,
                Layout = comboKainosLayout.SelectedIndex,
                UIScale = comboKainosUIScale.Text,
                LightMode = KainosLight,
            };
        }

        internal void KainosWizardApply(KainosWizardAnswers a)
        {
            if (_rade != null)
            {
                if (_rade.Callsign.Text != a.Callsign) _rade.Callsign.Text = a.Callsign;
                if (_rade.Grid.Text != a.Grid) _rade.Grid.Text = a.Grid;
            }
            txtKainosLicence.Text = a.Country + "|" + a.Licence;
            setChecked(chkHERCULES, a.N2adr);
            setChecked(chkHL2IOBoardPresent, a.IoBoard);
            setChecked(chkApolloTuner, a.Pa);
            setChecked(chkHL2BandVolts, a.BandVolts);
            setChecked(chkExt10MHz, a.Ext10MHz);
            setChecked(chkCl2Enable, a.Cl2);
            setValue(udCl2Freq, a.Cl2Freq);
            setValue(udTxBufferLat, a.TxLatency);
            setValue(udPTTHang, a.PttHang);

            // audio: the system first (Setup then lists its devices), then the devices, then VAC 1 on or off
            if (a.AudioOn)
            {
                select(comboAudioDriver2, a.AudioHost);
                select(comboAudioOutput2, a.AudioOut);
                select(comboAudioInput2, a.AudioIn);
            }
            setChecked(chkAudioEnableVAC, a.AudioOn);

            if (a.Layout >= 0 && comboKainosLayout.SelectedIndex != a.Layout) comboKainosLayout.SelectedIndex = a.Layout;
            select(comboKainosUIScale, a.UIScale);
            KainosLight = a.LightMode;
            KainosWizardState = "done";
            SaveOptions();
        }

        private static void setChecked(CheckBox c, bool on) { if (c.Checked != on) c.Checked = on; }

        private static void select(ComboBox c, string text)
        {
            if (string.IsNullOrEmpty(text) || c.Text == text) return;
            for (int i = 0; i < c.Items.Count; i++)
                if (c.GetItemText(c.Items[i]) == text) { c.SelectedIndex = i; return; }
        }

        private static void setValue(NumericUpDown u, decimal v)
        {
            v = Math.Max(u.Minimum, Math.Min(u.Maximum, v));
            if (u.Value != v) u.Value = v;
        }
    }
}
