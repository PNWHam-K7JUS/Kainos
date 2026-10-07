/*  consoleKainosPhone.cs

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
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Thetis
{
    // The phone-mode panel (MIC, COMP, VOX, DEXP, transmit profile, TX filter, RX / TX EQ, AV, TX FL) laid out for the
    // right-hand column's TX section instead of scaled down as a block: each level a full-width label and slider like
    // Master AF (VOX and DEXP keep their level bars under the slider), the transmit profile a Kainos drop-down under
    // them with the TX filter's Low / High on one row, and the buttons in two rows of four. The panel's original
    // layout is recorded first (kainosRecord) and put back in Classic (kainosUnfitPanels).
    public partial class Console
    {
        private Label[] _kainosPhoneCaps;
        private KainosDropDown _kainosProfileDrop;
        private readonly Dictionary<Label, KeyValuePair<bool, ContentAlignment>> _kainosPhoneLabelLook = new Dictionary<Label, KeyValuePair<bool, ContentAlignment>>();

        // caption, value label, slider, level bar (or null)
        private Control[][] kainosPhoneSliders
        {
            get
            {
                return new[]
                {
                    new Control[] { lblMicVal, ptbMic, null },
                    new Control[] { lblCPDRVal, ptbCPDR, null },
                    new Control[] { lblVOXVal, ptbVOX, picVOX },
                    new Control[] { lblNoiseGateVal, ptbNoiseGate, picNoiseGate },
                };
            }
        }
        private static readonly string[] _kainosPhoneCaptions = { "Mic", "Comp", "VOX", "DEXP" };

        private ButtonBase[][] kainosPhoneButtons
        {
            get
            {
                return new[]
                {
                    new ButtonBase[] { chkMicMute, chkCPDR, chkVOX, chkNoiseGate },
                    new ButtonBase[] { chkRXEQ, chkTXEQ, chkAetherVoice, chkShowTXFilter },
                };
            }
        }

        // AM and SAM have their own profile list; Thetis brings the one for the mode to the front (both stay visible)
        private ComboBox kainosTxProfileCombo { get { return RX1DSPMode == DSPMode.AM || RX1DSPMode == DSPMode.SAM ? comboAMTXProfile : comboTXProfile; } }

        // called with the flags' placing (a mode change swaps the list without showing or hiding anything)
        private ComboBox _kainosProfileShown;
        private void kainosProfileDropCheck()
        {
            if (_kainosProfileDrop == null || !_kainosProfileDrop.Visible) return;
            ComboBox c = kainosTxProfileCombo;
            if (c != _kainosProfileShown) { _kainosProfileShown = c; _kainosProfileDrop.Invalidate(); }
        }

        // the panel's height for a column width; with apply, lays it out
        private int kainosPhoneLayout(int w, bool apply)
        {
            Control panel = panelModeSpecificPhone;
            if (apply)
            {
                if (!_kainosFits.ContainsKey(panel))
                {
                    KainosPanelFit fit = new KainosPanelFit { Factor = -1f };      // < 1: Classic puts the recorded layout back
                    kainosRecord(panel, fit);
                    _kainosFits[panel] = fit;
                }
                kainosPhoneExtras();
                panel.SuspendLayout();
            }

            int y = 0, gap = KainosUI.S(6), capH = KainosUI.S(18), sliderH = KainosUI.S(26);
            Control[][] sliders = kainosPhoneSliders;
            for (int i = 0; i < sliders.Length; i++)
            {
                Control val = sliders[i][0], bar = sliders[i][2];
                TrackBar slider = sliders[i][1] as TrackBar;
                bool shown = KainosUI.OwnVisible(sliders[i][1]);
                if (apply)
                {
                    _kainosPhoneCaps[i].Visible = shown;
                    if (shown)
                    {
                        _kainosPhoneCaps[i].SetBounds(0, y, w / 2, capH);
                        kainosPhoneValueLabel(val as Label);
                        val.SetBounds(w / 2, y, w - w / 2, capH);
                        sliders[i][1].SetBounds(0, y + capH, w, sliderH);
                        if (bar != null) bar.SetBounds(KainosUI.S(8), y + capH + sliderH - 2, w - KainosUI.S(16), 3);
                    }
                }
                if (shown) y += capH + sliderH + gap;
            }

            // the transmit profile, and the TX filter's Low / High
            int ddH = KainosDropDown.PreferredHeight, rowH = KainosUI.S(24), lw = KainosUI.S(40), half = w / 2;
            if (apply)
            {
                foreach (Control c in new Control[] { lblMIC, lblTransmitProfile, comboTXProfile, comboAMTXProfile }) c.Location = new Point(-20000, -20000);
                _kainosProfileDrop.SetBounds(0, y, w, ddH);
                _kainosProfileDrop.Invalidate();
            }
            y += ddH + gap;
            if (apply)
            {
                kainosPhoneValueLabel(lblTXLow, ContentAlignment.MiddleLeft);
                kainosPhoneValueLabel(lblTXHigh, ContentAlignment.MiddleLeft);
                lblTXLow.SetBounds(0, y, lw, rowH);
                udTXFilterLow.SetBounds(lw, y + 1, half - lw - gap, rowH - 2);
                lblTXHigh.SetBounds(half, y, lw, rowH);
                udTXFilterHigh.SetBounds(half + lw, y + 1, w - half - lw, rowH - 2);
            }
            y += rowH + gap + KainosUI.S(2);

            // the buttons: two rows of four
            int bh = KainosUI.S(26), bgap = KainosUI.S(6), bw = (w - bgap * 3) / 4;
            foreach (ButtonBase[] row in kainosPhoneButtons)
            {
                if (apply)
                    for (int i = 0; i < row.Length; i++)
                        if (row[i] != null) row[i].SetBounds(i * (bw + bgap), y, i == row.Length - 1 ? w - i * (bw + bgap) : bw, bh);
                y += bh + bgap;
            }
            y -= bgap;

            if (apply)
            {
                panel.Size = new Size(w, y);
                panel.ResumeLayout();
            }
            return y;
        }

        private void kainosPhoneValueLabel(Label l, ContentAlignment align = ContentAlignment.MiddleRight)
        {
            if (l == null) return;
            if (!_kainosPhoneLabelLook.ContainsKey(l)) _kainosPhoneLabelLook[l] = new KeyValuePair<bool, ContentAlignment>(l.AutoSize, l.TextAlign);
            l.AutoSize = false;
            l.TextAlign = align;
        }

        // Kainos's own controls on the panel: the sliders' captions and the transmit profile drop-down
        private void kainosPhoneExtras()
        {
            if (_kainosPhoneCaps == null)
            {
                _kainosPhoneCaps = _kainosPhoneCaptions.Select((t, i) => new Label
                {
                    Name = "lblKainosPhone" + i,      // Thetis keeps its controls by name: each must be unique
                    Text = t,
                    AutoSize = false,
                    TextAlign = ContentAlignment.MiddleLeft,
                    BackColor = Color.Transparent,
                }).ToArray();
                _kainosProfileDrop = new KainosDropDown(() => kainosTxProfileCombo, "TRANSMIT PROFILE") { Name = "kainosProfileDrop" };
                // Thetis shows / hides VOX and DEXP's sliders: lay out again
                foreach (Control c in kainosPhoneSliders.Select(r => r[1]))
                    c.VisibleChanged += (s, e) => { if (_kainosLayout && !_kainosPlacing) positionKainosColumn(); };
            }
            foreach (Label l in _kainosPhoneCaps)
            {
                l.Font = lblAF.Font;
                l.ForeColor = lblAF.ForeColor;
                if (l.Parent != panelModeSpecificPhone) panelModeSpecificPhone.Controls.Add(l);
            }
            if (_kainosProfileDrop.Parent != panelModeSpecificPhone) panelModeSpecificPhone.Controls.Add(_kainosProfileDrop);
            _kainosProfileDrop.Visible = true;
        }

        // Classic: Kainos's own controls away, the labels as they were (the recorded bounds and fonts are put back by
        // kainosUnfitPanels)
        private void kainosPhoneRestore()
        {
            if (_kainosPhoneCaps != null) foreach (Label l in _kainosPhoneCaps) l.Visible = false;
            if (_kainosProfileDrop != null) _kainosProfileDrop.Visible = false;
            foreach (KeyValuePair<Label, KeyValuePair<bool, ContentAlignment>> kv in _kainosPhoneLabelLook)
            {
                kv.Key.TextAlign = kv.Value.Value;
                kv.Key.AutoSize = kv.Value.Key;
            }
            _kainosPhoneLabelLook.Clear();
        }
    }
}
