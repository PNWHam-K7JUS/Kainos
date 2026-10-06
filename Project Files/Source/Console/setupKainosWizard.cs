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
        }

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
            KainosWizardState = "done";
            SaveOptions();
        }

        private static void setChecked(CheckBox c, bool on) { if (c.Checked != on) c.Checked = on; }

        private static void setValue(NumericUpDown u, decimal v)
        {
            v = Math.Max(u.Minimum, Math.Min(u.Maximum, v));
            if (u.Value != v) u.Value = v;
        }
    }
}
