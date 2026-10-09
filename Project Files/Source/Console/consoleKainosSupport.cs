/*  consoleKainosSupport.cs

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
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;

namespace Thetis
{
    // Safety nets and light mode, reached from the Setup menu (and Report a bug at the top right of the menu bar):
    //   - Back up settings now: a Database Manager backup of the current settings (restore, and settings profiles,
    //     are in Thetis's Database Manager, which the Setup menu already has)
    //   - Light mode: no 3D and 20 frames a second, for slower PCs (the previous values are put back when it's off)
    //   - Check for updates (and once a day at startup): version.json on GitHub, with the release notes
    //   - Report a bug: a new GitHub issue with diagnostics (KainosSupport.cs)
    public partial class Console
    {
        private const string KAINOS_RAW = "https://raw.githubusercontent.com/PNWHam-K7JUS/Kainos/refs/heads/main/";
        private const string KAINOS_RELEASES = "https://github.com/PNWHam-K7JUS/Kainos/releases";
        private ToolStripMenuItem _kainosLightItem;

        private void kainosSupportMenus()
        {
            ToolStripItemCollection items = setupToolStripMenuItem.DropDownItems;
            Func<string, Action, ToolStripMenuItem> item = (text, act) =>
            {
                ToolStripMenuItem m = new ToolStripMenuItem(text);
                m.Click += (s, e) => act();
                return m;
            };
            databaseManagerToolStripMenuItem.Text = "Database Manager (settings profiles, backups, restore)...";
            items.Add(item("Back up settings now", kainosBackupNow));
            items.Add(new ToolStripSeparator());
            items.Add(item("Run setup wizard...", () => KainosRunWizard(false)));
            _kainosLightItem = item("Light mode (for slower PCs)", () => KainosSetLightMode(!KainosLightMode, true));
            items.Add(_kainosLightItem);
            items.Add(new ToolStripSeparator());
            items.Add(item("Check for updates...", () => kainosCheckForUpdate(true)));
            items.Add(item("Report a bug...", KainosReportBug));
            setupToolStripMenuItem.DropDownOpening += (s, e) => _kainosLightItem.Checked = KainosLightMode;

            // Report a bug, at the top right of the menu bar
            ToolStripMenuItem bug = item("Report a bug", KainosReportBug);
            bug.Alignment = ToolStripItemAlignment.Right;
            bug.ForeColor = setupToolStripMenuItem.ForeColor;
            menuStrip1.Items.Add(bug);
        }

        // ---- back up ----

        private void kainosBackupNow()
        {
            if (IsSetupFormNull) return;
            try
            {
                SetupForm.SaveOptions();        // the current settings into the database
                DB.WriteDB();                   // and the database to its file (as on exit), so the backup has them
                bool ok = DBMan.TakeBackup(Guid.Empty, "Kainos backup " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
                MessageBox.Show(this, ok ? "Your settings are backed up.\r\n\r\nTo restore a backup, or keep several settings profiles, use Setup > Database Manager."
                                         : "The backup couldn't be made. Setup > Database Manager can make one by hand.",
                    "Back up settings", MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
            catch (Exception ex) { MessageBox.Show(this, "The backup couldn't be made: " + ex.Message, "Back up settings", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        // ---- light mode ----

        internal bool KainosLightMode { get { return !IsSetupFormNull && SetupForm.KainosLight; } }

        // on: remember the frame rate and 3D, then 20 frames a second and no 3D; off: put them back. fromMenu: also
        // ticks Setup's box (whose own change calls back here with fromMenu false)
        internal void KainosSetLightMode(bool on, bool fromMenu)
        {
            if (IsSetupFormNull) return;
            if (fromMenu) { SetupForm.KainosLight = on; return; }
            if (on)
            {
                SetupForm.KainosLightSaved = "fps=" + SetupForm.KainosDisplayFps + ";3d=" + (Display.Kainos3D ? 1 : 0);
                SetupForm.KainosDisplayFps = Math.Min(SetupForm.KainosDisplayFps, 20);
                if (Display.Kainos3D) { Display.Kainos3D = false; kainos3DSave(); }
            }
            else
            {
                foreach (string kv in (SetupForm.KainosLightSaved ?? "").Split(';'))
                {
                    string[] p = kv.Split('=');
                    int v;
                    if (p.Length != 2 || !int.TryParse(p[1], out v)) continue;
                    if (p[0] == "fps") SetupForm.KainosDisplayFps = v;
                    if (p[0] == "3d" && v == 1 && !Display.Kainos3D) { Display.Kainos3D = true; kainos3DSave(); }
                }
            }
            if (_kainosLightItem != null) _kainosLightItem.Checked = on;
        }

        // ---- updates ----

        private bool _kainosUpdateChecked;

        // once a day at startup (quietly, only if there's something new), or from the menu (always says)
        private async void kainosCheckForUpdate(bool asked)
        {
            if (IsSetupFormNull) return;
            if (!asked)
            {
                if (_kainosUpdateChecked) return;
                _kainosUpdateChecked = true;
                if (SetupForm.KainosUpdateLastCheck == DateTime.Today.ToString("yyyy-MM-dd")) return;
            }
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                string json, notes = "";
                using (HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) })
                {
                    json = await http.GetStringAsync(KAINOS_RAW + "version.json");
                    JObject o = JObject.Parse(json);
                    string ver = (string)o["ReleaseVersion"] ?? "";
                    string name = (string)o["ReleaseName"] ?? "";
                    string url = (string)o["ReleaseURL"];
                    SetupForm.KainosUpdateLastCheck = DateTime.Today.ToString("yyyy-MM-dd");
                    bool newer = ver.Length > 0 && Common.CompareVersions(KainosVersion.Number, ver) < 0;
                    if (!newer)
                    {
                        if (asked) MessageBox.Show(this, "You have the latest Kainos (" + KainosVersion.Number + ").", "Check for updates", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    if (!asked && SetupForm.KainosUpdateSkip == ver) return;
                    try { notes = await http.GetStringAsync(KAINOS_RAW + "Documentation/ReleaseNotes/kainos-" + ver + ".md"); } catch { }
                    using (KainosUpdateNotice n = new KainosUpdateNotice(ver, name, notes, string.IsNullOrEmpty(url) ? KAINOS_RELEASES : url))
                    {
                        n.ShowDialog(this);
                        if (n.Skip) SetupForm.KainosUpdateSkip = ver;
                    }
                }
            }
            catch (Exception ex)
            {
                if (asked) MessageBox.Show(this, "Kainos couldn't check for updates (" + ex.Message + ").\r\n\r\nThe releases are at " + KAINOS_RELEASES,
                                           "Check for updates", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // ---- squelch kept over a restart (GitHub #10) ----

        // InitConsole calls ptbSquelch_Scroll / ptbRX2Squelch_Scroll, which store the slider's value as the threshold for
        // the current mode, before anything has put the saved threshold on the slider: in FM the saved level was replaced
        // by the slider's default (SQL 100) at every start. Put the saved threshold on each slider first, so the Scroll
        // stores what was saved.
        private void kainosSquelchSlidersFromSaved()
        {
            Func<PrettyTrackBar, int, int> clamp = (t, v) => Math.Max(t.Minimum, Math.Min(t.Maximum, v));
            ptbSquelch.Value = clamp(ptbSquelch, chkSquelch.CheckState == CheckState.Indeterminate ? rx1_voice_squelch_threshold_scroll :
                                     _rx1_dsp_mode == DSPMode.FM ? rx1_fm_squelch_threshold_scroll : rx1_squelch_threshold_scroll);
            ptbRX2Squelch.Value = clamp(ptbRX2Squelch, chkRX2Squelch.CheckState == CheckState.Indeterminate ? rx2_voice_squelch_threshold_scroll :
                                        _rx2_dsp_mode == DSPMode.FM ? rx2_fm_squelch_threshold_scroll : rx2_squelch_threshold_scroll);
        }

        // ---- report a bug ----

        internal void KainosReportBug()
        {
            string diag;
            try { diag = kainosDiagnostics(); } catch (Exception ex) { diag = "(diagnostics failed: " + ex.Message + ")"; }
            using (KainosBugReport r = new KainosBugReport(diag)) r.ShowDialog(this);
        }

        [System.Runtime.InteropServices.DllImport("gdi32.dll", EntryPoint = "GetDeviceCaps")]
        private static extern int kainosGetDeviceCaps(IntPtr hdc, int index);

        private string kainosDiagnostics()
        {
            StringBuilder sb = new StringBuilder();
            Action<string, Func<object>> line = (what, get) =>
            {
                string v;
                try { v = Convert.ToString(get()); } catch (Exception ex) { v = "(" + ex.GetType().Name + ")"; }
                sb.Append(what).Append(": ").Append(v).Append('\n');
            };
            line("Kainos", () => KainosVersion.Number + " (Thetis " + Common.GetVerNum(true) + ")");
            line("Windows", () =>
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    if (k == null) return Environment.OSVersion.ToString();
                    // Windows 11 still calls itself Windows 10 here: its builds start at 22000
                    string product = Convert.ToString(k.GetValue("ProductName"));
                    int build;
                    if (int.TryParse(Convert.ToString(k.GetValue("CurrentBuild")), out build) && build >= 22000) product = product.Replace("Windows 10", "Windows 11");
                    return product + " " + k.GetValue("DisplayVersion") + " (build " + build + ")";
                }
            });
            line("PC", () => Environment.ProcessorCount + " logical processors, " + (Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit"));
            // the real resolution and scaling (Windows reports scaled-down figures to a program that isn't DPI aware)
            line("Screen", () =>
            {
                using (System.Drawing.Graphics g = CreateGraphics())
                {
                    float dpi = g.DpiX;     // before GetHdc: the Graphics can't be used while its HDC is out
                    IntPtr hdc = g.GetHdc();
                    try
                    {
                        int real = kainosGetDeviceCaps(hdc, 118), seen = kainosGetDeviceCaps(hdc, 8), realH = kainosGetDeviceCaps(hdc, 117);   // DESKTOPHORZRES, HORZRES, DESKTOPVERTRES
                        double scale = seen > 0 ? (double)real / seen : 1.0;
                        return real + "x" + realH + ", scaling " + Math.Round(scale * dpi / 96.0 * 100) + "%, " + Screen.AllScreens.Length + " screen(s)";
                    }
                    finally { g.ReleaseHdc(hdc); }
                }
            });
            line("Window", () => WindowState + ", " + Width + "x" + Height);
            line("Radio", () => HardwareSpecific.Model + (PowerOn && !IsSetupFormNull ? ", " + SetupForm.GetFirmwareCodeVersionString() : ", off"));
            line("Layout", () => (_kainosLayout ? "Kainos" : "Classic") + ", UI scale " + Math.Round(KainosUI.Scale * 100) + "%");
            line("Column tabs", () => KainosColumnTabs);
            line("Audio (VAC 1)", () => IsSetupFormNull ? "?" : (SetupForm.KainosWizardRead().AudioOn ? "on, " + SetupForm.KainosWizardRead().AudioHost + " / " + SetupForm.KainosWizardRead().AudioOut : "off"));
            line("Mode / frequency", () => RX1DSPMode + " " + VFOAFreq.ToString("0.000000") + " MHz");
            line("3D / light mode", () => (Display.Kainos3D ? "3D on" : "3D off") + ", light mode " + (KainosLightMode ? "on" : "off"));
            line("Setup wizard", () => IsSetupFormNull ? "?" : SetupForm.KainosWizardState);
            try
            {
                string errors = Path.Combine(AppDataPath, "KainosErrors.txt");
                if (File.Exists(errors))
                {
                    string[] all = File.ReadAllLines(errors);
                    sb.Append("\nKainosErrors.txt (last lines):\n").Append(string.Join("\n", all.Skip(Math.Max(0, all.Length - 25)))).Append('\n');
                }
                else sb.Append("\nKainosErrors.txt: none\n");
            }
            catch { }
            return sb.ToString();
        }
    }
}
