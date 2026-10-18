/*  setupKainosModules.cs

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
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Thetis
{
    // Setup > Modules: the downloadable modules (KainosModules.cs). The published ones are listed from modules.json
    // in the Kainos repository; a module is downloaded only when it's installed here, and runs while it's on.
    public partial class Setup
    {
        // the station's grid square (Setup > DSP > FreeDV (RADE)), for modules
        internal string KainosGrid { get { return _rade != null ? _rade.Grid.Text.Trim().ToUpperInvariant() : ""; } }

        private TextBoxTS txtKainosModules;
        private ListView lvKainosModules;
        private LabelTS lblKainosModuleInfo, lblKainosModuleStatus;
        private ButtonTS btnKainosModuleInstall, btnKainosModuleToggle, btnKainosModuleRemove;
        private bool _kainosModulesListed, _kainosModulesApplied;

        private void addKainosModulesTab()
        {
            TabPage tp = new TabPage("Modules") { Name = "tpKainosModules", BackColor = SystemColors.Control, Padding = new Padding(3) };

            LabelTS intro = new LabelTS
            {
                Name = "lblKainosModulesIntro", Location = new Point(10, 10), Size = new Size(700, 34),
                Text = "Modules add features to Kainos that not everyone needs. A module is downloaded from GitHub only when you install it, " +
                       "and runs while it's on. Turn one off or remove it here at any time."
            };
            tp.Controls.Add(intro);

            lvKainosModules = new ListView
            {
                Name = "lvKainosModules", Location = new Point(10, 50), Size = new Size(700, 190),
                View = View.Details, FullRowSelect = true, MultiSelect = false, HideSelection = false, HeaderStyle = ColumnHeaderStyle.Nonclickable,
            };
            lvKainosModules.Columns.Add("Module", 250);
            lvKainosModules.Columns.Add("Installed", 80);
            lvKainosModules.Columns.Add("Available", 80);
            lvKainosModules.Columns.Add("State", 270);
            lvKainosModules.SelectedIndexChanged += (s, e) => kainosModulesSelected();
            tp.Controls.Add(lvKainosModules);

            lblKainosModuleInfo = new LabelTS { Name = "lblKainosModuleInfo", Location = new Point(10, 248), Size = new Size(700, 64), Text = "" };
            tp.Controls.Add(lblKainosModuleInfo);

            Func<string, string, int, int, ButtonTS> button = (name, text, x, w) => new ButtonTS { Name = name, Text = text, Location = new Point(x, 318), Size = new Size(w, 26) };
            btnKainosModuleInstall = button("btnKainosModuleInstall", "Install", 10, 110);
            btnKainosModuleInstall.Click += async (s, e) =>
            {
                KainosModuleInfo m = kainosSelectedPublished();
                if (m == null) return;
                string size = m.Size > 0 ? " (" + (m.Size / 1048576.0).ToString("0.0") + " MB)" : "";
                if (MessageBox.Show(this, "Download " + m.Name + " " + m.Version + size + " from GitHub and install it?\r\n\r\n" + m.Description,
                        "Install module", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
                btnKainosModuleInstall.Enabled = false;
                string why = await console.KainosModules.InstallPublished(m, new Progress<string>(t => lblKainosModuleStatus.Text = t));
                if (why != null) { lblKainosModuleStatus.Text = why; MessageBox.Show(this, why, "Install module", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
                kainosModulesFill();
            };
            btnKainosModuleToggle = button("btnKainosModuleToggle", "Turn on", 126, 110);
            btnKainosModuleToggle.Click += (s, e) =>
            {
                KainosModuleManager.Installed i = console.KainosModules.Get(kainosSelectedId());
                if (i == null) return;
                console.KainosModules.SetEnabled(i.Info.Id, !i.Enabled);
            };
            btnKainosModuleRemove = button("btnKainosModuleRemove", "Remove", 242, 110);
            btnKainosModuleRemove.Click += (s, e) =>
            {
                KainosModuleManager.Installed i = console.KainosModules.Get(kainosSelectedId());
                if (i == null) return;
                if (MessageBox.Show(this, "Remove " + i.Info.Name + "? Its settings are kept in case you install it again.", "Remove module",
                        MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
                console.KainosModules.Remove(i.Info.Id);
            };
            ButtonTS fromFile = button("btnKainosModuleFromFile", "Install from file...", 400, 150);
            fromFile.Click += (s, e) =>
            {
                using (OpenFileDialog d = new OpenFileDialog { Filter = "Kainos module (*.zip)|*.zip", Title = "Install a Kainos module" })
                {
                    if (d.ShowDialog(this) != DialogResult.OK) return;
                    if (MessageBox.Show(this, "A module runs inside Kainos with the same access to your PC and radio as Kainos itself.\r\n\r\n" +
                            "Only install a module from a file if you trust where it came from. Install it?", "Install module",
                            MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK) return;
                    string why = console.KainosModules.InstallFromFile(d.FileName, new Progress<string>(t => lblKainosModuleStatus.Text = t));
                    if (why != null) { lblKainosModuleStatus.Text = why; MessageBox.Show(this, why, "Install module", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
                    kainosModulesFill();
                }
            };
            ButtonTS refresh = button("btnKainosModuleRefresh", "Refresh list", 560, 150);
            refresh.Click += async (s, e) => { lblKainosModuleStatus.Text = "Reading the list of modules..."; await console.KainosModules.RefreshPublished(); };
            tp.Controls.AddRange(new Control[] { btnKainosModuleInstall, btnKainosModuleToggle, btnKainosModuleRemove, fromFile, refresh });

            lblKainosModuleStatus = new LabelTS { Name = "lblKainosModuleStatus", Location = new Point(10, 354), Size = new Size(700, 34), Text = "", ForeColor = SystemColors.GrayText };
            tp.Controls.Add(lblKainosModuleStatus);

            LinkLabel folder = new LinkLabel { Name = "lnkKainosModuleFolder", Text = "Open the modules folder", Location = new Point(10, 394), AutoSize = true };
            folder.LinkClicked += (s, e) =>
            {
                try
                {
                    string d = System.IO.Path.Combine(console.AppDataPath, "Modules");
                    System.IO.Directory.CreateDirectory(d);
                    Process.Start("explorer.exe", "\"" + d + "\"");
                }
                catch { }
            };
            tp.Controls.Add(folder);

            // the saved state (installed versions, which are on, their settings)
            txtKainosModules = new TextBoxTS { Name = "txtKainosModules", Visible = false, Text = "" };
            tp.Controls.Add(txtKainosModules);

            // the list is read from GitHub the first time the page is shown
            tp.Enter += async (s, e) =>
            {
                kainosModulesFill();
                if (_kainosModulesListed) return;
                _kainosModulesListed = true;
                lblKainosModuleStatus.Text = "Reading the list of modules...";
                await console.KainosModules.RefreshPublished();
            };
            tcSetup.TabPages.Add(tp);
        }

        // at start-up (applyKainosUI): the saved state to the console, and the console's changes back to the box
        private void applyKainosModules()
        {
            if (_kainosModulesApplied || txtKainosModules == null) return;
            _kainosModulesApplied = true;
            console.KainosModulesLoad(txtKainosModules.Text);
            console.KainosSettingsChanged += (s, e) => txtKainosModules.Text = console.KainosModuleSettings;
            console.KainosModules.Changed += (s, e) =>
            {
                try { if (!IsDisposed && IsHandleCreated) BeginInvoke(new Action(kainosModulesFill)); } catch { }
            };
        }

        // ---- the list ----

        private string kainosSelectedId() { return lvKainosModules.SelectedItems.Count > 0 ? (string)lvKainosModules.SelectedItems[0].Tag : null; }

        private KainosModuleInfo kainosSelectedPublished()
        {
            string id = kainosSelectedId();
            return console.KainosModules.Published.FirstOrDefault(m => m.Id == id);
        }

        private void kainosModulesFill()
        {
            if (lvKainosModules == null) return;
            KainosModuleManager mgr = console.KainosModules;
            string sel = kainosSelectedId();
            List<string> ids = mgr.Published.Select(m => m.Id).Concat(mgr.All.Select(i => i.Info.Id)).Distinct().ToList();
            lvKainosModules.BeginUpdate();
            lvKainosModules.Items.Clear();
            foreach (string id in ids)
            {
                KainosModuleInfo pub = mgr.Published.FirstOrDefault(m => m.Id == id);
                KainosModuleManager.Installed ins = mgr.Get(id);
                string name = ins != null ? ins.Info.Name : pub.Name;
                ListViewItem it = new ListViewItem(new[] { name, ins != null ? ins.Info.Version : "", pub != null ? pub.Version : "", kainosModuleState(pub, ins) }) { Tag = id };
                lvKainosModules.Items.Add(it);
                if (id == sel) it.Selected = true;
            }
            lvKainosModules.EndUpdate();
            if (ids.Count == 0) lblKainosModuleInfo.Text = mgr.PublishedProblem ?? (_kainosModulesListed ? "No modules have been published yet." : "");
            if (mgr.PublishedProblem != null) lblKainosModuleStatus.Text = mgr.PublishedProblem;
            else if (lblKainosModuleStatus.Text.StartsWith("Reading")) lblKainosModuleStatus.Text = "";
            kainosModulesSelected();
        }

        private static string kainosModuleState(KainosModuleInfo pub, KainosModuleManager.Installed ins)
        {
            if (ins == null) return pub.Incompatible != null ? "Can't install: " + pub.Incompatible : "Not installed";
            if (ins.PendingVersion != null) return "Restart Kainos to use " + ins.PendingVersion;
            string s = !ins.Enabled ? "Off" : ins.Instance != null ? "On" : ins.Problem != null ? "Not running: " + ins.Problem : "On (starts with the console)";
            if (pub != null && Common.CompareVersions(ins.Info.Version, pub.Version) < 0) s += "; update available";
            return s;
        }

        private void kainosModulesSelected()
        {
            KainosModuleManager mgr = console.KainosModules;
            string id = kainosSelectedId();
            KainosModuleInfo pub = mgr.Published.FirstOrDefault(m => m.Id == id);
            KainosModuleManager.Installed ins = mgr.Get(id);
            KainosModuleInfo info = ins != null ? ins.Info : pub;
            if (info != null)
                lblKainosModuleInfo.Text = info.Description + (info.Author.Length > 0 ? "\r\nBy " + info.Author : "") +
                                           (info.License.Length > 0 ? " (" + info.License + ")" : "") + (info.Homepage.Length > 0 ? "\r\n" + info.Homepage : "");
            bool canUpdate = pub != null && pub.Incompatible == null && (ins == null || (ins.PendingVersion == null && Common.CompareVersions(ins.Info.Version, pub.Version) < 0));
            btnKainosModuleInstall.Enabled = canUpdate;
            btnKainosModuleInstall.Text = ins != null ? "Update" : "Install";
            btnKainosModuleToggle.Enabled = ins != null && ins.PendingVersion == null;
            btnKainosModuleToggle.Text = ins != null && ins.Enabled ? "Turn off" : "Turn on";
            btnKainosModuleRemove.Enabled = ins != null;
        }
    }
}
