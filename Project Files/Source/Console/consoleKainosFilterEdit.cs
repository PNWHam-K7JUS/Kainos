/*  consoleKainosFilterEdit.cs

Custom filters from Kainos's own filter buttons (GitHub #79). In Thetis, right-clicking the filter buttons opens their
panel's menu (Configure, Reset) and Configure opens the filter editor, where a filter's name, width and shift are set.
Kainos's filter buttons (the column's FILTER drop-down and the VFO flag's MODE drawer) pass a right click on by calling
the Thetis button's mouse handler, which never brings up a panel's menu, so there was no way in.

Now a right click on a Kainos filter button offers: edit that filter (the editor opens on it, rather than on the
filter in use), and reset that receiver's filters to their defaults (Thetis's own reset, which asks first).

This file is part of Kainos. Copyright (C) 2026 Justin Cron K7JUS. GPLv3 or later.
*/

using System;
using System.Windows.Forms;

namespace Thetis
{
    partial class Console
    {
        // a right click on one of Kainos's buttons standing in for a Thetis button: true if it was a filter button and
        // the filter menu was shown (KainosUI.RightClick calls it, from the button grids and drop-downs)
        internal bool KainosFilterRightClick(Control target)
        {
            if (target == null) return false;
            int rx = target.Parent == panelFilter ? 1 : target.Parent == panelRX2Filter ? 2 : 0;
            if (rx == 0) return false;
            DSPMode mode = rx == 1 ? _rx1_dsp_mode : _rx2_dsp_mode;
            Filter? f = kainosFilterOf(target);
            ContextMenuStrip m = new ContextMenuStrip { Renderer = new KainosToolStripRenderer(), BackColor = KainosUI.Raised, ForeColor = KainosUI.Text, ShowImageMargin = false };
            bool can = mode != DSPMode.DRM && mode != DSPMode.SPEC;
            ToolStripMenuItem edit = new ToolStripMenuItem("Edit the " + target.Text + " filter (name, width, shift)...") { Enabled = can && f.HasValue };
            edit.Click += (s, e) => kainosEditFilter(rx, f.Value);
            ToolStripMenuItem reset = new ToolStripMenuItem("Reset the RX" + rx + " filters to their defaults...") { Enabled = can };
            reset.Click += (s, e) => { if (rx == 1) toolStripMenuItemRX1FilterReset_Click(this, EventArgs.Empty); else toolStripMenuItemRX2FilterReset_Click(this, EventArgs.Empty); };
            m.Items.Add(edit);
            m.Items.Add(reset);
            if (!can) m.Items.Add(new ToolStripMenuItem("(no filters to edit in " + mode + ")") { Enabled = false });
            m.Closed += (s, e) => BeginInvoke((Action)(() => m.Dispose()));
            m.Show(MousePosition);
            return true;
        }

        // the button's filter, from its name: radFilter1 - 10 / radRX2Filter1 - 7 are F1..., radFilterVar1 / 2 VAR1 / VAR2
        private static Filter? kainosFilterOf(Control b)
        {
            string n = b.Name ?? "";
            int at = n.IndexOf("Filter", StringComparison.Ordinal);
            if (at < 0) return null;
            string s = n.Substring(at + 6);
            string name = s.StartsWith("Var", StringComparison.OrdinalIgnoreCase) ? s.ToUpperInvariant() : "F" + s;
            Filter f;
            return Enum.TryParse(name, out f) ? f : (Filter?)null;
        }

        // Thetis's filter editor for that receiver, opened on this filter
        private void kainosEditFilter(int rx, Filter f)
        {
            if (rx == 1)
            {
                toolStripMenuItemRX1FilterConfigure_Click(this, EventArgs.Empty);
                if (filterRX1Form != null && !filterRX1Form.IsDisposed) filterRX1Form.CurrentFilter = f;
            }
            else
            {
                toolStripMenuItemRX2FilterConfigure_Click(this, EventArgs.Empty);
                if (filterRX2Form != null && !filterRX2Form.IsDisposed) filterRX2Form.CurrentFilter = f;
            }
        }
    }
}
