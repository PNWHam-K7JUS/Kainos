/*  consoleKainosSync.cs

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
using System.Linq;
using System.Windows.Forms;

namespace Thetis
{
    // The VFO SYNC tab: Thetis's panel between the VFOs (VFO sync, VFO lock A / B, Rx Ant, tune step, quick memory,
    // band stack position and the quick recall pad) laid out for the column instead of scaled down as a block, in rows
    // like the rest of the column, its buttons drawn as Kainos's (consoleKainosSliders.cs). Thetis's own labels are
    // replaced by Kainos captions; the panel's layout is recorded first and put back in Classic (kainosUnfitPanels).
    public partial class Console
    {
        private Label[] _kainosSyncCaps;        // VFO lock, Tune step, Quick memory, Band stack, the "/" between the band stack numbers
        private static readonly string[] _kainosSyncCaptions = { "VFO lock", "Tune step", "Quick memory", "Band stack", "/" };

        // the panel's height for a column width; with apply, lays it out
        private int kainosSyncLayout(int w, bool apply)
        {
            Control panel = grpVFOBetween;
            if (apply)
            {
                if (!_kainosFits.ContainsKey(panel))
                {
                    KainosPanelFit fit = new KainosPanelFit { Factor = -1f };      // < 1: Classic puts the recorded layout back
                    kainosRecord(panel, fit);
                    _kainosFits[panel] = fit;
                }
                kainosSyncExtras();
                panel.SuspendLayout();
                foreach (Control c in new Control[] { labelTS1, lblTuneStep, lblBandStack }) c.Location = new Point(-20000, -20000);
            }

            int y = 0, gap = KainosUI.S(6), row = KainosUI.S(26), half = (w - gap) / 2, cap = (int)(w * 0.36);
            int box = KainosUI.S(22);
            if (apply)
            {
                // VFO SYNC and RX ANT
                chkVFOSync.SetBounds(0, y, half, row);
                chkRxAnt.SetBounds(half + gap, y, w - half - gap, row);
            }
            y += row + gap;

            if (apply)
            {
                // VFO lock: A B
                kainosSyncCaption(0, 0, y, cap, row);
                int bw = (w - cap - gap) / 2;
                chkVFOLock.SetBounds(cap, y, bw, row);
                chkVFOBLock.SetBounds(cap + bw + gap, y, w - cap - bw - gap, row);
            }
            y += row + gap;

            if (apply)
            {
                // Tune step: - value +
                kainosSyncCaption(1, 0, y, cap, row);
                int sb = KainosUI.S(28);
                btnTuneStepChangeSmaller.SetBounds(cap, y, sb, row);
                btnTuneStepChangeLarger.SetBounds(w - sb, y, sb, row);
                txtWheelTune.SetBounds(cap + sb + gap, y + (row - box) / 2, w - cap - sb * 2 - gap * 2, box);
                kainosSyncAlign(txtWheelTune);
            }
            y += row + gap;

            if (apply)
            {
                // Quick memory: the frequency, then SAVE and RESTORE under it
                kainosSyncCaption(2, 0, y, cap, row);
                txtMemoryQuick.SetBounds(cap, y + (row - box) / 2, w - cap, box);
                kainosSyncAlign(txtMemoryQuick);
            }
            y += row + gap;
            if (apply)
            {
                int bw = (w - cap - gap) / 2;
                btnMemoryQuickSave.SetBounds(cap, y, bw, row);
                btnMemoryQuickRestore.SetBounds(cap + bw + gap, y, w - cap - bw - gap, row);
            }
            y += row + gap;

            if (apply)
            {
                // Band stack: current / total, and the quick recall pad (< V >) at the right
                kainosSyncCaption(3, 0, y, cap, row);
                int nb = KainosUI.S(30);
                regBandStackCurrentEntry.SetBounds(cap, y + (row - box) / 2, nb, box);
                kainosSyncCaption(4, cap + nb, y, KainosUI.S(14), row);
                _kainosSyncCaps[4].TextAlign = ContentAlignment.MiddleCenter;
                regBandStackTotalEntries.SetBounds(cap + nb + KainosUI.S(14), y + (row - box) / 2, nb, box);
                kainosSyncAlign(regBandStackCurrentEntry);
                kainosSyncAlign(regBandStackTotalEntries);
                int pw = Math.Min(KainosUI.S(96), w - (cap + nb * 2 + KainosUI.S(14) + gap));
                ucQuickRecallPad.SetBounds(w - pw, y, pw, row);
            }
            y += row;

            if (apply)
            {
                panel.Size = new Size(w, y);
                panel.ResumeLayout();
            }
            return y;
        }

        private void kainosSyncCaption(int i, int x, int y, int w, int h)
        {
            Label l = _kainosSyncCaps[i];
            l.Visible = true;
            l.SetBounds(x, y, w, h);
        }

        private readonly System.Collections.Generic.Dictionary<TextBox, HorizontalAlignment> _kainosSyncAligns = new System.Collections.Generic.Dictionary<TextBox, HorizontalAlignment>();
        private void kainosSyncAlign(TextBox t)
        {
            if (!_kainosSyncAligns.ContainsKey(t)) _kainosSyncAligns[t] = t.TextAlign;
            t.TextAlign = HorizontalAlignment.Center;
        }

        private bool _kainosSyncPaintHooked;
        private void kainosSyncExtras()
        {
            if (!_kainosSyncPaintHooked)
            {
                // the group box's frame: covered with the column's colour in Kainos layout (Classic keeps it)
                _kainosSyncPaintHooked = true;
                grpVFOBetween.Paint += (s, e) =>
                {
                    if (!_kainosLayout || !_kainosPartsOn) return;
                    using (Brush b = new SolidBrush(KainosUI.Surface)) e.Graphics.FillRectangle(b, grpVFOBetween.ClientRectangle);
                };
            }
            if (_kainosSyncCaps == null)
                _kainosSyncCaps = _kainosSyncCaptions.Select((t, i) => new Label { Name = "lblKainosSync" + i, Text = t, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent }).ToArray();
            foreach (Label l in _kainosSyncCaps)
            {
                l.Font = lblAF.Font;
                l.ForeColor = lblAF.ForeColor;
                if (l.Parent != grpVFOBetween) grpVFOBetween.Controls.Add(l);
                l.BringToFront();
            }
        }

        // Classic: Kainos's captions away (the recorded bounds and fonts are put back by kainosUnfitPanels)
        private void kainosSyncRestore()
        {
            if (_kainosSyncCaps != null) foreach (Label l in _kainosSyncCaps) l.Visible = false;
            foreach (System.Collections.Generic.KeyValuePair<TextBox, HorizontalAlignment> kv in _kainosSyncAligns) kv.Key.TextAlign = kv.Value;
            _kainosSyncAligns.Clear();
            grpVFOBetween.Invalidate();
        }
    }
}
