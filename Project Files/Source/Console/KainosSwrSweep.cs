/*  KainosSwrSweep.cs

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
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Thetis
{
    // The SWR sweep window: pick a band (or a range), sweep it (consoleKainosSwr.cs does the transmitting), see the
    // curve with the lowest SWR and the 2:1 bandwidth, save it, and lay earlier sweeps over it. Sweeps are CSV files
    // in %APPDATA%\OpenHPSDR\Kainos-x64\Sweeps, so they can also be opened in a spreadsheet.
    internal class KainosSwrSweep : Form
    {
        private readonly Console _console;
        private readonly ComboBox _band, _steps;
        private readonly NumericUpDown _start, _stop;
        private readonly KainosWizardButton _go, _save, _export, _delete;
        private readonly Label _status, _readout;
        private readonly SweepPlot _plot;
        private readonly CheckedListBox _saved;
        private List<Console.SweepPoint> _points = new List<Console.SweepPoint>();
        private bool _running, _stop_requested;
        private string _sweepBand = "";

        private static string Folder { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpenHPSDR", "Kainos-x64", "Sweeps"); } }

        public KainosSwrSweep(Console console)
        {
            _console = console;
            Text = "SWR sweep";
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            BackColor = KainosWindowTheme.WindowBg;
            ForeColor = KainosWindowTheme.Text;
            Font = new Font("Segoe UI", 9.5f);
            AutoScaleDimensions = new SizeF(96f, 96f);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(900, 560);
            MinimumSize = new Size(700, 460);

            // controls along the top
            Controls.Add(label("Band", 16, 18));
            _band = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Visible = false };
            Controls.Add(_band);
            foreach (KeyValuePair<string, double[]> b in bands()) _band.Items.Add(b.Key);
            _band.Items.Add("Custom");
            Controls.Add(new KainosDropDown(() => _band, null) { Location = new Point(60, 12), Size = new Size(110, 28) });
            Controls.Add(label("From", 186, 18));
            _start = mhz(226, 14);
            Controls.Add(label("to", 330, 18));
            _stop = mhz(352, 14);
            Controls.Add(label("MHz", 456, 18));
            Controls.Add(label("Steps", 500, 18));
            _steps = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Visible = false };
            _steps.Items.AddRange(new object[] { "50", "100", "200" });
            _steps.SelectedIndex = 0;                  // 50: quick, and the least time on the air
            Controls.Add(_steps);
            Controls.Add(new KainosDropDown(() => _steps, null) { Location = new Point(546, 12), Size = new Size(70, 28) });
            _go = new KainosWizardButton("Start sweep") { Location = new Point(774, 10), Size = new Size(110, 32), Accent = true, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _go.Click += (s, e) => startStop();
            Controls.Add(_go);

            _band.SelectedIndexChanged += (s, e) =>
            {
                double[] edges = bands().Where(b => b.Key == _band.Text).Select(b => b.Value).FirstOrDefault();
                if (edges != null) { _start.Value = (decimal)edges[0]; _stop.Value = (decimal)edges[1]; }
                _start.Enabled = _stop.Enabled = _band.Text == "Custom";
                refreshSaved();
            };
            int current = bands().FindIndex(b => _console.VFOAFreq >= b.Value[0] && _console.VFOAFreq <= b.Value[1]);
            _band.SelectedIndex = Math.Max(0, current);

            // the plot, and the saved sweeps beside it
            _plot = new SweepPlot { Location = new Point(16, 54), Size = new Size(660, 440), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
            Controls.Add(_plot);
            Controls.Add(new Label { Text = "Saved sweeps (tick to compare)", Location = new Point(690, 54), Size = new Size(194, 20), ForeColor = KainosWindowTheme.TextMid, Anchor = AnchorStyles.Top | AnchorStyles.Right });
            _saved = new CheckedListBox { Location = new Point(690, 76), Size = new Size(194, 336), BackColor = KainosWindowTheme.FieldBg, ForeColor = KainosWindowTheme.Text, BorderStyle = BorderStyle.FixedSingle, CheckOnClick = true, IntegralHeight = false, Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right };
            _saved.ItemCheck += (s, e) => BeginInvoke(new Action(refreshOverlays));
            Controls.Add(_saved);
            _save = new KainosWizardButton("Save") { Location = new Point(690, 420), Size = new Size(94, 30), Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
            _save.Click += (s, e) => save();
            _delete = new KainosWizardButton("Delete") { Location = new Point(790, 420), Size = new Size(94, 30), Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
            _delete.Click += (s, e) => deleteSelected();
            _export = new KainosWizardButton("Export CSV") { Location = new Point(690, 456), Size = new Size(194, 30), Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
            _export.Click += (s, e) => export();
            Controls.AddRange(new Control[] { _save, _delete, _export });

            _readout = new Label { Location = new Point(16, 500), Size = new Size(660, 22), Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = KainosUI.GoldHi, Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
            _status = new Label { Location = new Point(16, 526), Size = new Size(868, 22), ForeColor = KainosWindowTheme.TextMid, Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                                  Text = "Sweeps transmit a carrier of up to 1 W, briefly, on each step." };
            Controls.AddRange(new Control[] { _readout, _status });

            FormClosing += (s, e) => { if (_running) { _stop_requested = true; e.Cancel = true; } };
            // centred over Kainos (a window shown with Show, not ShowDialog, ignores CenterParent)
            Load += (s, e) =>
            {
                Form o = Owner;
                if (o != null) Location = new Point(o.Left + (o.Width - Width) / 2, o.Top + (o.Height - Height) / 2);
            };
            refreshSaved();
        }

        private Label label(string text, int x, int y) { return new Label { Text = text, Location = new Point(x, y), AutoSize = true }; }

        private NumericUpDown mhz(int x, int y)
        {
            NumericUpDown u = new NumericUpDown { Location = new Point(x, y), Size = new Size(96, 24), DecimalPlaces = 3, Minimum = 1.0m, Maximum = 54.0m, Increment = 0.01m, BackColor = KainosWindowTheme.FieldBg, ForeColor = KainosWindowTheme.Text };
            Controls.Add(u);
            return u;
        }

        // amateur band edges, Region 2 (the Americas) or Region 1 / 3 by the country chosen in the setup wizard
        private List<KeyValuePair<string, double[]>> bands()
        {
            // 2 kHz inside each edge: the carrier sits a CW pitch from the dial (the sweep also checks every step)
            string c = _console.KainosCountry;
            bool r2 = c == "" || c == "United States" || c == "Canada";
            return new List<KeyValuePair<string, double[]>>
            {
                new KeyValuePair<string, double[]>("160 m", r2 ? new[] { 1.802, 1.998 } : new[] { 1.812, 1.998 }),
                new KeyValuePair<string, double[]>("80 m", r2 ? new[] { 3.502, 3.998 } : new[] { 3.502, 3.798 }),
                new KeyValuePair<string, double[]>("40 m", r2 ? new[] { 7.002, 7.298 } : new[] { 7.002, 7.198 }),
                new KeyValuePair<string, double[]>("30 m", new[] { 10.102, 10.148 }),
                new KeyValuePair<string, double[]>("20 m", new[] { 14.002, 14.348 }),
                new KeyValuePair<string, double[]>("17 m", new[] { 18.070, 18.166 }),
                new KeyValuePair<string, double[]>("15 m", new[] { 21.002, 21.448 }),
                new KeyValuePair<string, double[]>("12 m", new[] { 24.892, 24.988 }),
                new KeyValuePair<string, double[]>("10 m", new[] { 28.002, 29.698 }),
                new KeyValuePair<string, double[]>("6 m", r2 ? new[] { 50.002, 53.998 } : new[] { 50.002, 51.998 }),
            };
        }

        // ---- sweeping ----

        private async void startStop()
        {
            if (_running) { _stop_requested = true; return; }
            if (_stop.Value <= _start.Value) { _status.Text = "The end frequency must be above the start."; return; }

            DialogResult ok = MessageBox.Show(this,
                "Before sweeping:\r\n\r\n" +
                "•  Turn off, or bypass, any antenna tuner (ATU).\r\n" +
                "•  Put any amplifier in bypass, or switch it off.\r\n" +
                "•  Make sure an antenna or a dummy load is connected.\r\n\r\n" +
                "Kainos transmits a carrier of up to 1 W, briefly, on each step. Press Stop (or close this window) at any time.",
                "SWR sweep", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (ok != DialogResult.OK) return;

            _running = true;
            _stop_requested = false;
            _go.Text = "Stop";
            _points = new List<Console.SweepPoint>();
            _sweepBand = _band.Text;
            _plot.Range((double)_start.Value, (double)_stop.Value);
            _plot.Current = _points;
            _plot.Invalidate();
            _readout.Text = "";
            string why;
            try
            {
                why = await _console.KainosRunSweep((double)_start.Value, (double)_stop.Value, int.Parse(_steps.Text), () => _stop_requested,
                    p => { _points.Add(p); _plot.Invalidate(); updateReadout(); },
                    t => _status.Text = t);
            }
            catch (Exception ex) { why = "The sweep stopped: " + ex.Message; }
            _running = false;
            _go.Text = "Start sweep";
            _status.Text = why ?? ("Done: " + _points.Count + " points. Save it to compare later.");
            updateReadout();
        }

        private void updateReadout()
        {
            if (_points.Count == 0) { _readout.Text = ""; return; }
            Console.SweepPoint best = _points.OrderBy(p => p.Swr).First();
            List<Console.SweepPoint> under2 = _points.Where(p => p.Swr <= 2f).ToList();
            string bw = under2.Count > 1 ? string.Format("   2:1 from {0:0.000} to {1:0.000} MHz ({2:0} kHz)", under2.First().MHz, under2.Last().MHz, (under2.Last().MHz - under2.First().MHz) * 1000) : "";
            _readout.Text = string.Format("Lowest {0:0.00}:1 at {1:0.000} MHz{2}", best.Swr, best.MHz, bw);
        }

        // ---- saved sweeps ----

        private void save()
        {
            if (_points.Count == 0) { _status.Text = "Nothing to save yet: run a sweep first."; return; }
            try
            {
                Directory.CreateDirectory(Folder);
                string name = (_sweepBand.Replace(" ", "") + "_" + DateTime.Now.ToString("yyyy-MM-dd_HHmm")) + ".csv";
                File.WriteAllText(Path.Combine(Folder, name), csv(_points, _sweepBand));
                _status.Text = "Saved " + name;
                refreshSaved();
            }
            catch (Exception ex) { _status.Text = "Couldn't save: " + ex.Message; }
        }

        private static string csv(List<Console.SweepPoint> pts, string band)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("# Kainos SWR sweep, " + band + ", " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            sb.AppendLine("freq_mhz,swr,fwd_w,rev_w");
            foreach (Console.SweepPoint p in pts)
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0:0.000000},{1:0.000},{2:0.000},{3:0.0000}", p.MHz, p.Swr, p.Fwd, p.Rev));
            return sb.ToString();
        }

        private static List<Console.SweepPoint> load(string file)
        {
            List<Console.SweepPoint> pts = new List<Console.SweepPoint>();
            foreach (string line in File.ReadAllLines(file))
            {
                string[] f = line.Split(',');
                double mhz; float swr;
                if (f.Length >= 2 && double.TryParse(f[0], NumberStyles.Float, CultureInfo.InvariantCulture, out mhz) && float.TryParse(f[1], NumberStyles.Float, CultureInfo.InvariantCulture, out swr))
                    pts.Add(new Console.SweepPoint { MHz = mhz, Swr = swr });
            }
            return pts;
        }

        // this band's saved sweeps, newest first
        private void refreshSaved()
        {
            if (_saved == null) return;
            _saved.Items.Clear();
            try
            {
                if (Directory.Exists(Folder))
                {
                    string prefix = _band.Text == "Custom" ? "" : _band.Text.Replace(" ", "") + "_";
                    foreach (string f in Directory.GetFiles(Folder, "*.csv").Where(f => Path.GetFileName(f).StartsWith(prefix)).OrderByDescending(f => f))
                        _saved.Items.Add(Path.GetFileNameWithoutExtension(f));
                }
            }
            catch { }
            refreshOverlays();
        }

        private void refreshOverlays()
        {
            List<List<Console.SweepPoint>> overlays = new List<List<Console.SweepPoint>>();
            foreach (object o in _saved.CheckedItems)
                try { overlays.Add(load(Path.Combine(Folder, o + ".csv"))); } catch { }
            _plot.Overlays = overlays;
            if (_points.Count == 0) _plot.Range((double)_start.Value, (double)_stop.Value);
            _plot.Invalidate();
        }

        private void deleteSelected()
        {
            if (_saved.SelectedItem == null) { _status.Text = "Select a saved sweep to delete."; return; }
            string name = _saved.SelectedItem.ToString();
            if (MessageBox.Show(this, "Delete the saved sweep " + name + "?", "SWR sweep", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            try { File.Delete(Path.Combine(Folder, name + ".csv")); } catch { }
            refreshSaved();
        }

        // the current sweep, or the selected saved one, to a CSV file of the user's choosing
        private void export()
        {
            string text;
            string suggested;
            if (_saved.SelectedItem != null)
            {
                suggested = _saved.SelectedItem + ".csv";
                text = File.ReadAllText(Path.Combine(Folder, suggested));
            }
            else if (_points.Count > 0)
            {
                suggested = _sweepBand.Replace(" ", "") + "_" + DateTime.Now.ToString("yyyy-MM-dd_HHmm") + ".csv";
                text = csv(_points, _sweepBand);
            }
            else { _status.Text = "Run a sweep, or select a saved one, to export."; return; }
            using (SaveFileDialog d = new SaveFileDialog { FileName = suggested, Filter = "CSV files (*.csv)|*.csv", Title = "Export SWR sweep" })
                if (d.ShowDialog(this) == DialogResult.OK)
                    try { File.WriteAllText(d.FileName, text); _status.Text = "Exported " + Path.GetFileName(d.FileName); }
                    catch (Exception ex) { _status.Text = "Couldn't export: " + ex.Message; }
        }

        // ---- the plot ----

        private class SweepPlot : Control
        {
            public List<Console.SweepPoint> Current = new List<Console.SweepPoint>();
            public List<List<Console.SweepPoint>> Overlays = new List<List<Console.SweepPoint>>();
            private double _lo = 7.0, _hi = 7.3;
            private static readonly Color[] _overlayColours = { KainosUI.Ice, KainosUI.Violet, Color.FromArgb(0x5f, 0xc9, 0x8a), Color.FromArgb(0xe0, 0x7a, 0x5f) };

            public SweepPlot()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                BackColor = KainosUI.Bg;
            }

            public void Range(double lo, double hi) { _lo = lo; _hi = Math.Max(lo + 0.001, hi); }

            private const float MaxSwr = 5f;
            private RectangleF area { get { return new RectangleF(44, 10, Width - 56, Height - 40); } }
            private float x(double mhz) { RectangleF a = area; return a.Left + (float)((mhz - _lo) / (_hi - _lo)) * a.Width; }
            private float y(float swr) { RectangleF a = area; return a.Bottom - (Math.Min(MaxSwr, Math.Max(1f, swr)) - 1f) / (MaxSwr - 1f) * a.Height; }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                RectangleF a = area;
                using (Pen border = new Pen(KainosUI.Line)) g.DrawRectangle(border, a.X, a.Y, a.Width, a.Height);
                using (Font f = new Font("Segoe UI", 8.5f))
                using (Brush text = new SolidBrush(KainosWindowTheme.TextMid))
                {
                    // SWR lines: 1.5 and 2 dashed (the usual targets), 3 and 4 faint
                    foreach (float s in new[] { 1.5f, 2f, 3f, 4f })
                        using (Pen p = new Pen(s <= 2f ? KainosUI.Gold : KainosUI.Line) { DashStyle = s <= 2f ? DashStyle.Dash : DashStyle.Solid })
                        {
                            p.Color = Color.FromArgb(s <= 2f ? 120 : 255, p.Color);
                            g.DrawLine(p, a.Left, y(s), a.Right, y(s));
                            g.DrawString(s.ToString("0.#"), f, text, a.Left - 30, y(s) - 7);
                        }
                    g.DrawString("1.0", f, text, a.Left - 30, a.Bottom - 7);
                    g.DrawString(MaxSwr.ToString("0.0") + "+", f, text, a.Left - 34, a.Top - 4);
                    // frequency labels
                    for (int i = 0; i <= 5; i++)
                    {
                        double mhz = _lo + (_hi - _lo) * i / 5;
                        float px = x(mhz);
                        using (Pen p = new Pen(KainosUI.Line)) g.DrawLine(p, px, a.Top, px, a.Bottom);
                        string s = mhz.ToString("0.000");
                        SizeF sz = g.MeasureString(s, f);
                        g.DrawString(s, f, text, Math.Max(a.Left, Math.Min(a.Right - sz.Width, px - sz.Width / 2)), a.Bottom + 4);
                    }
                }
                for (int i = 0; i < Overlays.Count; i++) curve(g, Overlays[i], _overlayColours[i % _overlayColours.Length], 1.5f);
                curve(g, Current, KainosUI.GoldHi, 2.2f);
                if (Current.Count == 0 && Overlays.Count == 0)
                    using (Font f = new Font("Segoe UI", 11f))
                    using (Brush b = new SolidBrush(KainosWindowTheme.TextDim))
                    using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                        g.DrawString("Press Start sweep", f, b, a, sf);
            }

            private void curve(Graphics g, List<Console.SweepPoint> pts, Color c, float width)
            {
                if (pts == null || pts.Count < 2) return;
                PointF[] line = pts.Where(p => p.MHz >= _lo - 1e-6 && p.MHz <= _hi + 1e-6).Select(p => new PointF(x(p.MHz), y(p.Swr))).ToArray();
                if (line.Length < 2) return;
                using (Pen p = new Pen(c, width)) g.DrawLines(p, line);
            }
        }
    }
}
