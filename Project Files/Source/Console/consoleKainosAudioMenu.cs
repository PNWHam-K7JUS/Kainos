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
    // The Kainos layout's menu bar, grouped: Audio (in place of Equalizer and Kainos Audio), Modes (FreeDV, RTTY, CW,
    // CWX) and Tools (XVTRs, Linearity, RA, Finder). The grouped items' own menu entries are hidden while it's on; the
    // new entries do what they did. Classic keeps Thetis's menu bar as it was.
    public partial class Console
    {
        private ToolStripMenuItem _kainosAudioMenu, _kainosModesMenu, _kainosToolsMenu;

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

            // Modes: the operating modes Kainos adds windows for (RTTY and CW ticked while their terminal is open)
            _kainosModesMenu = new ToolStripMenuItem("Modes") { Name = "kainosModesMenu", Visible = false };
            ToolStripMenuItem rtty = item("RTTY", "The RTTY terminal (open / close).", () => { if (_rttyOpen) RttyClose(); else RttyOpen(); });
            ToolStripMenuItem cw = item("CW", "The CW terminal (open / close).", () => { if (_cwOpen) CwClose(); else CwOpen(); });
            _kainosModesMenu.DropDownItems.AddRange(new ToolStripItem[]
            {
                item("FreeDV...", "FreeDV RADE digital voice, and the FreeDV Reporter.", ShowFreeDV),
                rtty,
                cw,
                item("CWX...", "Thetis's CW keyboard and memories.", () => cWXToolStripMenuItem_Click(this, EventArgs.Empty)),
            });
            _kainosModesMenu.DropDownOpening += (s, e) => { rtty.Checked = _rttyOpen; cw.Checked = _cwOpen; };
            menuStrip1.Items.Insert(menuStrip1.Items.IndexOf(_kainosAudioMenu) + 1, _kainosModesMenu);

            // Tools: Thetis's other windows
            _kainosToolsMenu = new ToolStripMenuItem("Tools") { Name = "kainosToolsMenu", Visible = false };
            _kainosToolsMenu.DropDownItems.AddRange(new ToolStripItem[]
            {
                item("XVTRs...", "Transverters.", () => xVTRsToolStripMenuItem_Click(this, EventArgs.Empty)),
                item("Linearity...", "PureSignal linearity correction.", () => linearityToolStripMenuItem_Click(this, EventArgs.Empty)),
                item("Radio Astronomy (RA)...", "Thetis's radio astronomy tool: logs RX1 and RX2 signal strength over time to a file.", () => RAtoolStripMenuItem_Click(this, EventArgs.Empty)),
                item("Finder...", "Find any setting in Kainos by name.", () => finderMenuItem_Click(this, EventArgs.Empty)),
            });
            menuStrip1.Items.Insert(menuStrip1.Items.IndexOf(_kainosModesMenu) + 1, _kainosToolsMenu);
        }

        // the items the grouped menus take over
        private ToolStripItem[] kainosGroupedItems
        {
            get
            {
                return new ToolStripItem[] { equalizerToolStripMenuItem, kainosAudioToolStripMenuItem, freeDVToolStripMenuItem, rttyToolStripMenuItem,
                                             cwToolStripMenuItem, cWXToolStripMenuItem, xVTRsToolStripMenuItem, linearityToolStripMenuItem,
                                             RAtoolStripMenuItem, finderMenuItem };
            }
        }

        // the Kainos layout: Audio in place of Equalizer and Kainos Audio; Classic: as Thetis had it
        private void kainosAudioMenuOn(bool on)
        {
            kainosAudioMenuCreate();
            _kainosAudioMenu.Visible = _kainosModesMenu.Visible = _kainosToolsMenu.Visible = on;
            foreach (ToolStripItem i in kainosGroupedItems) if (i != null) i.Visible = !on;
            foreach (ToolStripItem i in _kainosModuleClassicItems) i.Visible = !on;        // modules' items (consoleKainosModules.cs)
        }

        private void kainosOpenPluginPage()
        {
            ShowKainosAudio();
            if (_frmAether != null && !_frmAether.IsDisposed) _frmAether.ShowPluginPage();
        }
    }
}
