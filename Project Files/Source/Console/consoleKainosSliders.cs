/*  consoleKainosSliders.cs

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
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace Thetis
{
    // Kainos layout's sliders and menus, the same on every PC whatever skin is loaded.
    //
    // Sliders: Thetis's own sliders (PrettyTrackBar) stay in charge (dragging, clicks, the wheel, right clicks, the
    // TX limit bar); a skin only gives them their pictures, a track (the background image) and a thumb (the head
    // image). In Kainos layout they get Kainos's instead: a slim rounded track and a rounded thumb, made to each
    // slider's size, and the skin's come back in Classic. A skin loaded in Kainos layout gets them replaced again.
    //
    // Menus: the menu bar and its drop-downs in a modern spacing and font (the renderer in KainosUI.cs draws them);
    // Thetis's sizes come back in Classic.
    public partial class Console
    {
        private class KainosSliderLook { public Image Head, Back; public ImageLayout Layout; public Color BackColor; }
        private readonly Dictionary<PrettyTrackBar, KainosSliderLook> _kslSaved = new Dictionary<PrettyTrackBar, KainosSliderLook>();
        private readonly HashSet<Image> _kslOurs = new HashSet<Image>();
        private bool _kslOn;

        // the sliders Kainos layout shows: the column's (RX, TX, filter) and those in the mode panels
        private IEnumerable<PrettyTrackBar> kainosSliders
        {
            get
            {
                IEnumerable<Control> column = kainosRxRows.SelectMany(r => r).Concat(kainosTxRows.SelectMany(r => r))
                                                         .Concat(new Control[] { ptbFilterWidth, ptbFilterShift });
                IEnumerable<Control> panels = kainosModePanels.SelectMany(p => kainosDescendants(p));
                return column.Concat(panels).OfType<PrettyTrackBar>().Where(t => t.Orientation == Orientation.Horizontal).Distinct();
            }
        }

        private static IEnumerable<Control> kainosDescendants(Control c)
        {
            foreach (Control ch in c.Controls)
            {
                yield return ch;
                foreach (Control d in kainosDescendants(ch)) yield return d;
            }
        }

        private void kainosSlidersOn()
        {
            foreach (PrettyTrackBar t in kainosSliders)
            {
                // the skin's pictures, unless they're already ours (a skin load puts its own back: save those)
                if (!_kslSaved.ContainsKey(t) || (t.HeadImage != null && !_kslOurs.Contains(t.HeadImage)))
                {
                    _kslSaved[t] = new KainosSliderLook { Head = t.HeadImage, Back = t.BackgroundImage, Layout = t.BackgroundImageLayout, BackColor = t.BackColor };
                    t.SizeChanged -= kainosSliderResized;
                    t.SizeChanged += kainosSliderResized;
                    t.ParentChanged -= kainosSliderResized;
                    t.ParentChanged += kainosSliderResized;
                }
                kainosDressSlider(t);
            }
            _kslOn = true;
        }

        private void kainosSlidersOff()
        {
            _kslOn = false;
            foreach (KeyValuePair<PrettyTrackBar, KainosSliderLook> kv in _kslSaved)
            {
                PrettyTrackBar t = kv.Key;
                t.SizeChanged -= kainosSliderResized;
                t.ParentChanged -= kainosSliderResized;
                t.BackgroundImage = kv.Value.Back;
                t.BackgroundImageLayout = kv.Value.Layout;
                t.BackColor = kv.Value.BackColor;
                t.HeadImage = kv.Value.Head;
            }
            _kslSaved.Clear();
            foreach (Image i in _kslOurs) i.Dispose();
            _kslOurs.Clear();
        }

        private void kainosSliderResized(object sender, EventArgs e)
        {
            if (_kslOn && _kainosLayout) kainosDressSlider((PrettyTrackBar)sender);
        }

        // a slider's Kainos track and thumb, at its size
        private void kainosDressSlider(PrettyTrackBar t)
        {
            int w = t.Width, h = t.Height;
            if (w < 8 || h < 6) return;
            Color back = t.Parent != null ? t.Parent.BackColor : KainosUI.Surface;
            if (back.A < 255) back = KainosUI.Surface;

            int thumbH = Math.Max(8, Math.Min(h - 2, KainosUI.S(18))), thumbW = Math.Max(6, Math.Min(KainosUI.S(11), thumbH));
            Bitmap head = new Bitmap(thumbW, thumbH);
            using (Graphics g = Graphics.FromImage(head))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(back);
                RectangleF r = new RectangleF(0.5f, 0.5f, thumbW - 1.5f, thumbH - 1.5f);
                using (GraphicsPath p = KainosUI.RoundedRect(r, Math.Min(thumbW, thumbH) / 2.5f))
                {
                    using (Brush b = new LinearGradientBrush(new RectangleF(0, 0, thumbW, thumbH), KainosUI.Text, Color.FromArgb(0xb9, 0xc9, 0xd4), LinearGradientMode.Vertical))
                        g.FillPath(b, p);
                    using (Pen pen = new Pen(KainosUI.Ice)) g.DrawPath(pen, p);
                }
            }

            Bitmap track = new Bitmap(w, h);
            using (Graphics g = Graphics.FromImage(track))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(back);
                float th = Math.Max(3, KainosUI.S(4)), y = (h - th) / 2f, x0 = thumbW / 2f, x1 = w - thumbW / 2f;
                using (GraphicsPath p = KainosUI.RoundedRect(new RectangleF(x0, y, Math.Max(1, x1 - x0), th), th / 2))
                {
                    using (Brush b = new SolidBrush(Color.FromArgb(0x24, 0x3b, 0x51))) g.FillPath(b, p);
                    using (Pen pen = new Pen(KainosUI.Line)) g.DrawPath(pen, p);
                }
            }

            Image oldHead = t.HeadImage, oldBack = t.BackgroundImage;
            t.BackColor = back;
            t.BackgroundImageLayout = ImageLayout.None;
            t.BackgroundImage = track;
            t.HeadImage = head;
            _kslOurs.Add(head);
            _kslOurs.Add(track);
            if (oldHead != null && _kslOurs.Remove(oldHead)) oldHead.Dispose();
            if (oldBack != null && _kslOurs.Remove(oldBack)) oldBack.Dispose();
        }

        // ---- the menus ----

        private Font _kmFont;
        private Padding _kmStripPadding;
        private readonly Dictionary<ToolStripItem, Padding> _kmPadding = new Dictionary<ToolStripItem, Padding>();
        private bool _kmOn;

        private void kainosMenusOn()
        {
            if (!_kmOn)
            {
                _kmFont = menuStrip1.Font;
                _kmStripPadding = menuStrip1.Padding;
                _kmOn = true;
            }
            menuStrip1.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Regular);
            menuStrip1.Padding = new Padding(6, 3, 0, 3);
            foreach (ToolStripItem item in menuStrip1.Items)
            {
                if (!_kmPadding.ContainsKey(item)) _kmPadding[item] = item.Padding;
                item.Padding = new Padding(9, 3, 9, 3);
                ToolStripMenuItem mi = item as ToolStripMenuItem;
                if (mi != null) kainosMenuDropDown(mi);
            }
        }

        // a drop-down's rows: roomier, in the regular weight (menus built later get the same when they open)
        private void kainosMenuDropDown(ToolStripMenuItem parent)
        {
            parent.DropDown.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            parent.DropDown.Padding = new Padding(0, 4, 0, 4);
            parent.DropDownOpening -= kainosMenuOpening;
            parent.DropDownOpening += kainosMenuOpening;
            foreach (ToolStripItem item in parent.DropDownItems)
            {
                if (item is ToolStripSeparator) continue;
                if (!_kmPadding.ContainsKey(item)) _kmPadding[item] = item.Padding;
                item.Padding = new Padding(4, 4, 4, 4);
                ToolStripMenuItem mi = item as ToolStripMenuItem;
                if (mi != null && mi.HasDropDownItems) kainosMenuDropDown(mi);
            }
        }

        private void kainosMenuOpening(object sender, EventArgs e)
        {
            if (_kmOn && _kainosLayout) kainosMenuDropDown((ToolStripMenuItem)sender);
        }

        private void kainosMenusOff()
        {
            if (!_kmOn) return;
            _kmOn = false;
            menuStrip1.Font = _kmFont;
            menuStrip1.Padding = _kmStripPadding;
            foreach (KeyValuePair<ToolStripItem, Padding> kv in _kmPadding) kv.Key.Padding = kv.Value;
            _kmPadding.Clear();
            foreach (ToolStripItem item in menuStrip1.Items)
            {
                ToolStripMenuItem mi = item as ToolStripMenuItem;
                if (mi != null) kainosMenuDropDownOff(mi);
            }
        }

        private void kainosMenuDropDownOff(ToolStripMenuItem parent)
        {
            parent.DropDownOpening -= kainosMenuOpening;
            parent.DropDown.Font = menuStrip1.Font;
            parent.DropDown.Padding = new Padding(0, 2, 0, 2);
            foreach (ToolStripItem item in parent.DropDownItems)
            {
                ToolStripMenuItem mi = item as ToolStripMenuItem;
                if (mi != null && mi.HasDropDownItems) kainosMenuDropDownOff(mi);
            }
        }
    }
}
