/*  consoleKainosKiwi.cs

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
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using NAudio.Wave;

namespace Thetis
{
    // The KIWI tab: the nearest public KiwiSDRs with a free channel (from your grid square), click one to listen,
    // right-click to star it (starred ones stay at the top); a search box; it follows VFO A (frequency and mode)
    // unless Follow is off, when its own frequency is wheeled or typed and its mode chosen; its own volume and
    // Windows output (a VAC cable feeds a decoder); Stop disconnects. Its S reading also shows on VFO A's flag.
    // Favourites, output, Follow and volume are saved with the options (a hidden Setup box). KainosKiwi.cs has the
    // receiver list and client.
    public partial class Console
    {
        private KainosTextLine _kiwiStatus;
        private KainosActionGrid _kiwiButtons;
        private KainosUpDown _kiwiVolume;
        private KainosKiwiList _kiwiList;
        private readonly NumericUpDown KiwiVolume = new NumericUpDown { Minimum = 0, Maximum = 100, Increment = 5, Value = 60 };
        private KiwiClient _kiwi;
        private KiwiReceiver _kiwiOn;
        private List<KiwiReceiver> _kiwiAll = new List<KiwiReceiver>();
        internal List<KiwiReceiver> KiwiNearest = new List<KiwiReceiver>();
        private string _kiwiState = "Pick a receiver to listen";
        private bool _kiwiFollow = true, _kiwiLoading;
        private double _kiwiLastKhz;
        private string _kiwiLastMode = "";
        private WaveOutEvent _kiwiOut;
        private BufferedWaveProvider _kiwiBuf;
        // the Kiwi's volume, on its own samples as they're played: the player's own Volume (waveOutSetVolume) sets the
        // volume of Kainos's whole audio session in Windows, VAC included
        private VolumeWaveProvider16 _kiwiGain;
        private int _kiwiRate;
        private System.Windows.Forms.Timer _kiwiTimer;
        private KainosKiwiTune _kiwiTune;
        private KainosActionGrid _kiwiOutButton;
        private TextBox _kiwiSearch;
        internal readonly HashSet<string> KiwiFavourites = new HashSet<string>();
        // receivers added by address (Add Kiwi): listed first, and checked (their /status) on each refresh
        private readonly List<KiwiReceiver> _kiwiOwn = new List<KiwiReceiver>();
        // the list shows the first _kiwiShow that match; "Show more" adds ten, a new search or refresh starts again
        private int _kiwiShow = KainosKiwiList.Rows;
        internal int KiwiMatches;
        internal double KiwiOwnKhz = 7074;
        internal string KiwiOwnMode = "usb";
        private int _kiwiDevice = -1;           // -1: Windows's default output
        // Mute, and muted while transmitting (Discussion #54): a Kiwi near you hears your own signal, a second or so
        // late. The TX mute holds for a moment after the un-key, for the audio still on its way.
        internal bool KiwiMuted;
        private bool _kiwiTxMute = true, _kiwiTxHeld;
        private DateTime _kiwiTxHoldUntil;
        private bool _kiwiHearShown;
        private const int KiwiTxHoldMs = 1500;

        // saved with the options: "fav=url|url;dev=-1;follow=1;vol=60"
        public string KainosKiwiSettings = "";

        internal void KiwiLoadSettings()
        {
            foreach (string kv in (KainosKiwiSettings ?? "").Split(';'))
            {
                int eq = kv.IndexOf('=');
                if (eq <= 0) continue;
                string k = kv.Substring(0, eq), v = kv.Substring(eq + 1);
                int n;
                switch (k)
                {
                    case "fav": KiwiFavourites.Clear(); foreach (string u in v.Split('|')) if (u.Length > 0) KiwiFavourites.Add(Uri.UnescapeDataString(u)); break;
                    case "dev": if (int.TryParse(v, out n)) _kiwiDevice = n; break;
                    case "follow": _kiwiFollow = v != "0"; break;
                    case "followb": _kiwiFollowB = v == "1"; break;
                    case "main": KiwiOnPanadapter = v != "0"; break;
                    case "wfc": if (int.TryParse(v, out n)) KiwiWfContrast = Math.Max(0, Math.Min(2, n)); break;
                    case "wfs": if (int.TryParse(v, out n)) KiwiWfSpeed = Math.Max(1, Math.Min(4, n)); break;
                    case "zooms":
                        _kiwiZoomByBand.Clear();
                        foreach (string bz in v.Split('|'))
                        {
                            string[] p = bz.Split(':');
                            int z;
                            if (p.Length == 2 && int.TryParse(p[1], out z)) _kiwiZoomByBand[p[0]] = Math.Max(0, Math.Min(14, z));
                        }
                        break;
                    case "vol": if (int.TryParse(v, out n)) KiwiVolume.Value = Math.Max(0, Math.Min(100, n)); break;
                    case "mute": KiwiMuted = v == "1"; break;
                    case "txmute": _kiwiTxMute = v != "0"; break;
                    case "own":
                        _kiwiOwn.Clear();
                        foreach (string o in v.Split('|'))
                        {
                            string[] p = o.Split('~');
                            if (p[0].Length == 0) continue;
                            string label = p.Length > 1 ? Uri.UnescapeDataString(p[1]) : "";
                            _kiwiOwn.Add(new KiwiReceiver { Own = true, Url = Uri.UnescapeDataString(p[0]), Name = label, Loc = label, Antenna = "", Grid = "" });
                        }
                        break;
                }
            }
        }

        private void kiwiSave()
        {
            KainosKiwiSettings = "fav=" + string.Join("|", KiwiFavourites.Select(Uri.EscapeDataString)) + ";dev=" + _kiwiDevice
                                 + ";follow=" + (_kiwiFollow ? 1 : 0) + ";followb=" + (_kiwiFollowB ? 1 : 0) + ";main=" + (KiwiOnPanadapter ? 1 : 0)
                                 + ";wfc=" + KiwiWfContrast + ";wfs=" + KiwiWfSpeed
                                 + ";zooms=" + string.Join("|", _kiwiZoomByBand.Select(kv => kv.Key + ":" + kv.Value))
                                 + ";vol=" + (int)KiwiVolume.Value + ";mute=" + (KiwiMuted ? 1 : 0) + ";txmute=" + (_kiwiTxMute ? 1 : 0)
                                 + ";own=" + string.Join("|", _kiwiOwn.Select(r => Uri.EscapeDataString(r.Url) + "~" + Uri.EscapeDataString(r.Loc.Length > 0 ? r.Loc : r.Name)));
            KainosSettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        internal void KiwiToggleFavourite(KiwiReceiver r)
        {
            if (!KiwiFavourites.Remove(r.Url)) KiwiFavourites.Add(r.Url);
            kiwiSave();
            kiwiSort();
        }

        // the output device's name, and the next one round
        private string kiwiDeviceName
        {
            get
            {
                if (_kiwiDevice < 0 || _kiwiDevice >= WaveOut.DeviceCount) return "Default";
                try { return WaveOut.GetCapabilities(_kiwiDevice).ProductName; } catch { return "Device " + _kiwiDevice; }
            }
        }

        private void kiwiNextDevice()
        {
            int count = WaveOut.DeviceCount;
            _kiwiDevice = _kiwiDevice + 1 >= count ? -1 : _kiwiDevice + 1;
            kiwiSave();
            // restart the player on the new device (the next audio makes it)
            try { _kiwiOut?.Stop(); _kiwiOut?.Dispose(); } catch { }
            _kiwiOut = null;
            _kiwiBuf = null;
            _kiwiRate = 0;
        }

        // Follow off: the tab tunes the Kiwi itself
        internal void KiwiTuneOwn(double khz, string mode)
        {
            KiwiOwnKhz = Math.Max(10, Math.Min(30000, khz));
            if (mode != null) KiwiOwnMode = mode;
            if (_kiwi != null && !_kiwiFollow) _kiwi.Tune(KiwiOwnKhz, KiwiOwnMode);
        }

        internal bool KiwiFollow { get { return _kiwiFollow; } }

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        private void kainosAddKiwiSection()
        {
            _kiwiStatus = new KainosTextLine(() => _kiwiState, () => _kiwi != null && _kiwi.Connected ? KainosUI.Text : KainosUI.Dim);
            _kiwiButtons = new KainosActionGrid(3);
            _kiwiButtons.Add("Refresh", () => _kiwiLoading, kiwiRefresh, KainosUI.Tone.Ice);
            // Follow: VFO A, then VFO B, then off (the Kiwi tuned on its own)
            _kiwiButtons.Add("Follow", () => _kiwiFollow, () =>
            {
                if (!_kiwiFollow) { _kiwiFollow = true; _kiwiFollowB = false; }
                else if (!_kiwiFollowB) _kiwiFollowB = true;
                else
                {
                    KiwiOwnMode = kiwiFollowMode; KiwiOwnKhz = kiwiFollowKhz(KiwiOwnMode);     // carry on from where it was
                    _kiwiFollow = false; _kiwiFollowB = false;
                }
                _kiwiLastKhz = 0;
                kiwiSave();
            }, KainosUI.Tone.Gold);
            _kiwiButtons.LabelFor = (i, l) => l == "Follow" ? (_kiwiFollow ? (_kiwiFollowB ? "Follow B" : "Follow A") : "Follow") : l;
            _kiwiButtons.Add("Stop", () => false, kiwiStop, KainosUI.Tone.Tx);
            _kiwiButtons.Add("Waterfall", () => _kiwiWfForm != null, KiwiShowWaterfall, KainosUI.Tone.Ice);      // consoleKainosKiwiWaterfall.cs
            _kiwiButtons.Add("Main view", () => KiwiOnPanadapter, () => { KiwiOnPanadapter = !KiwiOnPanadapter; kiwiSave(); kiwiWfTick(); }, KainosUI.Tone.Ice);
            _kiwiButtons.Add("Add Kiwi", () => false, kiwiAdd, KainosUI.Tone.Ice);
            _kiwiButtons.Add("Mute", () => KiwiMuted, KiwiToggleMute, KainosUI.Tone.Tx);
            _kiwiButtons.Add("Hear", () => false, KiwiHearSwap, KainosUI.Tone.Gold);
            _kiwiButtons.Add("TX mute", () => _kiwiTxMute, () => { _kiwiTxMute = !_kiwiTxMute; kiwiTxRelease(false); kiwiSave(); _kiwiButtons.Invalidate(); }, KainosUI.Tone.Tx);
            Func<int, string, string> follow = _kiwiButtons.LabelFor;
            _kiwiButtons.LabelFor = (i, l) => l == "Hear" ? (KiwiHearingKiwi ? "Hear radio" : "Hear Kiwi") : follow(i, l);
            _kiwiVolume = new KainosUpDown(KiwiVolume, "Vol", "%", false);
            KiwiVolume.ValueChanged += (s, e) => { kiwiApplyVolume(); kiwiSave(); };
            MoxChangeHandlers += (rx, oldMox, newMox) =>
            {
                if (InvokeRequired) { try { BeginInvoke(new Action(() => kiwiMox(newMox))); } catch { } }
                else kiwiMox(newMox);
            };
            _kiwiTune = new KainosKiwiTune(this);
            _kiwiOutButton = new KainosActionGrid(1);
            _kiwiOutButton.Add("Out", () => false, kiwiNextDevice, KainosUI.Tone.Ice);
            _kiwiOutButton.LabelFor = (i, l) => "Out: " + kiwiDeviceName;
            _kiwiSearch = new TextBox
            {
                BorderStyle = BorderStyle.FixedSingle, BackColor = KainosUI.Bg, ForeColor = KainosUI.Text,
                Font = new Font("Segoe UI", Math.Max(8f, KainosUI.S(12)), FontStyle.Regular, GraphicsUnit.Pixel),
            };
            _kiwiSearch.TextChanged += (s, e) => { _kiwiShow = KainosKiwiList.Rows; kiwiSort(); kiwiListResized(); };
            _kiwiSearch.HandleCreated += (s, e) => SendMessage(_kiwiSearch.Handle, 0x1501, (IntPtr)1, "Search: place, call, antenna or address");     // EM_SETCUEBANNER
            _kiwiList = new KainosKiwiList(this);
            foreach (Control c in new Control[] { _kiwiStatus, _kiwiButtons, _kiwiTune, _kiwiVolume, _kiwiOutButton, _kiwiSearch, _kiwiList })
                _kainosColumn.Viewport.Controls.Add(c);
            int row = KainosUI.S(26), gap = KainosUI.S(6);
            _kainosColumn.AddSection("kiwi", "KIWI", w => KainosUI.S(20) + gap + _kiwiButtons.PreferredHeight(w) + gap + (row + gap) * 4 + _kiwiList.PreferredHeight, r =>
            {
                int y = r.Top;
                _kiwiStatus.SetBounds(r.Left, y, r.Width, KainosUI.S(20)); y += KainosUI.S(20) + gap;
                int bh = _kiwiButtons.PreferredHeight(r.Width);
                _kiwiButtons.SetBounds(r.Left, y, r.Width, bh); y += bh + gap;
                _kiwiTune.SetBounds(r.Left, y, r.Width, row); y += row + gap;
                _kiwiVolume.SetBounds(r.Left, y, r.Width, row); y += row + gap;
                _kiwiOutButton.SetBounds(r.Left, y, r.Width, row); y += row + gap;
                _kiwiSearch.SetBounds(r.Left, y + (row - _kiwiSearch.PreferredHeight) / 2, r.Width, _kiwiSearch.PreferredHeight); y += row + gap;
                _kiwiList.SetBounds(r.Left, y, r.Width, _kiwiList.PreferredHeight);
            }, false);
            KiwiLoadSettings();

            _kiwiTimer = new System.Windows.Forms.Timer { Interval = 300 };
            _kiwiTimer.Tick += (s, e) => kiwiTick();
            _kiwiTimer.Start();
            FormClosing += (s, e) => kiwiStop();
        }

        private string kiwiGrid
        {
            get
            {
                try { return IsSetupFormNull || SetupForm.RadeSettings == null ? "" : SetupForm.RadeSettings.Grid.Text.Trim(); } catch { return ""; }
            }
        }

        // the list: fetched when the tab is first shown, and on Refresh
        private void kiwiRefresh()
        {
            if (_kiwiLoading) return;
            _kiwiLoading = true;
            _kiwiState = "Getting the list of KiwiSDRs...";
            Task.Run(() =>
            {
                List<KiwiReceiver> own = kiwiCheckOwn();
                try
                {
                    List<KiwiReceiver> all = KiwiDirectory.Fetch();
                    BeginInvoke(new Action(() =>
                    {
                        _kiwiAll = all; kiwiOwnChecked(own); _kiwiShow = KainosKiwiList.Rows; kiwiSort(); _kiwiLoading = false;
                        _kiwiState = all.Count + (own.Count > 0 ? " receivers and " + own.Count + " of yours" : " receivers") + ". Click one to listen.";
                        kiwiListResized();
                    }));
                }
                catch (Exception ex)
                {
                    Exception e = ex is AggregateException && ex.InnerException != null ? ex.InnerException : ex;
                    BeginInvoke(new Action(() => { kiwiOwnChecked(own); kiwiSort(); _kiwiLoading = false; _kiwiState = "Couldn't get the list (" + e.Message + ")"; kiwiListResized(); }));
                }
            });
        }

        // the nearest with a free channel that cover the frequency (or the most recently heard, without a grid)
        private void kiwiSort()
        {
            double la, lo;
            bool have = KiwiDirectory.GridToLatLon(kiwiGrid, out la, out lo);
            long hz = (long)(VFOAFreq * 1e6);
            // yours first (and instead of the same receiver from the list), then the list
            HashSet<string> ownUrls = new HashSet<string>(_kiwiOwn.Select(r => r.Url), StringComparer.OrdinalIgnoreCase);
            List<KiwiReceiver> all = _kiwiOwn.Concat(_kiwiAll.Where(r => !ownUrls.Contains(r.Url))).ToList();
            foreach (KiwiReceiver r in all) r.Km = have && (r.Lat != 0 || r.Lon != 0) ? KiwiDirectory.DistanceKm(la, lo, r.Lat, r.Lon) : -1;
            string find = _kiwiSearch != null ? _kiwiSearch.Text.Trim() : "";
            IEnumerable<KiwiReceiver> q = all.Where(r => r.Own || ((r.Free || KiwiFavourites.Contains(r.Url)) && (r.HighHz == 0 || (hz >= r.LowHz && hz <= r.HighHz))));
            if (find.Length > 0)
                q = q.Where(r => (r.Loc + " " + r.Name + " " + r.Antenna + " " + r.Grid + " " + r.Url).IndexOf(find, StringComparison.OrdinalIgnoreCase) >= 0);     // the address too: many carry the owner's call
            q = have ? q.OrderBy(r => r.Km < 0 ? double.MaxValue : r.Km) : q.OrderBy(r => r.Users);
            List<KiwiReceiver> matched = q.OrderBy(r => r.Own ? 0 : KiwiFavourites.Contains(r.Url) ? 1 : 2).ToList();
            KiwiMatches = matched.Count;
            KiwiNearest = matched.Take(_kiwiShow).ToList();
        }

        internal void KiwiShowMore()
        {
            _kiwiShow += 10;
            kiwiSort();
            kiwiListResized();
        }

        // the list's height follows how many it shows
        private void kiwiListResized()
        {
            if (_kainosColumn != null) _kainosColumn.ArrangeSections();
            if (_kiwiList != null) _kiwiList.Invalidate();
        }

        // ---- receivers added by address (GitHub discussion #43) ----

        private void kiwiAdd()
        {
            string typed = KainosKiwiAddDialog.Ask(this);
            if (typed == null) return;
            string url = KiwiDirectory.NormalUrl(typed);
            if (url == null) { MessageBox.Show(this, "That doesn't look like an address: try something like kiwi.example.com:8073.", "Add Kiwi", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            if (_kiwiOwn.Any(r => string.Equals(r.Url, url, StringComparison.OrdinalIgnoreCase))) { _kiwiState = "Already in your list: " + url; return; }
            _kiwiState = "Checking " + url + " ...";
            Task.Run(() =>
            {
                KiwiReceiver r = null;
                string why = null;
                try { r = KiwiDirectory.FetchStatus(url); }
                catch (Exception ex) { Exception e = ex is AggregateException && ex.InnerException != null ? ex.InnerException : ex; why = e.Message; }
                BeginInvoke(new Action(() =>
                {
                    if (r == null)
                    {
                        if (MessageBox.Show(this, "No KiwiSDR answered at " + url + " (" + why + ").\r\n\r\nAdd it anyway?", "Add Kiwi",
                                            MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) { _kiwiState = "Not added."; return; }
                        r = new KiwiReceiver { Url = url, Name = url, Loc = "", Antenna = "", Grid = "", NoAnswer = true };
                    }
                    r.Own = true;
                    _kiwiOwn.Add(r);
                    kiwiSave();
                    kiwiSort();
                    _kiwiState = "Added " + (r.Loc.Length > 0 ? r.Loc : r.Name) + ". Right-click it to remove it.";
                    kiwiListResized();
                }));
            });
        }

        internal void KiwiRemoveOwn(KiwiReceiver r)
        {
            if (MessageBox.Show(this, "Remove " + (r.Loc.Length > 0 ? r.Loc : r.Name) + " from your Kiwis?", "Kiwi", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            _kiwiOwn.RemoveAll(o => string.Equals(o.Url, r.Url, StringComparison.OrdinalIgnoreCase));
            kiwiSave();
            kiwiSort();
            kiwiListResized();
        }

        // on a worker thread: each added receiver's /status, kept as it was (marked no answer) if it doesn't answer
        private List<KiwiReceiver> kiwiCheckOwn()
        {
            List<KiwiReceiver> was = (List<KiwiReceiver>)Invoke(new Func<List<KiwiReceiver>>(() => _kiwiOwn.ToList()));
            List<KiwiReceiver> now = new List<KiwiReceiver>();
            foreach (KiwiReceiver o in was)
            {
                try
                {
                    KiwiReceiver r = KiwiDirectory.FetchStatus(o.Url);
                    r.Own = true;
                    if (r.Loc.Length == 0 && r.Name.Length == 0) { r.Loc = o.Loc; r.Name = o.Name; }
                    now.Add(r);
                }
                catch
                {
                    now.Add(new KiwiReceiver { Own = true, Url = o.Url, Name = o.Name, Loc = o.Loc, Antenna = o.Antenna, Grid = o.Grid, Lat = o.Lat, Lon = o.Lon, NoAnswer = true });
                }
            }
            return now;
        }

        // on the UI thread: the checked ones in place of the old (any added or removed meanwhile are kept as they are)
        private void kiwiOwnChecked(List<KiwiReceiver> now)
        {
            for (int i = 0; i < _kiwiOwn.Count; i++)
            {
                KiwiReceiver n = now.FirstOrDefault(r => string.Equals(r.Url, _kiwiOwn[i].Url, StringComparison.OrdinalIgnoreCase));
                if (n != null) _kiwiOwn[i] = n;
            }
        }

        // the Kiwi's frequency when following VFO A. In CW, Thetis's VFO is the signal itself, while the Kiwi listens
        // above its frequency: so it's tuned a CW pitch below, and the signal is heard at the same pitch (issue #5)
        private double kiwiFollowKhz(string mode)
        {
            return (_kiwiFollowB ? VFOBFreq : VFOAFreq) * 1000 - (mode == "cw" ? cw_pitch / 1000.0 : 0);
        }

        // following VFO B: RX2's mode with RX2 on (B is RX2's VFO), otherwise RX1's (split, without RX2)
        private bool _kiwiFollowB;
        internal bool KiwiFollowB { get { return _kiwiFollow && _kiwiFollowB; } }
        private string kiwiFollowMode { get { return kiwiMode(_kiwiFollowB && RX2Enabled ? _rx2_dsp_mode : _rx1_dsp_mode); } }

        private static string kiwiMode(DSPMode m)
        {
            switch (m)
            {
                case DSPMode.LSB: case DSPMode.DIGL: return "lsb";
                case DSPMode.CWL: case DSPMode.CWU: return "cw";
                case DSPMode.AM: case DSPMode.SAM: case DSPMode.DSB: return "am";
                case DSPMode.FM: return "nbfm";
                default: return "usb";
            }
        }

        internal void KiwiListen(KiwiReceiver r)
        {
            kiwiStop();
            _kiwiOn = r;
            _kiwi = new KiwiClient();
            _kiwi.Status += s => { try { BeginInvoke(new Action(() => _kiwiState = s + (_kiwiOn != null ? " - " + _kiwiOn.Loc : ""))); } catch { } };
            _kiwi.Audio += kiwiAudio;
            _kiwiLastMode = kiwiFollowMode;
            _kiwiLastKhz = kiwiFollowKhz(_kiwiLastMode);
            _kiwi.CwPitch = cw_pitch;
            if (_kiwiFollow) _kiwi.Connect(r.Url, KainosMyCallsign, _kiwiLastKhz, _kiwiLastMode);
            else _kiwi.Connect(r.Url, KainosMyCallsign, KiwiOwnKhz, KiwiOwnMode);
        }

        private void kiwiStop()
        {
            if (_kiwi != null) { _kiwi.Audio -= kiwiAudio; _kiwi.Disconnect(); _kiwi = null; }
            _kiwiOn = null;
            try { _kiwiOut?.Stop(); _kiwiOut?.Dispose(); } catch { }
            _kiwiOut = null;
            _kiwiBuf = null;
            _kiwiRate = 0;
            _kiwiState = "Pick a receiver to listen";
        }

        // audio from the receiver's thread into the player (made when the rate is known)
        private void kiwiAudio(short[] pcm, int rate)
        {
            if (rate <= 0) return;
            if (_kiwiBuf == null || _kiwiRate != rate)
            {
                try { _kiwiOut?.Stop(); _kiwiOut?.Dispose(); } catch { }
                _kiwiRate = rate;
                _kiwiBuf = new BufferedWaveProvider(new WaveFormat(rate, 16, 1)) { BufferDuration = TimeSpan.FromSeconds(3), DiscardOnBufferOverflow = true };
                _kiwiOut = new WaveOutEvent { DesiredLatency = 200, DeviceNumber = _kiwiDevice < WaveOut.DeviceCount ? _kiwiDevice : -1 };
                _kiwiGain = new VolumeWaveProvider16(_kiwiBuf);
                _kiwiOut.Init(_kiwiGain);
                try { _kiwiOut.Volume = 1f; } catch { }     // puts back the session volume an older Kainos set with Vol
                kiwiApplyVolume();
                _kiwiOut.Play();
            }
            byte[] b = new byte[pcm.Length * 2];
            Buffer.BlockCopy(pcm, 0, b, 0, b.Length);
            _kiwiBuf.AddSamples(b, 0, b.Length);
        }

        // the player's volume: the Vol setting, or nothing while muted or transmitting
        private void kiwiApplyVolume()
        {
            if (_kiwiGain == null) return;
            _kiwiGain.Volume = KiwiMuted || _kiwiTxHeld ? 0f : (float)KiwiVolume.Value / 100f;
        }

        internal void KiwiToggleMute()
        {
            KiwiMuted = !KiwiMuted;
            kiwiApplyVolume();
            kiwiSave();
            kiwiAudioChanged();
        }

        // hearing the Kiwi rather than the radio: the Kiwi on and the radio (RX1) muted
        internal bool KiwiHearingKiwi { get { return !KiwiMuted && chkMUT.Checked; } }

        // one click from the radio to the Kiwi and back: mutes one and unmutes the other
        internal void KiwiHearSwap()
        {
            bool toKiwi = !KiwiHearingKiwi;
            KiwiMuted = !toKiwi;
            if (chkMUT.Checked != toKiwi) kainosClick(chkMUT);
            kiwiApplyVolume();
            kiwiSave();
            kiwiAudioChanged();
        }

        private void kiwiAudioChanged()
        {
            _kiwiButtons?.Invalidate();
            if (_kiwiWfForm != null && !_kiwiWfForm.IsDisposed) _kiwiWfForm.RefreshAudio();
        }

        private void kiwiMox(bool tx)
        {
            if (tx && _kiwiTxMute) { _kiwiTxHeld = true; kiwiApplyVolume(); }
            if (!tx && _kiwiTxHeld) _kiwiTxHoldUntil = DateTime.Now.AddMilliseconds(KiwiTxHoldMs);
        }

        // the TX mute ends (after its hold, or when TX mute is turned off): what came in meanwhile is dropped
        private void kiwiTxRelease(bool afterHold)
        {
            if (!_kiwiTxHeld) return;
            if (afterHold && (_mox || DateTime.Now < _kiwiTxHoldUntil)) return;
            if (!afterHold && _kiwiTxMute) return;
            _kiwiTxHeld = false;
            try { _kiwiBuf?.ClearBuffer(); } catch { }
            kiwiApplyVolume();
        }

        private void kiwiTick()
        {
            kiwiTxRelease(true);
            // the radio's MUTE changes from its own buttons too: the Hear buttons say which one you hear
            if (_kiwiHearShown != KiwiHearingKiwi) { _kiwiHearShown = KiwiHearingKiwi; kiwiAudioChanged(); }
            // following (VFO A or B) and the waterfall keep going with the tab closed
            if (_kiwi != null && _kiwiFollow)
            {
                string mode = kiwiFollowMode;
                double khz = kiwiFollowKhz(mode);
                _kiwi.CwPitch = cw_pitch;
                if (Math.Abs(khz - _kiwiLastKhz) > 0.0005 || mode != _kiwiLastMode)
                {
                    _kiwiLastKhz = khz;
                    _kiwiLastMode = mode;
                    _kiwi.Tune(khz, mode);
                }
            }
            kiwiWfTick();
            if (_kainosColumn == null || !_kainosColumn.IsOn("kiwi")) return;
            if (_kiwiAll.Count == 0 && !_kiwiLoading && _kiwiState.StartsWith("Pick")) kiwiRefresh();
            _kiwiStatus.Invalidate();
            _kiwiButtons.Invalidate();
            _kiwiTune.Invalidate();
            _kiwiOutButton.Invalidate();
            _kiwiList.Invalidate();
        }

        internal string KiwiSignalText
        {
            get
            {
                if (_kiwi == null || !_kiwi.Connected) return "";
                double su = Common.GetSMeterUnits(_kiwi.Rssi, VFOAFreq >= S9Frequency);
                return su <= 9 ? "S" + Math.Max(0, (int)Math.Floor(su)) : "S9+" + (int)Math.Round((su - 9) * 6);
            }
        }

        internal KiwiReceiver KiwiOn { get { return _kiwiOn; } }
    }

    // The Kiwi's own frequency and mode (Follow off): wheel to tune (1 kHz; Shift 100 Hz, Ctrl 10 kHz), click the
    // frequency to type one (kHz), click the mode to step through USB / LSB / CW / AM. With Follow on it shows VFO A's.
    internal class KainosKiwiTune : Control
    {
        private static readonly string[] Modes = { "usb", "lsb", "cw", "am" };
        private readonly Console _console;
        private RectangleF _modeRect, _freqRect;
        private TextBox _edit;

        public KainosKiwiTune(Console console)
        {
            _console = console;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            BackColor = KainosUI.Surface;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            bool own = !_console.KiwiFollow;
            double khz = own ? _console.KiwiOwnKhz : _console.VFOAFreq * 1000;
            string mode = own ? _console.KiwiOwnMode : "";
            float mw = KainosUI.S(54);
            _modeRect = new RectangleF(Width - mw, 0, mw, Height);
            _freqRect = new RectangleF(0, 0, Width - mw - KainosUI.S(6), Height);
            using (Font f = new Font("Segoe UI", Math.Max(7f, KainosUI.S(10)), FontStyle.Bold, GraphicsUnit.Pixel))
            using (Brush b = new SolidBrush(KainosUI.Faint))
                g.DrawString(own ? "TUNE" : "VFO A", f, b, 2, (Height - f.Height) / 2f);
            using (Font f = new Font("Consolas", Math.Max(9f, KainosUI.S(15)), FontStyle.Bold, GraphicsUnit.Pixel))
            using (Brush b = new SolidBrush(own ? KainosUI.GoldHi : KainosUI.Dim))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center })
                g.DrawString(khz.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " kHz", f, b, _freqRect, sf);
            if (own) KainosUI.DrawButton(g, _modeRect, mode.ToUpperInvariant(), false, true, false, KainosUI.Tone.Ice, Math.Max(9f, KainosUI.S(12)));
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (_console.KiwiFollow) return;
            double step = (ModifierKeys & Keys.Shift) != 0 ? 0.1 : (ModifierKeys & Keys.Control) != 0 ? 10 : 1;
            _console.KiwiTuneOwn(_console.KiwiOwnKhz + Math.Sign(e.Delta) * step, null);
            if (e is HandledMouseEventArgs) ((HandledMouseEventArgs)e).Handled = true;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); Cursor = _console.KiwiFollow ? Cursors.Default : Cursors.Hand; }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (_console.KiwiFollow || e.Button != MouseButtons.Left) return;
            if (_modeRect.Contains(e.Location))
            {
                int i = Array.IndexOf(Modes, _console.KiwiOwnMode);
                _console.KiwiTuneOwn(_console.KiwiOwnKhz, Modes[(i + 1) % Modes.Length]);
                Invalidate();
                return;
            }
            if (_freqRect.Contains(e.Location) && _edit == null)
            {
                _edit = new TextBox
                {
                    BorderStyle = BorderStyle.None, BackColor = KainosUI.Bg, ForeColor = KainosUI.Text, TextAlign = HorizontalAlignment.Right,
                    Font = new Font("Consolas", Math.Max(9f, KainosUI.S(15)), FontStyle.Bold, GraphicsUnit.Pixel),
                    Text = _console.KiwiOwnKhz.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                };
                _edit.SetBounds((int)_freqRect.X + KainosUI.S(44), (Height - _edit.PreferredHeight) / 2, (int)_freqRect.Width - KainosUI.S(44), _edit.PreferredHeight);
                _edit.KeyDown += (s, a) =>
                {
                    if (a.KeyCode == Keys.Enter)
                    {
                        double v;
                        if (double.TryParse(_edit.Text.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v))
                            _console.KiwiTuneOwn(v < 100 ? v * 1000 : v, null);     // under 100: MHz
                        endEdit(); a.SuppressKeyPress = true;
                    }
                    else if (a.KeyCode == Keys.Escape) { endEdit(); a.SuppressKeyPress = true; }
                };
                _edit.LostFocus += (s, a) => BeginInvoke(new Action(endEdit));
                Controls.Add(_edit);
                _edit.Focus();
                _edit.SelectAll();
            }
        }

        private void endEdit()
        {
            if (_edit == null) return;
            TextBox t = _edit;
            _edit = null;
            Controls.Remove(t);
            t.Dispose();
            Invalidate();
        }
    }

    // The nearest free receivers: distance, users, place and antenna; the one you're on in gold with its S reading
    internal class KainosKiwiList : Control
    {
        public const int Rows = 8;
        private readonly Console _console;
        private int _hover = -1;

        public KainosKiwiList(Console console)
        {
            _console = console;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = KainosUI.Surface;
        }

        // at least Rows high, so a search doesn't make the column jump; longer with "Show more"
        public int PreferredHeight { get { return Math.Max(Rows, _console.KiwiNearest.Count + (more ? 1 : 0)) * rowH; } }
        private bool more { get { return _console.KiwiMatches > _console.KiwiNearest.Count; } }
        private int rowH { get { return KainosUI.S(30); } }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            List<KiwiReceiver> list = _console.KiwiNearest;
            using (Font big = new Font("Segoe UI", Math.Max(8f, KainosUI.S(12)), FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font small = new Font("Segoe UI", Math.Max(7f, KainosUI.S(10)), FontStyle.Regular, GraphicsUnit.Pixel))
            using (Brush text = new SolidBrush(KainosUI.Text))
            using (Brush gold = new SolidBrush(KainosUI.GoldHi))
            using (Brush dim = new SolidBrush(KainosUI.Dim))
            using (Brush faint = new SolidBrush(KainosUI.Faint))
            using (Pen line = new Pen(KainosUI.Line))
            using (StringFormat clip = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                if (list.Count == 0)
                {
                    g.DrawString("No receivers to show. Set your grid square in Setup > DSP > FreeDV (RADE) to see the nearest first, or change the search.", small, faint, new RectangleF(2, 4, Width - 4, rowH * 2));
                    return;
                }
                for (int i = 0; i < list.Count; i++)
                {
                    KiwiReceiver r = list[i];
                    int y = i * rowH;
                    bool on = _console.KiwiOn != null && _console.KiwiOn.Url == r.Url;
                    if (i == _hover || on) using (Brush hb = new SolidBrush(on ? KainosUI.Selected : KainosUI.Raised)) g.FillRectangle(hb, 0, y, Width, rowH - 1);
                    bool fav = r.Own || _console.KiwiFavourites.Contains(r.Url);
                    g.DrawLine(line, 0, y + rowH - 1, Width, y + rowH - 1);
                    string km = r.Km >= 0 ? r.Km.ToString("0", CultureInfo.InvariantCulture) + " km" : "";
                    g.DrawString((fav ? "\u2605 " : "") + (r.Loc.Length > 0 ? r.Loc : r.Name), big, on || fav ? gold : text, new RectangleF(2, y + 1, Width - KainosUI.S(90), KainosUI.S(16)), clip);
                    string right = on ? _console.KiwiSignalText : r.NoAnswer ? "no answer" : !r.Free ? "full" : r.Users + "/" + r.UsersMax;
                    using (StringFormat rf = new StringFormat { Alignment = StringAlignment.Far })
                        g.DrawString(km + "  " + right, small, on ? gold : dim, new RectangleF(Width - KainosUI.S(90), y + 3, KainosUI.S(88), KainosUI.S(14)), rf);
                    g.DrawString(r.Own && r.Antenna.Length == 0 ? r.Url : r.Antenna, small, faint, new RectangleF(2, y + KainosUI.S(16), Width - 4, KainosUI.S(14)), clip);
                }
                if (more)
                {
                    int y = list.Count * rowH;
                    if (_hover == list.Count) using (Brush hb = new SolidBrush(KainosUI.Raised)) g.FillRectangle(hb, 0, y, Width, rowH - 1);
                    int left = _console.KiwiMatches - list.Count;
                    using (StringFormat cf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                        g.DrawString("Show " + Math.Min(10, left) + " more  (" + left + " not shown)", small, gold, new RectangleF(0, y, Width, rowH - 1), cf);
                }
            }
        }

        // a receiver's row, or the row past them ("Show more", when there are more)
        private int hit(Point p) { int i = p.Y / Math.Max(1, rowH); return i >= 0 && (i < _console.KiwiNearest.Count || (i == _console.KiwiNearest.Count && more)) ? i : -1; }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int h = hit(e.Location);
            Cursor = h >= 0 ? Cursors.Hand : Cursors.Default;
            if (h != _hover) { _hover = h; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = -1; Invalidate(); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            int h = hit(e.Location);
            if (h < 0) return;
            if (h == _console.KiwiNearest.Count) { if (e.Button == MouseButtons.Left) _console.KiwiShowMore(); return; }
            KiwiReceiver r = _console.KiwiNearest[h];
            if (e.Button == MouseButtons.Left) _console.KiwiListen(r);
            else if (e.Button == MouseButtons.Right) { if (r.Own) _console.KiwiRemoveOwn(r); else _console.KiwiToggleFavourite(r); Invalidate(); }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            KainosViewport v = Parent as KainosViewport;
            if (v != null) typeof(Control).GetMethod("OnMouseWheel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(v, new object[] { e });
        }
    }

    // Add Kiwi: a receiver's address
    internal static class KainosKiwiAddDialog
    {
        public static string Ask(IWin32Window owner)
        {
            using (Form f = new Form())
            {
                f.Text = "Add a KiwiSDR";
                f.FormBorderStyle = FormBorderStyle.FixedDialog;
                f.MinimizeBox = f.MaximizeBox = false;
                f.ShowInTaskbar = false;
                f.StartPosition = FormStartPosition.CenterParent;
                f.BackColor = KainosUI.Panel;
                f.ForeColor = KainosUI.Text;
                f.Font = new Font("Segoe UI", 9f);
                f.ClientSize = new Size(440, 150);
                Label l = new Label { Text = "The receiver's address, as you'd open it in a browser:", Location = new Point(12, 14), AutoSize = true };
                TextBox box = new TextBox { Location = new Point(12, 38), Width = 416, BackColor = KainosUI.Bg, ForeColor = KainosUI.Text, BorderStyle = BorderStyle.FixedSingle };
                Label hint = new Label { Text = "For example kiwi.example.com:8073 (8073 is used when there's no port).\r\nIt's listed first, and right-click removes it.", Location = new Point(12, 66), AutoSize = true, ForeColor = KainosUI.Dim };
                Button ok = new Button { Text = "Add", Location = new Point(258, 108), Size = new Size(80, 30), FlatStyle = FlatStyle.Flat, BackColor = KainosUI.Raised, DialogResult = DialogResult.OK };
                Button cancel = new Button { Text = "Cancel", Location = new Point(348, 108), Size = new Size(80, 30), FlatStyle = FlatStyle.Flat, BackColor = KainosUI.Raised, DialogResult = DialogResult.Cancel };
                f.Controls.AddRange(new Control[] { l, box, hint, ok, cancel });
                f.AcceptButton = ok;
                f.CancelButton = cancel;
                return f.ShowDialog(owner) == DialogResult.OK && box.Text.Trim().Length > 0 ? box.Text.Trim() : null;
            }
        }
    }
}
