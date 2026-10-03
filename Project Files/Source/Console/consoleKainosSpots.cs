/*  consoleKainosSpots.cs

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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace Thetis
{
    // The built-in spotting (KainosSpots.cs) in the console: spots onto the panadapter through Thetis's own spot
    // display (SpotManager2: the callsign tags, flags, click to tune), and the SPOTS tab in the right column (the
    // sources on / off and their status, and the latest spots; click one to tune to it). The user's callsign is taken
    // from what Kainos already knows (the RADE callsign, else the TCI / own callsign, else the Discord one).
    public partial class Console
    {
        // saved with the options (a hidden Setup box): "cluster=1;pota=1;host=dxc.nc7j.com;port=7373;band=0"
        public string KainosSpotSettings = "";
        internal bool SpotBandOnly;

        private readonly ConcurrentQueue<KainosSpot> _spotIn = new ConcurrentQueue<KainosSpot>();
        private readonly List<KainosSpot> _spotRecent = new List<KainosSpot>();     // newest first
        private System.Windows.Forms.Timer _spotTimer;
        private KainosTextLine _spotStatusDx, _spotStatusPota;
        private KainosActionGrid _spotButtons;
        private KainosSpotList _spotList;
        private bool _spotStarted;

        private static readonly Color SpotColourDx = Color.FromArgb(0x7f, 0xb0, 0xcc);     // Kainos ice
        private static readonly Color SpotColourPota = Color.FromArgb(0x4c, 0xaf, 0x6e);   // park green

        internal string KainosMyCallsign
        {
            get
            {
                string c = (RadeCallsign ?? "").Trim();
                if (c.Length < 3 && !IsSetupFormNull) c = SetupForm.KainosOtherCallsign;
                return (c ?? "").Trim().ToUpperInvariant();
            }
        }

        internal void KainosSpotsLoad()
        {
            KainosSpotting.ClusterOn = true;            // on by default: spots without setting anything up
            KainosSpotting.PotaOn = true;
            foreach (string kv in (KainosSpotSettings ?? "").Split(';'))
            {
                string[] p = kv.Split('=');
                if (p.Length != 2) continue;
                int v;
                switch (p[0])
                {
                    case "cluster": KainosSpotting.ClusterOn = p[1] != "0"; break;
                    case "pota": KainosSpotting.PotaOn = p[1] != "0"; break;
                    case "host": if (p[1].Trim().Length > 0) KainosSpotting.ClusterHost = p[1].Trim(); break;
                    case "port": if (int.TryParse(p[1], out v) && v > 0 && v < 65536) KainosSpotting.ClusterPort = v; break;
                    case "band": SpotBandOnly = p[1] == "1"; break;
                }
            }
            KainosSpotting.Callsign = KainosMyCallsign;
            if (_spotStarted) KainosSpotting.Kick();
        }

        private void kainosSpotsSave()
        {
            KainosSpotSettings = "cluster=" + (KainosSpotting.ClusterOn ? 1 : 0) + ";pota=" + (KainosSpotting.PotaOn ? 1 : 0)
                                 + ";host=" + KainosSpotting.ClusterHost + ";port=" + KainosSpotting.ClusterPort + ";band=" + (SpotBandOnly ? 1 : 0);
            KainosSettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        // once the console is up: start the sources, show spots on the panadapter
        private void kainosSpotsStart()
        {
            if (_spotStarted) return;
            _spotStarted = true;
            KainosSpotting.Callsign = KainosMyCallsign;
            KainosSpotting.Spotted += sp => _spotIn.Enqueue(sp);
            KainosSpotting.StatusChanged += () => { };
            _spotTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _spotTimer.Tick += (s, e) => kainosSpotsTick();
            _spotTimer.Start();
            if (KainosSpotting.ClusterOn || KainosSpotting.PotaOn) kainosShowSpotsOnPanadapter(true);
            KainosSpotting.Start();
        }

        private void kainosShowSpotsOnPanadapter(bool on)
        {
            Display.ShowTCISpots = on;
            if (!IsSetupFormNull) SetupForm.KainosShowSpots = on;
        }

        private void kainosSpotsTick()
        {
            // the callsign can be set or changed in Setup at any time
            string call = KainosMyCallsign;
            if (call != KainosSpotting.Callsign) { KainosSpotting.Callsign = call; KainosSpotting.Kick(); }

            KainosSpot sp;
            bool any = false;
            int guard = 0;
            while (guard++ < 500 && _spotIn.TryDequeue(out sp))
            {
                if (string.IsNullOrEmpty(sp.Call) || sp.Hz <= 0) continue;
                DSPMode mode = kainosSpotMode(sp.Mode, sp.Hz);
                SpotManager2.JsonSpotData j = new SpotManager2.JsonSpotData
                {
                    Spotter = sp.Spotter ?? "",
                    Comment = sp.Info ?? "",
                    UtcTime = sp.Utc.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'", CultureInfo.InvariantCulture),
                };
                try { SpotManager2.AddSpot(sp.Call, mode, sp.Hz, sp.Source == "POTA" ? SpotColourPota : SpotColourDx, sp.Info ?? "", j); } catch { }
                // the list: one line per call and source, newest first
                _spotRecent.RemoveAll(o => o.Call == sp.Call && o.Source == sp.Source);
                _spotRecent.Insert(0, sp);
                any = true;
            }
            if (_spotRecent.Count > 80) _spotRecent.RemoveRange(80, _spotRecent.Count - 80);
            if (_spotStatusDx != null) { _spotStatusDx.Invalidate(); _spotStatusPota.Invalidate(); _spotButtons.Invalidate(); }
            if (any && _spotList != null) _spotList.Invalidate();
        }

        // the mode a spot gives, or a guess from the band plan (FT8 / FT4 calling frequencies, the CW segments,
        // else SSB on the usual sideband); FIRST leaves the mode alone when the spot is clicked
        private static DSPMode kainosSpotMode(string mode, long hz)
        {
            double khz = hz / 1000.0;
            bool lower = khz < 10000;
            switch ((mode ?? "").ToUpperInvariant())
            {
                case "CW": return lower ? DSPMode.CWL : DSPMode.CWU;
                case "SSB": return lower ? DSPMode.LSB : DSPMode.USB;
                case "USB": return DSPMode.USB;
                case "LSB": return DSPMode.LSB;
                case "AM": return DSPMode.AM;
                case "FM": return DSPMode.FM;
                case "RTTY": return DSPMode.DIGL;
                case "FT8": case "FT4": case "PSK": case "JS8": case "DATA": case "DIGI": case "RADE": case "FREEDV": return DSPMode.DIGU;
            }
            foreach (double f in new[] { 1840, 3573, 5357, 7074, 10136, 14074, 18100, 21074, 24915, 28074, 50313, 3575, 7047.5, 10140, 14080, 18104, 21140, 24919, 28180, 50318 })
                if (Math.Abs(khz - f) <= 3) return DSPMode.DIGU;
            double[,] cw = { { 1800, 1840 }, { 3500, 3600 }, { 7000, 7060 }, { 10100, 10130 }, { 14000, 14070 }, { 18068, 18095 }, { 21000, 21070 }, { 24890, 24915 }, { 28000, 28070 }, { 50000, 50100 } };
            for (int i = 0; i < cw.GetLength(0); i++)
                if (khz >= cw[i, 0] && khz < cw[i, 1]) return lower ? DSPMode.CWL : DSPMode.CWU;
            if (khz >= 1800 && khz < 54000) return lower ? DSPMode.LSB : DSPMode.USB;
            return DSPMode.FIRST;
        }

        // ---- the SPOTS tab ----

        private void kainosAddSpotsSection()
        {
            _spotStatusDx = new KainosTextLine(() => "DX: " + KainosSpotting.ClusterStatus, () => KainosSpotting.ClusterStatus.StartsWith("Connected") ? KainosUI.Text : KainosUI.Dim);
            _spotStatusPota = new KainosTextLine(() => "POTA: " + KainosSpotting.PotaStatus, () => KainosSpotting.PotaStatus.Contains("on the air") ? KainosUI.Text : KainosUI.Dim);
            _spotButtons = new KainosActionGrid(4);
            _spotButtons.Add("DX", () => KainosSpotting.ClusterOn, () => { KainosSpotting.ClusterOn = !KainosSpotting.ClusterOn; kainosSpotsChanged(); }, KainosUI.Tone.Ice);
            _spotButtons.Add("POTA", () => KainosSpotting.PotaOn, () => { KainosSpotting.PotaOn = !KainosSpotting.PotaOn; kainosSpotsChanged(); }, KainosUI.Tone.Ice);
            _spotButtons.Add("Pan", () => Display.ShowTCISpots, () => kainosShowSpotsOnPanadapter(!Display.ShowTCISpots), KainosUI.Tone.Gold);
            _spotButtons.Add("Band", () => SpotBandOnly, () => { SpotBandOnly = !SpotBandOnly; kainosSpotsSave(); _spotList.Invalidate(); }, KainosUI.Tone.Gold);
            _spotList = new KainosSpotList(this);
            foreach (Control c in new Control[] { _spotStatusDx, _spotStatusPota, _spotButtons, _spotList })
                _kainosColumn.Viewport.Controls.Add(c);
            _kainosColumn.AddSection("spots", "SPOTS", w => KainosUI.S(20) * 2 + KainosUI.S(6) + _spotButtons.PreferredHeight(w) + KainosUI.S(6) + _spotList.PreferredHeight, r =>
            {
                int y = r.Top, line = KainosUI.S(20);
                _spotStatusDx.SetBounds(r.Left, y, r.Width, line); y += line;
                _spotStatusPota.SetBounds(r.Left, y, r.Width, line); y += line + KainosUI.S(6);
                int bh = _spotButtons.PreferredHeight(r.Width);
                _spotButtons.SetBounds(r.Left, y, r.Width, bh); y += bh + KainosUI.S(6);
                _spotList.SetBounds(r.Left, y, r.Width, _spotList.PreferredHeight);
            });
        }

        private void kainosSpotsChanged()
        {
            if (KainosSpotting.ClusterOn || KainosSpotting.PotaOn) kainosShowSpotsOnPanadapter(true);
            if (!KainosSpotting.ClusterOn) SpotManager2.ClearAllSpots(true, false);
            kainosSpotsSave();
            KainosSpotting.Kick();
        }

        // the latest spots for the list (this band only when Band is on)
        internal List<KainosSpot> KainosSpotsShown(int max)
        {
            IEnumerable<KainosSpot> q = _spotRecent;
            if (SpotBandOnly)
            {
                double f = VFOAFreq * 1e6;
                q = q.Where(sp => kainosSameBand(sp.Hz, f));
            }
            return q.Take(max).ToList();
        }

        private static bool kainosSameBand(double a, double b)
        {
            double[,] bands = { { 1.8, 2.0 }, { 3.5, 4.0 }, { 5.3, 5.41 }, { 7.0, 7.3 }, { 10.1, 10.15 }, { 14.0, 14.35 }, { 18.068, 18.168 }, { 21.0, 21.45 }, { 24.89, 24.99 }, { 28.0, 29.7 }, { 50.0, 54.0 } };
            for (int i = 0; i < bands.GetLength(0); i++)
            {
                bool ia = a / 1e6 >= bands[i, 0] && a / 1e6 <= bands[i, 1], ib = b / 1e6 >= bands[i, 0] && b / 1e6 <= bands[i, 1];
                if (ia || ib) return ia && ib;
            }
            return false;
        }

        // click a spot in the list: tune VFO A there, in its mode
        internal void KainosTuneToSpot(KainosSpot sp)
        {
            DSPMode m = kainosSpotMode(sp.Mode, sp.Hz);
            if (m != DSPMode.FIRST && RX1DSPMode != m) RX1DSPMode = m;
            VFOAFreq = sp.Hz * 1e-6;
        }
    }

    // The latest spots: time, frequency, call (gold) and what the spot says; click a row to tune to it
    internal class KainosSpotList : Control
    {
        private const int Rows = 10;
        private readonly Console _console;
        private List<KainosSpot> _shown = new List<KainosSpot>();
        private int _hover = -1;

        public KainosSpotList(Console console)
        {
            _console = console;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = KainosUI.Surface;
        }

        public int PreferredHeight { get { return Rows * KainosUI.S(30); } }
        private int rowH { get { return KainosUI.S(30); } }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            _shown = _console.KainosSpotsShown(Rows);
            using (Font call = new Font("Segoe UI", Math.Max(8f, KainosUI.S(12)), FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font small = new Font("Segoe UI", Math.Max(7f, KainosUI.S(10)), FontStyle.Regular, GraphicsUnit.Pixel))
            using (Font mono = new Font("Consolas", Math.Max(8f, KainosUI.S(12)), FontStyle.Regular, GraphicsUnit.Pixel))
            using (Brush gold = new SolidBrush(KainosUI.GoldHi))
            using (Brush green = new SolidBrush(Color.FromArgb(0x7c, 0xcf, 0x93)))
            using (Brush text = new SolidBrush(KainosUI.Text))
            using (Brush dim = new SolidBrush(KainosUI.Dim))
            using (Brush faint = new SolidBrush(KainosUI.Faint))
            using (Pen line = new Pen(KainosUI.Line))
            {
                if (_shown.Count == 0)
                {
                    g.DrawString(SpotsEmptyText(), small, faint, new RectangleF(2, 4, Width - 4, rowH * 2));
                    return;
                }
                int y = 0;
                for (int i = 0; i < _shown.Count; i++, y += rowH)
                {
                    KainosSpot sp = _shown[i];
                    if (i == _hover) using (Brush hb = new SolidBrush(KainosUI.Raised)) g.FillRectangle(hb, 0, y, Width, rowH - 1);
                    g.DrawLine(line, 0, y + rowH - 1, Width, y + rowH - 1);
                    float x = 2;
                    g.DrawString(sp.Utc.ToString("HHmm", CultureInfo.InvariantCulture), small, faint, x, y + 2);
                    g.DrawString((sp.Hz / 1000.0).ToString("0.0", CultureInfo.InvariantCulture), mono, text, x + KainosUI.S(34), y + 1);
                    g.DrawString(sp.Call, call, sp.Source == "POTA" ? green : gold, x + KainosUI.S(112), y + 1);
                    string info = (string.IsNullOrEmpty(sp.Mode) ? "" : sp.Mode + "  ") + (sp.Info ?? "");
                    g.DrawString(info, small, dim, new RectangleF(x + KainosUI.S(34), y + KainosUI.S(16), Width - x - KainosUI.S(36), KainosUI.S(14)),
                                 new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap });
                }
            }
        }

        private string SpotsEmptyText()
        {
            if (!KainosSpotting.ClusterOn && !KainosSpotting.PotaOn) return "Spotting is off. Turn on DX or POTA above.";
            if (_console.KainosMyCallsign.Length < 3) return "Waiting for spots. For DX spots, set your callsign in Setup > DSP > FreeDV (RADE).";
            return _console.SpotBandOnly ? "No spots on this band yet." : "Waiting for spots...";
        }

        private int hit(Point p) { int i = p.Y / Math.Max(1, rowH); return i >= 0 && i < _shown.Count ? i : -1; }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int h = hit(e.Location);
            Cursor = h >= 0 ? Cursors.Hand : Cursors.Default;
            if (h != _hover) { _hover = h; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = -1; Invalidate(); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            int h = hit(e.Location);
            if (h >= 0 && e.Button == MouseButtons.Left) _console.KainosTuneToSpot(_shown[h]);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            KainosViewport v = Parent as KainosViewport;
            if (v != null) typeof(Control).GetMethod("OnMouseWheel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(v, new object[] { e });
        }
    }
}
