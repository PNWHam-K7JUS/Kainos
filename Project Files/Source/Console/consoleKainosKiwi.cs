/*  consoleKainosKiwi.cs

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
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using NAudio.Wave;

namespace Thetis
{
    // The KIWI tab: the nearest public KiwiSDRs with a free channel (from your grid square), click one to listen; it
    // follows VFO A (frequency and mode) unless Follow is off; its own volume; Stop disconnects. The audio plays on
    // the default Windows output (KainosKiwi.cs has the receiver list and client).
    public partial class Console
    {
        private KainosTextLine _kiwiStatus;
        private KainosActionGrid _kiwiButtons;
        private KainosUpDown _kiwiVolume;
        private KainosKiwiList _kiwiList;
        private readonly NumericUpDown KiwiVolume = new NumericUpDown { Minimum = 0, Maximum = 100, Increment = 5, Value = 60 };
        private KiwiClient _kiwi;
        private KiwiReceiver _kiwiOn;
        private List<KiwiReceiver> _kiwiAll = new List<KiwiReceiver>();
        internal List<KiwiReceiver> KiwiNearest = new List<KiwiReceiver>();
        private string _kiwiState = "Pick a receiver to listen";
        private bool _kiwiFollow = true, _kiwiLoading;
        private double _kiwiLastKhz;
        private string _kiwiLastMode = "";
        private WaveOutEvent _kiwiOut;
        private BufferedWaveProvider _kiwiBuf;
        private int _kiwiRate;
        private System.Windows.Forms.Timer _kiwiTimer;

        private void kainosAddKiwiSection()
        {
            _kiwiStatus = new KainosTextLine(() => _kiwiState, () => _kiwi != null && _kiwi.Connected ? KainosUI.Text : KainosUI.Dim);
            _kiwiButtons = new KainosActionGrid(3);
            _kiwiButtons.Add("Refresh", () => _kiwiLoading, kiwiRefresh, KainosUI.Tone.Ice);
            _kiwiButtons.Add("Follow", () => _kiwiFollow, () => { _kiwiFollow = !_kiwiFollow; _kiwiLastKhz = 0; }, KainosUI.Tone.Gold);
            _kiwiButtons.Add("Stop", () => false, kiwiStop, KainosUI.Tone.Tx);
            _kiwiVolume = new KainosUpDown(KiwiVolume, "Vol", "%", false);
            KiwiVolume.ValueChanged += (s, e) => { if (_kiwiOut != null) _kiwiOut.Volume = (float)KiwiVolume.Value / 100f; };
            _kiwiList = new KainosKiwiList(this);
            foreach (Control c in new Control[] { _kiwiStatus, _kiwiButtons, _kiwiVolume, _kiwiList })
                _kainosColumn.Viewport.Controls.Add(c);
            _kainosColumn.AddSection("kiwi", "KIWI", w => KainosUI.S(20) + KainosUI.S(6) + _kiwiButtons.PreferredHeight(w) + KainosUI.S(6) + KainosUI.S(26) + KainosUI.S(6) + _kiwiList.PreferredHeight, r =>
            {
                int y = r.Top;
                _kiwiStatus.SetBounds(r.Left, y, r.Width, KainosUI.S(20)); y += KainosUI.S(26);
                int bh = _kiwiButtons.PreferredHeight(r.Width);
                _kiwiButtons.SetBounds(r.Left, y, r.Width, bh); y += bh + KainosUI.S(6);
                _kiwiVolume.SetBounds(r.Left, y, r.Width, KainosUI.S(26)); y += KainosUI.S(32);
                _kiwiList.SetBounds(r.Left, y, r.Width, _kiwiList.PreferredHeight);
            }, false);

            _kiwiTimer = new System.Windows.Forms.Timer { Interval = 300 };
            _kiwiTimer.Tick += (s, e) => kiwiTick();
            _kiwiTimer.Start();
            FormClosing += (s, e) => kiwiStop();
        }

        private string kiwiGrid
        {
            get
            {
                try { return IsSetupFormNull || SetupForm.RadeSettings == null ? "" : SetupForm.RadeSettings.Grid.Text.Trim(); } catch { return ""; }
            }
        }

        // the list: fetched when the tab is first shown, and on Refresh
        private void kiwiRefresh()
        {
            if (_kiwiLoading) return;
            _kiwiLoading = true;
            _kiwiState = "Getting the list of KiwiSDRs...";
            Task.Run(() =>
            {
                try
                {
                    List<KiwiReceiver> all = KiwiDirectory.Fetch();
                    BeginInvoke(new Action(() => { _kiwiAll = all; kiwiSort(); _kiwiLoading = false; _kiwiState = all.Count + " receivers. Click one to listen."; _kiwiList.Invalidate(); }));
                }
                catch (Exception ex)
                {
                    Exception e = ex is AggregateException && ex.InnerException != null ? ex.InnerException : ex;
                    BeginInvoke(new Action(() => { _kiwiLoading = false; _kiwiState = "Couldn't get the list (" + e.Message + ")"; }));
                }
            });
        }

        // the nearest with a free channel that cover the frequency (or the most recently heard, without a grid)
        private void kiwiSort()
        {
            double la, lo;
            bool have = KiwiDirectory.GridToLatLon(kiwiGrid, out la, out lo);
            long hz = (long)(VFOAFreq * 1e6);
            foreach (KiwiReceiver r in _kiwiAll) r.Km = have && (r.Lat != 0 || r.Lon != 0) ? KiwiDirectory.DistanceKm(la, lo, r.Lat, r.Lon) : -1;
            IEnumerable<KiwiReceiver> q = _kiwiAll.Where(r => r.Free && (r.HighHz == 0 || (hz >= r.LowHz && hz <= r.HighHz)));
            KiwiNearest = (have ? q.OrderBy(r => r.Km < 0 ? double.MaxValue : r.Km) : q.OrderBy(r => r.Users)).Take(KainosKiwiList.Rows).ToList();
        }

        private static string kiwiMode(DSPMode m)
        {
            switch (m)
            {
                case DSPMode.LSB: case DSPMode.DIGL: return "lsb";
                case DSPMode.CWL: case DSPMode.CWU: return "cw";
                case DSPMode.AM: case DSPMode.SAM: case DSPMode.DSB: return "am";
                case DSPMode.FM: return "nbfm";
                default: return "usb";
            }
        }

        internal void KiwiListen(KiwiReceiver r)
        {
            kiwiStop();
            _kiwiOn = r;
            _kiwi = new KiwiClient();
            _kiwi.Status += s => { try { BeginInvoke(new Action(() => _kiwiState = s + (_kiwiOn != null ? " - " + _kiwiOn.Loc : ""))); } catch { } };
            _kiwi.Audio += kiwiAudio;
            _kiwiLastKhz = VFOAFreq * 1000;
            _kiwiLastMode = kiwiMode(_rx1_dsp_mode);
            _kiwi.Connect(r.Url, KainosMyCallsign, _kiwiLastKhz, _kiwiLastMode);
        }

        private void kiwiStop()
        {
            if (_kiwi != null) { _kiwi.Audio -= kiwiAudio; _kiwi.Disconnect(); _kiwi = null; }
            _kiwiOn = null;
            try { _kiwiOut?.Stop(); _kiwiOut?.Dispose(); } catch { }
            _kiwiOut = null;
            _kiwiBuf = null;
            _kiwiRate = 0;
            _kiwiState = "Pick a receiver to listen";
        }

        // audio from the receiver's thread into the player (made when the rate is known)
        private void kiwiAudio(short[] pcm, int rate)
        {
            if (rate <= 0) return;
            if (_kiwiBuf == null || _kiwiRate != rate)
            {
                try { _kiwiOut?.Stop(); _kiwiOut?.Dispose(); } catch { }
                _kiwiRate = rate;
                _kiwiBuf = new BufferedWaveProvider(new WaveFormat(rate, 16, 1)) { BufferDuration = TimeSpan.FromSeconds(3), DiscardOnBufferOverflow = true };
                _kiwiOut = new WaveOutEvent { DesiredLatency = 200 };
                _kiwiOut.Init(_kiwiBuf);
                _kiwiOut.Volume = (float)KiwiVolume.Value / 100f;
                _kiwiOut.Play();
            }
            byte[] b = new byte[pcm.Length * 2];
            Buffer.BlockCopy(pcm, 0, b, 0, b.Length);
            _kiwiBuf.AddSamples(b, 0, b.Length);
        }

        private void kiwiTick()
        {
            if (_kainosColumn == null || !_kainosColumn.IsOn("kiwi")) return;
            if (_kiwiAll.Count == 0 && !_kiwiLoading && _kiwiState.StartsWith("Pick")) kiwiRefresh();
            if (_kiwi != null && _kiwiFollow)
            {
                double khz = VFOAFreq * 1000;
                string mode = kiwiMode(_rx1_dsp_mode);
                if (Math.Abs(khz - _kiwiLastKhz) > 0.0005 || mode != _kiwiLastMode)
                {
                    _kiwiLastKhz = khz;
                    _kiwiLastMode = mode;
                    _kiwi.Tune(khz, mode);
                }
            }
            _kiwiStatus.Invalidate();
            _kiwiButtons.Invalidate();
            _kiwiList.Invalidate();
        }

        internal string KiwiSignalText
        {
            get
            {
                if (_kiwi == null || !_kiwi.Connected) return "";
                double su = Common.GetSMeterUnits(_kiwi.Rssi, VFOAFreq >= S9Frequency);
                return su <= 9 ? "S" + Math.Max(0, (int)Math.Floor(su)) : "S9+" + (int)Math.Round((su - 9) * 6);
            }
        }

        internal KiwiReceiver KiwiOn { get { return _kiwiOn; } }
    }

    // The nearest free receivers: distance, users, place and antenna; the one you're on in gold with its S reading
    internal class KainosKiwiList : Control
    {
        public const int Rows = 8;
        private readonly Console _console;
        private int _hover = -1;

        public KainosKiwiList(Console console)
        {
            _console = console;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = KainosUI.Surface;
        }

        public int PreferredHeight { get { return Rows * rowH; } }
        private int rowH { get { return KainosUI.S(30); } }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            List<KiwiReceiver> list = _console.KiwiNearest;
            using (Font big = new Font("Segoe UI", Math.Max(8f, KainosUI.S(12)), FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font small = new Font("Segoe UI", Math.Max(7f, KainosUI.S(10)), FontStyle.Regular, GraphicsUnit.Pixel))
            using (Brush text = new SolidBrush(KainosUI.Text))
            using (Brush gold = new SolidBrush(KainosUI.GoldHi))
            using (Brush dim = new SolidBrush(KainosUI.Dim))
            using (Brush faint = new SolidBrush(KainosUI.Faint))
            using (Pen line = new Pen(KainosUI.Line))
            using (StringFormat clip = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                if (list.Count == 0)
                {
                    g.DrawString("Set your grid square in Setup > DSP > FreeDV (RADE) to see the nearest receivers first.", small, faint, new RectangleF(2, 4, Width - 4, rowH * 2));
                    return;
                }
                for (int i = 0; i < list.Count; i++)
                {
                    KiwiReceiver r = list[i];
                    int y = i * rowH;
                    bool on = _console.KiwiOn != null && _console.KiwiOn.Url == r.Url;
                    if (i == _hover || on) using (Brush hb = new SolidBrush(on ? KainosUI.Selected : KainosUI.Raised)) g.FillRectangle(hb, 0, y, Width, rowH - 1);
                    g.DrawLine(line, 0, y + rowH - 1, Width, y + rowH - 1);
                    string km = r.Km >= 0 ? r.Km.ToString("0", CultureInfo.InvariantCulture) + " km" : "";
                    g.DrawString(r.Loc.Length > 0 ? r.Loc : r.Name, big, on ? gold : text, new RectangleF(2, y + 1, Width - KainosUI.S(90), KainosUI.S(16)), clip);
                    string right = on ? _console.KiwiSignalText : r.Users + "/" + r.UsersMax;
                    using (StringFormat rf = new StringFormat { Alignment = StringAlignment.Far })
                        g.DrawString(km + "  " + right, small, on ? gold : dim, new RectangleF(Width - KainosUI.S(90), y + 3, KainosUI.S(88), KainosUI.S(14)), rf);
                    g.DrawString(r.Antenna, small, faint, new RectangleF(2, y + KainosUI.S(16), Width - 4, KainosUI.S(14)), clip);
                }
            }
        }

        private int hit(Point p) { int i = p.Y / Math.Max(1, rowH); return i >= 0 && i < _console.KiwiNearest.Count ? i : -1; }

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
            if (h >= 0 && e.Button == MouseButtons.Left) _console.KiwiListen(_console.KiwiNearest[h]);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            KainosViewport v = Parent as KainosViewport;
            if (v != null) typeof(Control).GetMethod("OnMouseWheel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(v, new object[] { e });
        }
    }
}
