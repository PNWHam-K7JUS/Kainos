/*  setupKainosUI.cs

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
using System.Drawing;
using System.Windows.Forms;

namespace Thetis
{
    // Setup > Appearance > Kainos: the console layout (Classic or Kainos)
    public partial class Setup
    {
        // the PA profile list, for the console's PA PROFILE tab
        internal ComboBox KainosPAProfileCombo { get { return comboPAProfile; } }

        private TabPage tpAppearanceKainos;
        private ComboBoxTS comboKainosLayout, comboKainosUIScale, comboKainosBackdropLogo;
        private CheckBoxTS chkKainosBackdrop;
        // saved with the options: which right-column tabs are on, and the METERS tab's meter container
        private TextBoxTS txtKainosColumnTabs, txtKainosMeterId, txtKainosMeterType, txtKainosFtdxOffered, txtKainosRtty, txtKainosCw, txtKainosRttyMacros, txtKainosCwMacros, txtKainos3D;
        private bool _kainosSettingsHooked;

        private void addKainosUITab()
        {
            tpAppearanceKainos = new TabPage("Kainos")
            {
                Name = "tpAppearanceKainos",
                BackColor = SystemColors.Control,
                Padding = new Padding(3)
            };

            GroupBoxTS grp = new GroupBoxTS { Name = "grpKainosLayout", Text = "Console layout", Location = new Point(8, 8), Size = new Size(430, 150) };
            comboKainosLayout = new ComboBoxTS
            {
                Name = "comboKainosLayout",
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(100, 24),
                Size = new Size(120, 21)
            };
            comboKainosLayout.Items.AddRange(new object[] { "Classic", "Kainos" });
            comboKainosLayout.SelectedIndex = 1;      // Kainos by default (a fresh install, or settings brought over from Thetis, which has no such setting)
            toolTip1.SetToolTip(comboKainosLayout, "Classic: the Thetis console exactly as your skin draws it.\r\n" +
                "Kainos: the console in the Kainos colours. Switch back to Classic at any time.");
            comboKainosLayout.SelectedIndexChanged += (s, e) => { if (!initializing) applyKainosUI(); };
            grp.Controls.Add(avLabel("Layout", 14, 27));
            grp.Controls.Add(comboKainosLayout);
            comboKainosUIScale = new ComboBoxTS
            {
                Name = "comboKainosUIScale",
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(320, 24),
                Size = new Size(90, 21)
            };
            comboKainosUIScale.Items.AddRange(new object[] { "75%", "90%", "100%", "110%", "125%", "150%", "175%", "200%" });
            comboKainosUIScale.SelectedIndex = 2;
            toolTip1.SetToolTip(comboKainosUIScale, "Size of the Kainos layout's own controls (the left dock, and the right column and slice flags\r\n" +
                "as they arrive), on top of Windows's display scaling.");
            comboKainosUIScale.SelectedIndexChanged += (s, e) => { if (!initializing) applyKainosUI(); };
            grp.Controls.Add(avLabel("UI scale", 250, 27));
            grp.Controls.Add(comboKainosUIScale);
            LabelTS note = avLabel("Kainos: the console in the Kainos colours, with the left-hand column of buttons, the " +
                "right-hand column of tabs, the slice flags on the panadapter and the bar under it. Right-click a button " +
                "for the same settings shortcut as in Classic. Classic: Thetis's console exactly as your skin draws it.", 14, 60);
            note.AutoSize = false;
            note.Size = new Size(404, 82);
            grp.Controls.Add(note);
            tpAppearanceKainos.Controls.Add(grp);

            // the panadapter's backdrop in Kainos layout (displayKainos.cs)
            GroupBoxTS grpPan = new GroupBoxTS { Name = "grpKainosPanadapter", Text = "Panadapter (Kainos layout)", Location = new Point(8, 166), Size = new Size(430, 84) };
            chkKainosBackdrop = new CheckBoxTS { Name = "chkKainosBackdrop", Text = "Kainos background (dark navy, with the Kainos logo behind the trace)", Location = new Point(14, 22), AutoSize = true, Checked = true };
            chkKainosBackdrop.CheckedChanged += (s, e) => { if (!initializing) applyKainosUI(); };
            comboKainosBackdropLogo = new ComboBoxTS { Name = "comboKainosBackdropLogo", DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(100, 50), Size = new Size(140, 21) };
            comboKainosBackdropLogo.Items.AddRange(new object[] { "Off", "Faint (6%)", "Light (12%)", "Medium (20%)" });
            comboKainosBackdropLogo.SelectedIndex = 2;
            toolTip1.SetToolTip(comboKainosBackdropLogo, "How strongly the Kainos logo shows through the panadapter's background.");
            comboKainosBackdropLogo.SelectedIndexChanged += (s, e) => { if (!initializing) applyKainosUI(); };
            grpPan.Controls.Add(chkKainosBackdrop);
            grpPan.Controls.Add(avLabel("Logo", 14, 53));
            grpPan.Controls.Add(comboKainosBackdropLogo);
            tpAppearanceKainos.Controls.Add(grpPan);

            txtKainosColumnTabs = new TextBoxTS { Name = "txtKainosColumnTabs", Visible = false, Text = "meters,band" };
            txtKainosMeterId = new TextBoxTS { Name = "txtKainosMeterId", Visible = false, Text = "" };
            txtKainosMeterType = new TextBoxTS { Name = "txtKainosMeterType", Visible = false, Text = "ANANMM" };
            txtKainosFtdxOffered = new TextBoxTS { Name = "txtKainosFtdxOffered", Visible = false, Text = "" };
            tpAppearanceKainos.Controls.Add(txtKainosColumnTabs);
            tpAppearanceKainos.Controls.Add(txtKainosMeterId);
            tpAppearanceKainos.Controls.Add(txtKainosMeterType);
            tpAppearanceKainos.Controls.Add(txtKainosFtdxOffered);
            txtKainosRtty = new TextBoxTS { Name = "txtKainosRtty", Visible = false, Text = "" };
            tpAppearanceKainos.Controls.Add(txtKainosRtty);
            txtKainosCw = new TextBoxTS { Name = "txtKainosCw", Visible = false, Text = "" };
            tpAppearanceKainos.Controls.Add(txtKainosCw);
            txtKainosRttyMacros = new TextBoxTS { Name = "txtKainosRttyMacros", Visible = false, Text = "" };
            txtKainosCwMacros = new TextBoxTS { Name = "txtKainosCwMacros", Visible = false, Text = "" };
            tpAppearanceKainos.Controls.Add(txtKainosRttyMacros);
            tpAppearanceKainos.Controls.Add(txtKainosCwMacros);
            txtKainos3D = new TextBoxTS { Name = "txtKainos3D", Visible = false, Text = "" };
            tpAppearanceKainos.Controls.Add(txtKainos3D);

            tcAppearance.Controls.Add(tpAppearanceKainos);
        }

        // called from ForceAllEvents at startup, and when the setting changes
        private void applyKainosUI()
        {
            if (!_kainosSettingsHooked)
            {
                _kainosSettingsHooked = true;
                console.KainosSettingsChanged += (s, e) =>
                {
                    txtKainosColumnTabs.Text = console.KainosColumnTabs;
                    txtKainosMeterId.Text = console.KainosMeterId;
                    txtKainosMeterType.Text = console.KainosMeterType;
                    txtKainosFtdxOffered.Text = console.KainosFtdxOffered ? "1" : "";
                    txtKainosRtty.Text = console.KainosRttySettings;
                    txtKainosCw.Text = console.KainosCwSettings;
                    txtKainosRttyMacros.Text = console.KainosRttyMacros;
                    txtKainosCwMacros.Text = console.KainosCwMacros;
                    txtKainos3D.Text = console.Kainos3DSettings;
                };
            }
            console.KainosColumnTabs = txtKainosColumnTabs.Text;
            console.KainosMeterId = txtKainosMeterId.Text;
            console.KainosMeterType = string.IsNullOrEmpty(txtKainosMeterType.Text) ? "ANANMM" : txtKainosMeterType.Text;
            console.KainosFtdxOffered = txtKainosFtdxOffered.Text == "1";
            console.KainosRttySettings = txtKainosRtty.Text;
            console.RttyLoadSettings();
            console.KainosCwSettings = txtKainosCw.Text;
            console.CwLoadSettings();
            console.KainosRttyMacros = txtKainosRttyMacros.Text;
            console.KainosCwMacros = txtKainosCwMacros.Text;
            console.RttyMacrosLoaded();
            console.CwMacrosLoaded();
            console.Kainos3DSettings = txtKainos3D.Text;
            console.Kainos3DLoad();
            Display.KainosBackdrop = chkKainosBackdrop.Checked;
            Display.KainosBackdropLogo = new[] { 0f, 0.06f, 0.12f, 0.20f }[Math.Max(0, comboKainosBackdropLogo.SelectedIndex)];
            comboKainosBackdropLogo.Enabled = chkKainosBackdrop.Checked;
            int pct;
            if (int.TryParse(comboKainosUIScale.Text.TrimEnd('%'), out pct)) KainosUI.SetScalePercent(pct);
            console.KainosLayout = comboKainosLayout.SelectedIndex == 1;
        }
    }
}
