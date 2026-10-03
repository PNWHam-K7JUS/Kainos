/*  KainosWindowTheme.cs

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
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Thetis
{
    // Every window Kainos opens (Setup, Memory, Equalizer, CWX, XVTRs, Linearity, Finder, About, the database
    // manager...) in the look of the Kainos Audio window: a dark title bar, the navy background, light text, flat
    // dark buttons, dark text / number / drop-down boxes, lists and grids, and dark tabs. Nothing in Thetis's windows is
    // changed in code: each window is restyled as it opens (and controls added to it later as they arrive).
    //
    // Only default colours are changed: a colour a window sets on purpose (a red or green status, a colour picker) is
    // left as it is. Windows Kainos draws itself, the console, meter windows and borderless pop-ups are left alone.
    internal static class KainosWindowTheme
    {
        // the Kainos Audio window's colours
        public static readonly Color WindowBg = Color.FromArgb(0x08, 0x12, 0x1d);
        public static readonly Color TitleBg = Color.FromArgb(0x0f, 0x0f, 0x1a);
        public static readonly Color PanelBg = Color.FromArgb(0x0b, 0x17, 0x24);
        public static readonly Color FieldBg = Color.FromArgb(0x06, 0x0e, 0x17);
        public static readonly Color Border = Color.FromArgb(0x2a, 0x3a, 0x4d);
        public static readonly Color Text = Color.FromArgb(0xc8, 0xd8, 0xe8);
        public static readonly Color TextMid = Color.FromArgb(0x8a, 0xa8, 0xc0);
        public static readonly Color TextDim = Color.FromArgb(0x5a, 0x6a, 0x7a);
        public static readonly Color ButtonBg = Color.FromArgb(0x14, 0x25, 0x38);
        public static readonly Color ButtonHover = Color.FromArgb(0x1b, 0x31, 0x46);
        public static readonly Color ButtonOn = Color.FromArgb(0x2a, 0x24, 0x14);
        public static readonly Color Gold = Color.FromArgb(0xd4, 0xad, 0x6a);
        public static readonly Color Selection = Color.FromArgb(0x1d, 0x3a, 0x55);

        private static Timer _scan;
        private static readonly HashSet<Form> _done = new HashSet<Form>();

        // look for new windows a few times a second
        public static void Start()
        {
            if (_scan != null) return;
            _scan = new Timer { Interval = 300 };
            _scan.Tick += (s, e) => scan();
            _scan.Start();
        }

        private static void scan()
        {
            _done.RemoveWhere(f => f.IsDisposed);
            foreach (Form f in Application.OpenForms)
            {
                if (_done.Contains(f) || f.IsDisposed) continue;
                _done.Add(f);
                if (skipForm(f)) continue;
                try { Theme(f); } catch { }
            }
        }

        private static bool skipForm(Form f)
        {
            if (f is Console || f is Splash || f is KainosFlagForm || f is frmAetherStrip || f is frmAetherVoice || f is frmMeterDisplay || f is frmInfoBarPopup) return true;
            if (f.FormBorderStyle == FormBorderStyle.None) return true;         // drawn pop-ups and overlays
            if (f.Tag as string == "kainos") return true;
            foreach (Control c in f.Controls) if (c is IKainosTerminal) return true;     // the RTTY / CW windows
            return false;
        }

        public static void Theme(Form f)
        {
            darkTitleBar(f);
            if (isDefaultBack(f.BackColor)) f.BackColor = WindowBg;
            if (isDefaultFore(f.ForeColor)) f.ForeColor = Text;
            if (f.BackgroundImage != null && isDefaultBack(f.BackColor)) f.BackgroundImage = null;
            themeChildren(f);
        }

        private static void themeChildren(Control parent)
        {
            parent.ControlAdded -= controlAdded;
            parent.ControlAdded += controlAdded;
            foreach (Control c in parent.Controls)
            {
                try { themeControl(c); } catch { }
                if (c.HasChildren || c is TabControl || c is Panel || c is GroupBox || c is UserControl) themeChildren(c);
            }
        }

        private static void controlAdded(object sender, ControlEventArgs e)
        {
            try { themeControl(e.Control); themeChildren(e.Control); } catch { }
        }

        // ---- each kind of control ----

        private static void themeControl(Control c)
        {
            string type = c.GetType().Name;
            if (type.StartsWith("Kainos") || type.Contains("Color") || type.Contains("Colour")) return;     // ours, colour pickers
            if (c is PictureBox || c is WebBrowser || c is PrettyTrackBar) return;

            if (c is TabControl) { themeTabs((TabControl)c); return; }
            if (c is TabPage) { TabPage p = (TabPage)c; p.UseVisualStyleBackColor = false; p.BackColor = WindowBg; p.ForeColor = Text; return; }
            if (c is ToolStrip) { ((ToolStrip)c).Renderer = new KainosToolStripRenderer(); if (isDefaultBack(c.BackColor)) c.BackColor = PanelBg; c.ForeColor = Text; return; }

            if (c is ButtonBase && !(c is CheckBox) && !(c is RadioButton)) { themeButton((ButtonBase)c); return; }
            if (c is CheckBox || c is RadioButton)
            {
                ButtonBase b = (ButtonBase)c;
                if ((c is CheckBox && ((CheckBox)c).Appearance == Appearance.Button) || (c is RadioButton && ((RadioButton)c).Appearance == Appearance.Button))
                {
                    themeButton(b);
                    b.FlatAppearance.CheckedBackColor = ButtonOn;
                    return;
                }
                if (isDefaultFore(c.ForeColor)) c.ForeColor = Text;
                if (isDefaultBack(c.BackColor)) c.BackColor = Color.Transparent;
                return;
            }
            if (c is LinkLabel) { LinkLabel l = (LinkLabel)c; l.LinkColor = Gold; l.ActiveLinkColor = Color.White; l.VisitedLinkColor = Gold; if (isDefaultBack(c.BackColor)) c.BackColor = Color.Transparent; return; }
            if (c is Label) { if (isDefaultFore(c.ForeColor)) c.ForeColor = Text; if (isDefaultBack(c.BackColor)) c.BackColor = Color.Transparent; return; }
            if (c is GroupBox) { if (isDefaultFore(c.ForeColor)) c.ForeColor = TextMid; if (isDefaultBack(c.BackColor)) c.BackColor = Color.Transparent; return; }

            if (c is TextBoxBase)
            {
                if (isDefaultBack(c.BackColor) || c.BackColor == SystemColors.Window) c.BackColor = FieldBg;
                if (isDefaultFore(c.ForeColor)) c.ForeColor = Text;
                TextBox t = c as TextBox;
                if (t != null && t.BorderStyle == BorderStyle.Fixed3D) t.BorderStyle = BorderStyle.FixedSingle;
                RichTextBox r = c as RichTextBox;
                if (r != null && r.BorderStyle == BorderStyle.Fixed3D) r.BorderStyle = BorderStyle.FixedSingle;
                return;
            }
            if (c is UpDownBase)
            {
                UpDownBase u = (UpDownBase)c;
                if (isDefaultBack(c.BackColor) || c.BackColor == SystemColors.Window) c.BackColor = FieldBg;
                if (isDefaultFore(c.ForeColor)) c.ForeColor = Text;
                if (u.BorderStyle == BorderStyle.Fixed3D) u.BorderStyle = BorderStyle.FixedSingle;
                return;
            }
            if (c is ComboBox)
            {
                ComboBox cb = (ComboBox)c;
                if (isDefaultBack(c.BackColor) || c.BackColor == SystemColors.Window) c.BackColor = FieldBg;
                if (isDefaultFore(c.ForeColor)) c.ForeColor = Text;
                if (cb.FlatStyle == FlatStyle.Standard || cb.FlatStyle == FlatStyle.System) cb.FlatStyle = FlatStyle.Flat;
                return;
            }
            if (c is ListBox)
            {
                ListBox lb = (ListBox)c;
                if (isDefaultBack(c.BackColor) || c.BackColor == SystemColors.Window) c.BackColor = FieldBg;
                if (isDefaultFore(c.ForeColor)) c.ForeColor = Text;
                if (lb.BorderStyle == BorderStyle.Fixed3D) lb.BorderStyle = BorderStyle.FixedSingle;
                return;
            }
            if (c is ListView)
            {
                ListView lv = (ListView)c;
                if (isDefaultBack(c.BackColor) || c.BackColor == SystemColors.Window) c.BackColor = FieldBg;
                if (isDefaultFore(c.ForeColor)) c.ForeColor = Text;
                if (lv.BorderStyle == BorderStyle.Fixed3D) lv.BorderStyle = BorderStyle.FixedSingle;
                return;
            }
            if (c is DataGridView) { themeGrid((DataGridView)c); return; }
            if (c is TreeView)
            {
                if (isDefaultBack(c.BackColor) || c.BackColor == SystemColors.Window) c.BackColor = FieldBg;
                if (isDefaultFore(c.ForeColor)) c.ForeColor = Text;
                return;
            }
            if (c is TrackBar) { if (isDefaultBack(c.BackColor)) c.BackColor = WindowBg; return; }

            // panels, layout panels, user controls and anything else in the default colours
            if (isDefaultBack(c.BackColor)) c.BackColor = c is Panel || c is UserControl ? Color.Transparent : WindowBg;
            if (isDefaultFore(c.ForeColor)) c.ForeColor = Text;
            if (c.ContextMenuStrip != null) c.ContextMenuStrip.Renderer = new KainosToolStripRenderer();
        }

        private static void themeButton(ButtonBase b)
        {
            if (b.BackgroundImage != null || b.Image != null && !isDefaultBack(b.BackColor)) return;    // a skinned or pictured button
            if (!isDefaultBack(b.BackColor) && b.BackColor != SystemColors.ButtonFace) return;        // a colour set on purpose
            b.FlatStyle = FlatStyle.Flat;
            b.BackColor = ButtonBg;
            if (isDefaultFore(b.ForeColor)) b.ForeColor = Text;
            b.FlatAppearance.BorderColor = Border;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.MouseOverBackColor = ButtonHover;
            b.FlatAppearance.MouseDownBackColor = Selection;
            b.UseVisualStyleBackColor = false;
        }

        private static void themeGrid(DataGridView g)
        {
            g.EnableHeadersVisualStyles = false;
            g.BackgroundColor = FieldBg;
            g.GridColor = Border;
            g.BorderStyle = BorderStyle.FixedSingle;
            g.DefaultCellStyle.BackColor = PanelBg;
            g.DefaultCellStyle.ForeColor = Text;
            g.DefaultCellStyle.SelectionBackColor = Selection;
            g.DefaultCellStyle.SelectionForeColor = Color.White;
            g.AlternatingRowsDefaultCellStyle.BackColor = WindowBg;
            g.ColumnHeadersDefaultCellStyle.BackColor = ButtonBg;
            g.ColumnHeadersDefaultCellStyle.ForeColor = TextMid;
            g.ColumnHeadersDefaultCellStyle.SelectionBackColor = ButtonBg;
            g.RowHeadersDefaultCellStyle.BackColor = ButtonBg;
            g.RowHeadersDefaultCellStyle.ForeColor = TextMid;
            g.RowHeadersDefaultCellStyle.SelectionBackColor = Selection;
        }

        // ---- tabs: drawn by Kainos (dark, the selected one gold), and the strip behind them painted dark ----

        private static void themeTabs(TabControl tc)
        {
            if (tc.Tag as string == "kainos-tabs") return;
            tc.Tag = tc.Tag ?? "kainos-tabs";
            if (tc.Alignment == TabAlignment.Top || tc.Alignment == TabAlignment.Bottom)
            {
                tc.DrawMode = TabDrawMode.OwnerDrawFixed;
                tc.DrawItem += drawTab;
            }
            foreach (TabPage p in tc.TabPages) { p.UseVisualStyleBackColor = false; p.BackColor = WindowBg; p.ForeColor = Text; }
            if (tc.IsHandleCreated) new TabStripPainter(tc);
            else tc.HandleCreated += (s, e) => new TabStripPainter(tc);
        }

        private static void drawTab(object sender, DrawItemEventArgs e)
        {
            TabControl tc = (TabControl)sender;
            if (e.Index < 0 || e.Index >= tc.TabPages.Count) return;
            Rectangle r = tc.GetTabRect(e.Index);
            bool sel = e.Index == tc.SelectedIndex;
            using (Brush b = new SolidBrush(sel ? PanelBg : TitleBg)) e.Graphics.FillRectangle(b, r);
            if (sel) using (Pen p = new Pen(Gold, 2)) e.Graphics.DrawLine(p, r.Left + 2, r.Bottom - 1, r.Right - 2, r.Bottom - 1);
            TextRenderer.DrawText(e.Graphics, tc.TabPages[e.Index].Text, tc.Font, r, sel ? Gold : TextMid,
                                  TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }

        // paints what the owner-drawn tabs don't: the strip beside the tabs and the page's light border
        private class TabStripPainter : NativeWindow
        {
            private readonly TabControl _tc;
            public TabStripPainter(TabControl tc) { _tc = tc; AssignHandle(tc.Handle); tc.HandleDestroyed += (s, e) => ReleaseHandle(); }

            protected override void WndProc(ref Message m)
            {
                base.WndProc(ref m);
                const int WM_PAINT = 0x000F;
                if (m.Msg != WM_PAINT || _tc.TabCount == 0) return;
                try
                {
                    using (Graphics g = Graphics.FromHwnd(_tc.Handle))
                    {
                        Rectangle last = _tc.GetTabRect(_tc.TabCount - 1), first = _tc.GetTabRect(0);
                        int stripTop = Math.Min(first.Top, last.Top), stripBottom = Math.Max(first.Bottom, last.Bottom);
                        Rectangle page = _tc.DisplayRectangle;
                        using (Brush b = new SolidBrush(WindowBg))
                        {
                            if (_tc.Alignment == TabAlignment.Top)
                            {
                                g.FillRectangle(b, last.Right + 1, 0, Math.Max(0, _tc.Width - last.Right - 1), stripBottom + 1);
                                if (stripTop > 0) g.FillRectangle(b, 0, 0, _tc.Width, stripTop);
                            }
                            else if (_tc.Alignment == TabAlignment.Bottom)
                                g.FillRectangle(b, last.Right + 1, stripTop - 1, Math.Max(0, _tc.Width - last.Right - 1), _tc.Height - stripTop + 1);
                        }
                        // the page's border, in the window colours (covers the light 3-D edge)
                        using (Pen p = new Pen(WindowBg, 4)) g.DrawRectangle(p, page.X - 3, page.Y - 3, page.Width + 5, page.Height + 5);
                        using (Pen p = new Pen(Border)) g.DrawRectangle(p, page.X - 2, page.Y - 2, page.Width + 3, page.Height + 3);
                    }
                }
                catch { }
            }
        }

        // ---- the title bar: Windows 11 lets an app set its colours (Windows 10: dark mode only) ----

        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        private static void darkTitleBar(Form f)
        {
            if (f.IsHandleCreated) applyTitle(f);
            else f.HandleCreated += (s, e) => applyTitle(f);
        }

        private static void applyTitle(Form f)
        {
            try
            {
                int on = 1;
                DwmSetWindowAttribute(f.Handle, 20, ref on, 4);                        // DWMWA_USE_IMMERSIVE_DARK_MODE
                int caption = colorRef(TitleBg), text = colorRef(Text), border = colorRef(Border);
                DwmSetWindowAttribute(f.Handle, 35, ref caption, 4);                   // DWMWA_CAPTION_COLOR
                DwmSetWindowAttribute(f.Handle, 36, ref text, 4);                      // DWMWA_TEXT_COLOR
                DwmSetWindowAttribute(f.Handle, 34, ref border, 4);                    // DWMWA_BORDER_COLOR
            }
            catch { }
        }

        private static int colorRef(Color c) { return c.R | (c.G << 8) | (c.B << 16); }

        // ---- default colours (the ones a window didn't choose) ----

        private static bool isDefaultBack(Color c)
        {
            return c == SystemColors.Control || c == SystemColors.ControlLight || c == SystemColors.ButtonFace || c == SystemColors.Window
                   || c == SystemColors.Menu || c == SystemColors.InactiveBorder || c.ToArgb() == SystemColors.Control.ToArgb()
                   || c.ToArgb() == Color.White.ToArgb() && c.IsKnownColor;
        }

        private static bool isDefaultFore(Color c)
        {
            return c == SystemColors.ControlText || c == SystemColors.WindowText || c == SystemColors.MenuText
                   || c.ToArgb() == Color.Black.ToArgb() || c.ToArgb() == SystemColors.ControlText.ToArgb();
        }
    }
}
