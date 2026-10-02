/*  KainosMacros.cs

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
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Thetis
{
    // The RTTY and CW terminals' macro buttons (six each, F1-F6 from the typing line): a label, the text ({MY} your
    // call, {CALL} theirs) and what a click does. Right-click a button to edit it. Saved with the options (hidden
    // Setup boxes) as Base64 so any text survives.
    internal enum KainosMacroAction { SendThenReceive, SendKeepGoing, Insert }

    internal class KainosMacro
    {
        public string Label = "", Text = "";
        public KainosMacroAction Action = KainosMacroAction.SendThenReceive;
        public KainosMacro Copy() { return new KainosMacro { Label = Label, Text = Text, Action = Action }; }
    }

    internal static class KainosMacros
    {
        public const int Count = 6;

        public static List<KainosMacro> RttyDefaults()
        {
            return new List<KainosMacro>
            {
                new KainosMacro { Label = "CQ", Text = "\nCQ CQ CQ DE {MY} {MY} {MY} PSE K\n" },
                new KainosMacro { Label = "ANS", Text = "\n{CALL} {CALL} DE {MY} {MY} K\n" },
                new KainosMacro { Label = "599", Text = "\n{CALL} DE {MY} TU UR 599 599 BK\n" },
                new KainosMacro { Label = "73", Text = "\n{CALL} TU 73 DE {MY} SK\n" },
                new KainosMacro { Label = "QRZ", Text = "\nQRZ? DE {MY} K\n" },
                new KainosMacro { Label = "MY", Text = "{MY} ", Action = KainosMacroAction.Insert },
            };
        }

        public static List<KainosMacro> CwDefaults()
        {
            return new List<KainosMacro>
            {
                new KainosMacro { Label = "CQ", Text = "CQ CQ CQ DE {MY} {MY} K " },
                new KainosMacro { Label = "ANS", Text = "{CALL} DE {MY} {MY} K " },
                new KainosMacro { Label = "599", Text = "{CALL} TU UR 5NN 5NN BK " },
                new KainosMacro { Label = "73", Text = "{CALL} TU 73 DE {MY} SK " },
                new KainosMacro { Label = "QRZ", Text = "QRZ? DE {MY} K " },
                new KainosMacro { Label = "MY", Text = "{MY} ", Action = KainosMacroAction.Insert },
            };
        }

        // records separated by 0x1E, fields (label, text, action) by 0x1F, the whole in Base64
        public static string Serialize(List<KainosMacro> list)
        {
            string raw = string.Join("\x1E", list.Select(m => m.Label + "\x1F" + m.Text + "\x1F" + (int)m.Action));
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
        }

        public static List<KainosMacro> Parse(string saved, List<KainosMacro> defaults)
        {
            List<KainosMacro> list = defaults.Select(m => m.Copy()).ToList();
            if (string.IsNullOrEmpty(saved)) return list;
            try
            {
                string raw = Encoding.UTF8.GetString(Convert.FromBase64String(saved));
                string[] recs = raw.Split('\x1E');
                for (int i = 0; i < recs.Length && i < list.Count; i++)
                {
                    string[] f = recs[i].Split('\x1F');
                    if (f.Length < 3) continue;
                    int a;
                    list[i] = new KainosMacro { Label = f[0], Text = f[1], Action = int.TryParse(f[2], out a) && a >= 0 && a <= 2 ? (KainosMacroAction)a : KainosMacroAction.SendThenReceive };
                }
            }
            catch (FormatException) { }
            return list;
        }

        // the editor: label, text, what a click does; Reset puts back the default. True if anything changed.
        public static bool Edit(IWin32Window owner, KainosMacro m, KainosMacro def, string mode, int index)
        {
            using (Form f = new Form())
            {
                f.Text = mode + " macro " + (index + 1) + " (F" + (index + 1) + ")";
                f.FormBorderStyle = FormBorderStyle.FixedDialog;
                f.MinimizeBox = f.MaximizeBox = false;
                f.ShowInTaskbar = false;
                f.StartPosition = FormStartPosition.CenterParent;
                f.BackColor = KainosUI.Panel;
                f.ForeColor = KainosUI.Text;
                f.Font = new Font("Segoe UI", 9f);
                f.ClientSize = new Size(470, 300);

                Label l1 = new Label { Text = "Label", Location = new Point(12, 15), AutoSize = true };
                TextBox label = new TextBox { Text = m.Label, Location = new Point(110, 12), Width = 120, MaxLength = 10, BackColor = KainosUI.Bg, ForeColor = KainosUI.Text, BorderStyle = BorderStyle.FixedSingle };
                Label l2 = new Label { Text = "Text", Location = new Point(12, 46), AutoSize = true };
                TextBox text = new TextBox
                {
                    Text = m.Text.Replace("\r\n", "\n").Replace("\n", "\r\n"),
                    Location = new Point(110, 43), Size = new Size(346, 110), Multiline = true, AcceptsReturn = true, ScrollBars = ScrollBars.Vertical,
                    CharacterCasing = CharacterCasing.Upper, BackColor = KainosUI.Bg, ForeColor = KainosUI.GoldHi, BorderStyle = BorderStyle.FixedSingle,
                    Font = new Font("Consolas", 10f),
                };
                Label hint = new Label { Text = "{MY}  your call (Setup > DSP > FreeDV (RADE))\r\n{CALL}  their call (the box beside the macros)", Location = new Point(110, 158), AutoSize = true, ForeColor = KainosUI.Dim };
                Label l3 = new Label { Text = "A click", Location = new Point(12, 199), AutoSize = true };
                ComboBox action = new ComboBox { Location = new Point(110, 195), Width = 346, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = KainosUI.Bg, ForeColor = KainosUI.Text, FlatStyle = FlatStyle.Flat };
                // the choices (CW has two: CWX keys the radio and lets go when it's done)
                KainosMacroAction[] choices = mode == "CW"
                    ? new[] { KainosMacroAction.SendThenReceive, KainosMacroAction.Insert }
                    : new[] { KainosMacroAction.SendThenReceive, KainosMacroAction.SendKeepGoing, KainosMacroAction.Insert };
                foreach (KainosMacroAction a in choices)
                    action.Items.Add(a == KainosMacroAction.Insert ? "Puts it in the typing line"
                                     : a == KainosMacroAction.SendKeepGoing ? "Sends it and keeps transmitting"
                                     : mode == "CW" ? "Sends it" : "Sends it, then back to receive");
                Func<KainosMacroAction, int> indexOf = a => Math.Max(0, Array.IndexOf(choices, a == KainosMacroAction.SendKeepGoing && mode == "CW" ? KainosMacroAction.SendThenReceive : a));
                action.SelectedIndex = indexOf(m.Action);

                Button reset = new Button { Text = "Reset to default", Location = new Point(12, 255), Size = new Size(120, 30), FlatStyle = FlatStyle.Flat, BackColor = KainosUI.Raised };
                Button ok = new Button { Text = "Save", Location = new Point(286, 255), Size = new Size(80, 30), FlatStyle = FlatStyle.Flat, BackColor = KainosUI.Raised, DialogResult = DialogResult.OK };
                Button cancel = new Button { Text = "Cancel", Location = new Point(376, 255), Size = new Size(80, 30), FlatStyle = FlatStyle.Flat, BackColor = KainosUI.Raised, DialogResult = DialogResult.Cancel };
                foreach (Button b in new[] { reset, ok, cancel }) b.FlatAppearance.BorderColor = KainosUI.Line;
                reset.Click += (s, e) => { label.Text = def.Label; text.Text = def.Text.Replace("\n", "\r\n"); action.SelectedIndex = indexOf(def.Action); };
                f.AcceptButton = ok;
                f.CancelButton = cancel;
                f.Controls.AddRange(new Control[] { l1, label, l2, text, hint, l3, action, reset, ok, cancel });
                if (f.ShowDialog(owner) != DialogResult.OK) return false;

                m.Label = label.Text.Trim();
                m.Text = text.Text.Replace("\r\n", "\n");
                m.Action = choices[Math.Max(0, action.SelectedIndex)];
                return true;
            }
        }
    }
}
