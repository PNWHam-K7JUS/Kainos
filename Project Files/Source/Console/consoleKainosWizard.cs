/*  consoleKainosWizard.cs

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
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace Thetis
{
    // The setup wizard (KainosSetupWizard.cs) from the console: once the console is up, it runs if the first-run
    // choice asked for it (KainosFirstRun), or is offered once (with Not now) to anyone it hasn't run for, which
    // includes everyone updating from an earlier Kainos. Setup > Appearance > Kainos > Run setup wizard runs it again.
    public partial class Console
    {
        private bool _kainosWizardStartupDone;

        private void kainosWizardOnStartup()
        {
            if (_kainosWizardStartupDone) return;
            _kainosWizardStartupDone = true;
            // a moment for the layout to settle, and the splash to go
            Timer t = new Timer { Interval = 1500 };
            t.Tick += (s, e) =>
            {
                t.Stop();
                t.Dispose();
                try
                {
                    if (IsSetupFormNull) return;
                    string pending = KainosFirstRun.TakePending(AppDataPath);
                    if (pending == "skip") { SetupForm.KainosWizardState = "skipped"; SetupForm.SaveOptions(); kainosCheckForUpdate(false); return; }
                    if (pending == "hl2" || pending == "import") { KainosRunWizard(false); kainosCheckForUpdate(false); return; }
                    if (string.IsNullOrEmpty(SetupForm.KainosWizardState)) KainosRunWizard(true);
                    kainosCheckForUpdate(false);        // once a day: a newer Kainos?
                }
                catch (Exception ex)
                {
                    try { File.AppendAllText(Path.Combine(AppDataPath, "KainosErrors.txt"), DateTime.Now.ToString("u") + "  setup wizard: " + ex + Environment.NewLine + Environment.NewLine); } catch { }
                }
            };
            t.Start();
        }

        // offer: start on the welcome page, with Not now
        internal void KainosRunWizard(bool offer)
        {
            if (IsSetupFormNull) return;
            KainosWizardAnswers now = SetupForm.KainosWizardRead();
            if (_kainosColumn != null)
                foreach (KeyValuePair<string, string> t in _kainosColumn.TabList)
                {
                    now.TabTitles.Add(new KeyValuePair<string, string>(t.Key, t.Value));
                    now.Tabs[t.Key] = _kainosColumn.IsOn(t.Key);
                }
            using (KainosSetupWizard w = new KainosSetupWizard(now, offer))
            {
                DialogResult dr = w.ShowDialog(this);
                if (dr == DialogResult.OK)
                {
                    if (_kainosColumn != null)
                        foreach (KeyValuePair<string, bool> t in w.Answers.Tabs) _kainosColumn.SetOn(t.Key, t.Value);
                    SetupForm.KainosWizardApply(w.Answers);
                }
                else if (SetupForm.KainosWizardState == "") { SetupForm.KainosWizardState = "later"; SetupForm.SaveOptions(); }
            }
        }
    }

    // Before the settings load, on a first run with a Thetis install to import from: how to start (set up for the
    // HL2, import the Thetis settings, or skip). The answer waits in a small file for the console (above).
    internal static class KainosFirstRun
    {
        private const string PENDING_FILE = "kainos_wizard_pending.txt";

        public static void SetPending(string kainos_data_path, string choice)
        {
            try { File.WriteAllText(Path.Combine(kainos_data_path, PENDING_FILE), choice); } catch { }
        }

        public static string TakePending(string kainos_data_path)
        {
            try
            {
                string f = Path.Combine(kainos_data_path, PENDING_FILE);
                if (!File.Exists(f)) return "";
                string s = File.ReadAllText(f).Trim();
                File.Delete(f);
                return s;
            }
            catch { return ""; }
        }
    }
}
