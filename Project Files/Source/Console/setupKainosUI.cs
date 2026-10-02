/*  setupKainosUI.cs

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
using System.Windows.Forms;

namespace Thetis
{
    // Setup > Appearance > Kainos: the console layout (Classic or Kainos)
    public partial class Setup
    {
        private TabPage tpAppearanceKainos;
        private ComboBoxTS comboKainosLayout;

        private void addKainosUITab()
        {
            tpAppearanceKainos = new TabPage("Kainos")
            {
                Name = "tpAppearanceKainos",
                BackColor = SystemColors.Control,
                Padding = new Padding(3)
            };

            GroupBoxTS grp = new GroupBoxTS { Name = "grpKainosLayout", Text = "Console layout", Location = new Point(8, 8), Size = new Size(430, 150) };
            comboKainosLayout = new ComboBoxTS
            {
                Name = "comboKainosLayout",
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(100, 24),
                Size = new Size(120, 21)
            };
            comboKainosLayout.Items.AddRange(new object[] { "Classic", "Kainos" });
            comboKainosLayout.SelectedIndex = 0;
            toolTip1.SetToolTip(comboKainosLayout, "Classic: the Thetis console exactly as your skin draws it.\r\n" +
                "Kainos: the console in the Kainos colours. Switch back to Classic at any time.");
            comboKainosLayout.SelectedIndexChanged += (s, e) => { if (!initializing) applyKainosUI(); };
            grp.Controls.Add(avLabel("Layout", 14, 27));
            grp.Controls.Add(comboKainosLayout);
            LabelTS note = avLabel("The Kainos layout is being built in stages (roadmap Phase 7). This stage gives the console, " +
                "menu bar and status bar the Kainos colours from the splash screen; your skin's buttons and the " +
                "panadapter are unchanged. Coming next: the left dock, the tabbed right column (with meters) and " +
                "the slice flags.", 14, 60);
            note.AutoSize = false;
            note.Size = new Size(404, 80);
            grp.Controls.Add(note);
            tpAppearanceKainos.Controls.Add(grp);

            tcAppearance.Controls.Add(tpAppearanceKainos);
        }

        // called from ForceAllEvents at startup, and when the setting changes
        private void applyKainosUI()
        {
            console.KainosLayout = comboKainosLayout.SelectedIndex == 1;
        }
    }
}
