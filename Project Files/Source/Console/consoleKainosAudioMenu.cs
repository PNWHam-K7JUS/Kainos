/*  consoleKainosAudioMenu.cs

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
using System.Windows.Forms;

namespace Thetis
{
    // The Kainos layout's Audio menu: everything about audio in one place on the menu bar, in place of the separate
    // Equalizer and Kainos Audio items (Classic keeps Thetis's menu bar as it was)
    public partial class Console
    {
        private ToolStripMenuItem _kainosAudioMenu;

        private void kainosAudioMenuCreate()
        {
            if (_kainosAudioMenu != null) return;
            _kainosAudioMenu = new ToolStripMenuItem("Audio") { Name = "kainosAudioMenu", Visible = false };
            Func<string, string, Action, ToolStripMenuItem> item = (text, tip, act) =>
            {
                ToolStripMenuItem it = new ToolStripMenuItem(text) { ToolTipText = tip };
                it.Click += (s, e) => act();
                return it;
            };
            _kainosAudioMenu.DropDownItems.AddRange(new ToolStripItem[]
            {
                item("Kainos Audio: Receive...", "Kainos Audio's receive chain: EQ, gate, compressor, tube and AetherVoice.", () => kainosOpenAudio(true)),
                item("Kainos Audio: Transmit...", "Kainos Audio's transmit chain: gate, EQ, de-esser, compressor, tube, AetherVoice, VST3 plugin, reverb and limiter.", () => kainosOpenAudio(false)),
                item("VST3 Plugin...", "Choose or open the VST3 plugin in the transmit chain (experimental).", kainosOpenPluginPage),
                item("Equalizer...", "Thetis's receive and transmit equalizer.", () => equalizerToolStripMenuItem_Click(this, EventArgs.Empty)),
                new ToolStripSeparator(),
                item("VAC 1 Settings...", "Virtual audio cable 1 (Setup > Audio).", () => SetupForm.ShowSetupTab(Setup.SetupTab.VAC1_Tab)),
                item("VAC 2 Settings...", "Virtual audio cable 2 (Setup > Audio).", () => SetupForm.ShowSetupTab(Setup.SetupTab.VAC2_Tab)),
                item("VOX / DEXP Settings...", "Voice-operated transmit and the downward expander (Setup > DSP).", () => SetupForm.ShowSetupTab(Setup.SetupTab.VOXDE_Tab)),
                item("CFC Settings...", "Thetis's continuous frequency compressor (Setup > DSP).", () => SetupForm.ShowSetupTab(Setup.SetupTab.CFC_Tab)),
            });
            menuStrip1.Items.Insert(menuStrip1.Items.IndexOf(equalizerToolStripMenuItem), _kainosAudioMenu);
        }

        // the Kainos layout: Audio in place of Equalizer and Kainos Audio; Classic: as Thetis had it
        private void kainosAudioMenuOn(bool on)
        {
            kainosAudioMenuCreate();
            _kainosAudioMenu.Visible = on;
            equalizerToolStripMenuItem.Visible = !on;
            if (kainosAudioToolStripMenuItem != null) kainosAudioToolStripMenuItem.Visible = !on;
        }

        private void kainosOpenPluginPage()
        {
            ShowKainosAudio();
            if (_frmAether != null && !_frmAether.IsDisposed) _frmAether.ShowPluginPage();
        }
    }
}
