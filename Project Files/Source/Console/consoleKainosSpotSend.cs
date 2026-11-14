/*  consoleKainosSpotSend.cs

Spotting to the DX cluster, and choosing the cluster (GitHub #68, Sam NR4A). The SPOTS tab's Spot button (or a right
click on a spot in its list) opens a small window with the frequency (VFO A's, or the one you transmit on), the call
and a comment; it shows the exact line the cluster will get, which goes out under your callsign, and nothing is sent
until you press Send. The Server button picks the cluster: a few well-known ones, the Reverse Beacon Network's two
feeds (receive only: its spots come from skimmers, so the Spot button says so), or one of your own.

This file is part of Kainos. Copyright (C) 2026 Justin Cron K7JUS. GPLv3 or later.
*/

using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace Thetis
{
    partial class Console
    {
        // ---- the cluster ----

        private ContextMenuStrip kainosClusterMenu()
        {
            ContextMenuStrip m = new ContextMenuStrip { Renderer = new KainosToolStripRenderer(), BackColor = KainosUI.Raised, ForeColor = KainosUI.Text, ShowImageMargin = false, ShowCheckMargin = true };
            bool known = false;
            foreach (KainosCluster c in KainosCluster.Known)
            {
                KainosCluster cl = c;
                bool on = string.Equals(KainosSpotting.ClusterHost, cl.Host, StringComparison.OrdinalIgnoreCase) && KainosSpotting.ClusterPort == cl.Port;
                known |= on;
                ToolStripMenuItem it = new ToolStripMenuItem(cl.Name + (cl.Rbn ? "  (receive only)" : "")) { Checked = on, ToolTipText = cl.Host + ":" + cl.Port };
                it.Click += (s, e) => kainosSetCluster(cl.Host, cl.Port);
                m.Items.Add(it);
            }
            m.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem other = new ToolStripMenuItem(known ? "Other cluster..." : "Other: " + KainosSpotting.ClusterHost + ":" + KainosSpotting.ClusterPort + "...") { Checked = !known };
            other.Click += (s, e) => kainosOtherCluster();
            m.Items.Add(other);
            m.Closed += (s, e) => BeginInvoke((Action)(() => m.Dispose()));
            return m;
        }

        private void kainosSetCluster(string host, int port)
        {
            KainosSpotting.ClusterHost = host;
            KainosSpotting.ClusterPort = port;
            if (!KainosSpotting.ClusterOn) { KainosSpotting.ClusterOn = true; kainosShowSpotsOnPanadapter(true); }
            SpotManager2.ClearAllSpots(true, false);
            kainosSpotsSave();
            KainosSpotting.Kick();
        }

        private void kainosOtherCluster()
        {
            using (Form f = new Form { Text = "DX cluster", FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(380, 120), ShowInTaskbar = false })
            {
                Label l = new Label { Text = "The cluster's address and port, like dxc.example.com:7300", Location = new Point(12, 12), AutoSize = true };
                TextBox t = new TextBox { Location = new Point(12, 36), Width = 356, Text = KainosSpotting.ClusterHost + ":" + KainosSpotting.ClusterPort };
                Button ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(212, 80), Width = 75 };
                Button cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(293, 80), Width = 75 };
                f.Controls.AddRange(new Control[] { l, t, ok, cancel });
                f.AcceptButton = ok; f.CancelButton = cancel;
                KainosWindowTheme.Theme(f);
                if (f.ShowDialog(this) != DialogResult.OK) return;
                string s = t.Text.Trim();
                int colon = s.LastIndexOf(':'), port = 23;
                string host = colon > 0 ? s.Substring(0, colon).Trim() : s;
                if (colon > 0 && !int.TryParse(s.Substring(colon + 1), out port)) port = -1;
                if (host.Length == 0 || host.Contains(" ") || port <= 0 || port > 65535)
                {
                    MessageBox.Show(this, "That doesn't look like an address: try something like dxc.example.com:7300.", "DX cluster", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                kainosSetCluster(host, port);
            }
        }

        // ---- spotting ----

        // the frequency transmitted on, in kHz
        private double kainosSpotKhz()
        {
            try { return (VFOBTX ? VFOBFreq : VFOAFreq) * 1000.0; }
            catch { return 0; }
        }

        // a comment to start with, from the mode
        private string kainosSpotModeText()
        {
            switch (RX1DSPMode)
            {
                case DSPMode.CWL: case DSPMode.CWU: return "CW";
                case DSPMode.LSB: case DSPMode.USB: return "SSB";
                case DSPMode.AM: case DSPMode.SAM: return "AM";
                case DSPMode.FM: return "FM";
                default: return "";
            }
        }

        // the Spot window: prefilled from VFO A (or a spot in the list), the exact line shown, Send to spot it
        internal void KainosSpotWindow(KainosSpot from = null)
        {
            if (KainosCluster.IsRbn(KainosSpotting.ClusterHost))
            {
                MessageBox.Show(this, "The Reverse Beacon Network only takes spots from its skimmers, so you can't spot to it.\r\n\r\nPick a DX cluster with the Server button to spot from Kainos.", "Spot", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (Form f = new Form { Text = "Spot to the DX cluster", FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(420, 230), ShowInTaskbar = false })
            {
                Label lf = new Label { Text = "Frequency (kHz)", Location = new Point(12, 16), AutoSize = true };
                TextBox tf = new TextBox { Location = new Point(130, 12), Width = 110, Text = ((from != null ? from.Hz / 1000.0 : kainosSpotKhz())).ToString("0.0", CultureInfo.InvariantCulture) };
                Label lc = new Label { Text = "Their call", Location = new Point(12, 48), AutoSize = true };
                TextBox tc = new TextBox { Location = new Point(130, 44), Width = 140, CharacterCasing = CharacterCasing.Upper, Text = from != null ? from.Call : "" };
                Label lm = new Label { Text = "Comment", Location = new Point(12, 80), AutoSize = true };
                TextBox tm = new TextBox { Location = new Point(130, 76), Width = 278, MaxLength = 30, Text = from != null && !string.IsNullOrEmpty(from.Mode) ? from.Mode : kainosSpotModeText() };
                Label lp = new Label { Location = new Point(12, 114), Size = new Size(396, 18), Font = new Font("Consolas", 9f) };
                Label lw = new Label { Location = new Point(12, 136), Size = new Size(396, 34) };
                Button send = new Button { Text = "Send", Location = new Point(252, 190), Width = 75 };
                Button cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(333, 190), Width = 75 };
                f.Controls.AddRange(new Control[] { lf, tf, lc, tc, lm, tm, lp, lw, send, cancel });
                f.CancelButton = cancel;

                Func<double> khz = () => { double v; return double.TryParse(tf.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : 0; };
                Action show = () =>
                {
                    lp.Text = KainosSpotting.SpotLine(khz(), tc.Text, tm.Text);
                    string why = KainosSpotting.SpotProblem(khz(), tc.Text);
                    lw.Text = why ?? "Goes to " + KainosSpotting.ClusterHost + " as " + KainosMyCallsign + ", and everyone on the cluster sees it.";
                    lw.ForeColor = why == null ? KainosUI.Dim : KainosUI.Gold;
                    send.Enabled = why == null;
                };
                foreach (TextBox t in new[] { tf, tc, tm }) t.TextChanged += (s, e) => show();
                send.Click += (s, e) =>
                {
                    string why = KainosSpotting.SendSpot(khz(), tc.Text, tm.Text);
                    if (why != null) { lw.Text = why; lw.ForeColor = KainosUI.Gold; return; }
                    f.DialogResult = DialogResult.OK;
                };
                KainosWindowTheme.Theme(f);
                show();
                f.Shown += (s, e) => { if (tc.Text.Length == 0) tc.Focus(); else tm.Focus(); };
                f.ShowDialog(this);
            }
        }
    }
}
