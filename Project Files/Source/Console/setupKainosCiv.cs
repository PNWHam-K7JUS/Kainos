/*  setupKainosCiv.cs

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
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace Thetis
{
    // Setup > Serial / Network / Midi CAT > Amplifier: band switching for an amp that follows an Icom radio over CI-V
    // (the Xiegu GPA100 over Bluetooth). The settings are ordinary Setup controls, saved with the options.
    public partial class Setup
    {
        private CheckBoxTS chkKainosCivOn, chkKainosCivLog;
        private ComboBoxTS comboKainosCivPort;
        private TextBoxTS txtKainosCivAddr;
        private LabelTS lblKainosCivStatus;

        private void addKainosCivTab()
        {
            TabPage tp = new TabPage("Amplifier (CI-V)") { Name = "tpKainosCiv", BackColor = SystemColors.Control, Padding = new Padding(3) };
            Func<string, string, int, int, LabelTS> label = (name, text, x, y) => new LabelTS { Name = name, Text = text, Location = new Point(x, y), AutoSize = true };

            GroupBoxTS grp = new GroupBoxTS { Name = "grpKainosCiv", Text = "Band switching over CI-V (Xiegu GPA100 by Bluetooth)", Location = new Point(8, 8), Size = new Size(360, 230) };
            chkKainosCivOn = new CheckBoxTS { Name = "chkKainosCivOn", Text = "Tell the amplifier the band (answer its CI-V polls)", Location = new Point(12, 24), AutoSize = true };
            toolTip1.SetToolTip(chkKainosCivOn, "Kainos answers the amp as an Icom IC-705 would: its transmit frequency, so the amp switches band,\r\n" +
                "and whether it is transmitting. Band switching only: the amp's PTT and ALC stay on its ACC1 cable.");
            grp.Controls.Add(chkKainosCivOn);

            grp.Controls.Add(label("lblKainosCivPort", "COM port", 12, 58));
            comboKainosCivPort = new ComboBoxTS { Name = "comboKainosCivPort", DropDownStyle = ComboBoxStyle.DropDown, Location = new Point(96, 54), Size = new Size(110, 21) };
            toolTip1.SetToolTip(comboKainosCivPort, "The amp's Bluetooth serial port. Windows makes two for it: use the outgoing one (Find GPA100 picks it).");
            comboKainosCivPort.DropDown += (s, e) => fillKainosCivPorts();
            grp.Controls.Add(comboKainosCivPort);
            ButtonTS find = new ButtonTS { Name = "btnKainosCivFind", Text = "Find GPA100", Location = new Point(214, 52), Size = new Size(132, 25) };
            toolTip1.SetToolTip(find, "Finds the paired XG_GPA100's outgoing Bluetooth COM port.");
            find.Click += (s, e) =>
            {
                Cursor = Cursors.WaitCursor;
                string why, port;
                try { port = KainosCivLink.FindAmpPort("XG_GPA100", out why); } finally { Cursor = Cursors.Default; }
                if (port != null) comboKainosCivPort.Text = port;
                MessageBox.Show(this, why, "Find GPA100", MessageBoxButtons.OK, port != null ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            };
            grp.Controls.Add(find);

            grp.Controls.Add(label("lblKainosCivAddr", "CI-V address", 12, 92));
            txtKainosCivAddr = new TextBoxTS { Name = "txtKainosCivAddr", Text = "A4", Location = new Point(96, 88), Size = new Size(40, 20) };
            toolTip1.SetToolTip(txtKainosCivAddr, "The radio's CI-V address, in hex, that the amp asks. The GPA100 asks for an IC-705: A4.");
            grp.Controls.Add(txtKainosCivAddr);
            grp.Controls.Add(label("lblKainosCivAddrHint", "hex (IC-705: A4)", 142, 92));

            chkKainosCivLog = new CheckBoxTS { Name = "chkKainosCivLog", Text = "Log the CI-V traffic (KainosCivLog.txt, for troubleshooting)", Location = new Point(12, 122), AutoSize = true };
            grp.Controls.Add(chkKainosCivLog);

            lblKainosCivStatus = new LabelTS { Name = "lblKainosCivStatus", Text = "Off", Location = new Point(12, 152), Size = new Size(336, 34), ForeColor = SystemColors.GrayText };
            grp.Controls.Add(lblKainosCivStatus);

            ButtonTS bt = new ButtonTS { Name = "btnKainosCivBluetooth", Text = "Open Windows Bluetooth settings", Location = new Point(12, 192), Size = new Size(220, 25) };
            bt.Click += (s, e) => { try { System.Diagnostics.Process.Start("ms-settings:bluetooth"); } catch { } };
            grp.Controls.Add(bt);
            tp.Controls.Add(grp);

            LabelTS how = new LabelTS
            {
                Name = "lblKainosCivHowTo",
                Location = new Point(380, 14), Size = new Size(336, 380),
                Text = "Setting up the Xiegu GPA100\r\n\r\n" +
                       "1. On the amp: hold AUTO, choose BLUETOOTH, press OK, then EXIT. Short-press AUTO until the display shows [A] (automatic band). " +
                       "The amp remembers both.\r\n\r\n" +
                       "2. On this PC: Open Windows Bluetooth settings, Add device, Bluetooth, choose XG_GPA100, and enter the PIN 0000.\r\n\r\n" +
                       "3. Click Find GPA100. Windows makes two COM ports for the amp; Kainos picks the outgoing one.\r\n\r\n" +
                       "4. Tick the box above and press OK or Apply. The status shows when the amp is following.\r\n\r\n" +
                       "Keep the amp's ACC1 cable for PTT and ALC: over Bluetooth the amp only follows the band. " +
                       "If the amp is switched off or out of range, Kainos keeps trying and picks it up again.\r\n\r\n" +
                       "Other amplifiers that follow an Icom radio over CI-V can work too: choose their COM port and the address they ask for."
            };
            tp.Controls.Add(how);
            tcCAT.TabPages.Add(tp);

            chkKainosCivOn.CheckedChanged += (s, e) => { if (!initializing) applyKainosCiv(); };
            chkKainosCivLog.CheckedChanged += (s, e) => { if (!initializing) applyKainosCiv(); };
            comboKainosCivPort.TextChanged += (s, e) => { if (!initializing) applyKainosCiv(); };
            txtKainosCivAddr.Leave += (s, e) => { if (!initializing) applyKainosCiv(); };
        }

        private void fillKainosCivPorts()
        {
            string current = comboKainosCivPort.Text;
            comboKainosCivPort.Items.Clear();
            foreach (KainosCivLink.PortInfo p in KainosCivLink.Ports()) comboKainosCivPort.Items.Add(p.Port);
            comboKainosCivPort.Text = current;
        }

        private bool _kainosCivHooked;

        // at start-up (applyKainosUI) and whenever a setting changes
        private void applyKainosCiv()
        {
            if (chkKainosCivOn == null) return;
            byte addr;
            if (!byte.TryParse(txtKainosCivAddr.Text.Trim().TrimStart('$').Replace("0x", ""), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out addr) || addr == 0 || addr >= 0xE0)
            {
                addr = 0xA4;
                txtKainosCivAddr.Text = "A4";
            }
            if (!_kainosCivHooked)
            {
                _kainosCivHooked = true;
                console.KainosCiv.StatusChanged += () =>
                {
                    try { if (!IsDisposed && IsHandleCreated) BeginInvoke(new Action(() => lblKainosCivStatus.Text = console.KainosCiv.Status)); } catch { }
                };
            }
            console.KainosCivApply(chkKainosCivOn.Checked, comboKainosCivPort.Text.Trim(), addr, chkKainosCivLog.Checked);
            lblKainosCivStatus.Text = console.KainosCiv.Status;
        }
    }
}
