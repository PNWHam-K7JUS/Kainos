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

using System.Drawing;
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
    }

    // Menu and status bars in the Kainos colours
    internal class KainosToolStripRenderer : ToolStripProfessionalRenderer
    {
        public KainosToolStripRenderer() : base(new KainosColorTable()) { RoundedEdges = false; }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            // top-level menu items and status labels: light text, gold while hovered or open; drop-down items: light text
            if (e.Item.Enabled)
                e.TextColor = e.Item.Selected || e.Item.Pressed ? KainosUI.GoldHi : KainosUI.Text;
            else
                e.TextColor = KainosUI.Faint;
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
