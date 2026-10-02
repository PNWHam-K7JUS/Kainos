/*  KainosUI.cs

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
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Thetis
{
    // The Kainos look: colours taken from the Kainos splash screen, shared by everything Kainos draws.
    internal static class KainosUI
    {
        public static readonly Color Bg = Color.FromArgb(0x05, 0x0b, 0x13);         // deep navy (splash background)
        public static readonly Color Surface = Color.FromArgb(0x0a, 0x15, 0x20);    // bars and columns
        public static readonly Color Panel = Color.FromArgb(0x0e, 0x1b, 0x29);      // panels
        public static readonly Color Raised = Color.FromArgb(0x14, 0x25, 0x38);     // buttons, menus
        public static readonly Color Line = Color.FromArgb(0x1f, 0x33, 0x46);       // borders and grid
        public static readonly Color Steel = Color.FromArgb(0x46, 0x5f, 0x6f);
        public static readonly Color Text = Color.FromArgb(0xe9, 0xee, 0xf3);
        public static readonly Color Dim = Color.FromArgb(0x94, 0xa7, 0xb2);
        public static readonly Color Faint = Color.FromArgb(0x5f, 0x75, 0x84);
        public static readonly Color Ice = Color.FromArgb(0x7f, 0xb0, 0xcc);        // spectrum, active controls
        public static readonly Color IceHi = Color.FromArgb(0xa8, 0xd8, 0xf0);
        public static readonly Color Gold = Color.FromArgb(0xd4, 0xad, 0x6a);       // VFO A, selected
        public static readonly Color GoldHi = Color.FromArgb(0xe8, 0xc8, 0x8a);
        public static readonly Color Violet = Color.FromArgb(0xb5, 0xa8, 0xe0);     // VFO B
        public static readonly Color Tx = Color.FromArgb(0xe2, 0x57, 0x4c);         // transmit only
        public static readonly Color Selected = Color.FromArgb(0x1d, 0x2a, 0x33);   // background of a lit button

        // Kainos UI scale (Setup > Appearance > Kainos): sizes Kainos's own controls on top of Windows's display
        // scaling (Thetis isn't DPI-aware, so Windows already scales the whole program)
        private static float _scale = 1f;
        public static float Scale { get { return _scale; } }
        public static event EventHandler ScaleChanged;

        public static void SetScalePercent(int percent)
        {
            float s = Math.Max(75, Math.Min(200, percent)) / 100f;
            if (Math.Abs(s - _scale) < 0.001f) return;
            _scale = s;
            ScaleChanged?.Invoke(null, EventArgs.Empty);
        }

        public static int S(float designPx) { return (int)Math.Round(designPx * _scale); }

        public static GraphicsPath RoundedRect(RectangleF r, float radius)
        {
            float dd = radius * 2f;
            GraphicsPath p = new GraphicsPath();
            p.AddArc(r.X, r.Y, dd, dd, 180, 90);
            p.AddArc(r.Right - dd, r.Y, dd, dd, 270, 90);
            p.AddArc(r.Right - dd, r.Bottom - dd, dd, dd, 0, 90);
            p.AddArc(r.X, r.Bottom - dd, dd, dd, 90, 90);
            p.CloseFigure();
            return p;
        }

        public enum Tone { Gold, Ice, Violet, Tx }

        // a Kainos push button: navy when off; lit in its tone when on (TX fills red); dimmed when disabled
        public static void DrawButton(Graphics g, RectangleF r, string text, bool on, bool enabled, bool hover, Tone tone, float fontPx)
        {
            Color accent = tone == Tone.Ice ? Ice : tone == Tone.Violet ? Violet : tone == Tone.Tx ? Tx : Gold;
            Color accentHi = tone == Tone.Ice ? IceHi : tone == Tone.Violet ? Violet : tone == Tone.Tx ? Color.White : GoldHi;
            Color back, border, fore;
            if (!enabled) { back = Surface; border = Line; fore = Color.FromArgb(0x3e, 0x50, 0x5e); }
            else if (on && tone == Tone.Tx) { back = Tx; border = Tx; fore = Color.White; }
            else if (on) { back = Selected; border = accent; fore = accentHi; }
            else { back = hover ? Color.FromArgb(0x1a, 0x2e, 0x44) : Raised; border = Line; fore = Dim; }
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = RoundedRect(r, Math.Max(2f, r.Height * 0.14f)))
            {
                using (Brush b = new SolidBrush(back)) g.FillPath(b, path);
                using (Pen p = new Pen(border)) g.DrawPath(p, path);
            }
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using (Font f = new Font("Segoe UI", fontPx, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Brush b = new SolidBrush(fore))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.None, FormatFlags = StringFormatFlags.NoWrap })
                g.DrawString(text, f, b, r, sf);
        }
    }

    // Menu and status bars in the Kainos colours
    internal class KainosToolStripRenderer : ToolStripProfessionalRenderer
    {
        public KainosToolStripRenderer() : base(new KainosColorTable()) { RoundedEdges = false; }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            // menu items: light text, gold while hovered or open; status bar items keep their own colour (a
            // warning can be red)
            if (!e.Item.Enabled)
                e.TextColor = KainosUI.Faint;
            else if (e.ToolStrip is StatusStrip)
                e.TextColor = e.Item.Selected || e.Item.Pressed ? KainosUI.GoldHi : e.Item.ForeColor;
            else
                e.TextColor = e.Item.Selected || e.Item.Pressed ? KainosUI.GoldHi : KainosUI.Text;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = KainosUI.Dim;
            base.OnRenderArrow(e);
        }

        private class KainosColorTable : ProfessionalColorTable
        {
            public override Color MenuStripGradientBegin { get { return KainosUI.Surface; } }
            public override Color MenuStripGradientEnd { get { return KainosUI.Surface; } }
            public override Color StatusStripGradientBegin { get { return KainosUI.Surface; } }
            public override Color StatusStripGradientEnd { get { return KainosUI.Surface; } }
            public override Color ToolStripDropDownBackground { get { return KainosUI.Raised; } }
            public override Color ImageMarginGradientBegin { get { return KainosUI.Raised; } }
            public override Color ImageMarginGradientMiddle { get { return KainosUI.Raised; } }
            public override Color ImageMarginGradientEnd { get { return KainosUI.Raised; } }
            public override Color MenuBorder { get { return KainosUI.Steel; } }
            public override Color MenuItemBorder { get { return KainosUI.Gold; } }
            public override Color MenuItemSelected { get { return Color.FromArgb(0x1d, 0x2a, 0x33); } }
            public override Color MenuItemSelectedGradientBegin { get { return Color.FromArgb(0x1d, 0x2a, 0x33); } }
            public override Color MenuItemSelectedGradientEnd { get { return Color.FromArgb(0x1d, 0x2a, 0x33); } }
            public override Color MenuItemPressedGradientBegin { get { return KainosUI.Raised; } }
            public override Color MenuItemPressedGradientMiddle { get { return KainosUI.Raised; } }
            public override Color MenuItemPressedGradientEnd { get { return KainosUI.Raised; } }
            public override Color SeparatorDark { get { return KainosUI.Line; } }
            public override Color SeparatorLight { get { return KainosUI.Line; } }
            public override Color CheckBackground { get { return KainosUI.Raised; } }
            public override Color CheckSelectedBackground { get { return Color.FromArgb(0x1d, 0x2a, 0x33); } }
            public override Color CheckPressedBackground { get { return Color.FromArgb(0x1d, 0x2a, 0x33); } }
            public override Color ButtonSelectedBorder { get { return KainosUI.Gold; } }
            public override Color ButtonSelectedHighlight { get { return Color.FromArgb(0x1d, 0x2a, 0x33); } }
            public override Color ToolStripBorder { get { return KainosUI.Line; } }
        }
    }
}
