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
        public ComboBoxTS Version, VersionRX2;                      // V1 / V2, per receiver
        public NumericUpDownTS MicLevel, RxLevel, RxLevelRX2;       // dB, encoder input / decoder inputs
        public CheckBoxTS MicRNNoise, MicAGC, MicEQ;
        public NumericUpDownTS MicAGCTarget;                        // LUFS
        public NumericUpDownTS BassFreq, BassGain, MidFreq, MidGain, MidQ, TrebleFreq, TrebleGain, EQVol;
        public TextBoxTS Grid, ReporterMessage;                     // FreeDV Reporter
        public CheckBoxTS Reporting, IgnoreQsy, ReporterUtc;

        public Control[] All
        {
            get
            {
                return new Control[] { Callsign, Version, VersionRX2, MicLevel, RxLevel, RxLevelRX2, MicRNNoise, MicAGC, MicEQ, MicAGCTarget,
                                       BassFreq, BassGain, MidFreq, MidGain, MidQ, TrebleFreq, TrebleGain, EQVol,
                                       Grid, ReporterMessage, Reporting, IgnoreQsy, ReporterUtc };
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

            GroupBoxTS grp = new GroupBoxTS { Name = "grpRade", Text = "RADE digital voice", Location = new Point(8, 8), Size = new Size(430, 158) };
            r.Callsign = new TextBoxTS { Name = "txtRadeCallsign", Location = new Point(120, 22), Size = new Size(110, 20), CharacterCasing = CharacterCasing.Upper, MaxLength = 8 };
            toolTip1.SetToolTip(r.Callsign, "Your callsign. It is sent in the end-of-over frame at the end of every RADE over,\r\n" +
                "so the other station sees who was transmitting.");
            r.MicLevel = avUpDown("udRadeMicLevel", -40, 40, 0, 1, 0, 340, 22, "Gain of your mic audio into the RADE encoder (dB). Aim for peaks a little below clipping\r\n" +
                "on the Mic meter in the FreeDV window.");
            r.Version = radeVersionCombo("comboRadeVersion", 120, 52);
            r.RxLevel = avUpDown("udRadeRxLevel", -100, 40, 0, 1, 0, 340, 52, "Gain of RX1's received signal into the RADE decoder (dB). Use it if the RX meter in the\r\n" +
                "FreeDV window shows clipping. The RX1 AF slider sets the volume of the decoded speech.");
            r.VersionRX2 = radeVersionCombo("comboRadeVersionRX2", 120, 80);
            r.RxLevelRX2 = avUpDown("udRadeRxLevelRX2", -100, 40, 0, 1, 0, 340, 80, "Gain of RX2's received signal into the RADE decoder (dB). The RX2 AF slider sets the\r\n" +
                "volume of the decoded speech.");
            grp.Controls.Add(avLabel("Callsign", 14, 25));
            grp.Controls.Add(r.Callsign);
            grp.Controls.Add(avLabel("Mic level (dB)", 250, 25));
            grp.Controls.Add(r.MicLevel);
            grp.Controls.Add(avLabel("RX1 version", 14, 55));
            grp.Controls.Add(r.Version);
            grp.Controls.Add(avLabel("RX1 level (dB)", 250, 55));
            grp.Controls.Add(r.RxLevel);
            grp.Controls.Add(avLabel("RX2 version", 14, 83));
            grp.Controls.Add(r.VersionRX2);
            grp.Controls.Add(avLabel("RX2 level (dB)", 250, 83));
            grp.Controls.Add(r.RxLevelRX2);
            LabelTS how = avLabel("Switch RADE on for RX1 or RX2 in the FreeDV window (FreeDV on the menu bar); the receiver changes " +
                "to DIGU or DIGL while it is on. The TX compressor, CFC and EQ are bypassed during RADE overs.", 14, 110);
            how.AutoSize = false;
            how.Size = new Size(404, 42);
            grp.Controls.Add(how);
            tpDSPRade.Controls.Add(grp);

            GroupBoxTS mic = new GroupBoxTS { Name = "grpRadeMic", Text = "Mic processing before the encoder (as in FreeDV-GUI)", Location = new Point(8, 174), Size = new Size(430, 200) };
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
                "The RX filter should pass at least 300 to 2700 Hz. Noise reduction, NB2 and the auto-notch are switched " +
                "off when RADE starts, because they damage the signal. Test transmit into a dummy load first.", 450, 12);
            note.AutoSize = false;
            note.Size = new Size(264, 140);
            tpDSPRade.Controls.Add(note);

            // FreeDV Reporter (qso.freedv.org)
            GroupBoxTS rep = new GroupBoxTS { Name = "grpRadeReporter", Text = "FreeDV Reporter (qso.freedv.org)", Location = new Point(450, 156), Size = new Size(264, 192) };
            r.Grid = new TextBoxTS { Name = "txtRadeGrid", Location = new Point(100, 20), Size = new Size(70, 20), CharacterCasing = CharacterCasing.Upper, MaxLength = 6 };
            r.ReporterMessage = new TextBoxTS { Name = "txtRadeReporterMessage", Location = new Point(10, 64), Size = new Size(244, 20), MaxLength = 64 };
            r.Reporting = new CheckBoxTS { Name = "chkRadeReporting", Text = "Report my station while RADE is on", Location = new Point(10, 94), AutoSize = true };
            r.IgnoreQsy = new CheckBoxTS { Name = "chkRadeIgnoreQsy", Text = "Ignore QSY requests", Location = new Point(10, 118), AutoSize = true };
            r.ReporterUtc = new CheckBoxTS { Name = "chkRadeReporterUtc", Text = "Show times in UTC", Location = new Point(10, 142), AutoSize = true, Checked = true };
            toolTip1.SetToolTip(r.Grid, "Your Maidenhead grid square (4 or 6 characters), e.g. CN87. Needed, with your callsign, to report.");
            toolTip1.SetToolTip(r.ReporterMessage, "A short message shown next to your station on the reporter, e.g. your rig and antenna.");
            toolTip1.SetToolTip(r.Reporting, "While RADE is on, publish your callsign, grid square, frequency, when you transmit and the\r\n" +
                "stations you decode (with SNR) to qso.freedv.org, as FreeDV-GUI does. Off: the Reporter window\r\n" +
                "only shows other stations and nothing about you is sent.");
            toolTip1.SetToolTip(r.IgnoreQsy, "Don't pop up a message when another station asks you to QSY.");
            rep.Controls.Add(avLabel("Grid square", 10, 23));
            rep.Controls.Add(r.Grid);
            rep.Controls.Add(avLabel("Message", 10, 46));
            rep.Controls.Add(r.ReporterMessage);
            rep.Controls.Add(r.Reporting);
            rep.Controls.Add(r.IgnoreQsy);
            rep.Controls.Add(r.ReporterUtc);
            LabelTS repHint = avLabel("Open the reporter from the FreeDV window.", 10, 166);
            repHint.ForeColor = SystemColors.GrayText;
            rep.Controls.Add(repHint);
            tpDSPRade.Controls.Add(rep);

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

        private ComboBoxTS radeVersionCombo(string name, int x, int y)
        {
            ComboBoxTS c = new ComboBoxTS { Name = name, DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(x, y), Size = new Size(64, 21) };
            c.Items.AddRange(new object[] { "V1", "V2" });
            c.SelectedIndex = 0;
            toolTip1.SetToolTip(c, "RADE protocol for this receiver, also used when transmitting from it. V1 is what most stations use;\r\n" +
                "both ends must use the same one. V2 does not send callsigns (its end-of-over frame carries no data).");
            return c;
        }

        private void rade_Changed(object sender, EventArgs e)
        {
            if (initializing) return;
            applyRade();
        }

        private int _radeVersionReported = -1;

        // CFC on/off as set here (Setup > Transmit), for restoring it after a RADE over
        internal bool TXCFCOn { get { return chkCFCEnable.Checked; } }

        // called from ForceAllEvents at startup, and whenever a setting changes
        private void applyRade()
        {
            RadeSetupControls r = _rade;
            console.RadeIgnoreQsyRequest = r.IgnoreQsy.Checked;
            console.RadeReporterTimesUtc = r.ReporterUtc.Checked;
            FreeDVReporter.FreeDVReporterManager.Configure(console, r.Callsign.Text, r.Grid.Text, r.ReporterMessage.Text, r.Reporting.Checked);
            try
            {
                console.RadeCallsign = r.Callsign.Text;
                Rade.SetRadaeProtocolV2(0, r.Version.SelectedIndex == 1 ? 1 : 0);
                Rade.SetRadaeMicScale(Math.Pow(10.0, (double)r.MicLevel.Value / 20.0));
                Rade.SetRadaeRxDialScale(0, Math.Pow(10.0, (double)r.RxLevel.Value / 20.0));
                Rade.SetRadaeProtocolV2(1, r.VersionRX2.SelectedIndex == 1 ? 1 : 0);
                Rade.SetRadaeRxDialScale(1, Math.Pow(10.0, (double)r.RxLevelRX2.Value / 20.0));
                Rade.SetRadaeMicRNNoiseEnabled(r.MicRNNoise.Checked ? 1 : 0);
                Rade.SetRadaeMicAGCTargetLufs((double)r.MicAGCTarget.Value);
                Rade.SetRadaeMicAGCEnabled(r.MicAGC.Checked ? 1 : 0);
                Rade.SetRadaeMicEQBass((double)r.BassFreq.Value, (double)r.BassGain.Value);
                Rade.SetRadaeMicEQMid((double)r.MidFreq.Value, (double)r.MidGain.Value, (double)r.MidQ.Value);
                Rade.SetRadaeMicEQTreble((double)r.TrebleFreq.Value, (double)r.TrebleGain.Value);
                Rade.SetRadaeMicEQVol((double)r.EQVol.Value);
                Rade.SetRadaeMicEQEnabled(r.MicEQ.Checked ? 1 : 0);
                int versions = r.Version.SelectedIndex * 2 + r.VersionRX2.SelectedIndex;
                if (versions != _radeVersionReported)
                {
                    _radeVersionReported = versions;
                    FreeDVReporter.FreeDVReporterManager.NotifyProtocolChanged();
                }
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }
    }
}
