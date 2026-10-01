/*  consoleKainos.cs

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
    // Kainos additions to the main console window. Built in code rather than in console.Designer.cs
    // so that merging new Thetis releases doesn't conflict with them.
    public partial class Console
    {
        private ToolStripMenuItem aetherVoiceToolStripMenuItem;
        private CheckBoxTS chkAetherVoice;
        private frmAetherVoice _frmAetherVoice;

        // called from the constructor straight after InitializeComponent, before the skin is applied
        private void addKainosControls()
        {
            // AetherVoice menu item, after Equalizer
            aetherVoiceToolStripMenuItem = new ToolStripMenuItem("AetherVoice")
            {
                Name = "aetherVoiceToolStripMenuItem",
                ToolTipText = "Open the AetherVoice receive exciter"
            };
            aetherVoiceToolStripMenuItem.Click += (s, e) => ShowAetherVoice();
            menuStrip1.Items.Insert(menuStrip1.Items.IndexOf(equalizerToolStripMenuItem) + 1, aetherVoiceToolStripMenuItem);

            // AV button on the phone-mode panel, below RX EQ and styled like it
            chkAetherVoice = new CheckBoxTS
            {
                Name = "chkAetherVoice",
                Text = "AV",
                Appearance = Appearance.Button,
                FlatStyle = chkRXEQ.FlatStyle,
                Font = chkRXEQ.Font,
                ForeColor = chkRXEQ.ForeColor,
                BackColor = SystemColors.Control,
                TextAlign = chkRXEQ.TextAlign,
                Size = chkRXEQ.Size,
                Location = new Point(chkRXEQ.Left, chkRXEQ.Top + 23),
                TabStop = false
            };
            chkAetherVoice.FlatAppearance.BorderSize = 0;
            Skin.ImageAlias[chkAetherVoice.Name] = chkRXEQ.Name;  // use RX EQ's skin images
            toolTip1.SetToolTip(chkAetherVoice, "AetherVoice receive exciter on/off (voice modes only).\r\nRight-click to open the AetherVoice window.");
            chkAetherVoice.CheckedChanged += chkAetherVoice_CheckedChanged;
            chkAetherVoice.MouseDown += chkAetherVoice_MouseDown;
            panelModeSpecificPhone.Controls.Add(chkAetherVoice);
        }

        public void ShowAetherVoice()
        {
            if (_frmAetherVoice == null || _frmAetherVoice.IsDisposed)
            {
                _frmAetherVoice = new frmAetherVoice(this) { Owner = this };
                // first time: centre it over the console
                _frmAetherVoice.Location = new Point(Left + (Width - _frmAetherVoice.Width) / 2, Top + (Height - _frmAetherVoice.Height) / 2);
            }
            _frmAetherVoice.Show();
            _frmAetherVoice.BringToFront();
            _frmAetherVoice.Activate();
        }

        // Setup calls this whenever the AetherVoice settings are applied, so the AV button follows
        // Setup and the AetherVoice window (Setup owns the setting and saves it)
        internal void AetherVoiceRXChanged(bool enabled)
        {
            if (chkAetherVoice == null) return;
            if (chkAetherVoice.Checked != enabled) chkAetherVoice.Checked = enabled;
        }

        private void chkAetherVoice_CheckedChanged(object sender, EventArgs e)
        {
            chkAetherVoice.BackColor = chkAetherVoice.Checked ? button_selected_color : SystemColors.Control;
            if (!IsSetupFormNull && SetupForm.AetherVoiceRX.Enable.Checked != chkAetherVoice.Checked)
                SetupForm.AetherVoiceRX.Enable.Checked = chkAetherVoice.Checked;
        }

        private void chkAetherVoice_MouseDown(object sender, MouseEventArgs e)
        {
            if (IsRightButton(e)) ShowAetherVoice();
        }
    }
}
