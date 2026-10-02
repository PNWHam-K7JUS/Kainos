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
        private ComboBoxTS comboKainosLayout, comboKainosUIScale;
        // saved with the options: which right-column tabs are on, and the METERS tab's meter container
        private TextBoxTS txtKainosColumnTabs, txtKainosMeterId, txtKainosMeterType, txtKainosFtdxOffered, txtKainosRtty, txtKainosCw, txtKainosRttyMacros, txtKainosCwMacros;
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
            comboKainosLayout.SelectedIndex = 0;
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
            LabelTS note = avLabel("The Kainos layout is being built in stages (roadmap Phase 7). So far: the Kainos colours " +
                "for the console, menu bar and status bar, and the left dock with POWER, RX2, MOX, TUN, 2TON, MON, VOX, " +
                "DUP, PS-A and REC/PLAY (right-click a dock button for the same settings shortcut as in Classic), with " +
                "forward power, SWR and ALC in the status bar. Coming next: the tabbed right column (with meters) and " +
                "the slice flags.", 14, 60);
            note.AutoSize = false;
            note.Size = new Size(404, 82);
            grp.Controls.Add(note);
            tpAppearanceKainos.Controls.Add(grp);

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
            int pct;
            if (int.TryParse(comboKainosUIScale.Text.TrimEnd('%'), out pct)) KainosUI.SetScalePercent(pct);
            console.KainosLayout = comboKainosLayout.SelectedIndex == 1;
        }
    }
}
