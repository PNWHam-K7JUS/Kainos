/*  consoleKainosDigital.cs

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

using System.Drawing;
using System.Windows.Forms;

namespace Thetis
{
    // The digital-mode panel (VAC RX / TX gain, digital transmit profile, VAC sample rate, stereo, and RX1 / RX2 when
    // RX2 is on) laid out for the right-hand column's TX section like the phone panel (consoleKainosPhone.cs), instead
    // of Thetis's panel scaled down as a block: each gain a full-width label and slider, the transmit profile a Kainos
    // drop-down, the sample rate a drop-down beside a STEREO button, and RX1 / RX2 as buttons. Thetis's own labels
    // ("RX Gain:  n", "VAC1") stay, as they keep themselves up to date. The panel's original layout is recorded first
    // (kainosRecord) and put back in Classic (kainosUnfitPanels).
    public partial class Console
    {
        private KainosDropDown _kainosDigProfileDrop, _kainosDigRateDrop;
        private KainosActionGrid _kainosDigStereo, _kainosDigRx;

        // the panel's height for a column width; with apply, lays it out
        private int kainosDigitalLayout(int w, bool apply)
        {
            Control panel = panelModeSpecificDigital;
            if (apply)
            {
                if (!_kainosFits.ContainsKey(panel))
                {
                    KainosPanelFit fit = new KainosPanelFit { Factor = -1f };      // < 1: Classic puts the recorded layout back
                    kainosRecord(panel, fit);
                    _kainosFits[panel] = fit;
                }
                kainosDigitalExtras();
                panel.SuspendLayout();
            }

            int y = 0, gap = KainosUI.S(6), capH = KainosUI.S(18), sliderH = KainosUI.S(26);
            // RX gain, then TX gain: Thetis's "RX Gain:  n" on the left, which VAC on the right
            foreach (Control[] row in new[] { new Control[] { lblRXGain, lblVACRXIndicator, ptbVACRXGain },
                                              new Control[] { lblTXGain, lblVACTXIndicator, ptbVACTXGain } })
            {
                if (apply)
                {
                    Label cap = (Label)row[0], vac = (Label)row[1];
                    kainosPhoneValueLabel(cap, ContentAlignment.MiddleLeft);
                    kainosPhoneValueLabel(vac);
                    cap.Font = vac.Font = lblAF.Font;
                    cap.SetBounds(0, y, w * 2 / 3, capH);
                    vac.SetBounds(w * 2 / 3, y, w - w * 2 / 3, capH);
                    row[2].SetBounds(0, y + capH, w, sliderH);
                }
                y += capH + sliderH + gap;
            }

            // the transmit profile
            int ddH = KainosDropDown.PreferredHeight, half = w / 2;
            if (apply)
            {
                foreach (Control c in new Control[] { lblDigTXProfile, comboDigTXProfile, grpDIGSampleRate, grpVACStereo, radRX1Show, radRX2Show })
                    c.Location = new Point(-20000, -20000);
                _kainosDigProfileDrop.SetBounds(0, y, w, ddH);
                _kainosDigProfileDrop.Invalidate();
            }
            y += ddH + gap;

            // the sample rate, and STEREO beside it
            int bh = _kainosDigStereo != null ? _kainosDigStereo.PreferredHeight(half) : KainosUI.S(26);
            if (apply)
            {
                _kainosDigRateDrop.SetBounds(0, y, half - gap / 2, ddH);
                _kainosDigRateDrop.Invalidate();
                _kainosDigStereo.SetBounds(half + gap / 2, y + (ddH - bh) / 2, w - half - gap / 2, bh);
                _kainosDigStereo.Invalidate();
            }
            y += ddH;

            // RX1 / RX2: which receiver VAC carries, only while Thetis shows the choice (RX2 on)
            bool rxShown = KainosUI.OwnVisible(radRX1Show);
            if (apply) _kainosDigRx.Visible = rxShown;
            if (rxShown)
            {
                y += gap;
                int rh = _kainosDigRx != null ? _kainosDigRx.PreferredHeight(w) : KainosUI.S(26);
                if (apply) { _kainosDigRx.SetBounds(0, y, w, rh); _kainosDigRx.Invalidate(); }
                y += rh;
            }

            if (apply)
            {
                panel.Size = new Size(w, y);
                panel.ResumeLayout();
            }
            return y;
        }

        // Kainos's own controls on the panel: the two drop-downs and the buttons
        private void kainosDigitalExtras()
        {
            if (_kainosDigProfileDrop == null)
            {
                _kainosDigProfileDrop = new KainosDropDown(() => comboDigTXProfile, "TRANSMIT PROFILE") { Name = "kainosDigProfileDrop" };
                _kainosDigRateDrop = new KainosDropDown(() => comboVACSampleRate, "SAMPLE RATE") { Name = "kainosDigRateDrop" };
                _kainosDigStereo = new KainosActionGrid(1) { Name = "kainosDigStereo" };
                _kainosDigStereo.Add("STEREO", () => chkVACStereo.Checked, () => chkVACStereo.Checked = !chkVACStereo.Checked, KainosUI.Tone.Ice);
                _kainosDigRx = new KainosActionGrid(2) { Name = "kainosDigRx" };
                _kainosDigRx.Add("VAC: RX1", () => radRX1Show.Checked, () => radRX1Show.Checked = true, KainosUI.Tone.Ice);
                _kainosDigRx.Add("VAC: RX2", () => radRX2Show.Checked, () => radRX2Show.Checked = true, KainosUI.Tone.Ice);
                // Thetis shows / hides RX1 / RX2 with RX2: lay out again
                radRX1Show.VisibleChanged += (s, e) => { if (_kainosLayout && !_kainosPlacing) positionKainosColumn(); };
            }
            foreach (Control c in new Control[] { _kainosDigProfileDrop, _kainosDigRateDrop, _kainosDigStereo, _kainosDigRx })
            {
                if (c.Parent != panelModeSpecificDigital) panelModeSpecificDigital.Controls.Add(c);
                c.Visible = true;
                c.BringToFront();
            }
        }

        // Classic: Kainos's own controls away (the recorded bounds and fonts are put back by kainosUnfitPanels, the
        // labels' alignment by kainosPhoneRestore)
        private void kainosDigitalRestore()
        {
            foreach (Control c in new Control[] { _kainosDigProfileDrop, _kainosDigRateDrop, _kainosDigStereo, _kainosDigRx })
                if (c != null) c.Visible = false;
        }
    }
}
