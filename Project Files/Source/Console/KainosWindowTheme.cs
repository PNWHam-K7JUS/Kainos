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
        public static readonly Color Text = Color.FromArgb(0xe8, 0xef, 0xf6);        // near white: easy to read on the navy (issue #3)
        public static readonly Color TextMid = Color.FromArgb(0xa8, 0xc4, 0xda);     // group titles and secondary text
        public static readonly Color TextDim = Color.FromArgb(0x7a, 0x8c, 0x9e);
        public static readonly Color ButtonBg = Color.FromArgb(0x14, 0x25, 0x38);
        public static readonly Color ButtonHover = Color.FromArgb(0x1b, 0x31, 0x46);
        public static readonly Color ButtonOn = Color.FromArgb(0x2a, 0x24, 0x14);
        public static readonly Color Gold = Color.FromArgb(0xd4, 0xad, 0x6a);
        public static readonly Color Selection = Color.FromArgb(0x1d, 0x3a, 0x55);
        public static readonly Color Accent = Color.FromArgb(0xa8, 0xd8, 0xf0);       // section titles (the Kainos ice blue)
        private static readonly Color BoxEdge = Color.FromArgb(0x4a, 0x60, 0x78);     // an unticked check box's edge

        private static Timer _scan;
        private static readonly HashSet<Form> _done = new HashSet<Form>();

        // windows are styled as Windows is about to show them (a hook on the UI thread sees WM_SHOWWINDOW before the
        // window appears), so they never flash up in the standard look first; and, as a safety net, looked for a few
        // times a second
        public static void Start()
        {
            if (_scan != null) return;
            _scan = new Timer { Interval = 300 };
            _scan.Tick += (s, e) => scan();
            _scan.Start();
            try
            {
                _hookProc = callWndProc;
                _hook = SetWindowsHookEx(4 /* WH_CALLWNDPROC */, _hookProc, IntPtr.Zero, GetCurrentThreadId());
                Application.ApplicationExit += (s, e) => { if (_hook != IntPtr.Zero) UnhookWindowsHookEx(_hook); _hook = IntPtr.Zero; };
            }
            catch { }
        }

        private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);
        private static HookProc _hookProc;          // kept, so it isn't collected while Windows holds it
        private static IntPtr _hook;
        private static bool _inHook;
        [StructLayout(LayoutKind.Sequential)] private struct CWPSTRUCT { public IntPtr lParam, wParam; public int message; public IntPtr hwnd; }
        [DllImport("user32.dll")] private static extern IntPtr SetWindowsHookEx(int id, HookProc proc, IntPtr mod, int thread);
        [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll")] private static extern int GetCurrentThreadId();

        private static IntPtr callWndProc(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code >= 0 && !_inHook)
            {
                try
                {
                    CWPSTRUCT m = (CWPSTRUCT)Marshal.PtrToStructure(lParam, typeof(CWPSTRUCT));
                    if (m.message == 0x0018 /* WM_SHOWWINDOW */ && m.wParam != IntPtr.Zero)
                    {
                        Form f = Control.FromHandle(m.hwnd) as Form;
                        if (f != null && !f.IsDisposed)
                        {
                            _inHook = true;
                            try
                            {
                                if (!_done.Contains(f)) adopt(f, false);
                                if (!skipForm(f)) cloakUntilPainted(f);
                            }
                            finally { _inHook = false; }
                        }
                    }
                }
                catch { }
            }
            return CallNextHookEx(_hook, code, wParam, lParam);
        }

        // a window is shown before everything in it has drawn itself (the parts not drawn yet show white for a frame):
        // hidden from the screen (DWM cloaking) until it has all been painted, then shown at once
        private static void cloakUntilPainted(Form f)
        {
            int on = 1;
            if (DwmSetWindowAttribute(f.Handle, 13 /* DWMWA_CLOAK */, ref on, 4) != 0) return;
            bool done = false;
            Action reveal = () =>
            {
                if (done) return;
                done = true;
                try { if (!f.IsDisposed && f.Visible) f.Refresh(); } catch { }        // everything painted, while hidden
                int off = 0;
                try { if (f.IsHandleCreated) DwmSetWindowAttribute(f.Handle, 13, ref off, 4); } catch { }
            };
            try { f.BeginInvoke(reveal); } catch { reveal(); }
            Timer safety = new Timer { Interval = 400 };                               // never left hidden
            safety.Tick += (s, e) => { safety.Stop(); safety.Dispose(); reveal(); };
            safety.Start();
        }

        private static void scan()
        {
            _done.RemoveWhere(f => f.IsDisposed);
            foreach (Form f in Application.OpenForms)
                if (!_done.Contains(f) && !f.IsDisposed) adopt(f, true);
        }

        // a window seen for the first time: styled now, and again each time it's shown
        private static void adopt(Form f, bool visibleNow)
        {
            {
                _done.Add(f);
                if (skipForm(f)) return;
                try { Theme(f); } catch { }
                // a window can set its own colours again after it's made (Setup loads its settings, a skin is
                // applied...): restyle it each time it's shown
                Form form = f;
                form.VisibleChanged += (s, e) => { if (form.Visible && !form.IsDisposed) themeSoon(form); };
                if (visibleNow && form.Visible) themeSoon(form);
            }
        }

        // now, and again a moment later (a skin can style a window just after it appears)
        private static void themeSoon(Form f)
        {
            try { Theme(f); } catch { }
            Timer again = new Timer { Interval = 600 };
            again.Tick += (s, e) => { again.Stop(); again.Dispose(); if (!f.IsDisposed && f.Visible) try { Theme(f); } catch { } };
            again.Start();
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
            if (f is Setup) KainosSetupShell.Apply((Setup)f);      // the search box and page list (KainosSetupShell.cs)
            themeChildren(f);
            f.Invalidate(true);         // repaint everything in the new colours
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
            themeControlColours(c);
            if (c is Label || c is ButtonBase || c is TextBoxBase || c is Panel || c is GroupBox) watchContrast(c);
        }

        // ---- contrast: text that ends up light on a light background (a window's own coloured box, like Thetis's
        // orange "TX Profile modified") is made dark, and put back when the background is dark again; and disabled
        // text, which Windows draws darker than its background (dark on the dark theme), is redrawn in a readable
        // grey (GitHub issue #3) ----
        private static readonly Color DarkText = Color.FromArgb(0x10, 0x18, 0x22);
        private static readonly Color DisabledText = Color.FromArgb(0x8a, 0x9c, 0xae);
        private static readonly HashSet<Control> _darkened = new HashSet<Control>();

        private static void watchContrast(Control c)
        {
            c.BackColorChanged -= contrastChanged;
            c.BackColorChanged += contrastChanged;
            ensureContrast(c);
            if ((c is Label || ((c is CheckBox || c is RadioButton) && ((ButtonBase)c).FlatStyle != FlatStyle.Flat && !isButtonLook(c))))
            {
                c.Paint -= paintDisabled;
                c.Paint += paintDisabled;
            }
        }

        private static bool isButtonLook(Control c)
        {
            return (c is CheckBox && ((CheckBox)c).Appearance == Appearance.Button) || (c is RadioButton && ((RadioButton)c).Appearance == Appearance.Button);
        }

        private static void contrastChanged(object sender, EventArgs e)
        {
            Control c = (Control)sender;
            try
            {
                ensureContrast(c);
                foreach (Control child in c.Controls) ensureContrast(child);      // a container's colour is its children's background
            }
            catch { }
        }

        private static void ensureContrast(Control c)
        {
            if (c.ForeColor.A != 255) return;
            int back = lum(effectiveBack(c));
            if (back >= 150 && lum(c.ForeColor) >= 150)
            {
                c.ForeColor = DarkText;
                _darkened.Add(c);
            }
            else if (back < 100 && _darkened.Contains(c) && c.ForeColor == DarkText)
            {
                c.ForeColor = Text;
                _darkened.Remove(c);
            }
        }

        // disabled labels, check boxes and radio buttons: the text again in a grey that reads on the dark theme
        private static void paintDisabled(object sender, PaintEventArgs e)
        {
            Control c = (Control)sender;
            if (c.Enabled || string.IsNullOrEmpty(c.Text)) return;
            Color back = effectiveBack(c);
            if (lum(back) >= 100) return;                   // a light background: Windows's own grey reads fine
            Rectangle r = c.ClientRectangle;
            // drawn as Windows draws disabled text (GDI+, which is a little narrower than GDI: a label sized for it
            // keeps all its text)
            ContentAlignment align;
            if (c is Label) align = ((Label)c).TextAlign;
            else
            {
                ButtonBase b = (ButtonBase)c;
                ContentAlignment check = c is CheckBox ? ((CheckBox)c).CheckAlign : ((RadioButton)c).CheckAlign;
                if (check != ContentAlignment.MiddleLeft || c.RightToLeft == RightToLeft.Yes) return;      // only the usual layout
                int glyph = (int)Math.Round(13 * c.DeviceDpi / 96.0) + 3;
                r = new Rectangle(glyph, 0, Math.Max(0, r.Width - glyph), r.Height);
                align = b.TextAlign;
            }
            using (StringFormat sf = new StringFormat { HotkeyPrefix = System.Drawing.Text.HotkeyPrefix.None })
            {
                int a = (int)align;
                sf.Alignment = (a & 0x222) != 0 ? StringAlignment.Center : (a & 0x444) != 0 ? StringAlignment.Far : StringAlignment.Near;
                sf.LineAlignment = (a & 0x070) != 0 ? StringAlignment.Center : (a & 0x700) != 0 ? StringAlignment.Far : StringAlignment.Near;
                if (c is Label && ((Label)c).AutoSize) sf.FormatFlags |= StringFormatFlags.NoWrap;
                using (Brush bb = new SolidBrush(back)) e.Graphics.FillRectangle(bb, r);
                using (Brush tb = new SolidBrush(DisabledText)) e.Graphics.DrawString(c.Text, c.Font, tb, r, sf);
            }
        }

        private static void themeControlColours(Control c)
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
                // Windows draws the tick box itself, light whatever the colours: Kainos draws over it
                if (b.FlatStyle != FlatStyle.System)
                {
                    c.Paint -= paintCheck;
                    c.Paint += paintCheck;
                }
                return;
            }
            if (c is LinkLabel) { LinkLabel l = (LinkLabel)c; l.LinkColor = Gold; l.ActiveLinkColor = Color.White; l.VisitedLinkColor = Gold; if (isDefaultBack(c.BackColor)) c.BackColor = Color.Transparent; return; }
            if (c is Label) { if (isDefaultFore(c.ForeColor)) c.ForeColor = Text; if (isDefaultBack(c.BackColor)) c.BackColor = Color.Transparent; return; }
            if (c is GroupBox)
            {
                GroupBox gb = (GroupBox)c;
                if (isDefaultFore(c.ForeColor) || c.ForeColor == Text) c.ForeColor = TextMid;
                if (isDefaultBack(c.BackColor)) c.BackColor = Color.Transparent;
                gb.Paint -= paintGroup;                     // Windows's themed group box ignores the colours: Kainos paints it
                gb.Paint += paintGroup;
                gb.Invalidate();
                return;
            }

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
                // the up / down arrows (Windows's light buttons): drawn by Kainos
                foreach (Control part in u.Controls)
                    if (!(part is TextBoxBase))
                    {
                        part.Paint -= paintSpin;
                        part.Paint += paintSpin;
                        part.Invalidate();
                    }
                return;
            }
            if (c is ComboBox)
            {
                ComboBox cb = (ComboBox)c;
                if (isDefaultBack(c.BackColor) || c.BackColor == SystemColors.Window) c.BackColor = FieldBg;
                if (isDefaultFore(c.ForeColor)) c.ForeColor = Text;
                cb.FlatStyle = FlatStyle.Flat;
                darkNative(cb, "DarkMode_CFD");             // Windows's dark drop-down arrow and list
                if (cb.DrawMode == DrawMode.Normal)
                {
                    cb.DrawMode = DrawMode.OwnerDrawFixed;
                    cb.DrawItem -= drawComboItem;
                    cb.DrawItem += drawComboItem;
                }
                return;
            }
            if (c is ListBox)
            {
                ListBox lb = (ListBox)c;
                if (isDefaultBack(c.BackColor) || c.BackColor == SystemColors.Window) c.BackColor = FieldBg;
                if (isDefaultFore(c.ForeColor)) c.ForeColor = Text;
                if (lb.BorderStyle == BorderStyle.Fixed3D) lb.BorderStyle = BorderStyle.FixedSingle;
                darkNative(c, "DarkMode_Explorer");
                return;
            }
            if (c is ListView)
            {
                ListView lv = (ListView)c;
                if (isDefaultBack(c.BackColor) || c.BackColor == SystemColors.Window) c.BackColor = FieldBg;
                if (isDefaultFore(c.ForeColor)) c.ForeColor = Text;
                if (lv.BorderStyle == BorderStyle.Fixed3D) lv.BorderStyle = BorderStyle.FixedSingle;
                darkNative(c, "DarkMode_Explorer");
                return;
            }
            if (c is DataGridView) { themeGrid((DataGridView)c); darkNative(c, "DarkMode_Explorer"); return; }
            if (c is TreeView)
            {
                if (isDefaultBack(c.BackColor) || c.BackColor == SystemColors.Window) c.BackColor = FieldBg;
                if (isDefaultFore(c.ForeColor)) c.ForeColor = Text;
                return;
            }
            if (c is TrackBar) { if (isDefaultBack(c.BackColor)) c.BackColor = WindowBg; return; }
            if (c is ScrollBar) { darkNative(c, "DarkMode_Explorer"); return; }      // a grid's or panel's own scroll bars

            // panels, layout panels, user controls and anything else in the default colours
            if (isDefaultBack(c.BackColor)) c.BackColor = c is Panel || c is UserControl ? Color.Transparent : WindowBg;
            if (isDefaultFore(c.ForeColor)) c.ForeColor = Text;
            if (c.ContextMenuStrip != null) c.ContextMenuStrip.Renderer = new KainosToolStripRenderer();
        }

        private static void drawComboItem(object sender, DrawItemEventArgs e)
        {
            ComboBox cb = (ComboBox)sender;
            bool edit = (e.State & DrawItemState.ComboBoxEdit) != 0;
            bool sel = (e.State & DrawItemState.Selected) != 0 && !edit;
            Color back = sel ? Selection : cb.BackColor.A == 255 ? cb.BackColor : FieldBg;
            using (Brush b = new SolidBrush(back)) e.Graphics.FillRectangle(b, e.Bounds);
            if (e.Index < 0) return;
            string text = cb.GetItemText(cb.Items[e.Index]);
            Color fore = !cb.Enabled ? TextDim : sel ? Color.White : cb.ForeColor;
            TextRenderer.DrawText(e.Graphics, text, cb.Font, Rectangle.Inflate(e.Bounds, -2, 0), fore,
                                  TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }

        private static Color effectiveBack(Control c)
        {
            for (Control p = c; p != null; p = p.Parent) if (p.BackColor.A == 255) return p.BackColor;
            return WindowBg;
        }

        // a section, as AetherSDR draws them: a rounded frame with its title in bold ice blue on the line
        private static void paintGroup(object sender, PaintEventArgs e)
        {
            GroupBox gb = (GroupBox)sender;
            Graphics g = e.Graphics;
            Color back = effectiveBack(gb);
            using (Brush b = new SolidBrush(back)) g.FillRectangle(b, gb.ClientRectangle);
            using (Font bold = new Font(gb.Font, FontStyle.Bold))
            {
                Size ts = string.IsNullOrEmpty(gb.Text) ? Size.Empty : TextRenderer.MeasureText(gb.Text, bold);
                int top = Math.Max(ts.Height / 2, 1);
                Rectangle frame = new Rectangle(0, top, gb.Width - 1, gb.Height - top - 1);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                float rad = 4 * gb.DeviceDpi / 96f, d = rad * 2;
                using (System.Drawing.Drawing2D.GraphicsPath path = new System.Drawing.Drawing2D.GraphicsPath())
                {
                    path.AddArc(frame.X, frame.Y, d, d, 180, 90);
                    path.AddArc(frame.Right - d, frame.Y, d, d, 270, 90);
                    path.AddArc(frame.Right - d, frame.Bottom - d, d, d, 0, 90);
                    path.AddArc(frame.X, frame.Bottom - d, d, d, 90, 90);
                    path.CloseFigure();
                    using (Pen p = new Pen(Border)) g.DrawPath(p, path);
                }
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
                if (ts.Width > 0)
                {
                    Rectangle tr = new Rectangle(8, 0, ts.Width + 4, ts.Height);
                    using (Brush b = new SolidBrush(back)) g.FillRectangle(b, tr);
                    Color tc = !gb.Enabled ? TextDim : gb.ForeColor == TextMid ? Accent : gb.ForeColor;
                    TextRenderer.DrawText(g, gb.Text, bold, new Rectangle(tr.X + 2, tr.Y, tr.Width, tr.Height), tc, TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.SingleLine);
                }
            }
        }

        // check boxes and radio buttons: a dark box (or circle), gold when on, over Windows's light one
        private static void paintCheck(object sender, PaintEventArgs e)
        {
            ButtonBase c = (ButtonBase)sender;
            ContentAlignment align = c is CheckBox ? ((CheckBox)c).CheckAlign : ((RadioButton)c).CheckAlign;
            if (c.RightToLeft == RightToLeft.Yes) return;
            float k = c.DeviceDpi / 96f;
            int size = (int)Math.Round(13 * k);
            int x = 0, y;
            int a = (int)align;
            if ((a & 0x222) != 0) x = (c.Width - size) / 2;                 // centre
            else if ((a & 0x444) != 0) x = c.Width - size - 1;              // right
            if ((a & 0x007) != 0) y = 0;                                    // top
            else if ((a & 0x700) != 0) y = c.Height - size - 1;             // bottom
            else y = (c.Height - size) / 2;
            Rectangle box = new Rectangle(x, y, size - 1, size - 1);
            Graphics g = e.Graphics;
            Color back = effectiveBack(c);
            using (Brush b = new SolidBrush(back)) g.FillRectangle(b, Rectangle.Inflate(box, 2, 2));
            bool on, mixed = false;
            if (c is CheckBox) { CheckState st = ((CheckBox)c).CheckState; on = st == CheckState.Checked; mixed = st == CheckState.Indeterminate; }
            else on = ((RadioButton)c).Checked;
            Color edge = !c.Enabled ? Border : on || mixed ? Gold : BoxEdge;
            Color mark = c.Enabled ? Gold : TextDim;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            if (c is RadioButton)
            {
                using (Brush b = new SolidBrush(FieldBg)) g.FillEllipse(b, box);
                using (Pen p = new Pen(edge, Math.Max(1f, k))) g.DrawEllipse(p, box);
                if (on)
                {
                    RectangleF dot = RectangleF.Inflate(box, -3.2f * k, -3.2f * k);
                    using (Brush b = new SolidBrush(mark)) g.FillEllipse(b, dot);
                }
            }
            else
            {
                using (Brush b = new SolidBrush(FieldBg)) g.FillRectangle(b, box);
                using (Pen p = new Pen(edge, Math.Max(1f, k))) g.DrawRectangle(p, box);
                if (on)
                    using (Pen p = new Pen(mark, 1.8f * k) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round, LineJoin = System.Drawing.Drawing2D.LineJoin.Round })
                        g.DrawLines(p, new[] { new PointF(box.X + 2.8f * k, box.Y + 6.5f * k), new PointF(box.X + 5.2f * k, box.Y + 9f * k), new PointF(box.X + 9.8f * k, box.Y + 3.6f * k) });
                else if (mixed)
                    using (Brush b = new SolidBrush(mark)) g.FillRectangle(b, RectangleF.Inflate(box, -3.5f * k, -3.5f * k));
            }
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
        }

        // a number box's up / down buttons: dark, with ice arrows
        private static void paintSpin(object sender, PaintEventArgs e)
        {
            Control b = (Control)sender;
            Graphics g = e.Graphics;
            float k = b.DeviceDpi / 96f;
            Rectangle r = b.ClientRectangle;
            using (Brush bg = new SolidBrush(ButtonBg)) g.FillRectangle(bg, r);
            int mid = r.Height / 2;
            using (Pen p = new Pen(Border)) { g.DrawLine(p, 0, 0, 0, r.Height); g.DrawLine(p, 0, mid, r.Width, mid); }
            Color arrow = b.Enabled && b.Parent != null && b.Parent.Enabled ? TextMid : TextDim;
            float cx = r.Width / 2f + 0.5f, w = Math.Min(3.5f * k, r.Width / 3f), h = Math.Min(2.2f * k, mid / 3f);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (Brush ab = new SolidBrush(arrow))
            {
                float uy = mid / 2f;
                g.FillPolygon(ab, new[] { new PointF(cx - w, uy + h), new PointF(cx + w, uy + h), new PointF(cx, uy - h) });
                float dy = mid + (r.Height - mid) / 2f;
                g.FillPolygon(ab, new[] { new PointF(cx - w, dy - h), new PointF(cx + w, dy - h), new PointF(cx, dy + h) });
            }
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
        }

        private static void themeButton(ButtonBase b)
        {
            if (b.BackgroundImage != null) b.BackgroundImage = null;                                     // a skin's picture: the Kainos style instead
            bool plainBack = isDefaultBack(b.BackColor) || b.BackColor == SystemColors.ButtonFace || b.BackColor.A < 255
                             || b.BackColor.ToArgb() == Color.White.ToArgb() || b.BackColor == SystemColors.ControlLightLight
                             || lum(b.BackColor) < 60;     // the window's own dark colour, inherited
            if (!plainBack) return;                                                                         // a colour set on purpose
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
            // single lines in the grid colour (the raised 3-D header borders are drawn light)
            if (g.ColumnHeadersBorderStyle == DataGridViewHeaderBorderStyle.Raised) g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            if (g.RowHeadersBorderStyle == DataGridViewHeaderBorderStyle.Raised) g.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
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
            if (tc.Tag as string == "kainos-tabs" || tc.Tag as string == "kainos-shell") return;     // done, or Setup's hidden strips
            tc.Tag = tc.Tag ?? "kainos-tabs";
            if (tc.Alignment == TabAlignment.Top || tc.Alignment == TabAlignment.Bottom)
            {
                tc.DrawMode = TabDrawMode.OwnerDrawFixed;
                tc.DrawItem += drawTab;
                // flat buttons: no light 3-D border round the page; and room round each name, so the pill is bigger than
                // its text
                tc.Appearance = TabAppearance.FlatButtons;
                if (tc.Padding.X < 10) tc.Padding = new Point(10, Math.Max(tc.Padding.Y, 3));
                // without the separator bars Windows draws between flat tabs (light, whatever the colours)
                if (tc.IsHandleCreated) noSeparators(tc);
                tc.HandleCreated += (s, e) => noSeparators(tc);
            }
            foreach (TabPage p in tc.TabPages) { p.UseVisualStyleBackColor = false; p.BackColor = WindowBg; p.ForeColor = Text; }
            if (tc.IsHandleCreated) new TabStripPainter(tc);
            else tc.HandleCreated += (s, e) => new TabStripPainter(tc);
        }

        private static void drawTab(object sender, DrawItemEventArgs e)
        {
            TabControl tc = (TabControl)sender;
            if (e.Index < 0 || e.Index >= tc.TabPages.Count) return;
            drawPill(e.Graphics, tc, e.Index, tc.GetTabRect(e.Index));
        }

        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
        private static void noSeparators(TabControl tc)
        {
            try { SendMessage(tc.Handle, 0x1334 /* TCM_SETEXTENDEDSTYLE */, (IntPtr)1 /* TCS_EX_FLATSEPARATORS */, IntPtr.Zero); } catch { }
        }

        // a tab as a rounded pill: the one shown filled, with gold text and edge
        private static void drawPill(Graphics g, TabControl tc, int i, Rectangle r)
        {
            bool sel = i == tc.SelectedIndex;
            float k = tc.DeviceDpi / 96f;
            Rectangle p = new Rectangle(r.X + (int)(1 * k), r.Y + (int)(1 * k), r.Width - (int)(2 * k) - 1, r.Height - (int)(2 * k) - 1);
            using (Brush b = new SolidBrush(WindowBg)) g.FillRectangle(b, r);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            float rad = Math.Min(p.Height / 2f, 7 * k), d = rad * 2;
            using (System.Drawing.Drawing2D.GraphicsPath path = new System.Drawing.Drawing2D.GraphicsPath())
            {
                path.AddArc(p.X, p.Y, d, d, 180, 90);
                path.AddArc(p.Right - d, p.Y, d, d, 270, 90);
                path.AddArc(p.Right - d, p.Bottom - d, d, d, 0, 90);
                path.AddArc(p.X, p.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
                using (Brush b = new SolidBrush(sel ? Selection : PanelBg)) g.FillPath(b, path);
                using (Pen pen = new Pen(sel ? Gold : Border)) g.DrawPath(pen, path);
            }
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
            TextRenderer.DrawText(g, tc.TabPages[i].Text, tc.Font, p, sel ? Gold : Text,
                                  TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
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
                if (m.Msg != WM_PAINT || _tc.TabCount == 0 || _tc.Tag as string == "kainos-shell") return;
                try
                {
                    using (Graphics g = Graphics.FromHwnd(_tc.Handle))
                    {
                        // everything but the page in the window colour (the gaps between flat tabs and the edges round
                        // the page are drawn light), then each tab as a pill, and a thin frame round the page
                        Rectangle page = _tc.SelectedTab != null ? _tc.SelectedTab.Bounds : _tc.DisplayRectangle;
                        using (Region r = new Region(new Rectangle(0, 0, _tc.Width, _tc.Height)))
                        using (Brush b = new SolidBrush(WindowBg))
                        {
                            r.Exclude(page);
                            g.FillRegion(b, r);
                        }
                        for (int i = 0; i < _tc.TabCount; i++) drawPill(g, _tc, i, _tc.GetTabRect(i));
                        using (Pen p = new Pen(Border)) g.DrawRectangle(p, page.X - 1, page.Y - 1, page.Width + 1, page.Height + 1);
                    }
                }
                catch { }
            }
        }

        // ---- the title bar: Windows 11 lets an app set its colours (Windows 10: dark mode only) ----

        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] private static extern int SetWindowTheme(IntPtr hwnd, string app, string idList);

        // Windows 10 / 11's own dark versions of its controls (the drop-down's arrow, scroll bars)
        private static void darkNative(Control c, string theme)
        {
            if (c.IsHandleCreated) { try { SetWindowTheme(c.Handle, theme, null); } catch { } }
            else c.HandleCreated += (s, e) => { try { SetWindowTheme(c.Handle, theme, null); } catch { } };
        }

        // set now, and again whenever the window's handle is made again (a window that changes its border or taskbar
        // setting gets a new one, without the colours) or it's activated
        private static readonly HashSet<Form> _titleHooked = new HashSet<Form>();
        private static void darkTitleBar(Form f)
        {
            if (f.IsHandleCreated) applyTitle(f);
            if (_titleHooked.Add(f))
            {
                f.HandleCreated += (s, e) => applyTitle(f);
                f.Shown += (s, e) => applyTitle(f);
                f.Activated += (s, e) => applyTitle(f);
                f.Disposed += (s, e) => _titleHooked.Remove(f);
            }
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
                   || c == SystemColors.ControlLightLight || c.ToArgb() == Color.White.ToArgb();
        }

        // the default text colours, and any colour too dark to read on the dark windows
        private static int lum(Color c) { return (c.R * 299 + c.G * 587 + c.B * 114) / 1000; }

        private static bool isDefaultFore(Color c)
        {
            if (c == SystemColors.ControlText || c == SystemColors.WindowText || c == SystemColors.MenuText) return true;
            if (c.A == 0) return false;
            return (c.R * 299 + c.G * 587 + c.B * 114) / 1000 < 100;
        }
    }
}
