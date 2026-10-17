/*  KainosModules.cs

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
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Kainos.Modules;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Thetis
{
    // A module as the list of published modules (modules.json in the Kainos repository) or its own module.json
    // describes it
    internal class KainosModuleInfo
    {
        public string Id = "", Name = "", Description = "", Version = "", Entry = "", Author = "", License = "", Homepage = "";
        public int Api;
        public string MinKainos = "";
        public string Url = "", Sha256 = "";
        public long Size;

        public static KainosModuleInfo From(JObject o)
        {
            Func<string, string> s = k => ((string)o[k] ?? "").Trim();
            KainosModuleInfo m = new KainosModuleInfo
            {
                Id = s("id").ToLowerInvariant(), Name = s("name"), Description = s("description"), Version = s("version"),
                Entry = s("entry"), Author = s("author"), License = s("license"), Homepage = s("homepage"),
                MinKainos = s("minKainos"), Url = s("url"), Sha256 = s("sha256").ToLowerInvariant(),
                Api = (int?)o["api"] ?? 0, Size = (long?)o["size"] ?? 0,
            };
            if (m.Name.Length == 0) m.Name = m.Id;
            return m;
        }

        // an id is also a folder name: letters, digits, '-' and '_'
        public bool IdOk { get { return Id.Length > 0 && Id.Length <= 40 && Id.All(c => (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' || c == '_'); } }

        // why this Kainos can't run it, or null
        public string Incompatible
        {
            get
            {
                if (Api != KainosModuleApi.Version) return "made for module interface " + Api + "; this Kainos has " + KainosModuleApi.Version;
                if (MinKainos.Length > 0 && Common.CompareVersions(KainosVersion.Number, MinKainos) < 0) return "needs Kainos " + MinKainos + " or later";
                return null;
            }
        }
    }

    // Downloadable modules: the list of published ones, installing (download, check, unpack), enabling and loading.
    //
    // On disk (under the settings folder, %APPDATA%\OpenHPSDR\Kainos-x64):
    //   Modules\<id>\<version>\     the unpacked module: module.json, its DLL and whatever it brings
    //   ModuleData\<id>\            the module's own writable folder (IKainosHost.DataFolder)
    // Each version has its own folder because a loaded module's DLL can't be replaced while Kainos runs: an update
    // is unpacked beside it and used from the next start; old folders are removed at start-up.
    //
    // The state (which modules are installed at which version, which are on, and their settings) is JSON in a hidden
    // Setup box (txtKainosModules), saved with the other options.
    internal class KainosModuleManager
    {
        public const string REGISTRY_URL = "https://raw.githubusercontent.com/PNWHam-K7JUS/Kainos/refs/heads/main/modules.json";

        // where a module may be downloaded from (GitHub releases and raw files)
        private static readonly string[] AllowedHosts = { "github.com", "objects.githubusercontent.com", "raw.githubusercontent.com", "release-assets.githubusercontent.com" };

        internal class Installed
        {
            public KainosModuleInfo Info;
            public string Folder;
            public bool Enabled;
            public IKainosModule Instance;          // while running
            public KainosModuleHost Host;
            public string Problem;                  // why it isn't running (failed to load, incompatible...)
            public string PendingVersion;           // unpacked, used from the next start
        }

        private readonly Console _console;
        private readonly string _root, _dataRoot;
        private readonly Dictionary<string, Installed> _installed = new Dictionary<string, Installed>();
        private readonly Dictionary<string, Dictionary<string, string>> _settings = new Dictionary<string, Dictionary<string, string>>();
        private readonly HashSet<string> _loadedOnce = new HashSet<string>();     // DLLs loaded this session (can't be unloaded)
        private bool _started;

        public event EventHandler Changed;          // the list or a module's state changed (Setup's page redraws)
        public event EventHandler StateChanged;     // something to save

        public List<KainosModuleInfo> Published = new List<KainosModuleInfo>();
        public string PublishedProblem;

        public KainosModuleManager(Console console, string appDataPath)
        {
            _console = console;
            _root = Path.Combine(appDataPath, "Modules");
            _dataRoot = Path.Combine(appDataPath, "ModuleData");
        }

        public IEnumerable<Installed> All { get { return _installed.Values.OrderBy(i => i.Info.Name); } }
        public Installed Get(string id) { Installed i; return _installed.TryGetValue(id ?? "", out i) ? i : null; }
        public string DataFolderFor(string id) { string d = Path.Combine(_dataRoot, id); Directory.CreateDirectory(d); return d; }

        // ---- state ----

        public string Serialize()
        {
            JObject o = new JObject();
            JObject mods = new JObject();
            foreach (Installed i in _installed.Values)
                mods[i.Info.Id] = new JObject { ["version"] = i.PendingVersion ?? i.Info.Version, ["enabled"] = i.Enabled };
            o["modules"] = mods;
            JObject set = new JObject();
            foreach (KeyValuePair<string, Dictionary<string, string>> kv in _settings) set[kv.Key] = JObject.FromObject(kv.Value);
            o["settings"] = set;
            return o.ToString(Formatting.None);
        }

        // at start-up, from the saved state: what's installed (and still on disk), what's on, the settings
        public void Load(string json)
        {
            _installed.Clear();
            _settings.Clear();
            JObject o = null;
            try { if (!string.IsNullOrWhiteSpace(json)) o = JObject.Parse(json); } catch { }
            if (o != null && o["settings"] is JObject set)
                foreach (KeyValuePair<string, JToken> kv in set)
                    if (kv.Value is JObject so) _settings[kv.Key] = so.Properties().ToDictionary(p => p.Name, p => (string)p.Value ?? "");
            if (o != null && o["modules"] is JObject mods)
            {
                foreach (KeyValuePair<string, JToken> kv in mods)
                {
                    string id = kv.Key, ver = (string)kv.Value["version"] ?? "";
                    string folder = Path.Combine(_root, id, ver);
                    KainosModuleInfo info = readModuleJson(folder);
                    if (info == null || info.Id != id) continue;          // removed by hand, or damaged
                    _installed[id] = new Installed { Info = info, Folder = folder, Enabled = (bool?)kv.Value["enabled"] ?? false };
                }
            }
            cleanUp();
        }

        // version folders no longer used, and modules no longer installed
        private void cleanUp()
        {
            try
            {
                if (!Directory.Exists(_root)) return;
                foreach (string modDir in Directory.GetDirectories(_root))
                {
                    Installed i = Get(Path.GetFileName(modDir));
                    foreach (string verDir in Directory.GetDirectories(modDir))
                        if (i == null || !string.Equals(Path.GetFullPath(verDir).TrimEnd('\\'), Path.GetFullPath(i.Folder).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                            tryDelete(verDir);
                    if (i == null && Directory.GetFileSystemEntries(modDir).Length == 0) tryDelete(modDir);
                }
            }
            catch { }
        }

        private static void tryDelete(string dir) { try { Directory.Delete(dir, true); } catch { } }

        private static KainosModuleInfo readModuleJson(string folder)
        {
            try
            {
                string f = Path.Combine(folder, "module.json");
                if (!File.Exists(f)) return null;
                return KainosModuleInfo.From(JObject.Parse(File.ReadAllText(f)));
            }
            catch { return null; }
        }

        public string GetSetting(string id, string key, string fallback)
        {
            lock (_settings)
            {
                Dictionary<string, string> d; string v;
                return _settings.TryGetValue(id, out d) && d.TryGetValue(key, out v) ? v : fallback;
            }
        }

        public void SetSetting(string id, string key, string value)
        {
            lock (_settings)
            {
                Dictionary<string, string> d;
                if (!_settings.TryGetValue(id, out d)) _settings[id] = d = new Dictionary<string, string>();
                string old;
                if (d.TryGetValue(key, out old) && old == value) return;
                d[key] = value ?? "";
            }
            raiseState();
        }

        private void raiseState() { StateChanged?.Invoke(this, EventArgs.Empty); }
        private void raiseChanged() { Changed?.Invoke(this, EventArgs.Empty); }

        // ---- the published list ----

        public async Task RefreshPublished()
        {
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                using (HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) })
                {
                    http.DefaultRequestHeaders.UserAgent.ParseAdd("Kainos/" + KainosVersion.Number);
                    string json = await http.GetStringAsync(REGISTRY_URL + "?t=" + DateTime.UtcNow.Ticks);
                    JObject o = JObject.Parse(json);
                    List<KainosModuleInfo> list = new List<KainosModuleInfo>();
                    if (o["modules"] is JArray a)
                        foreach (JToken t in a) if (t is JObject mo) { KainosModuleInfo m = KainosModuleInfo.From(mo); if (m.IdOk) list.Add(m); }
                    Published = list;
                    PublishedProblem = null;
                }
            }
            catch (Exception ex) { PublishedProblem = "Couldn't read the list of modules: " + ex.Message; }
            raiseChanged();
        }

        // ---- installing ----

        public async Task<string> InstallPublished(KainosModuleInfo m, IProgress<string> progress)
        {
            if (!m.IdOk) return "The module's name isn't valid.";
            string why = m.Incompatible;
            if (why != null) return "This module " + why + ".";
            Uri u;
            if (!Uri.TryCreate(m.Url, UriKind.Absolute, out u) || u.Scheme != "https" || !AllowedHosts.Contains(u.Host.ToLowerInvariant()))
                return "The module's download address isn't one Kainos accepts (GitHub only).";
            if (m.Sha256.Length != 64) return "The list has no checksum for this module.";

            string tmp = Path.Combine(Path.GetTempPath(), "kainos-module-" + Guid.NewGuid().ToString("N") + ".zip");
            try
            {
                progress?.Report("Downloading " + m.Name + "...");
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                using (HttpClient http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) })
                {
                    http.DefaultRequestHeaders.UserAgent.ParseAdd("Kainos/" + KainosVersion.Number);
                    using (HttpResponseMessage r = await http.GetAsync(u, HttpCompletionOption.ResponseHeadersRead))
                    {
                        r.EnsureSuccessStatusCode();
                        using (Stream s = await r.Content.ReadAsStreamAsync())
                        using (FileStream f = File.Create(tmp))
                            await s.CopyToAsync(f);
                    }
                }
                progress?.Report("Checking...");
                string sum = sha256Of(tmp);
                if (sum != m.Sha256) return "The download doesn't match its checksum, so it wasn't installed. Try again later.";
                return installZip(tmp, m.Id, progress);
            }
            catch (Exception ex) { return "The download failed: " + ex.Message; }
            finally { try { File.Delete(tmp); } catch { } }
        }

        // a module zip chosen by hand (for testing a module before it's published)
        public string InstallFromFile(string zip, IProgress<string> progress)
        {
            try { return installZip(zip, null, progress); }
            catch (Exception ex) { return "The module couldn't be installed: " + ex.Message; }
        }

        public static string sha256Of(string file)
        {
            using (SHA256 h = SHA256.Create())
            using (FileStream f = File.OpenRead(file))
                return BitConverter.ToString(h.ComputeHash(f)).Replace("-", "").ToLowerInvariant();
        }

        // unpacks into Modules\<id>\<version>; returns null when done, or why not
        private string installZip(string zip, string expectId, IProgress<string> progress)
        {
            progress?.Report("Unpacking...");
            KainosModuleInfo info;
            using (ZipArchive z = ZipFile.OpenRead(zip))
            {
                ZipArchiveEntry mj = z.GetEntry("module.json");
                if (mj == null) return "That isn't a Kainos module (it has no module.json).";
                using (StreamReader r = new StreamReader(mj.Open())) info = KainosModuleInfo.From(JObject.Parse(r.ReadToEnd()));
                if (!info.IdOk || info.Version.Length == 0 || info.Version.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return "The module's module.json is missing its id or version.";
                if (expectId != null && info.Id != expectId) return "The download is a different module (" + info.Id + ").";
                string why = info.Incompatible;
                if (why != null) return "This module " + why + ".";
                if (info.Entry.Length == 0 || z.GetEntry(info.Entry) == null) return "The module's DLL (" + info.Entry + ") isn't in it.";

                string dest = Path.Combine(_root, info.Id, info.Version);
                string full = Path.GetFullPath(dest).TrimEnd('\\') + "\\";
                Installed cur = Get(info.Id);
                bool sameFolderLoaded = cur != null && _loadedOnce.Contains(info.Id) &&
                    string.Equals(Path.GetFullPath(cur.Folder).TrimEnd('\\') + "\\", full, StringComparison.OrdinalIgnoreCase);
                if (sameFolderLoaded) return "This version is already installed and running.";
                if (Directory.Exists(dest)) tryDelete(dest);
                Directory.CreateDirectory(dest);
                foreach (ZipArchiveEntry e in z.Entries)
                {
                    string target = Path.GetFullPath(Path.Combine(dest, e.FullName));
                    if (!target.StartsWith(full, StringComparison.OrdinalIgnoreCase)) return "The module has a file outside its folder, so it wasn't installed.";
                    if (e.FullName.EndsWith("/") || e.FullName.EndsWith("\\")) { Directory.CreateDirectory(target); continue; }
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    e.ExtractToFile(target, true);
                }
            }

            Installed was = Get(info.Id);
            if (was != null && _loadedOnce.Contains(info.Id))
            {
                // running (or ran) this session: its DLL stays loaded, so the new version starts next time
                was.PendingVersion = info.Version;
                raiseState();
                raiseChanged();
                progress?.Report(info.Name + " " + info.Version + " is installed. Restart Kainos to use it.");
                return null;
            }
            Installed now = new Installed { Info = info, Folder = Path.Combine(_root, info.Id, info.Version), Enabled = was != null ? was.Enabled : true };
            _installed[info.Id] = now;
            if (was != null && !string.Equals(was.Folder, now.Folder, StringComparison.OrdinalIgnoreCase)) tryDelete(was.Folder);
            raiseState();
            if (now.Enabled && _started) startOne(now);
            raiseChanged();
            progress?.Report(info.Name + " " + info.Version + " is installed.");
            return null;
        }

        // removes it now if it never ran this session, else at the next start
        public void Remove(string id)
        {
            Installed i = Get(id);
            if (i == null) return;
            stopOne(i);
            _installed.Remove(id);
            if (!_loadedOnce.Contains(id)) tryDelete(Path.Combine(_root, id));
            raiseState();
            raiseChanged();
        }

        // ---- running ----

        // once the console is up
        public void StartAll()
        {
            _started = true;
            foreach (Installed i in _installed.Values.ToList()) if (i.Enabled) startOne(i);
            raiseChanged();
        }

        public void StopAll()
        {
            foreach (Installed i in _installed.Values) stopOne(i);
            _started = false;
        }

        public void SetEnabled(string id, bool on)
        {
            Installed i = Get(id);
            if (i == null || i.Enabled == on) return;
            i.Enabled = on;
            if (on) { if (_started) startOne(i); }
            else stopOne(i);
            raiseState();
            raiseChanged();
        }

        private void startOne(Installed i)
        {
            if (i.Instance != null) return;
            i.Problem = i.Info.Incompatible;
            if (i.Problem != null) return;
            try
            {
                Assembly asm = Assembly.LoadFrom(Path.Combine(i.Folder, i.Info.Entry));
                _loadedOnce.Add(i.Info.Id);
                Type t = asm.GetExportedTypes().FirstOrDefault(x => typeof(IKainosModule).IsAssignableFrom(x) && !x.IsAbstract && !x.IsInterface);
                if (t == null) { i.Problem = "the module's DLL has no IKainosModule"; return; }
                i.Host = new KainosModuleHost(_console, this, i.Info, i.Folder);
                i.Instance = (IKainosModule)Activator.CreateInstance(t);
                i.Instance.Start(i.Host);
                i.Host.Log("started " + i.Info.Name + " " + i.Info.Version);
            }
            catch (Exception ex)
            {
                i.Problem = "it failed to start: " + (ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException.Message : ex.Message);
                if (i.Host != null) i.Host.Log("start failed: " + ex);
                stopOne(i);
                i.Problem = i.Problem ?? "it failed to start";
            }
        }

        private void stopOne(Installed i)
        {
            if (i.Instance != null)
            {
                try { i.Instance.Stop(); } catch (Exception ex) { i.Host?.Log("stop failed: " + ex.Message); }
                try { i.Instance.Dispose(); } catch { }
                i.Instance = null;
            }
            if (i.Host != null) { i.Host.Shutdown(); i.Host = null; }
        }
    }
}
