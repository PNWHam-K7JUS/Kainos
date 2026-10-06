/*  KainosSupport.cs

This file is part of Kainos, a program that implements a Software Defined Radio.

Copyright (C) 2026 Justin Cron, K7JUS

This program is free software; you can redistribute it and/or modify it under the terms of the GNU General Public
License as published by the Free Software Foundation; either version 2 of the License, or (at your option) any later
version.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied
warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License along with this program; if not, write to the Free
Software Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
*/

using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace Thetis
{
    // Report a bug: what happened, and the diagnostics Kainos gathers (versions, Windows, screen, layout, radio, the
    // end of KainosErrors.txt), opened as a new GitHub issue in the browser. Nothing is sent by Kainos itself: the
    // user sees it all on GitHub and presses Submit there (or copies it to paste elsewhere).
    internal class KainosBugReport : Form
    {
        private const string NEW_ISSUE = "https://github.com/PNWHam-K7JUS/Kainos/issues/new";
        private readonly TextBox _title, _what, _diag;

        public KainosBugReport(string diagnostics)
        {
            Text = "Report a bug";
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = KainosWindowTheme.WindowBg;
            ForeColor = KainosWindowTheme.Text;
            Font = new Font("Segoe UI", 9.5f);
            AutoScaleDimensions = new SizeF(96f, 96f);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(620, 560);
            MinimumSize = new Size(520, 480);

            Controls.Add(new Label { Text = "Report a bug", Location = new Point(20, 14), Size = new Size(580, 30), Font = new Font("Segoe UI", 15f, FontStyle.Bold), ForeColor = KainosUI.GoldHi });
            Controls.Add(new Label { Text = "This opens a new issue on Kainos's GitHub page with the details below filled in. You'll need a free GitHub account " +
                                            "to submit it. Nothing is sent until you press Submit there.", Location = new Point(20, 48), Size = new Size(580, 40), ForeColor = KainosWindowTheme.TextMid,
                                     Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right });
            Controls.Add(new Label { Text = "Short summary", Location = new Point(20, 96), AutoSize = true });
            _title = field(new TextBox { Location = new Point(20, 118), Size = new Size(580, 24), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right });
            Controls.Add(new Label { Text = "What happened, and what were you doing? (steps to make it happen again help a lot)", Location = new Point(20, 152), AutoSize = true });
            _what = field(new TextBox { Location = new Point(20, 174), Size = new Size(580, 110), Multiline = true, ScrollBars = ScrollBars.Vertical, AcceptsReturn = true,
                                        Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right });
            Controls.Add(new Label { Text = "Diagnostics (included automatically)", Location = new Point(20, 294), AutoSize = true, ForeColor = KainosWindowTheme.TextMid });
            _diag = field(new TextBox { Location = new Point(20, 316), Size = new Size(580, 180), Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, WordWrap = false,
                                        Font = new Font("Consolas", 8.5f), Text = diagnostics.Replace("\n", "\r\n").Replace("\r\r\n", "\r\n"),
                                        Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right });
            Controls.AddRange(new Control[] { _title, _what, _diag });

            KainosWizardButton copy = new KainosWizardButton("Copy to clipboard") { Location = new Point(20, 510), Size = new Size(160, 32), Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
            copy.Click += (s, e) => { try { Clipboard.SetText(body()); } catch { } };
            KainosWizardButton cancel = new KainosWizardButton("Cancel") { Location = new Point(330, 510), Size = new Size(100, 32), Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
            cancel.Click += (s, e) => Close();
            KainosWizardButton open = new KainosWizardButton("Open on GitHub") { Location = new Point(440, 510), Size = new Size(160, 32), Accent = true, Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
            open.Click += (s, e) => openIssue();
            Controls.AddRange(new Control[] { copy, cancel, open });
        }

        private static TextBox field(TextBox t)
        {
            t.BackColor = KainosWindowTheme.FieldBg;
            t.ForeColor = KainosWindowTheme.Text;
            t.BorderStyle = BorderStyle.FixedSingle;
            return t;
        }

        private string body()
        {
            return "**What happened**\n\n" + (_what.Text.Trim().Length > 0 ? _what.Text.Trim() : "(please describe)") +
                   "\n\n**Diagnostics**\n\n```\n" + _diag.Text.Replace("\r\n", "\n") + "\n```\n";
        }

        private void openIssue()
        {
            string title = _title.Text.Trim().Length > 0 ? _title.Text.Trim() : "[BUG] ";
            string b = body();
            // browsers and GitHub cut very long addresses: the diagnostics are shortened if needed (all of it can
            // still be copied with Copy to clipboard)
            string url = NEW_ISSUE + "?labels=bug&title=" + Uri.EscapeDataString(title) + "&body=" + Uri.EscapeDataString(b);
            if (url.Length > 7500)
            {
                string cut = b.Substring(0, Math.Max(0, b.Length - (url.Length - 7400) / 3)) + "\n... (shortened: use Copy to clipboard for the rest)\n```\n";
                url = NEW_ISSUE + "?labels=bug&title=" + Uri.EscapeDataString(title) + "&body=" + Uri.EscapeDataString(cut);
            }
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); Close(); }
            catch (Exception ex) { MessageBox.Show(this, "The browser couldn't be opened: " + ex.Message + "\r\n\r\nUse Copy to clipboard instead.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }
    }

    // A newer Kainos is out: its version, its release notes, and Download / Later / Skip this version
    internal class KainosUpdateNotice : Form
    {
        public bool Skip { get; private set; }

        public KainosUpdateNotice(string version, string name, string notes, string url)
        {
            Text = "Kainos update";
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = KainosWindowTheme.WindowBg;
            ForeColor = KainosWindowTheme.Text;
            Font = new Font("Segoe UI", 9.5f);
            AutoScaleDimensions = new SizeF(96f, 96f);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(600, 480);
            MinimumSize = new Size(480, 360);

            Controls.Add(new Label { Text = (string.IsNullOrEmpty(name) ? "Kainos " + version : name) + " is available", Location = new Point(20, 14), Size = new Size(560, 30),
                                     Font = new Font("Segoe UI", 15f, FontStyle.Bold), ForeColor = KainosUI.GoldHi, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right });
            Controls.Add(new Label { Text = "You have Kainos " + KainosVersion.Number + ". What's new:", Location = new Point(20, 48), AutoSize = true, ForeColor = KainosWindowTheme.TextMid });
            TextBox t = new TextBox
            {
                Location = new Point(20, 72), Size = new Size(560, 340), Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                BackColor = KainosWindowTheme.FieldBg, ForeColor = KainosWindowTheme.Text, BorderStyle = BorderStyle.FixedSingle,
                Text = string.IsNullOrEmpty(notes) ? "(the release notes couldn't be loaded: see the release page)" : notes.Replace("\r\n", "\n").Replace("\n", "\r\n"),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            };
            Controls.Add(t);
            KainosWizardButton skip = new KainosWizardButton("Skip this version") { Location = new Point(20, 428), Size = new Size(150, 32), Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
            skip.Click += (s, e) => { Skip = true; Close(); };
            KainosWizardButton later = new KainosWizardButton("Later") { Location = new Point(340, 428), Size = new Size(100, 32), Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
            later.Click += (s, e) => Close();
            KainosWizardButton get = new KainosWizardButton("Download") { Location = new Point(450, 428), Size = new Size(130, 32), Accent = true, Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
            get.Click += (s, e) => { try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { } Close(); };
            Controls.AddRange(new Control[] { skip, later, get });
        }
    }
}
