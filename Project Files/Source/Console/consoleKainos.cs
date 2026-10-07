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
        private ToolStripMenuItem kainosAudioToolStripMenuItem;
        private CheckBoxTS chkAetherVoice;
        private frmAetherVoice _frmAetherVoice;
        private frmAetherStrip _frmAether;

        // the AetherSDR channel strip on transmit; its settings are saved in each TX profile
        public AetherStrip AetherStripTX { get; private set; }
        // the AetherSDR channel strip on receive (all receivers); saved with the Setup options
        public AetherStrip AetherStripRX { get; private set; }

        // called from the constructor straight after InitializeComponent, before the skin is applied
        private void addKainosControls()
        {
            AetherStripTX = new AetherStrip(this, false);
            AetherStripRX = new AetherStrip(this, true);

            // Kainos Audio: one menu item, after Equalizer, for the receive and transmit audio chains
            kainosAudioToolStripMenuItem = new ToolStripMenuItem("Kainos Audio")
            {
                Name = "kainosAudioToolStripMenuItem",
                ToolTipText = "Open Kainos Audio Processing: gate, de-esser, compressor, tube, AetherVoice, reverb and limiter\r\n" +
                              "on receive and transmit (processing ported from AetherSDR)"
            };
            kainosAudioToolStripMenuItem.Click += (s, e) => ShowKainosAudio();
            menuStrip1.Items.Insert(menuStrip1.Items.IndexOf(equalizerToolStripMenuItem) + 1, kainosAudioToolStripMenuItem);
            kainosSupportMenus();       // consoleKainosSupport.cs: backup, light mode, updates, report a bug

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

            addRadeControls();
            addRttyControls();          // consoleKainosRtty.cs
            addCwControls();            // consoleKainosCw.cs
            // built-in spotting (consoleKainosSpots.cs): starts once the console is up, stops when it closes
            Shown += (s, e) => kainosSpotsStart();
            // every window Kainos opens in the Kainos Audio look (KainosWindowTheme.cs)
            Shown += (s, e) => KainosWindowTheme.Start();
            Shown += (s, e) => kainosWizardOnStartup();         // consoleKainosWizard.cs
            FormClosing += (s, e) => KainosSpotting.Stop();
            matchKainosMenuItems();     // consoleKainosLayout.cs
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

        // Kainos Audio Processing: one window with RX and TX tabs. From the menu it reopens on whichever
        // tab was last used (receive the first time)
        public void ShowKainosAudio()
        {
            if (_frmAether == null || _frmAether.IsDisposed)
            {
                _frmAether = new frmAetherStrip(this, true) { Owner = this };
                _frmAether.Location = new Point(Left + (Width - _frmAether.Width) / 2, Top + (Height - _frmAether.Height) / 2);
            }
            _frmAether.Show();
            _frmAether.BringToFront();
            _frmAether.Activate();
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
