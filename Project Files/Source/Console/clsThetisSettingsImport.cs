/*  clsThetisSettingsImport.cs

This file is part of Kainos, a program that implements a Software-Defined Radio.
Kainos is based on Thetis : https://github.com/ramdor/Thetis

Copyright (C) 2026 Justin Cron K7JUS

This program is free software; you can redistribute it and/or
modify it under the terms of the GNU General Public License
as published by the Free Software Foundation; either version 2
of the License, or (at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program; if not, write to the Free Software
Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
*/

using System;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Thetis
{
    // Kainos keeps its settings apart from Thetis (OpenHPSDR\Kainos-x64 rather than OpenHPSDR\Thetis-x64).
    // The first time Kainos starts without any settings of its own, offer once to copy an existing
    // Thetis install's settings folder (databases, meters, skins) and registry settings (cmASIO etc).
    internal static class ThetisSettingsImport
    {
        private const string MARKER_FILE = "thetis_import_offered.txt";
        private const string THETIS_REG_KEY = @"Software\OpenHPSDR\Thetis-x64";
        private const string KAINOS_REG_KEY = @"Software\OpenHPSDR\Kainos-x64";

        public static void OfferOnce(string kainos_data_path, string thetis_data_path)
        {
            try
            {
                string marker = Path.Combine(kainos_data_path, MARKER_FILE);
                if (File.Exists(marker)) return;
                if (!hasSettings(thetis_data_path)) return; // no Thetis install, ask again if one appears later

                if (hasSettings(kainos_data_path))
                {
                    writeMarker(marker, "not offered, Kainos already has its own settings");
                    return;
                }

                // Kainos: how to start (set up for the HL2, import, or skip); the setup wizard follows once the console is up
                string choice;
                using (KainosFirstRunChoice f = new KainosFirstRunChoice(thetis_data_path))
                {
                    f.ShowDialog();
                    choice = f.Choice;
                }
                KainosFirstRun.SetPending(kainos_data_path, choice);

                if (choice != "import")
                {
                    writeMarker(marker, "declined (" + choice + ")");
                    return;
                }

                int failed = 0;
                Cursor.Current = Cursors.WaitCursor;
                try
                {
                    copyFolder(thetis_data_path, kainos_data_path, ref failed);
                    copyRegistry();
                }
                finally
                {
                    Cursor.Current = Cursors.Default;
                }

                writeMarker(marker, failed == 0 ? "imported" : $"imported, {failed} file(s) could not be copied");

                if (failed > 0)
                {
                    MessageBox.Show($"Your Thetis settings were imported, but {failed} file(s) could not be copied (they may be in use).\n\n" +
                        "If anything is missing, close Thetis and import its database using the Database Manager.",
                        "Import Thetis Settings",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, Common.MB_TOPMOST);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("There was a problem importing your Thetis settings. Kainos will start with default settings.\n\n" + ex.Message,
                    "Import Thetis Settings",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, Common.MB_TOPMOST);
            }
        }

        private static bool hasSettings(string path)
        {
            if (!Directory.Exists(path)) return false;
            if (File.Exists(Path.Combine(path, "database.xml"))) return true; // pre database manager install

            string db_path = Path.Combine(path, "DB");
            return Directory.Exists(db_path) && Directory.GetFiles(db_path, "*dbman_settings.json").Length > 0;
        }

        private static void copyFolder(string source, string dest, ref int failed)
        {
            Directory.CreateDirectory(dest);

            foreach (string file in Directory.GetFiles(source))
            {
                string dest_file = Path.Combine(dest, Path.GetFileName(file));
                if (File.Exists(dest_file)) continue; // keep anything Kainos has already written this session (eg logs)

                try
                {
                    File.Copy(file, dest_file);
                }
                catch
                {
                    failed++;
                }
            }

            foreach (string folder in Directory.GetDirectories(source))
                copyFolder(folder, Path.Combine(dest, Path.GetFileName(folder)), ref failed);
        }

        private static void copyRegistry()
        {
            using (RegistryKey existing = Registry.CurrentUser.OpenSubKey(KAINOS_REG_KEY))
            {
                if (existing != null) return; // Kainos already has its own
            }

            using (RegistryKey source = Registry.CurrentUser.OpenSubKey(THETIS_REG_KEY))
            {
                if (source == null) return;

                using (RegistryKey dest = Registry.CurrentUser.CreateSubKey(KAINOS_REG_KEY))
                    copyKey(source, dest);
            }
        }

        private static void copyKey(RegistryKey source, RegistryKey dest)
        {
            foreach (string name in source.GetValueNames())
                dest.SetValue(name, source.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames), source.GetValueKind(name));

            foreach (string sub_name in source.GetSubKeyNames())
            {
                using (RegistryKey sub_source = source.OpenSubKey(sub_name))
                using (RegistryKey sub_dest = dest.CreateSubKey(sub_name))
                    copyKey(sub_source, sub_dest);
            }
        }

        private static void writeMarker(string marker, string result)
        {
            try
            {
                File.WriteAllText(marker, $"Thetis settings import offered {DateTime.Now:yyyy-MM-dd HH:mm}: {result}\r\n");
            }
            catch { }
        }
    }
}
