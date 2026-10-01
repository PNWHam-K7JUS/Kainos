/*  setupKainos.cs

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
    // Kainos additions to the Setup window. The controls are built in code rather than in
    // setup.designer.cs so that merging new Thetis releases doesn't conflict with them.
    // They use the Thetis 'TS' control types, so getOptions/saveOptions persist them by name.
    public partial class Setup
    {
        private TabPage tpDSPAetherVoice;
        private GroupBoxTS grpAetherVoiceRX;
        private CheckBoxTS chkAetherVoiceRX;
        private ComboBoxTS comboAetherVoiceRXMode;
        private NumericUpDownTS udAetherVoiceRXBodyDrive;
        private NumericUpDownTS udAetherVoiceRXBodyTune;
        private NumericUpDownTS udAetherVoiceRXBodyMix;
        private NumericUpDownTS udAetherVoiceRXClarityTune;
        private NumericUpDownTS udAetherVoiceRXClarityHarmonics;
        private NumericUpDownTS udAetherVoiceRXClarityMix;

        // the AetherVoice window (frmAetherVoice) reads and writes these controls directly, so the
        // window, this tab and the console AV button always agree and save the same way
        internal CheckBoxTS AetherVoiceRXEnable { get { return chkAetherVoiceRX; } }
        internal ComboBoxTS AetherVoiceRXMode { get { return comboAetherVoiceRXMode; } }
        internal NumericUpDownTS AetherVoiceRXBodyDrive { get { return udAetherVoiceRXBodyDrive; } }
        internal NumericUpDownTS AetherVoiceRXBodyTune { get { return udAetherVoiceRXBodyTune; } }
        internal NumericUpDownTS AetherVoiceRXBodyMix { get { return udAetherVoiceRXBodyMix; } }
        internal NumericUpDownTS AetherVoiceRXClarityTune { get { return udAetherVoiceRXClarityTune; } }
        internal NumericUpDownTS AetherVoiceRXClarityHarmonics { get { return udAetherVoiceRXClarityHarmonics; } }
        internal NumericUpDownTS AetherVoiceRXClarityMix { get { return udAetherVoiceRXClarityMix; } }

        // called from the constructor straight after InitializeComponent, before saved options are restored
        private void addKainosControls()
        {
            addAetherVoiceTab();
        }

        #region AetherVoice

        private void addAetherVoiceTab()
        {
            tpDSPAetherVoice = new TabPage("AetherVoice")
            {
                Name = "tpDSPAetherVoice",
                BackColor = SystemColors.Control,
                Padding = new Padding(3)
            };

            grpAetherVoiceRX = new GroupBoxTS
            {
                Name = "grpAetherVoiceRX",
                Text = "AetherVoice exciter - Receive",
                Location = new Point(8, 8),
                Size = new Size(430, 262)
            };

            chkAetherVoiceRX = new CheckBoxTS
            {
                Name = "chkAetherVoiceRX",
                Text = "Enable on receive (voice modes only)",
                Location = new Point(14, 22),
                AutoSize = true
            };
            toolTip1.SetToolTip(chkAetherVoiceRX, "Adds low-end body and high-end clarity to received voice, on every receiver.\r\n" +
                "It is bypassed automatically in CW, digital, DRM and SPEC modes so it never affects decoders.");

            comboAetherVoiceRXMode = new ComboBoxTS
            {
                Name = "comboAetherVoiceRXMode",
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(120, 50),
                Size = new Size(110, 21)
            };
            comboAetherVoiceRXMode.Items.AddRange(new object[] { "Aphex", "Behringer" });
            comboAetherVoiceRXMode.SelectedIndex = 0;
            toolTip1.SetToolTip(comboAetherVoiceRXMode, "Aphex: warm, asymmetric shaping with even and odd harmonics, and Big Bottom low-end saturation.\r\n" +
                "Behringer: brighter, symmetric shaping with odd harmonics, and a low-end compressor instead of saturation.");

            grpAetherVoiceRX.Controls.Add(chkAetherVoiceRX);
            grpAetherVoiceRX.Controls.Add(avLabel("Mode", 14, 53));
            grpAetherVoiceRX.Controls.Add(comboAetherVoiceRXMode);

            // Body: the low band ("Poo" in AetherSDR)
            grpAetherVoiceRX.Controls.Add(avLabel("Body (low band)", 14, 88, true));
            udAetherVoiceRXBodyDrive = avUpDown("udAetherVoiceRXBodyDrive", 0, 24, 0, 0.5m, 1, 120, 110,
                "How hard the low band is driven: saturation depth in Aphex mode, compression in Behringer mode.");
            udAetherVoiceRXBodyTune = avUpDown("udAetherVoiceRXBodyTune", 50, 160, 100, 5, 0, 120, 136,
                "Corner frequency of the low band.");
            udAetherVoiceRXBodyMix = avUpDown("udAetherVoiceRXBodyMix", 0, 100, 50, 5, 0, 120, 162,
                "How much of the processed low band is added to the original audio.");
            grpAetherVoiceRX.Controls.Add(avLabel("Drive (dB)", 26, 112));
            grpAetherVoiceRX.Controls.Add(avLabel("Tune (Hz)", 26, 138));
            grpAetherVoiceRX.Controls.Add(avLabel("Mix (%)", 26, 164));
            grpAetherVoiceRX.Controls.Add(udAetherVoiceRXBodyDrive);
            grpAetherVoiceRX.Controls.Add(udAetherVoiceRXBodyTune);
            grpAetherVoiceRX.Controls.Add(udAetherVoiceRXBodyMix);

            // Clarity: the high band ("Doo" in AetherSDR)
            grpAetherVoiceRX.Controls.Add(avLabel("Clarity (high band)", 224, 88, true));
            udAetherVoiceRXClarityTune = avUpDown("udAetherVoiceRXClarityTune", 1000, 10000, 5000, 100, 0, 340, 110,
                "Corner frequency of the high band: harmonics are generated above this frequency.");
            udAetherVoiceRXClarityHarmonics = avUpDown("udAetherVoiceRXClarityHarmonics", 0, 24, 6, 0.5m, 1, 340, 136,
                "How hard the high band is driven into the harmonic generator.");
            udAetherVoiceRXClarityMix = avUpDown("udAetherVoiceRXClarityMix", 0, 100, 50, 5, 0, 340, 162,
                "How much of the generated harmonics is added to the original audio.");
            grpAetherVoiceRX.Controls.Add(avLabel("Tune (Hz)", 236, 112));
            grpAetherVoiceRX.Controls.Add(avLabel("Harmonics (dB)", 236, 138));
            grpAetherVoiceRX.Controls.Add(avLabel("Mix (%)", 236, 164));
            grpAetherVoiceRX.Controls.Add(udAetherVoiceRXClarityTune);
            grpAetherVoiceRX.Controls.Add(udAetherVoiceRXClarityHarmonics);
            grpAetherVoiceRX.Controls.Add(udAetherVoiceRXClarityMix);

            Label note = avLabel("AetherVoice is ported from AetherSDR. High Drive, Harmonics or Mix settings raise the audio level; " +
                "if the audio distorts, lower the Mix or the volume.", 14, 200);
            note.AutoSize = false;
            note.Size = new Size(400, 44);
            grpAetherVoiceRX.Controls.Add(note);

            chkAetherVoiceRX.CheckedChanged += aetherVoiceRX_Changed;
            comboAetherVoiceRXMode.SelectedIndexChanged += aetherVoiceRX_Changed;
            foreach (NumericUpDownTS ud in new[] { udAetherVoiceRXBodyDrive, udAetherVoiceRXBodyTune, udAetherVoiceRXBodyMix,
                udAetherVoiceRXClarityTune, udAetherVoiceRXClarityHarmonics, udAetherVoiceRXClarityMix })
                ud.ValueChanged += aetherVoiceRX_Changed;

            tpDSPAetherVoice.Controls.Add(grpAetherVoiceRX);
            tcDSP.Controls.Add(tpDSPAetherVoice);
        }

        private LabelTS avLabel(string text, int x, int y, bool bold = false)
        {
            LabelTS l = new LabelTS
            {
                Text = text,
                Location = new Point(x, y),
                AutoSize = true
            };
            if (bold) l.Font = new Font(l.Font, FontStyle.Bold);
            return l;
        }

        private NumericUpDownTS avUpDown(string name, decimal min, decimal max, decimal value, decimal increment, int decimals,
            int x, int y, string tooltip)
        {
            NumericUpDownTS ud = new NumericUpDownTS
            {
                Name = name,
                Minimum = min,
                Maximum = max,
                Value = value,
                Increment = increment,
                DecimalPlaces = decimals,
                TinyStep = false,
                Location = new Point(x, y),
                Size = new Size(64, 20)
            };
            toolTip1.SetToolTip(ud, tooltip);
            return ud;
        }

        private void aetherVoiceRX_Changed(object sender, EventArgs e)
        {
            if (initializing) return;
            applyAetherVoiceRX();
        }

        // pushes the AetherVoice settings to every receiver (RX1, RX1 sub, RX2, RX2 sub)
        private void applyAetherVoiceRX()
        {
            int mode = Math.Max(0, comboAetherVoiceRXMode.SelectedIndex);
            for (uint thread = 0; thread < 2; thread++)
            {
                for (uint subrx = 0; subrx < 2; subrx++)
                {
                    RadioDSPRX rx = console.radio.GetDSPRX((int)thread, (int)subrx);
                    rx.RXAetherVoiceMode = mode;
                    rx.SetRXAetherVoiceBody((double)udAetherVoiceRXBodyDrive.Value, (double)udAetherVoiceRXBodyTune.Value,
                        (double)udAetherVoiceRXBodyMix.Value / 100.0);
                    rx.SetRXAetherVoiceClarity((double)udAetherVoiceRXClarityTune.Value, (double)udAetherVoiceRXClarityHarmonics.Value,
                        (double)udAetherVoiceRXClarityMix.Value / 100.0);
                    rx.RXAetherVoiceOn = chkAetherVoiceRX.Checked;
                }
            }
            console.AetherVoiceRXChanged(chkAetherVoiceRX.Checked);
        }

        #endregion
    }
}
