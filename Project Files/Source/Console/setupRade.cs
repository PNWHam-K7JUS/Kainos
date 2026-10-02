/*  setupRade.cs

This file is part of Kainos, a program that implements a Software-Defined Radio.
Kainos is based on Thetis : https://github.com/ramdor/Thetis

RADE (FreeDV Radio Autoencoder) support is ported from Thetis-RADE by
Christos Nikolaou (SV1EIA) : https://github.com/sv1eia/Thetis-RADE
Copyright (C) 2026 Christos Nikolaou (SV1EIA), Justin Cron K7JUS

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
    // The RADE settings on Setup > DSP > FreeDV (RADE). The FreeDV window reads and writes these controls.
    internal class RadeSetupControls
    {
        public TextBoxTS Callsign;
        public ComboBoxTS Version;                                  // V1 / V2
        public NumericUpDownTS MicLevel, RxLevel;                   // dB, encoder input / decoder input
        public CheckBoxTS MicRNNoise, MicAGC, MicEQ;
        public NumericUpDownTS MicAGCTarget;                        // LUFS
        public NumericUpDownTS BassFreq, BassGain, MidFreq, MidGain, MidQ, TrebleFreq, TrebleGain, EQVol;

        public Control[] All
        {
            get
            {
                return new Control[] { Callsign, Version, MicLevel, RxLevel, MicRNNoise, MicAGC, MicEQ, MicAGCTarget,
                                       BassFreq, BassGain, MidFreq, MidGain, MidQ, TrebleFreq, TrebleGain, EQVol };
            }
        }
    }

    public partial class Setup
    {
        private TabPage tpDSPRade;
        private RadeSetupControls _rade;
        internal RadeSetupControls RadeSettings { get { return _rade; } }

        private void addRadeTab()
        {
            tpDSPRade = new TabPage("FreeDV (RADE)")
            {
                Name = "tpDSPRade",
                BackColor = SystemColors.Control,
                Padding = new Padding(3)
            };
            RadeSetupControls r = _rade = new RadeSetupControls();

            GroupBoxTS grp = new GroupBoxTS { Name = "grpRade", Text = "RADE digital voice", Location = new Point(8, 8), Size = new Size(430, 132) };
            r.Callsign = new TextBoxTS { Name = "txtRadeCallsign", Location = new Point(120, 22), Size = new Size(110, 20), CharacterCasing = CharacterCasing.Upper, MaxLength = 8 };
            toolTip1.SetToolTip(r.Callsign, "Your callsign. It is sent in the end-of-over frame at the end of every RADE over,\r\n" +
                "so the other station sees who was transmitting.");
            r.Version = new ComboBoxTS { Name = "comboRadeVersion", DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(340, 22), Size = new Size(64, 21) };
            r.Version.Items.AddRange(new object[] { "V1", "V2" });
            r.Version.SelectedIndex = 0;
            toolTip1.SetToolTip(r.Version, "RADE protocol. V1 is what most stations use; both ends must use the same one.\r\n" +
                "V2 does not send callsigns (its end-of-over frame carries no data).");
            r.MicLevel = avUpDown("udRadeMicLevel", -40, 40, 0, 1, 0, 120, 52, "Gain of your mic audio into the RADE encoder (dB). Aim for peaks a little below clipping\r\n" +
                "on the Mic meter in the FreeDV window.");
            r.RxLevel = avUpDown("udRadeRxLevel", -100, 40, 0, 1, 0, 340, 52, "Gain of the received signal into the RADE decoder (dB). Use it if the RX meter in the\r\n" +
                "FreeDV window shows clipping. The RX1 AF slider sets the volume of the decoded speech.");
            grp.Controls.Add(avLabel("Callsign", 14, 25));
            grp.Controls.Add(r.Callsign);
            grp.Controls.Add(avLabel("Version", 250, 25));
            grp.Controls.Add(r.Version);
            grp.Controls.Add(avLabel("Mic level (dB)", 14, 54));
            grp.Controls.Add(r.MicLevel);
            grp.Controls.Add(avLabel("RX level (dB)", 250, 54));
            grp.Controls.Add(r.RxLevel);
            LabelTS how = avLabel("Switch RADE on in the FreeDV window (FreeDV on the menu bar); RX1 changes to DIGU or DIGL " +
                "while it is on. Use a DIGU/DIGL TX profile with the EQ, leveler, CFC and compressor off.", 14, 84);
            how.AutoSize = false;
            how.Size = new Size(404, 42);
            grp.Controls.Add(how);
            tpDSPRade.Controls.Add(grp);

            GroupBoxTS mic = new GroupBoxTS { Name = "grpRadeMic", Text = "Mic processing before the encoder (as in FreeDV-GUI)", Location = new Point(8, 148), Size = new Size(430, 200) };
            r.MicRNNoise = new CheckBoxTS { Name = "chkRadeMicRNNoise", Text = "Noise reduction (RNNoise)", Location = new Point(14, 22), AutoSize = true };
            r.MicAGC = new CheckBoxTS { Name = "chkRadeMicAGC", Text = "AGC, target (LUFS)", Location = new Point(14, 48), AutoSize = true };
            r.MicAGCTarget = avUpDown("udRadeMicAGCTarget", -30, 0, -23, 1, 0, 160, 46, "Loudness the AGC holds your mic audio at (ITU-R BS.1770), with a peak limiter.");
            r.MicEQ = new CheckBoxTS { Name = "chkRadeMicEQ", Text = "EQ", Location = new Point(14, 76), AutoSize = true };
            toolTip1.SetToolTip(r.MicRNNoise, "Removes background noise from your mic before it is encoded.");
            mic.Controls.Add(r.MicRNNoise);
            mic.Controls.Add(r.MicAGC);
            mic.Controls.Add(r.MicAGCTarget);
            mic.Controls.Add(r.MicEQ);

            mic.Controls.Add(avLabel("Hz", 120, 80));
            mic.Controls.Add(avLabel("dB", 192, 80));
            mic.Controls.Add(avLabel("Q", 264, 80));
            r.BassFreq = avUpDown("udRadeMicEQBassFreq", 50, 500, 100, 10, 0, 100, 98, "Bass shelf frequency.");
            r.BassGain = avUpDown("udRadeMicEQBassGain", -20, 20, 0, 0.1m, 1, 172, 98, "Bass shelf gain.");
            r.MidFreq = avUpDown("udRadeMicEQMidFreq", 200, 4000, 1000, 50, 0, 100, 124, "Mid peak frequency.");
            r.MidGain = avUpDown("udRadeMicEQMidGain", -20, 20, 0, 0.1m, 1, 172, 124, "Mid peak gain.");
            r.MidQ = avUpDown("udRadeMicEQMidQ", 0.1m, 5, 0.7m, 0.1m, 1, 244, 124, "Mid peak Q (higher is narrower).");
            r.TrebleFreq = avUpDown("udRadeMicEQTrebleFreq", 1000, 8000, 5000, 100, 0, 100, 150, "Treble shelf frequency.");
            r.TrebleGain = avUpDown("udRadeMicEQTrebleGain", -20, 20, 0, 0.1m, 1, 172, 150, "Treble shelf gain.");
            r.EQVol = avUpDown("udRadeMicEQVol", -20, 20, 0, 1, 0, 172, 176, "EQ output level.");
            r.EQVol.Location = new Point(172, 174);
            mic.Controls.Add(avLabel("Bass", 30, 100));
            mic.Controls.Add(avLabel("Mid", 30, 126));
            mic.Controls.Add(avLabel("Treble", 30, 152));
            mic.Controls.Add(avLabel("Volume", 30, 176));
            foreach (NumericUpDownTS ud in new[] { r.BassFreq, r.BassGain, r.MidFreq, r.MidGain, r.MidQ, r.TrebleFreq, r.TrebleGain, r.EQVol })
                mic.Controls.Add(ud);
            tpDSPRade.Controls.Add(mic);

            LabelTS note = avLabel("RADE (Radio Autoencoder) is FreeDV's machine-learning digital voice mode, by David Rowe " +
                "and the FreeDV project. It is ported into Kainos from Thetis-RADE by Christos Nikolaou, SV1EIA.\r\n\r\n" +
                "Tune with the mode set to DIGU or DIGL as you would for any digital mode, using the usual RADE frequencies " +
                "(e.g. 14.236 MHz). The filter should pass at least 300 to 2700 Hz.\r\n\r\n" +
                "Noise reduction, NB2 and the auto-notch are switched off when RADE starts, because they damage the signal.\r\n\r\n" +
                "Test transmit into a dummy load first.", 450, 16);
            note.AutoSize = false;
            note.Size = new Size(264, 300);
            tpDSPRade.Controls.Add(note);

            foreach (Control c in r.All)
            {
                if (c is TextBoxTS) ((TextBoxTS)c).TextChanged += rade_Changed;
                else hookChanged(c, rade_Changed);
            }

            tcDSP.Controls.Add(tpDSPRade);
        }

        // opens Setup on this tab (the FreeDV window's Settings button)
        internal void ShowRadeTab()
        {
            Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            if (tcDSP.Parent is TabPage) TabSetup.SelectedTab = (TabPage)tcDSP.Parent;
            tcDSP.SelectedTab = tpDSPRade;
            BringToFront();
            Activate();
        }

        private void rade_Changed(object sender, EventArgs e)
        {
            if (initializing) return;
            applyRade();
        }

        // called from ForceAllEvents at startup, and whenever a setting changes
        private void applyRade()
        {
            RadeSetupControls r = _rade;
            try
            {
                console.RadeCallsign = r.Callsign.Text;
                Rade.SetRadaeProtocolV2(0, r.Version.SelectedIndex == 1 ? 1 : 0);
                Rade.SetRadaeMicScale(Math.Pow(10.0, (double)r.MicLevel.Value / 20.0));
                Rade.SetRadaeRxDialScale(0, Math.Pow(10.0, (double)r.RxLevel.Value / 20.0));
                Rade.SetRadaeMicRNNoiseEnabled(r.MicRNNoise.Checked ? 1 : 0);
                Rade.SetRadaeMicAGCTargetLufs((double)r.MicAGCTarget.Value);
                Rade.SetRadaeMicAGCEnabled(r.MicAGC.Checked ? 1 : 0);
                Rade.SetRadaeMicEQBass((double)r.BassFreq.Value, (double)r.BassGain.Value);
                Rade.SetRadaeMicEQMid((double)r.MidFreq.Value, (double)r.MidGain.Value, (double)r.MidQ.Value);
                Rade.SetRadaeMicEQTreble((double)r.TrebleFreq.Value, (double)r.TrebleGain.Value);
                Rade.SetRadaeMicEQVol((double)r.EQVol.Value);
                Rade.SetRadaeMicEQEnabled(r.MicEQ.Checked ? 1 : 0);
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }
    }
}
