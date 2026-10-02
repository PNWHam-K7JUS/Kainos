/*  consoleKainosTabs.cs

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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Thetis
{
    // Kainos layout, stage 3c: the EQ, KAINOS AUDIO, FREEDV and MEMORY tabs of the right column, and the METERS
    // tab's right-click menu and the offer of OE3IDE's FTDX-5000 meter skin.
    public partial class Console
    {
        private KainosActionGrid _kainosEqGrid, _kainosAudioRx, _kainosAudioTx, _kainosFreeDvGrid, _kainosMemoryButtons;
        private KainosTextLine _kainosAudioRxLabel, _kainosAudioTxLabel, _kainosFreeDvStatus;
        private KainosMemoryList _kainosMemoryList;
        private Timer _kainosTabTimer;

        // saved with the options (hidden Setup boxes): the METERS tab's meter type, and whether the FTDX-5000
        // skin has been offered
        public string KainosMeterType = "ANANMM";
        public bool KainosFtdxOffered;

        private void kainosAddMoreSections()
        {
            KainosUI.Tone gold = KainosUI.Tone.Gold, ice = KainosUI.Tone.Ice;

            // EQ: Thetis's RX and TX EQ switches (the phone panel's buttons) and its Equalizer window
            _kainosEqGrid = new KainosActionGrid(3);
            _kainosEqGrid.Add("RX EQ", () => chkRXEQ.Checked, () => kainosClick(chkRXEQ), gold, () => kainosRightClick(chkRXEQ));
            _kainosEqGrid.Add("TX EQ", () => chkTXEQ.Checked, () => kainosClick(chkTXEQ), gold, () => kainosRightClick(chkTXEQ));
            _kainosEqGrid.Add("Equalizer...", () => false, () => equalizerToolStripMenuItem_Click(this, EventArgs.Empty), ice);
            _kainosColumn.AddSection("eq", "EQ", w => _kainosEqGrid.PreferredHeight(w), r => _kainosEqGrid.SetBounds(r.Left, r.Top, r.Width, r.Height), false);

            // KAINOS AUDIO: each stage of the receive and transmit chains on / off, in chain order, plus BYPASS
            // and the Kainos Audio window
            _kainosAudioRxLabel = new KainosTextLine(() => "Receive", () => KainosUI.Dim);
            _kainosAudioTxLabel = new KainosTextLine(() => "Transmit", () => KainosUI.Dim);
            _kainosAudioRx = kainosStripGrid(true);
            _kainosAudioTx = kainosStripGrid(false);
            _kainosColumn.AddSection("audio", "KAINOS AUDIO", kainosAudioMeasure, kainosAudioArrange, false);
            AetherStripRX.Changed += (s, e) => _kainosAudioRx.Invalidate();
            AetherStripTX.Changed += (s, e) => _kainosAudioTx.Invalidate();

            // FREEDV: RADE on RX1 / RX2, what it hears, the FreeDV window and the Reporter
            _kainosFreeDvStatus = new KainosTextLine(kainosFreeDvText, () => RadeAnyEnabled ? KainosUI.Text : KainosUI.Faint);
            _kainosFreeDvGrid = new KainosActionGrid(2);
            _kainosFreeDvGrid.Add("RADE RX1", () => RadeEnabledOn(0), () => SetRadeEnabled(0, !RadeEnabledOn(0)), gold);
            _kainosFreeDvGrid.Add("RADE RX2", () => RadeEnabledOn(1), () => SetRadeEnabled(1, !RadeEnabledOn(1)), KainosUI.Tone.Violet);
            _kainosFreeDvGrid.Add("FreeDV...", () => false, ShowFreeDV, ice);
            _kainosFreeDvGrid.Add("Reporter...", () => FreeDVReporter.FreeDVReporterManager.IsConnected, () => FreeDVReporter.FreeDVReporterManager.ShowWindow(this), ice);
            _kainosColumn.AddSection("freedv", "FREEDV", w => KainosUI.S(20) + KainosUI.S(6) + _kainosFreeDvGrid.PreferredHeight(w), kainosFreeDvArrange, false);
            RadeEnabledChanged += (s, e) => { _kainosFreeDvGrid.Invalidate(); _kainosFreeDvStatus.Invalidate(); };

            // MEMORY: the saved memories (click one to tune to it) and Thetis's Memory window
            _kainosMemoryList = new KainosMemoryList(this);
            _kainosMemoryButtons = new KainosActionGrid(2);
            _kainosMemoryButtons.Add("Memories...", () => false, () => memoryToolStripMenuItem_Click(this, EventArgs.Empty), ice);
            _kainosColumn.AddSection("memory", "MEMORY", w => _kainosMemoryList.PreferredHeight() + KainosUI.S(6) + _kainosMemoryButtons.PreferredHeight(w), kainosMemoryArrange, false);
            if (MemoryList != null && MemoryList.List != null)
                MemoryList.List.ListChanged += (s, e) => { if (_kainosLayout) positionKainosColumn(); };

            foreach (Control c in new Control[] { _kainosEqGrid, _kainosAudioRxLabel, _kainosAudioTxLabel, _kainosAudioRx, _kainosAudioTx,
                                                  _kainosFreeDvStatus, _kainosFreeDvGrid, _kainosMemoryList, _kainosMemoryButtons })
                _kainosColumn.Viewport.Controls.Add(c);

            // RADE status and anything Thetis changes behind these buttons
            _kainosTabTimer = new Timer { Interval = 500 };
            _kainosTabTimer.Tick += (s, e) =>
            {
                if (!_kainosLayout || _kainosColumn == null || !_kainosColumn.Visible) return;
                _kainosFreeDvStatus.Invalidate();
                _kainosFreeDvGrid.Invalidate();
                _kainosEqGrid.Invalidate();
            };
            _kainosTabTimer.Start();
        }

        private static readonly System.Reflection.MethodInfo _kainosOnClick = typeof(Control).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        private static readonly System.Reflection.MethodInfo _kainosOnMouseDown = typeof(Control).GetMethod("OnMouseDown", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        // a real click on a Thetis button (it toggles and runs its handlers), and its right click
        private static void kainosClick(Control c) { _kainosOnClick.Invoke(c, new object[] { EventArgs.Empty }); }
        private static void kainosRightClick(Control c) { _kainosOnMouseDown.Invoke(c, new object[] { new MouseEventArgs(MouseButtons.Right, 1, 1, 1, 0) }); }

        #region KAINOS AUDIO

        private KainosActionGrid kainosStripGrid(bool rx)
        {
            KainosActionGrid g = new KainosActionGrid(3);
            AetherStrip strip = rx ? AetherStripRX : AetherStripTX;
            // the chain in its current order (the final limiter last on transmit)
            List<int> stages = strip.Order.ToList();
            if (!rx) stages.Add(AetherStripDefs.Limiter);
            foreach (int stage in stages)
            {
                int st = stage;
                if (st == AetherStripDefs.Exciter)
                    g.Add("Exciter", () => kainosAv(rx).Enable.Checked, () => kainosAv(rx).Enable.Checked = !kainosAv(rx).Enable.Checked, KainosUI.Tone.Gold,
                          () => kainosOpenAudio(rx));
                else
                    g.Add(kainosStageName(st), () => strip.Enabled(st), () => strip.Set(st, 0, strip.Enabled(st) ? 0 : 1), KainosUI.Tone.Gold,
                          () => kainosOpenAudio(rx));
            }
            g.Add("BYPASS", () => strip.Bypass, () => strip.Bypass = !strip.Bypass, KainosUI.Tone.Tx);
            g.Add("Open...", () => false, () => kainosOpenAudio(rx), KainosUI.Tone.Ice);
            return g;
        }

        private AetherVoiceSetupControls kainosAv(bool rx) { return rx ? SetupForm.AetherVoiceRX : SetupForm.AetherVoiceTX; }

        private static string kainosStageName(int stage)
        {
            switch (stage)
            {
                case AetherStripDefs.DeEss: return "De-Ess";
                case AetherStripDefs.Comp: return "Comp";
                case AetherStripDefs.Limiter: return "Limiter";
                default: return AetherStripDefs.StageNames[stage];
            }
        }

        private void kainosOpenAudio(bool rx)
        {
            ShowKainosAudio();
            if (_frmAether != null && !_frmAether.IsDisposed) _frmAether.SetSide(rx);
        }

        private int kainosAudioMeasure(int w)
        {
            int line = KainosUI.S(18);
            return line + _kainosAudioRx.PreferredHeight(w) + KainosUI.S(8) + line + _kainosAudioTx.PreferredHeight(w);
        }

        private void kainosAudioArrange(Rectangle r)
        {
            int line = KainosUI.S(18), y = r.Top;
            _kainosAudioRxLabel.SetBounds(r.Left, y, r.Width, line); y += line;
            int h = _kainosAudioRx.PreferredHeight(r.Width);
            _kainosAudioRx.SetBounds(r.Left, y, r.Width, h); y += h + KainosUI.S(8);
            _kainosAudioTxLabel.SetBounds(r.Left, y, r.Width, line); y += line;
            _kainosAudioTx.SetBounds(r.Left, y, r.Width, _kainosAudioTx.PreferredHeight(r.Width));
        }

        #endregion

        #region FREEDV

        private string kainosFreeDvText()
        {
            if (!RadeAnyEnabled) return "RADE is off";
            int rx = RadeEnabledOn(0) ? 0 : 1;
            string s = "RX" + (rx + 1) + "  ";
            try
            {
                s += RadeSyncOn(rx) ? "SYNC · SNR " + RadeSnrDbOn(rx) + " dB" : "no sync";
                string call = RadeRemoteCallsignOn(rx);
                if (call.Length > 0) s += "  ·  last " + call;
            }
            catch { }
            return s;
        }

        private void kainosFreeDvArrange(Rectangle r)
        {
            int line = KainosUI.S(20);
            _kainosFreeDvStatus.SetBounds(r.Left, r.Top, r.Width, line);
            _kainosFreeDvGrid.SetBounds(r.Left, r.Top + line + KainosUI.S(6), r.Width, _kainosFreeDvGrid.PreferredHeight(r.Width));
        }

        #endregion

        #region MEMORY

        private void kainosMemoryArrange(Rectangle r)
        {
            int h = _kainosMemoryList.PreferredHeight();
            _kainosMemoryList.SetBounds(r.Left, r.Top, r.Width, h);
            _kainosMemoryButtons.SetBounds(r.Left, r.Top + h + KainosUI.S(6), r.Width, _kainosMemoryButtons.PreferredHeight(r.Width));
        }

        #endregion

        #region METERS: right-click menu and the FTDX-5000 skin

        private ContextMenuStrip _kainosMeterMenu;

        private static readonly KeyValuePair<string, MeterType>[] KainosMeterTypes =
        {
            new KeyValuePair<string, MeterType>("Multimeter", MeterType.ANANMM),
            new KeyValuePair<string, MeterType>("Cross needle", MeterType.CROSS),
            new KeyValuePair<string, MeterType>("Magic eye", MeterType.MAGIC_EYE),
        };

        private void kainosHookMeterMenu(ucMeter uc)
        {
            if (_kainosMeterMenu == null)
            {
                _kainosMeterMenu = new ContextMenuStrip { Renderer = new KainosToolStripRenderer() };
                _kainosMeterMenu.Opening += (s, e) => kainosBuildMeterMenu();
            }
            uc.ContextMenuStrip = _kainosMeterMenu;
            if (uc.DisplayContainer != null) uc.DisplayContainer.ContextMenuStrip = _kainosMeterMenu;
        }

        private void kainosBuildMeterMenu()
        {
            _kainosMeterMenu.Items.Clear();
            foreach (KeyValuePair<string, MeterType> t in KainosMeterTypes)
            {
                MeterType type = t.Value;
                ToolStripMenuItem item = new ToolStripMenuItem(t.Key) { Checked = KainosMeterType == type.ToString(), ForeColor = KainosUI.Text };
                item.Click += (s, e) => kainosSetMeterType(type);
                _kainosMeterMenu.Items.Add(item);
            }
            _kainosMeterMenu.Items.Add(new ToolStripSeparator());
            _kainosMeterMenu.Items.Add(new ToolStripMenuItem("Meter settings...", null,
                (s, e) => { if (!IsSetupFormNull) SetupForm.ShowMultiMeterSetupTab(KainosMeterId); }) { ForeColor = KainosUI.Text });
            _kainosMeterMenu.Items.Add(new ToolStripMenuItem("Get the FTDX-5000 meter skin (OE3IDE)...", null,
                (s, e) => kainosOfferFtdx(false)) { ForeColor = KainosUI.Text });
            _kainosMeterMenu.Items.Add(new ToolStripSeparator());
            _kainosMeterMenu.Items.Add(new ToolStripMenuItem("Hide meters", null, (s, e) =>
            {
                _kainosColumn.TabState = string.Join(",", (_kainosColumn.TabState + ",-meters").Split(',').Where(x => x != "meters"));
                KainosColumnTabs = _kainosColumn.TabState;
                KainosSettingsChanged?.Invoke(this, EventArgs.Empty);
                positionKainosColumn();
            }) { ForeColor = KainosUI.Text });
        }

        // the container holds one meter: replace it with the chosen type
        private void kainosSetMeterType(MeterType type)
        {
            MeterManager.clsMeter m = MeterManager.MeterFromId(KainosMeterId);
            if (m == null) return;
            MeterType old;
            if (!Enum.TryParse(KainosMeterType, out old)) old = MeterType.ANANMM;
            if (old == type) return;
            m.RemoveMeterType(old, 0, false);
            m.AddMeter(type);
            m.ZeroOut(true, true);
            m.Rebuild();
            KainosMeterType = type.ToString();
            KainosSettingsChanged?.Invoke(this, EventArgs.Empty);
            positionKainosColumn();
        }

        // OE3IDE's FTDX-5000 multimeter skin, from his skin server (the one Kainos lists), downloaded and unpacked by
        // Thetis's own skin downloader (Setup's handler puts it in the Meters folder and refreshes the meters).
        // Offered once, the first time the METERS tab shows; any time from the meter's right-click menu.
        private const string KainosOe3ideSkins = "https://www.oe3ide.com/wordp/wp-content/uploads/thetisskins/thetis_skins.json";
        private const string KainosFtdxName = "FTDX-5000 (multimeter)";

        private void kainosOfferFtdx(bool firstTime)
        {
            if (firstTime)
            {
                if (KainosFtdxOffered) return;
                KainosFtdxOffered = true;
                KainosSettingsChanged?.Invoke(this, EventArgs.Empty);
            }
            DialogResult r = MessageBox.Show(
                "Ernst Siderits, OE3IDE, makes a multimeter skin in the style of the Yaesu FTDX-5000, which suits the Kainos " +
                "METERS tab.\r\n\r\n" +
                "Download it now from OE3IDE's skin server? It replaces the face of the multimeter (in every multimeter " +
                "you use), like any meter skin from Setup > Appearance > Skin Servers, where you can also put the Thetis " +
                "default back.\r\n\r\nOE3IDE's skins: https://www.oe3ide.com/wp/thetis-skin/",
                "Kainos - FTDX-5000 meter skin", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r != DialogResult.Yes) return;

            Task.Run(async () =>
            {
                string url = null, error = null;
                try
                {
                    using (System.Net.Http.HttpClient http = new System.Net.Http.HttpClient())
                    {
                        string json = await http.GetStringAsync(KainosOe3ideSkins).ConfigureAwait(false);
                        Newtonsoft.Json.Linq.JObject o = Newtonsoft.Json.Linq.JObject.Parse(json.TrimStart('﻿'));
                        foreach (Newtonsoft.Json.Linq.JToken skin in o["ThetisSkins"])
                            if ((string)skin["SkinName"] == KainosFtdxName) { url = (string)skin["SkinUrl"]; break; }
                    }
                    if (url == null) error = "The FTDX-5000 skin wasn't found on OE3IDE's skin server.";
                }
                catch (Exception ex) { error = "OE3IDE's skin server couldn't be reached: " + ex.Message; }

                BeginInvoke(new Action(() =>
                {
                    if (error != null)
                    {
                        MessageBox.Show(error + "\r\n\r\nYou can also get it from Setup > Appearance > Skin Servers.", "Kainos - FTDX-5000 meter skin",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    ThetisSkinService.DownloadFile(url, Path.GetTempFileName(), false, true);
                }));
            });
        }

        #endregion
    }

    // a grid of Kainos buttons driven by code (not bound to a Thetis button)
    internal class KainosActionGrid : Control
    {
        private class Item
        {
            public string Label;
            public Func<bool> On;
            public Action Click, RightClick;
            public KainosUI.Tone Tone;
            public RectangleF Rect;
        }

        private readonly int _columns;
        private readonly List<Item> _items = new List<Item>();
        private Item _hover;

        public KainosActionGrid(int columns)
        {
            _columns = columns;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = KainosUI.Surface;
        }

        public void Add(string label, Func<bool> on, Action click, KainosUI.Tone tone, Action rightClick = null)
        {
            _items.Add(new Item { Label = label, On = on, Click = click, Tone = tone, RightClick = rightClick });
        }

        public int PreferredHeight(int width)
        {
            int rows = (_items.Count + _columns - 1) / _columns;
            return rows == 0 ? 0 : rows * KainosUI.S(26) + (rows - 1) * KainosUI.S(4);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            float gap = KainosUI.S(4), h = KainosUI.S(26), w = (Width - gap * (_columns - 1)) / (float)_columns;
            for (int i = 0; i < _items.Count; i++)
            {
                Item it = _items[i];
                it.Rect = new RectangleF((i % _columns) * (w + gap), (i / _columns) * (h + gap), w, h);
                bool on = false;
                try { on = it.On(); } catch { }
                KainosUI.DrawButton(e.Graphics, it.Rect, it.Label, on, true, it == _hover, it.Tone, Math.Max(9f, KainosUI.S(12)));
            }
        }

        private Item hit(Point p) { return _items.Find(i => i.Rect.Contains(p)); }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Item h = hit(e.Location);
            if (h != _hover) { _hover = h; Cursor = h != null ? Cursors.Hand : Cursors.Default; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = null; Invalidate(); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Item it = hit(e.Location);
            if (it == null) return;
            if (e.Button == MouseButtons.Left) it.Click?.Invoke();
            else if (e.Button == MouseButtons.Right) it.RightClick?.Invoke();
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            KainosViewport v = Parent as KainosViewport;
            if (v != null) typeof(Control).GetMethod("OnMouseWheel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(v, new object[] { e });
        }
    }

    // one line of text drawn in the Kainos colours
    internal class KainosTextLine : Control
    {
        private readonly Func<string> _text;
        private readonly Func<Color> _colour;

        public KainosTextLine(Func<string> text, Func<Color> colour)
        {
            _text = text;
            _colour = colour;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = KainosUI.Surface;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using (Font f = new Font("Segoe UI", Math.Max(8f, KainosUI.S(12)), FontStyle.Regular, GraphicsUnit.Pixel))
            using (Brush b = new SolidBrush(_colour()))
            using (StringFormat sf = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                e.Graphics.DrawString(_text(), f, b, ClientRectangle, sf);
        }
    }

    // the saved memories (up to 12): name, frequency and mode; click one to tune to it
    internal class KainosMemoryList : Control
    {
        private const int MaxRows = 12;
        private readonly Console _console;
        private int _hover = -1;

        public KainosMemoryList(Console console)
        {
            _console = console;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = KainosUI.Surface;
            Cursor = Cursors.Hand;
        }

        private List<MemoryRecord> records()
        {
            try
            {
                if (_console.MemoryList == null || _console.MemoryList.List == null) return new List<MemoryRecord>();
                return _console.MemoryList.List.Take(MaxRows).ToList();
            }
            catch { return new List<MemoryRecord>(); }
        }

        private int rowH { get { return KainosUI.S(22); } }

        public int PreferredHeight()
        {
            int n = records().Count;
            return n == 0 ? KainosUI.S(22) : n * rowH;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            List<MemoryRecord> list = records();
            using (Font f = new Font("Segoe UI", Math.Max(8f, KainosUI.S(12)), FontStyle.Regular, GraphicsUnit.Pixel))
            using (Font mono = new Font("Consolas", Math.Max(8f, KainosUI.S(12)), FontStyle.Regular, GraphicsUnit.Pixel))
            {
                if (list.Count == 0)
                {
                    using (Brush b = new SolidBrush(KainosUI.Faint)) g.DrawString("No memories saved yet", f, b, 2, KainosUI.S(3));
                    return;
                }
                for (int i = 0; i < list.Count; i++)
                {
                    MemoryRecord m = list[i];
                    Rectangle r = new Rectangle(0, i * rowH, Width, rowH);
                    if (i == _hover) using (Brush b = new SolidBrush(KainosUI.Raised)) g.FillRectangle(b, r);
                    string name = string.IsNullOrEmpty(m.Name) ? m.Group : m.Name;
                    string freq = m.RXFreq.ToString("0.000000");
                    string mode = m.DSPMode.ToString();
                    float modeW = KainosUI.S(44), freqW = KainosUI.S(92);
                    using (StringFormat sf = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                    {
                        using (Brush b = new SolidBrush(KainosUI.Text)) g.DrawString(name ?? "", f, b, new RectangleF(4, r.Top, Width - freqW - modeW - 8, rowH), sf);
                        using (Brush b = new SolidBrush(KainosUI.Ice)) g.DrawString(freq, mono, b, new RectangleF(Width - freqW - modeW, r.Top, freqW, rowH), sf);
                        using (Brush b = new SolidBrush(KainosUI.Dim)) g.DrawString(mode, f, b, new RectangleF(Width - modeW, r.Top, modeW, rowH), sf);
                    }
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int h = e.Y / rowH;
            if (h >= records().Count) h = -1;
            if (h != _hover) { _hover = h; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = -1; Invalidate(); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            List<MemoryRecord> list = records();
            int i = e.Y / rowH;
            if (i >= 0 && i < list.Count) _console.RecallMemory(list[i]);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            KainosViewport v = Parent as KainosViewport;
            if (v != null) typeof(Control).GetMethod("OnMouseWheel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(v, new object[] { e });
        }
    }
}
