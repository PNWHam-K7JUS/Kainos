/*  consoleKainosModules.cs

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
using System.Windows.Forms;
using Kainos.Modules;

namespace Thetis
{
    // The console's side of the downloadable modules (KainosModules.cs): the manager, the Modes menu items they add,
    // their panes (where the RTTY and CW terminals live), the shared digital audio tap and panadapter markers.
    public partial class Console
    {
        private KainosModuleManager _kainosModules;
        internal KainosModuleManager KainosModules
        {
            get
            {
                if (_kainosModules == null)
                {
                    _kainosModules = new KainosModuleManager(this, AppDataPath);
                    _kainosModules.StateChanged += (s, e) => KainosRaiseSettingsChanged();
                }
                return _kainosModules;
            }
        }

        // saved with the options (a hidden Setup box): KainosModuleManager.Serialize
        public string KainosModuleSettings
        {
            get { return KainosModules.Serialize(); }
        }

        private bool _kainosModulesLoaded, _kainosModulesHooked;

        // from Setup at start-up (applyKainosUI), before the console is shown
        internal void KainosModulesLoad(string state)
        {
            if (_kainosModulesLoaded) return;
            _kainosModulesLoaded = true;
            KainosModules.Load(state);
            if (!_kainosModulesHooked)
            {
                _kainosModulesHooked = true;
                Action start = () => { try { KainosModules.StartAll(); } catch (Exception ex) { kainosModuleError("start", ex); } };
                if (Visible) BeginInvoke(start);
                else Shown += (s, e) => start();
                FormClosing += (s, e) => { try { KainosModules.StopAll(); } catch { } };
            }
        }

        internal static void kainosModuleError(string what, Exception ex)
        {
            try
            {
                File.AppendAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpenHPSDR", "Kainos-x64", "KainosErrors.txt"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " modules " + what + ": " + ex + "\r\n");
            }
            catch { }
        }

        // ---- menu items: the Modes menu in Kainos layout, the menu bar in Classic ----

        private readonly List<ToolStripMenuItem> _kainosModuleClassicItems = new List<ToolStripMenuItem>();

        internal KainosModuleMenuItem KainosAddModuleMenuItem(string text, string tip, Action click)
        {
            kainosAudioMenuCreate();
            ToolStripMenuItem modes = new ToolStripMenuItem(text) { ToolTipText = tip };
            ToolStripMenuItem classic = new ToolStripMenuItem(text) { ToolTipText = tip, ForeColor = cWXToolStripMenuItem.ForeColor };
            EventHandler h = (s, e) => { try { click(); } catch (Exception ex) { kainosModuleError("menu " + text, ex); } };
            modes.Click += h;
            classic.Click += h;
            ToolStripItemCollection dd = _kainosModesMenu.DropDownItems;
            if (!dd.OfType<ToolStripSeparator>().Any(x => (string)x.Tag == "modules")) dd.Add(new ToolStripSeparator { Tag = "modules" });
            dd.Add(modes);
            int at = menuStrip1.Items.IndexOf(cwToolStripMenuItem);
            menuStrip1.Items.Insert(at >= 0 ? at + 1 + _kainosModuleClassicItems.Count : menuStrip1.Items.Count, classic);
            _kainosModuleClassicItems.Add(classic);
            classic.Visible = !_kainosLayout;
            return new KainosModuleMenuItem(this, modes, classic);
        }

        internal void KainosRemoveModuleMenuItem(ToolStripMenuItem modes, ToolStripMenuItem classic)
        {
            if (_kainosModesMenu != null)
            {
                _kainosModesMenu.DropDownItems.Remove(modes);
                ToolStripSeparator sep = _kainosModesMenu.DropDownItems.OfType<ToolStripSeparator>().FirstOrDefault(x => (string)x.Tag == "modules");
                if (sep != null && _kainosModesMenu.DropDownItems.IndexOf(sep) == _kainosModesMenu.DropDownItems.Count - 1) _kainosModesMenu.DropDownItems.Remove(sep);
            }
            menuStrip1.Items.Remove(classic);
            _kainosModuleClassicItems.Remove(classic);
            modes.Dispose();
            classic.Dispose();
        }

        // ---- panes ----

        private readonly List<KainosModulePane> _kainosModulePanes = new List<KainosModulePane>();

        internal KainosModulePane KainosCreateModulePane(KainosModuleHost host, string title, Control content)
        {
            KainosModulePane p = new KainosModulePane(this, host, title, content);
            _kainosModulePanes.Add(p);
            return p;
        }

        internal void KainosModulePaneGone(KainosModulePane p) { _kainosModulePanes.Remove(p); }

        // one docked terminal at a time: opening one closes the others (RTTY, CW and modules' panes)
        internal void KainosTerminalOpening(object opening)
        {
            if (_rttyOpen && opening != (object)_rttyPane) RttyClose();
            if (_cwOpen && opening != (object)_cwPane) CwClose();
            foreach (KainosModulePane p in _kainosModulePanes.ToList()) if (p != opening && p.IsOpen) p.Close();
        }

        internal Form KainosPlaceModulePane(Control pane, Form window, string title, bool open, bool popped, Action close)
        {
            return kainosTermPlace(pane, window, title, open, popped, close);
        }

        private bool kainosModulePaneDocked { get { return _kainosModulePanes.Any(p => p.IsOpen && !p.PoppedNow); } }

        private void kainosPlaceModulePanes()
        {
            foreach (KainosModulePane p in _kainosModulePanes) kainosTermPlaceDock(p.Panel, p.IsOpen, p.PoppedNow);
        }

        private void kainosModulePanesPlace()
        {
            foreach (KainosModulePane p in _kainosModulePanes) if (p.IsOpen) p.Place();
        }

        internal bool KainosLayoutOn { get { return _kainosLayout; } }

        // ---- the receive tap (shared with the RTTY and CW terminals) ----

        private KainosModuleHost _kainosTapOwner;
        private int _kainosTapRx = -1;

        internal bool KainosModuleAcquireTap(KainosModuleHost host, int rx, out string problem)
        {
            problem = null;
            if (rx < 0 || rx > 1) { problem = "No such receiver"; return false; }
            if (_rttyOpen) RttyClose();
            if (_cwOpen) CwClose();
            if (_kainosTapOwner != null && _kainosTapOwner != host) _kainosTapOwner.TapTakenAway();
            if (_kainosTapRx >= 0 && _kainosTapRx != rx) try { KDigi.KDigiRxTap(_kainosTapRx, 0); } catch { }
            try { KDigi.KDigiRxTap(rx, 1); }
            catch (Exception ex) { problem = ex.Message; return false; }
            _kainosTapOwner = host;
            _kainosTapRx = rx;
            return true;
        }

        internal void KainosModuleReleaseTap(KainosModuleHost host)
        {
            if (_kainosTapOwner != host) return;
            try { if (_kainosTapRx >= 0) KDigi.KDigiRxTap(_kainosTapRx, 0); } catch { }
            _kainosTapOwner = null;
            _kainosTapRx = -1;
        }

        internal int KainosModuleTapRx(KainosModuleHost host) { return _kainosTapOwner == host ? _kainosTapRx : -1; }

        // the RTTY or CW terminal is taking the tap
        private void kainosModulesLoseTap()
        {
            KainosModuleHost h = _kainosTapOwner;
            if (h == null) return;
            _kainosTapOwner = null;
            _kainosTapRx = -1;
            h.TapTakenAway();
        }

        // ---- panadapter markers ----

        private readonly KainosMarker[][] _kainosModuleMarkers = new KainosMarker[2][];

        internal void KainosSetModuleMarkers(int rx, KainosMarker[] m)
        {
            if (rx < 0 || rx > 1) return;
            _kainosModuleMarkers[rx] = m == null || m.Length == 0 ? null : m.ToArray();
        }

        // rx: 1 or 2, as the display counts them
        internal KainosMarker[] KainosModuleMarkers(int rx)
        {
            if (rx < 1 || rx > 2) return null;
            return _kainosModuleMarkers[rx - 1];
        }

        // ---- radio helpers for the host (UI thread) ----

        internal static readonly Dictionary<string, DSPMode> KainosModeNames = Enum.GetValues(typeof(DSPMode)).Cast<DSPMode>()
            .Where(m => m != DSPMode.FIRST && m != DSPMode.LAST).ToDictionary(m => m.ToString(), m => m, StringComparer.OrdinalIgnoreCase);

        internal string KainosModeOf(int rx) { return (rx == 1 ? _rx2_dsp_mode : _rx1_dsp_mode).ToString(); }

        internal void KainosSetModeOf(int rx, string mode)
        {
            DSPMode m;
            if (!KainosModeNames.TryGetValue(mode ?? "", out m)) return;
            if (rx == 1) RX2DSPMode = m; else RX1DSPMode = m;
        }

        internal int KainosTxReceiver { get { return chkVFOBTX.Checked ? 1 : 0; } }
    }

    // A menu item a module added (both its copies)
    internal class KainosModuleMenuItem : IKainosMenuItem
    {
        private readonly Console _c;
        private readonly ToolStripMenuItem _modes, _classic;
        public KainosModuleMenuItem(Console c, ToolStripMenuItem modes, ToolStripMenuItem classic) { _c = c; _modes = modes; _classic = classic; }

        private void ui(Action a) { if (_c.InvokeRequired) _c.BeginInvoke(a); else a(); }
        public bool Checked { get { return _modes.Checked; } set { ui(() => { _modes.Checked = _classic.Checked = value; }); } }
        public bool Enabled { get { return _modes.Enabled; } set { ui(() => { _modes.Enabled = _classic.Enabled = value; }); } }
        public string Text { get { return _modes.Text; } set { ui(() => { _modes.Text = _classic.Text = value; }); } }
        public void Remove() { ui(() => _c.KainosRemoveModuleMenuItem(_modes, _classic)); }
    }

    // A module's pane: its content in a Kainos panel, docked under the panadapter or in its own window
    internal class KainosModulePane : IKainosPane
    {
        private readonly Console _c;
        private readonly KainosModuleHost _host;
        private readonly string _title;
        internal readonly Panel Panel;
        private Form _window;
        private bool _open, _popped;

        public event EventHandler Closed;

        public KainosModulePane(Console c, KainosModuleHost host, string title, Control content)
        {
            _c = c;
            _host = host;
            _title = title;
            Panel = new Panel { BackColor = KainosUI.Bg, Visible = false, Name = "kainosModulePane_" + host.ModuleId + "_" + Guid.NewGuid().ToString("N").Substring(0, 6) };
            content.Dock = DockStyle.Fill;
            Panel.Controls.Add(content);
            _popped = host.GetSetting("pane.popped." + title, "0") == "1";
        }

        public bool IsOpen { get { return _open; } }
        internal bool PoppedNow { get { return _popped || !_c.KainosLayoutOn; } }

        public bool Popped
        {
            get { return _popped; }
            set
            {
                ui(() =>
                {
                    if (_popped == value) return;
                    _popped = value;
                    _host.SetSetting("pane.popped." + _title, value ? "1" : "0");
                    if (_open) Place();
                });
            }
        }

        private void ui(Action a) { if (_c.InvokeRequired) _c.BeginInvoke(a); else a(); }

        public void Open()
        {
            ui(() =>
            {
                if (_open) { Place(); return; }
                _c.KainosTerminalOpening(this);
                _open = true;
                Place();
            });
        }

        public void Close() { ui(() => closeNow(false)); }

        private void closeNow(bool byUser)
        {
            if (!_open) return;
            _open = false;
            Place();
            if (byUser) try { Closed?.Invoke(this, EventArgs.Empty); } catch (Exception ex) { Console.kainosModuleError("pane closed", ex); }
        }

        internal void Place() { _window = _c.KainosPlaceModulePane(Panel, _window, _title, _open, PoppedNow, () => closeNow(true)); }

        internal void Dispose()
        {
            closeNow(false);
            _c.KainosModulePaneGone(this);
            if (_window != null && !_window.IsDisposed) _window.Dispose();
            if (!Panel.IsDisposed) Panel.Dispose();
        }
    }

    // What one running module sees of Kainos (IKainosHost)
    internal class KainosModuleHost : IKainosHost, IKainosRadio, IKainosAudio
    {
        private readonly Console _c;
        private readonly KainosModuleManager _mgr;
        private readonly KainosModuleInfo _info;
        private readonly string _folder;
        private readonly List<KainosModuleMenuItem> _menus = new List<KainosModuleMenuItem>();
        private readonly List<KainosModulePane> _panes = new List<KainosModulePane>();
        private Action _tapReleased;
        private bool _txOn;
        private volatile bool _gone;
        private readonly object _logLock = new object();

        public KainosModuleHost(Console c, KainosModuleManager mgr, KainosModuleInfo info, string folder)
        {
            _c = c; _mgr = mgr; _info = info; _folder = folder;
        }

        // ---- IKainosHost ----

        public int ApiVersion { get { return KainosModuleApi.Version; } }
        public string KainosVersion { get { return Thetis.KainosVersion.Number; } }
        public string ModuleId { get { return _info.Id; } }
        public string ModuleFolder { get { return _folder; } }
        public string DataFolder { get { return _mgr.DataFolderFor(_info.Id); } }
        public string Callsign { get { return (_c.RadeCallsign ?? "").Trim().ToUpperInvariant(); } }
        public string Grid { get { return _c.IsSetupFormNull ? "" : _c.SetupForm.KainosGrid; } }
        public IKainosRadio Radio { get { return this; } }
        public IKainosAudio Audio { get { return this; } }
        public IKainosTheme Theme { get { return KainosModuleTheme.Instance; } }

        public IKainosMenuItem AddMenuItem(string text, string toolTip, Action click)
        {
            KainosModuleMenuItem m = onUi(() => _c.KainosAddModuleMenuItem(text, toolTip, click));
            lock (_menus) _menus.Add(m);
            return m;
        }

        public IKainosPane CreatePane(string title, Control content)
        {
            KainosModulePane p = onUi(() => _c.KainosCreateModulePane(this, title, content));
            lock (_panes) _panes.Add(p);
            return p;
        }

        public void SetMarkers(int rx, KainosMarker[] markers) { if (!_gone) _c.KainosSetModuleMarkers(rx, markers); }
        public string GetSetting(string key, string fallback) { return _mgr.GetSetting(_info.Id, key, fallback); }
        public void SetSetting(string key, string value) { _mgr.SetSetting(_info.Id, key, value); }

        public void Log(string message)
        {
            try
            {
                lock (_logLock)
                {
                    string f = Path.Combine(DataFolder, "module.log");
                    FileInfo fi = new FileInfo(f);
                    if (fi.Exists && fi.Length > 2_000_000) File.Delete(f);
                    File.AppendAllText(f, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  " + message + "\r\n");
                }
            }
            catch { }
        }

        public void BeginInvoke(Action action)
        {
            if (_gone || _c.IsDisposed) return;
            try { _c.BeginInvoke(new Action(() => { if (_gone) return; try { action(); } catch (Exception ex) { Log("UI action failed: " + ex); } })); } catch { }
        }

        private T onUi<T>(Func<T> f) { return _c.InvokeRequired ? (T)_c.Invoke(f) : f(); }
        private void onUi(Action a) { if (_c.InvokeRequired) _c.Invoke(a); else a(); }

        // ---- IKainosRadio ----

        public bool PowerOn { get { return _c.PowerOn; } }
        public bool Transmitting { get { return _c.MOX; } }
        public int TxReceiver { get { return _c.KainosTxReceiver; } }
        public double VfoHz(int rx) { return Math.Round((rx == 1 ? _c.VFOBFreq : _c.VFOAFreq) * 1e6); }
        public void TuneVfo(int rx, double hz)
        {
            if (_gone || hz <= 0) return;
            onUi(() => { if (rx == 1) _c.VFOBFreq = hz / 1e6; else _c.VFOAFreq = hz / 1e6; });
        }
        public string Mode(int rx) { return _c.KainosModeOf(rx); }
        public void SetMode(int rx, string mode) { if (!_gone) onUi(() => _c.KainosSetModeOf(rx, mode)); }

        public void SetMox(bool on)
        {
            if (_gone && on) return;
            onUi(() =>
            {
                if (on && !_c.PowerOn) return;
                if (_c.MOX != on) _c.MOX = on;
            });
        }

        // ---- IKainosAudio ----

        public bool AcquireRx(int rx, Action released, out string problem)
        {
            problem = null;
            if (_gone) { problem = "The module is stopping"; return false; }
            string p = null;
            bool ok = onUi(() => _c.KainosModuleAcquireTap(this, rx, out p));
            problem = p;
            if (ok) _tapReleased = released;
            return ok;
        }

        public void ReleaseRx() { _tapReleased = null; onUi(() => _c.KainosModuleReleaseTap(this)); }

        internal void TapTakenAway()
        {
            Action a = _tapReleased;
            _tapReleased = null;
            if (a != null) BeginInvoke(a);
        }

        private int tapRx { get { return _c.KainosModuleTapRx(this); } }
        public int RxRate { get { int rx = tapRx; return rx < 0 ? 0 : KDigi.KDigiRxRate(rx); } }
        public int ReadRx(float[] dst, int max)
        {
            int rx = tapRx;
            if (rx < 0 || dst == null) return 0;
            return KDigi.KDigiRxRead(rx, dst, Math.Min(max, dst.Length));
        }

        public int TxRate { get { return KDigi.KDigiTxRate(); } }
        public void TxStart()
        {
            if (_gone) return;
            KDigi.KDigiTxClear();
            KDigi.KDigiTxEnable(1);
            _txOn = true;
        }
        public int TxWrite(float[] src, int n) { return _txOn && src != null ? KDigi.KDigiTxWrite(src, Math.Min(n, src.Length)) : 0; }
        public int TxQueued { get { return _txOn ? KDigi.KDigiTxQueued() : 0; } }
        public void TxClear() { if (_txOn) KDigi.KDigiTxClear(); }
        public void TxStop()
        {
            if (!_txOn) return;
            _txOn = false;
            KDigi.KDigiTxClear();
            KDigi.KDigiTxEnable(0);
        }

        // ---- when the module stops ----

        internal void Shutdown()
        {
            if (_gone) return;
            bool wasTx = _txOn;
            TxStop();
            try
            {
                onUi(() =>
                {
                    if (wasTx && _c.MOX) _c.MOX = false;
                    _c.KainosModuleReleaseTap(this);
                    for (int rx = 0; rx < 2; rx++) _c.KainosSetModuleMarkers(rx, null);
                    lock (_panes) { foreach (KainosModulePane p in _panes) p.Dispose(); _panes.Clear(); }
                    lock (_menus) { foreach (KainosModuleMenuItem m in _menus) m.Remove(); _menus.Clear(); }
                });
            }
            catch (Exception ex) { Log("shutdown: " + ex.Message); }
            _gone = true;
            Log("stopped");
        }
    }

    internal class KainosModuleTheme : IKainosTheme
    {
        public static readonly KainosModuleTheme Instance = new KainosModuleTheme();
        public Color Bg { get { return KainosUI.Bg; } }
        public Color Surface { get { return KainosUI.Surface; } }
        public Color Panel { get { return KainosUI.Panel; } }
        public Color Raised { get { return KainosUI.Raised; } }
        public Color Line { get { return KainosUI.Line; } }
        public Color Text { get { return KainosUI.Text; } }
        public Color Dim { get { return KainosUI.Dim; } }
        public Color Faint { get { return KainosUI.Faint; } }
        public Color Ice { get { return KainosUI.Ice; } }
        public Color Gold { get { return KainosUI.Gold; } }
        public Color Violet { get { return KainosUI.Violet; } }
        public Color Tx { get { return KainosUI.Tx; } }
        public Color Selected { get { return KainosUI.Selected; } }
        public float Scale { get { return KainosUI.Scale; } }
        public int S(float designPx) { return KainosUI.S(designPx); }
        public Font Font(float px, bool bold) { return new Font("Segoe UI", px, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel); }
        public void DrawButton(Graphics g, RectangleF r, string text, bool on, bool enabled, bool hover, float fontPx)
        {
            KainosUI.DrawButton(g, r, text, on, enabled, hover, KainosUI.Tone.Gold, fontPx);
        }
        public void Style(Control root)
        {
            Form f = root as Form;
            if (f != null) { KainosWindowTheme.Theme(f); return; }
            root.BackColor = KainosUI.Bg;
            root.ForeColor = KainosUI.Text;
        }
    }
}
