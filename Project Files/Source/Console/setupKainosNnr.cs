/*  setupKainosNnr.cs

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
    // Setup > DSP > NNR: the settings for Kainos NNR (consoleKainosNnr.cs). They apply to every receiver; the NNR
    // button on each VFO flag's DSP tab turns it on and off for that receiver.
    public partial class Setup
    {
        private TabPage tpDSPNnr;
        private ComboBoxTS comboNnrModel;
        private NumericUpDownTS udNnrDepth;
        private CheckBoxTS chkNnrVoiceOnly;
        private TextBoxTS txtKainosNnr;

        private void addNnrTab()
        {
            tpDSPNnr = new TabPage("NNR")
            {
                Name = "tpDSPNnr",
                BackColor = SystemColors.Control,
                Padding = new Padding(3)
            };

            GroupBoxTS grp = new GroupBoxTS { Name = "grpNnr", Text = "NNR neural noise reduction", Location = new Point(8, 8), Size = new Size(430, 186) };

            comboNnrModel = new ComboBoxTS { Name = "comboNnrModel", DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(150, 22), Size = new Size(200, 21) };
            comboNnrModel.Items.AddRange(new object[] { "Standard", "Large (more CPU)" });
            comboNnrModel.SelectedIndex = 0;
            toolTip1.SetToolTip(comboNnrModel, "Which trained model NNR uses. The large model can take noise down more cleanly on weak\r\n" +
                "signals, and uses more CPU. Both are built in.");

            udNnrDepth = avUpDown("udNnrDepth", 6, 40, 25, 1, 0, 150, 50, "The most NNR takes the noise down, in dB. Lower is gentler and keeps more of the band's\r\n" +
                "sound; higher is quieter between words, with more chance of watery-sounding speech.");

            chkNnrVoiceOnly = new CheckBoxTS { Name = "chkNnrVoiceOnly", Text = "Voice modes only (stand by in CW and the digital modes)", Location = new Point(14, 108), AutoSize = true, Checked = true };
            toolTip1.SetToolTip(chkNnrVoiceOnly, "NNR is trained on speech and takes steady tones down by about 28 dB, so CW and digital\r\n" +
                "signals would be treated as noise. With this ticked, NNR stays on but only runs in LSB, USB, AM,\r\n" +
                "SAM, DSB and FM.");

            grp.Controls.Add(avLabel("Model", 14, 25));
            grp.Controls.Add(comboNnrModel);
            grp.Controls.Add(avLabel("Reduction depth (dB)", 14, 53));
            grp.Controls.Add(udNnrDepth);
            grp.Controls.Add(avLabel("Before or after the AGC: as set for the other NRs on the NR page.", 14, 81));
            grp.Controls.Add(chkNnrVoiceOnly);
            LabelTS how = avLabel("Turn NNR on for each receiver with the NNR button on its VFO flag's DSP tab (right-click it to open this page). " +
                "It can run on its own or together with NR, NR2, NR3 or NR4.", 14, 132);
            how.AutoSize = false;
            how.Size = new Size(404, 46);
            grp.Controls.Add(how);
            tpDSPNnr.Controls.Add(grp);

            LabelTS note = avLabel("NNR is WDSP 2.10's neural noise reduction, by Warren Pratt NR0V. It adds about 50 ms of delay to the receive audio.", 450, 16);
            note.AutoSize = false;
            note.Size = new Size(264, 60);
            tpDSPNnr.Controls.Add(note);

            // on / off per receiver, saved with the options like the other Kainos settings
            // (applyKainosUI in setupKainosUI.cs keeps it and the console in step)
            txtKainosNnr = new TextBoxTS { Name = "txtKainosNnr", Visible = false, Text = "" };
            tpDSPNnr.Controls.Add(txtKainosNnr);

            foreach (Control c in new Control[] { comboNnrModel, udNnrDepth, chkNnrVoiceOnly }) hookChanged(c, nnr_Changed);

            tcDSP.Controls.Add(tpDSPNnr);
        }

        private void nnr_Changed(object sender, EventArgs e)
        {
            console.KainosNnrModel = Math.Max(0, comboNnrModel.SelectedIndex);
            console.KainosNnrDepth = (int)udNnrDepth.Value;
            console.KainosNnrVoiceOnly = chkNnrVoiceOnly.Checked;
            console.KainosNnrSettingsChanged();
        }

        // opens Setup on this tab (right-click NNR on a VFO flag)
        internal void ShowNnrTab()
        {
            Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            showDSPPage();       // setupRade.cs
            tcDSP.SelectedTab = tpDSPNnr;
            BringToFront();
            Activate();
        }
    }
}
