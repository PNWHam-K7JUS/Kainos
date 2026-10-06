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

        // ---- the buttons: the squelch bar and the mode panels' buttons (MIC, COMP, VOX, DEXP, RX EQ, TX EQ, AV, TX FL...)
        // drawn flat in Kainos colours, gold while on, instead of the skin's pictures (put back in Classic) ----

        private class KainosButtonLook
        {
            public Image Back, Img; public ImageLayout Layout; public FlatStyle Flat; public Color BackColor, ForeColor, Checked, Over, Down, Border; public int BorderSize; public bool UseVisual; public Padding Padding; public Font Font; public ContentAlignment Align;
        }
        private readonly Dictionary<ButtonBase, KainosButtonLook> _kbtSaved = new Dictionary<ButtonBase, KainosButtonLook>();

        private IEnumerable<ButtonBase> kainosStyledButtons
        {
            get
            {
                IEnumerable<Control> panels = kainosModePanels.SelectMany(p => kainosDescendants(p));
                return new Control[] { chkSquelch }.Concat(panels).OfType<ButtonBase>()
                    .Where(b => !(b is CheckBox) || ((CheckBox)b).Appearance == Appearance.Button)
                    .Where(b => !(b is RadioButton) || ((RadioButton)b).Appearance == Appearance.Button)
                    .Distinct();
            }
        }

        private void kainosButtonsOn()
        {
            foreach (ButtonBase b in kainosStyledButtons)
            {
                // the skin's look, unless it's already ours (a skin load sets its pictures again: save those)
                if (!_kbtSaved.ContainsKey(b) || b.BackgroundImage != null || b.Image != null)
                {
                    _kbtSaved[b] = new KainosButtonLook
                    {
                        Back = b.BackgroundImage, Img = b.Image, Layout = b.BackgroundImageLayout, Flat = b.FlatStyle, BackColor = b.BackColor, ForeColor = b.ForeColor,
                        Checked = b.FlatAppearance.CheckedBackColor, Over = b.FlatAppearance.MouseOverBackColor, Down = b.FlatAppearance.MouseDownBackColor,
                        Border = b.FlatAppearance.BorderColor, BorderSize = b.FlatAppearance.BorderSize, UseVisual = b.UseVisualStyleBackColor, Padding = b.Padding, Font = b.Font, Align = b.TextAlign,
                    };
                }
                b.BackgroundImage = null;
                b.Image = null;
                b.FlatStyle = FlatStyle.Flat;
                b.UseVisualStyleBackColor = false;
                b.Padding = Padding.Empty;          // the scaled-down panels' buttons are small: all the room for the text
                b.TextAlign = ContentAlignment.MiddleCenter;
                b.BackColor = KainosUI.Raised;
                b.ForeColor = KainosUI.Text;
                b.FlatAppearance.BorderColor = KainosUI.Line;
                b.FlatAppearance.BorderSize = 1;
                b.FlatAppearance.MouseOverBackColor = Color.FromArgb(0x1b, 0x31, 0x46);
                b.FlatAppearance.MouseDownBackColor = KainosUI.Selected;
                b.FlatAppearance.CheckedBackColor = Color.FromArgb(0x3a, 0x2f, 0x18);      // on: a warm gold-brown, as the dock's lit buttons
                kainosFitButtonText(b);
                b.SizeChanged -= kainosButtonResized;
                b.SizeChanged += kainosButtonResized;
                b.Paint -= kainosButtonPaint;
                b.Paint += kainosButtonPaint;
                CheckBox cb = b as CheckBox;
                if (cb != null)
                {
                    cb.CheckedChanged -= kainosButtonChecked;
                    cb.CheckedChanged += kainosButtonChecked;
                    kainosButtonChecked(cb, EventArgs.Empty);
                }
            }
        }

        // the label's font made small enough to fit the (scaled-down) button
        private void kainosFitButtonText(ButtonBase b)
        {
            if (string.IsNullOrEmpty(b.Text) || b.Width < 8) return;
            KainosButtonLook l;
            Font basis = _kbtSaved.TryGetValue(b, out l) && l.Font != null ? l.Font : b.Font;
            float size = Math.Min(b.Font.Size, basis.Size);
            int room = b.Width - 6;
            FontFamily family = new FontFamily("Segoe UI");          // Kainos's font (clearer at small sizes than the skin's)
            Font f = new Font(family, size, FontStyle.Bold, basis.Unit);
            while (size > 5f && (TextRenderer.MeasureText(b.Text, f).Width > room || TextRenderer.MeasureText(b.Text, f).Height > b.Height - 2))
            {
                size -= 0.5f;
                f.Dispose();
                f = new Font(family, size, FontStyle.Bold, basis.Unit);
            }
            b.Font = f;
        }

        // drawn as the rest of Kainos's buttons (KainosUI.DrawButton: rounded, gold while on) over the flat button
        private void kainosButtonPaint(object sender, PaintEventArgs e)
        {
            ButtonBase b = (ButtonBase)sender;
            if (!_kainosLayout || !_kbtSaved.ContainsKey(b) || b.Width < 4 || b.Height < 4) return;
            Graphics g = e.Graphics;
            g.Clear(b.Parent != null ? b.Parent.BackColor : KainosUI.Bg);
            bool on = b is CheckBox && ((CheckBox)b).Checked;
            bool hover = b.Enabled && b.ClientRectangle.Contains(b.PointToClient(Cursor.Position));
            string text = b.Text.Replace("&&", "&");
            float px = Math.Min(KainosUI.S(12), b.Height * 0.46f);
            while (px > 7f)
            {
                using (Font f = new Font("Segoe UI", px, FontStyle.Bold, GraphicsUnit.Pixel))
                    if (g.MeasureString(text, f).Width <= b.Width - 4) break;
                px -= 0.5f;
            }
            KainosUI.DrawButton(g, new RectangleF(0.5f, 0.5f, b.Width - 1.5f, b.Height - 1.5f), text, on, b.Enabled, hover, KainosUI.Tone.Gold, px);
        }

        private void kainosButtonResized(object sender, EventArgs e)
        {
            ButtonBase b = (ButtonBase)sender;
            if (_kainosLayout && _kbtSaved.ContainsKey(b)) kainosFitButtonText(b);
        }

        // gold text and outline while on
        private void kainosButtonChecked(object sender, EventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            if (!_kbtSaved.ContainsKey(cb) || !_kainosLayout) return;
            cb.ForeColor = cb.Checked ? KainosUI.GoldHi : KainosUI.Text;
            cb.FlatAppearance.BorderColor = cb.Checked ? KainosUI.Gold : KainosUI.Line;
        }

        private void kainosButtonsOff()
        {
            foreach (KeyValuePair<ButtonBase, KainosButtonLook> kv in _kbtSaved)
            {
                ButtonBase b = kv.Key;
                KainosButtonLook l = kv.Value;
                if (b is CheckBox) ((CheckBox)b).CheckedChanged -= kainosButtonChecked;
                b.SizeChanged -= kainosButtonResized;
                b.Paint -= kainosButtonPaint;
                if (l.Font != null) b.Font = l.Font;
                b.FlatStyle = l.Flat;
                b.BackColor = l.BackColor;
                b.ForeColor = l.ForeColor;
                b.FlatAppearance.CheckedBackColor = l.Checked;
                b.FlatAppearance.MouseOverBackColor = l.Over;
                b.FlatAppearance.MouseDownBackColor = l.Down;
                b.FlatAppearance.BorderColor = l.Border;
                b.FlatAppearance.BorderSize = l.BorderSize;
                b.UseVisualStyleBackColor = l.UseVisual;
                b.Padding = l.Padding;
                b.TextAlign = l.Align;
                b.BackgroundImageLayout = l.Layout;
                b.BackgroundImage = l.Back;
                b.Image = l.Img;
            }
            _kbtSaved.Clear();
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
