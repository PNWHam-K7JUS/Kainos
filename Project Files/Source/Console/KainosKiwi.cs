/*  KainosKiwi.cs

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
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Thetis
{
    // Listening to a public KiwiSDR from inside Kainos (as OpenHPSDR-Zeus and AetherSDR do): the receiver list, and
    // a client for one receiver's audio, following the KiwiSDR author's own client (kiwiclient): a WebSocket to
    // /<timestamp>/SND, "SET auth t=kiwi p=", the user's callsign as ident_user so the owner sees who is listening,
    // then mode / passband / frequency, IMA-ADPCM compression and a keepalive each second. Receive only.
    //
    // The list comes from rx.linkfanel.net's copy of kiwisdr.com/public (made for map tools; kiwisdr.com's own page
    // is behind a human check). Each receiver's owner decides who may connect and for how long; Kainos shows their
    // answer (busy, time limit...) as it comes.

    internal class KiwiReceiver
    {
        public string Name, Url, Loc, Antenna, Grid;
        public int Users, UsersMax;
        public double Lat, Lon, Km = -1;
        public long LowHz, HighHz;
        public bool Free { get { return Users < UsersMax; } }
    }

    internal static class KiwiDirectory
    {
        private const string ListUrl = "http://rx.linkfanel.net/kiwisdr_com.js";
        private const string UserAgent = "Kainos/2.10.3.15 (+https://github.com/PNWHam-K7JUS/Kainos)";

        public static List<KiwiReceiver> Fetch()
        {
            using (HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) })
            {
                http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
                string js = http.GetStringAsync(ListUrl).Result;
                int a = js.IndexOf('['), b = js.LastIndexOf(']');
                if (a < 0 || b < a) return new List<KiwiReceiver>();
                Newtonsoft.Json.Linq.JArray arr = Newtonsoft.Json.Linq.JArray.Parse(js.Substring(a, b - a + 1));
                List<KiwiReceiver> list = new List<KiwiReceiver>();
                foreach (Newtonsoft.Json.Linq.JToken t in arr)
                {
                    if ((string)t["status"] != "active" || (string)t["offline"] == "yes") continue;
                    string url = (string)t["url"] ?? "";
                    if (!url.StartsWith("http")) continue;
                    if (url.IndexOf("proxy.kiwisdr.com", StringComparison.OrdinalIgnoreCase) >= 0) continue;     // their proxy serves browsers only
                    KiwiReceiver r = new KiwiReceiver
                    {
                        Name = clean((string)t["name"]),
                        Url = url.TrimEnd('/'),
                        Loc = clean((string)t["loc"]),
                        Antenna = clean((string)t["antenna"]),
                        Grid = (string)t["grid"] ?? "",
                    };
                    int.TryParse((string)t["users"], out r.Users);
                    int.TryParse((string)t["users_max"], out r.UsersMax);
                    Match m = Regex.Match((string)t["gps"] ?? "", @"\(\s*(-?[\d.]+)\s*,\s*(-?[\d.]+)\s*\)");
                    if (m.Success)
                    {
                        double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out r.Lat);
                        double.TryParse(m.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out r.Lon);
                    }
                    string[] bands = ((string)t["bands"] ?? "").Split('-');
                    if (bands.Length == 2) { long.TryParse(bands[0], out r.LowHz); long.TryParse(bands[1], out r.HighHz); }
                    list.Add(r);
                }
                return list;
            }
        }

        private static string clean(string s) { return Regex.Replace(s ?? "", @"\s+", " ").Trim(); }

        // a Maidenhead square's centre
        public static bool GridToLatLon(string grid, out double lat, out double lon)
        {
            lat = lon = 0;
            grid = (grid ?? "").Trim().ToUpperInvariant();
            if (grid.Length < 4 || grid[0] < 'A' || grid[0] > 'R' || grid[1] < 'A' || grid[1] > 'R' || !char.IsDigit(grid[2]) || !char.IsDigit(grid[3])) return false;
            lon = (grid[0] - 'A') * 20 - 180 + (grid[2] - '0') * 2 + 1;
            lat = (grid[1] - 'A') * 10 - 90 + (grid[3] - '0') + 0.5;
            if (grid.Length >= 6 && char.IsLetter(grid[4]) && char.IsLetter(grid[5]))
            {
                lon += -1 + (char.ToUpperInvariant(grid[4]) - 'A') * (2.0 / 24) + 1.0 / 24;
                lat += -0.5 + (char.ToUpperInvariant(grid[5]) - 'A') * (1.0 / 24) + 0.5 / 24;
            }
            return true;
        }

        public static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
        {
            double r = Math.PI / 180, dLat = (lat2 - lat1) * r, dLon = (lon2 - lon1) * r;
            double h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(lat1 * r) * Math.Cos(lat2 * r) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return 6371 * 2 * Math.Asin(Math.Min(1, Math.Sqrt(h)));
        }
    }

    // IMA ADPCM as the KiwiSDR sends it: the state carries over from one audio frame to the next
    internal class KiwiAdpcm
    {
        private static readonly int[] Steps =
        {
            7, 8, 9, 10, 11, 12, 13, 14, 16, 17, 19, 21, 23, 25, 28, 31, 34, 37, 41, 45, 50, 55, 60, 66, 73, 80, 88, 97, 107, 118,
            130, 143, 157, 173, 190, 209, 230, 253, 279, 307, 337, 371, 408, 449, 494, 544, 598, 658, 724, 796, 876, 963, 1060,
            1166, 1282, 1411, 1552, 1707, 1878, 2066, 2272, 2499, 2749, 3024, 3327, 3660, 4026, 4428, 4871, 5358, 5894, 6484,
            7132, 7845, 8630, 9493, 10442, 11487, 12635, 13899, 15289, 16818, 18500, 20350, 22385, 24623, 27086, 29794, 32767,
        };
        private static readonly int[] IndexAdjust = { -1, -1, -1, -1, 2, 4, 6, 8, -1, -1, -1, -1, 2, 4, 6, 8 };
        private int _index, _prev;

        public void Reset(int index = 0, int prev = 0) { _index = index; _prev = prev; }

        private short decode(int code)
        {
            int step = Steps[_index];
            _index = Math.Max(0, Math.Min(88, _index + IndexAdjust[code]));
            int diff = step >> 3;
            if ((code & 1) != 0) diff += step >> 2;
            if ((code & 2) != 0) diff += step >> 1;
            if ((code & 4) != 0) diff += step;
            if ((code & 8) != 0) diff = -diff;
            _prev = Math.Max(-32768, Math.Min(32767, _prev + diff));
            return (short)_prev;
        }

        // two samples per byte, low nibble first
        public void Decode(byte[] data, int offset, int count, List<short> output)
        {
            for (int i = offset; i < offset + count; i++)
            {
                output.Add(decode(data[i] & 0x0f));
                output.Add(decode((data[i] >> 4) & 0x0f));
            }
        }
    }

    // A KiwiSDR's waterfall stream (/<stamp>/W/F, the same address and stamp as the sound stream so it shares its
    // receiver slot): asks for a span (zoom 0 is the whole 0-30 MHz, each step halves it) around a centre, and hands
    // on each line of 1024 levels in dBm. Uncompressed (wf_comp=0), a few lines a second.
    internal class KiwiWaterfallClient : IDisposable
    {
        public const int Bins = 1024;
        public event Action<float[], int, double> Line;     // dBm per bin, zoom and centre (kHz) it was asked for
        public double FullSpanKhz { get; private set; } = 30000;
        public int Zoom { get; private set; } = 9;
        public double CentreKhz { get; private set; } = 7100;

        private ClientWebSocket _ws;
        private CancellationTokenSource _cts;
        private readonly object _sendLock = new object();
        private bool _ready;

        public double SpanKhz { get { return FullSpanKhz / Math.Pow(2, Zoom); } }

        // lines a second: 1 slow .. 4 fast
        private int _speed = 3;
        public int Speed
        {
            get { return _speed; }
            set { _speed = Math.Max(1, Math.Min(4, value)); if (_ready) send("SET wf_speed=" + _speed); }
        }

        public void Connect(string wsBase, string prefix, long stamp, string callsign)
        {
            Disconnect();
            _cts = new CancellationTokenSource();
            CancellationToken ct = _cts.Token;
            Task.Run(() => run(wsBase + prefix + "/" + stamp + "/W/F", callsign, ct));
        }

        public void Disconnect()
        {
            try { _cts?.Cancel(); } catch { }
            try { _ws?.Abort(); } catch { }
            _ws = null;
            _ready = false;
        }

        public void Dispose() { Disconnect(); }

        // the span and centre; the centre is kept so the span stays inside 0 .. the receiver's bandwidth
        public void View(int zoom, double centreKhz)
        {
            Zoom = Math.Max(0, Math.Min(14, zoom));
            double half = SpanKhz / 2;
            CentreKhz = Math.Max(half, Math.Min(FullSpanKhz - half, centreKhz));
            if (_ready) sendView();
        }

        private void sendView()
        {
            // older firmware takes the start (in bins at the deepest zoom), newer the centre: both are sent
            double start = (CentreKhz - SpanKhz / 2) / FullSpanKhz * Bins * Math.Pow(2, 14);
            send(string.Format(CultureInfo.InvariantCulture, "SET zoom={0} start={1:0}", Zoom, start));
            send(string.Format(CultureInfo.InvariantCulture, "SET zoom={0} cf={1:0.000}", Zoom, CentreKhz));
        }

        private void send(string text)
        {
            ClientWebSocket ws = _ws;
            if (ws == null || ws.State != WebSocketState.Open) return;
            byte[] b = Encoding.ASCII.GetBytes(text);
            lock (_sendLock)
            {
                try { ws.SendAsync(new ArraySegment<byte>(b), WebSocketMessageType.Text, true, CancellationToken.None).Wait(5000); } catch { }
            }
        }

        private void run(string url, string callsign, CancellationToken ct)
        {
            ClientWebSocket ws = new ClientWebSocket();
            _ws = ws;
            try
            {
                ws.ConnectAsync(new Uri(url), ct).Wait(15000, ct);
                if (ws.State != WebSocketState.Open) return;
                send("SET auth t=kiwi p=");
                string ident = string.IsNullOrWhiteSpace(callsign) ? "Kainos" : callsign.Trim().ToUpperInvariant() + " (Kainos)";
                send("SET ident_user=" + Uri.EscapeDataString(ident));
                send("SET maxdb=0 mindb=-120");
                send("SET wf_speed=" + _speed);
                send("SET wf_comp=0");
                send("SET interp=13");
                _ready = true;
                sendView();

                byte[] buf = new byte[65536];
                List<byte> msg = new List<byte>(8192);
                DateTime lastKeep = DateTime.MinValue;
                while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
                {
                    if ((DateTime.UtcNow - lastKeep).TotalSeconds >= 1) { send("SET keepalive"); lastKeep = DateTime.UtcNow; }
                    msg.Clear();
                    WebSocketReceiveResult r;
                    do
                    {
                        Task<WebSocketReceiveResult> t = ws.ReceiveAsync(new ArraySegment<byte>(buf), ct);
                        if (!t.Wait(10000, ct)) return;
                        r = t.Result;
                        if (r.MessageType == WebSocketMessageType.Close) return;
                        for (int i = 0; i < r.Count; i++) msg.Add(buf[i]);
                    } while (!r.EndOfMessage);
                    if (msg.Count < 3) continue;
                    byte[] m = msg.ToArray();
                    string tag = Encoding.ASCII.GetString(m, 0, 3);
                    if (tag == "MSG") handleMsg(Encoding.UTF8.GetString(m, 4, Math.Max(0, m.Length - 4)));
                    else if (tag == "W/F") handleLine(m);
                }
            }
            catch { }
            finally
            {
                _ready = false;
                try { ws.Abort(); ws.Dispose(); } catch { }
            }
        }

        private void handleMsg(string text)
        {
            foreach (string part in text.Split(' '))
            {
                int eq = part.IndexOf('=');
                if (eq <= 0) continue;
                string key = part.Substring(0, eq), val = part.Substring(eq + 1);
                double v;
                if (key == "bandwidth" && double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out v) && v > 1e6)
                {
                    FullSpanKhz = v / 1000;
                    View(Zoom, CentreKhz);
                }
            }
        }

        // "W/F", a pad byte, the start bin, zoom and flags, a sequence number (three 32-bit), then a byte a bin:
        // the level in dB above -255
        private void handleLine(byte[] m)
        {
            int start = 4 + 12, n = m.Length - start;
            if (n < 64) return;
            float[] dbm = new float[n];
            for (int i = 0; i < n; i++) dbm[i] = m[start + i] - 255f;
            Line?.Invoke(dbm, Zoom, CentreKhz);
        }
    }

    internal class KiwiClient : IDisposable
    {
        public event Action<string> Status;                 // what the receiver says, for the tab
        public event Action<short[], int> Audio;            // samples, sample rate
        public double Rssi { get; private set; } = -127;
        public int SampleRate { get; private set; }
        public bool Connected { get; private set; }
        // where the sound stream connected (the waterfall stream joins the same slot: same address, path and stamp)
        public string WsBase { get; private set; }
        public string Prefix { get; private set; }
        public long Stamp { get; private set; }
        public event Action Ready;

        private ClientWebSocket _ws;
        private CancellationTokenSource _cts;
        private readonly KiwiAdpcm _adpcm = new KiwiAdpcm();
        private readonly object _sendLock = new object();
        private string _mode = "usb";
        private double _khz = 7074;
        private int _lowCut = 300, _highCut = 2700;
        private bool _ready;

        public void Connect(string httpUrl, string callsign, double khz, string mode)
        {
            Disconnect();
            _khz = khz;
            setModeOnly(mode);
            _cts = new CancellationTokenSource();
            Task.Run(() => run(httpUrl, callsign, _cts.Token));
        }

        public void Disconnect()
        {
            try { _cts?.Cancel(); } catch { }
            try { _ws?.Abort(); } catch { }
            _ws = null;
            _ready = false;
            Connected = false;
        }

        public void Dispose() { Disconnect(); }

        // tune (kHz, the dial frequency) and the mode ("usb", "lsb", "cw", "am", "nbfm")
        // CW: the pitch the signal is heard at (Hz); the CW passband is centred on it
        public int CwPitch = 500;

        public void Tune(double khz, string mode)
        {
            _khz = khz;
            setModeOnly(mode);
            if (_ready) sendTune();
        }

        private void setModeOnly(string mode)
        {
            _mode = (mode ?? "usb").ToLowerInvariant();
            switch (_mode)
            {
                case "lsb": _lowCut = -2700; _highCut = -300; break;
                case "cw": _lowCut = Math.Max(50, CwPitch - 250); _highCut = CwPitch + 250; break;
                case "am": _lowCut = -4900; _highCut = 4900; break;
                case "nbfm": _lowCut = -6000; _highCut = 6000; break;
                default: _mode = "usb"; _lowCut = 300; _highCut = 2700; break;
            }
        }

        private void sendTune()
        {
            send(string.Format(CultureInfo.InvariantCulture, "SET mod={0} low_cut={1} high_cut={2} freq={3:0.000}", _mode, _lowCut, _highCut, _khz));
        }

        private void send(string text)
        {
            ClientWebSocket ws = _ws;
            if (ws == null || ws.State != WebSocketState.Open) return;
            byte[] b = Encoding.ASCII.GetBytes(text);
            lock (_sendLock)
            {
                try { ws.SendAsync(new ArraySegment<byte>(b), WebSocketMessageType.Text, true, CancellationToken.None).Wait(5000); } catch { }
            }
        }

        private void say(string s) { Status?.Invoke(s); }

        // newer KiwiSDR firmware answers at /kiwi/<stamp>/SND, older at /<stamp>/SND: try one, then the other
        private void run(string httpUrl, string callsign, CancellationToken ct)
        {
            foreach (string prefix in new[] { "/kiwi", "" })
            {
                if (ct.IsCancellationRequested) return;
                if (runOnce(httpUrl, prefix, callsign, ct)) return;
            }
            if (!ct.IsCancellationRequested) say("No answer from the receiver");
        }

        // false: the receiver said nothing at this path (try the other)
        private bool runOnce(string httpUrl, string prefix, string callsign, CancellationToken ct)
        {
            ClientWebSocket ws = new ClientWebSocket();
            bool heard = false;
            _ws = ws;
            try
            {
                string wsUrl = Regex.Replace(httpUrl.TrimEnd('/'), "^http", "ws", RegexOptions.IgnoreCase);
                long stamp = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
                say("Connecting...");
                ws.ConnectAsync(new Uri(wsUrl + prefix + "/" + stamp + "/SND"), ct).Wait(15000, ct);
                if (ws.State != WebSocketState.Open) return false;
                WsBase = wsUrl; Prefix = prefix; Stamp = stamp;

                send("SET auth t=kiwi p=");
                string ident = string.IsNullOrWhiteSpace(callsign) ? "Kainos" : callsign.Trim().ToUpperInvariant() + " (Kainos)";
                send("SET ident_user=" + Uri.EscapeDataString(ident));

                byte[] buf = new byte[65536];
                List<byte> msg = new List<byte>(65536);
                List<short> pcm = new List<short>(8192);
                DateTime lastKeep = DateTime.MinValue;
                while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
                {
                    if (_ready && (DateTime.UtcNow - lastKeep).TotalSeconds >= 1) { send("SET keepalive"); lastKeep = DateTime.UtcNow; }

                    msg.Clear();
                    WebSocketReceiveResult r;
                    do
                    {
                        Task<WebSocketReceiveResult> t = ws.ReceiveAsync(new ArraySegment<byte>(buf), ct);
                        if (!t.Wait(heard ? 10000 : 4000, ct))
                        {
                            if (!heard) return false;                   // nothing at this path
                            throw new TimeoutException("the receiver went quiet");
                        }
                        r = t.Result;
                        if (r.MessageType == WebSocketMessageType.Close) { if (!heard) return false; say("Disconnected by the receiver"); return true; }
                        heard = true;
                        for (int i = 0; i < r.Count; i++) msg.Add(buf[i]);
                    } while (!r.EndOfMessage);

                    if (msg.Count < 3) continue;
                    string tag = Encoding.ASCII.GetString(msg.ToArray(), 0, 3);
                    if (tag == "MSG") handleMsg(Encoding.UTF8.GetString(msg.ToArray(), 4, Math.Max(0, msg.Count - 4)));
                    else if (tag == "SND" && msg.Count > 10) handleSnd(msg.ToArray(), pcm);
                }
            }
            catch (Exception ex)
            {
                if (!heard && !ct.IsCancellationRequested) return false;
                if (!ct.IsCancellationRequested)
                {
                    Exception e = ex is AggregateException && ex.InnerException != null ? ex.InnerException : ex;
                    say("Lost the receiver (" + e.Message + ")");
                }
            }
            finally
            {
                Connected = false;
                _ready = false;
                try { ws.Abort(); ws.Dispose(); } catch { }
            }
            return true;
        }

        private void handleMsg(string text)
        {
            foreach (string part in text.Split(' '))
            {
                int eq = part.IndexOf('=');
                string key = eq > 0 ? part.Substring(0, eq) : part, val = eq > 0 ? Uri.UnescapeDataString(part.Substring(eq + 1)) : "";
                switch (key)
                {
                    case "sample_rate":
                        double sr;
                        if (double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out sr)) SampleRate = (int)Math.Round(sr);
                        // the receiver is ready for us: compression, AGC, no squelch, and where to listen
                        send("SET compression=1");
                        send("SET agc=1 hang=0 thresh=-100 slope=6 decay=1000 manGain=50");
                        send("SET squelch=0 max=0");
                        sendTune();
                        _ready = true;
                        Connected = true;
                        say("Listening");
                        Ready?.Invoke();
                        break;
                    case "audio_rate":
                        int ar;
                        if (int.TryParse(val, out ar)) send("SET AR OK in=" + ar + " out=44100");
                        break;
                    case "audio_adpcm_state":
                        string[] st = val.Split(',');
                        int idx, prev;
                        if (st.Length == 2 && int.TryParse(st[0], out idx) && int.TryParse(st[1], out prev)) _adpcm.Reset(idx, prev);
                        break;
                    case "too_busy": say("The receiver is full: try another"); break;
                    case "badp":
                        if (val == "0") break;                      // 0: accepted
                        say(val == "1" ? "The receiver is full or needs a password" : val == "5" ? "One connection per address on this receiver" : "Refused by the receiver (" + val + ")");
                        break;
                    case "down": say("The receiver is down"); break;
                    case "inactivity_timeout": case "reason_disabled": case "kick": say("Time limit reached on this receiver"); break;
                    case "redirect": say("The receiver moved: try again from the list"); break;
                }
            }
        }

        private void handleSnd(byte[] m, List<short> pcm)
        {
            // "SND", flags, sequence (4), S-meter (2, big-endian), then audio
            int flags = m[3];
            int smeter = (m[8] << 8) | m[9];
            Rssi = 0.1 * smeter - 127;
            int start = 10, n = m.Length - start;
            if (n <= 0 || SampleRate <= 0) return;
            if ((flags & 0x08) != 0) return;                // stereo (IQ) isn't asked for
            pcm.Clear();
            if ((flags & 0x10) != 0) _adpcm.Decode(m, start, n, pcm);
            else
            {
                bool little = (flags & 0x80) != 0;
                for (int i = start; i + 1 < m.Length; i += 2)
                    pcm.Add(little ? (short)(m[i] | (m[i + 1] << 8)) : (short)((m[i] << 8) | m[i + 1]));
            }
            Audio?.Invoke(pcm.ToArray(), SampleRate);
        }
    }
}
