/*  KainosVst.cs

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
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Thetis
{
    // A VST3 plugin as the Plugin stage of Kainos Audio's transmit chain. KainosVst.dll (Source/KainosVst, on
    // Steinberg's VST3 SDK) loads and runs the plugin; WDSP calls it at the chain's plugin slot (AS_PLUGIN) on the
    // DSP thread. RadioDSPTX attaches it only in voice modes, while it's on and Kainos Audio isn't bypassed.
    internal static class KainosVst
    {
        private const string Dll = "KainosVst.dll";

        [DllImport(Dll, CharSet = CharSet.Unicode)] private static extern int kv_scan(string path, StringBuilder buf, int cap);
        [DllImport(Dll, CharSet = CharSet.Unicode)] private static extern IntPtr kv_load(string path, string classId, double rate, int maxBlock, StringBuilder err, int cap);
        [DllImport(Dll)] private static extern IntPtr kv_process_proc();
        [DllImport(Dll)] private static extern int kv_latency(IntPtr h);
        [DllImport(Dll)] private static extern int kv_editor_open(IntPtr h, IntPtr hwnd, float scale, out int w, out int hh);
        [DllImport(Dll)] private static extern void kv_editor_size(IntPtr h, out int w, out int hh);
        [DllImport(Dll)] private static extern void kv_editor_close(IntPtr h);
        [DllImport(Dll)] private static extern void kv_unload(IntPtr h);

        internal class Plugin
        {
            public string Path, ClassId, Name;
            public override string ToString() { return Name; }
        }

        private static Console _console;
        private static IntPtr _h = IntPtr.Zero;
        private static Plugin _loaded;
        private static bool _on = true;
        private static List<Plugin> _found;
        private static KainosVstEditor _editor;

        public static event EventHandler Changed;

        public static bool Available
        {
            get { try { return File.Exists(System.IO.Path.Combine(Application.StartupPath, Dll)); } catch { return false; } }
        }

        public static Plugin Loaded { get { return _loaded; } }
        public static bool On { get { return _on; } }
        public static int LatencySamples { get { try { return _h != IntPtr.Zero ? kv_latency(_h) : 0; } catch { return 0; } } }

        // the plugins installed in the standard VST3 folders (scanned once; Rescan to look again)
        public static List<Plugin> Found(bool rescan = false)
        {
            if (_found != null && !rescan) return _found;
            List<Plugin> list = new List<Plugin>();
            foreach (string root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Programs\Common" })
            {
                string dir = System.IO.Path.Combine(root, "VST3");
                if (!Directory.Exists(dir)) continue;
                foreach (string p in vst3Files(dir)) list.AddRange(scan(p));
            }
            _found = list.OrderBy(p => p.Name).ToList();
            return _found;
        }

        // a .vst3 is either a file or a bundle folder: don't look inside bundles
        private static IEnumerable<string> vst3Files(string dir)
        {
            List<string> r = new List<string>();
            try
            {
                foreach (string d in Directory.GetDirectories(dir))
                {
                    if (d.EndsWith(".vst3", StringComparison.OrdinalIgnoreCase)) r.Add(d);
                    else r.AddRange(vst3Files(d));
                }
                foreach (string f in Directory.GetFiles(dir, "*.vst3")) r.Add(f);
            }
            catch { }
            return r;
        }

        public static List<Plugin> scan(string path)
        {
            List<Plugin> r = new List<Plugin>();
            try
            {
                StringBuilder sb = new StringBuilder(16384);
                if (kv_scan(path, sb, sb.Capacity) <= 0) return r;
                foreach (string line in sb.ToString().Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string[] p = line.Split('\t');
                    if (p.Length == 3) r.Add(new Plugin { Path = p[0], ClassId = p[1], Name = p[2] });
                }
            }
            catch { }
            return r;
        }

        // loads a plugin at the transmit chain's rate and attaches it (replacing any other); null: no plugin
        public static string Load(Console console, Plugin p)
        {
            _console = console;
            Unload();
            if (p == null) return null;
            int rate = 48000, size = 1024;
            try { WDSP.GetTXAStripFormat(WDSP.id(1, 0), out rate, out size); } catch { }
            StringBuilder err = new StringBuilder(512);
            IntPtr h;
            try { h = kv_load(p.Path, p.ClassId ?? "", rate, Math.Max(4096, size), err, err.Capacity); }
            catch (Exception ex) { return ex.Message; }
            if (h == IntPtr.Zero) return err.Length > 0 ? err.ToString() : "the plugin wouldn't load";
            _h = h;
            _loaded = p;
            attach();
            Changed?.Invoke(null, EventArgs.Empty);
            return null;
        }

        public static void Unload()
        {
            if (_editor != null && !_editor.IsDisposed) _editor.Close();
            _editor = null;
            if (_h == IntPtr.Zero) { _loaded = null; return; }
            IntPtr h = _h;
            _h = IntPtr.Zero;
            _loaded = null;
            attach();                       // out of the chain (under the DSP lock) before it goes
            try { kv_unload(h); } catch { }
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static void SetOn(bool on)
        {
            _on = on;
            attach();
            Changed?.Invoke(null, EventArgs.Empty);
        }

        // hands the plugin (or none) to the transmit chain; RadioDSPTX decides whether it runs (voice modes, bypass)
        internal static void attach()
        {
            if (_console == null || _console.radio == null) return;
            if (!_hooked && _console.AetherStripTX != null)
            {
                // Kainos Audio's BYPASS takes the plugin out too
                _hooked = true;
                _console.AetherStripTX.Changed += (s, e) => attach();
            }
            IntPtr proc = IntPtr.Zero;
            if (_h != IntPtr.Zero) try { proc = kv_process_proc(); } catch { }
            bool bypass = _console.AetherStripTX != null && _console.AetherStripTX.Bypass;
            _console.radio.GetDSPTX(0).SetTXStripPlugin(proc, _h, _on && !bypass && _h != IntPtr.Zero);
        }
        private static bool _hooked;

        public static void ShowEditor(IWin32Window owner)
        {
            if (_h == IntPtr.Zero) return;
            if (_editor == null || _editor.IsDisposed)
            {
                _editor = new KainosVstEditor(_loaded.Name);
                int w, hh;
                float scale = 1f;
                try { using (Graphics g = _editor.CreateGraphics()) scale = g.DpiX / 96f; } catch { }
                if (kv_editor_open(_h, _editor.Host.Handle, scale, out w, out hh) == 0)
                {
                    _editor.Dispose();
                    _editor = null;
                    MessageBox.Show(owner, _loaded.Name + " has no editor window of its own.", "VST3 plugin", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                _editor.FitTo(w, hh);
                IntPtr inst = _h;
                _editor.SizeWanted = () => { int a, b; kv_editor_size(inst, out a, out b); return new Size(a, b); };
                _editor.FormClosing += (s, e) => { try { kv_editor_close(inst); } catch { } };
                // over the window it was opened from
                Form o = owner as Form;
                if (o != null)
                {
                    _editor.StartPosition = FormStartPosition.Manual;
                    _editor.Location = new Point(o.Left + (o.Width - _editor.Width) / 2, o.Top + (o.Height - _editor.Height) / 2);
                }
                _editor.Show(owner);
            }
            _editor.Activate();
        }

        // ---- saved with the options (a hidden Setup box): "path|classId|on" ----
        public static string Settings
        {
            get { return _loaded == null ? "" : _loaded.Path + "|" + _loaded.ClassId + "|" + (_on ? 1 : 0); }
        }

        public static void LoadSettings(Console console, string s)
        {
            _console = console;
            string[] p = (s ?? "").Split('|');
            if (p.Length < 3 || p[0].Length == 0) return;
            _on = p[2] == "1";
            if (!File.Exists(p[0]) && !Directory.Exists(p[0])) return;
            Plugin found = scan(p[0]).FirstOrDefault(x => x.ClassId == p[1]);
            if (found != null) Load(console, found);
        }
    }

    // the plugin's own editor, in a window of its own (it can ask for a new size)
    internal class KainosVstEditor : Form
    {
        public readonly Panel Host = new Panel { Dock = DockStyle.Fill, BackColor = Color.Black };
        public Func<Size> SizeWanted;
        private readonly Timer _follow = new Timer { Interval = 250 };

        public KainosVstEditor(string name)
        {
            Text = name + " (VST3)";
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Controls.Add(Host);
            _follow.Tick += (s, e) =>
            {
                if (SizeWanted == null) return;
                Size want = SizeWanted();
                if (want.Width > 0 && want.Height > 0 && want != Host.ClientSize) FitTo(want.Width, want.Height);
            };
            _follow.Start();
        }

        public void FitTo(int w, int h) { if (w > 0 && h > 0) ClientSize = new Size(w, h); }

        protected override void Dispose(bool disposing) { if (disposing) _follow.Dispose(); base.Dispose(disposing); }
    }
}
