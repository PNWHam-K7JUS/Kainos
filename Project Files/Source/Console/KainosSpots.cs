/*  KainosSpots.cs

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
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace Thetis
{
    // Kainos's built-in spotting: a DX cluster (telnet, logged in with the user's callsign) and POTA's activator spots
    // (its public web feed). Each spot is handed on through Spotted (on a background thread); the console puts them on
    // the panadapter (Thetis's spot display, SpotManager2) and in the SPOTS tab. No other program is needed.
    //
    // SOTA is not included: SOTA's API terms require prior approval before AI-generated software connects to it.

    internal class KainosSpot
    {
        public string Call, Mode, Info, Spotter, Source;
        public long Hz;
        public DateTime Utc;
    }

    // A DX cluster the SPOTS tab offers (GitHub #68). The Reverse Beacon Network's two feeds are clusters too, but
    // only of skimmers' spots: nothing can be spotted to them.
    internal class KainosCluster
    {
        public string Name, Host;
        public int Port;
        public bool Rbn;
        public KainosCluster(string name, string host, int port, bool rbn = false) { Name = name; Host = host; Port = port; Rbn = rbn; }

        public static readonly KainosCluster[] Known =
        {
            new KainosCluster("NC7J (USA)", "dxc.nc7j.com", 7373),
            new KainosCluster("W3LPL (USA)", "w3lpl.net", 7373),
            new KainosCluster("VE7CC (Canada)", "dxc.ve7cc.net", 23),
            new KainosCluster("K1TTT (USA)", "k1ttt.net", 7373),
            new KainosCluster("Reverse Beacon Network: CW and RTTY", "telnet.reversebeacon.net", 7000, true),
            new KainosCluster("Reverse Beacon Network: FT8 and FT4", "telnet.reversebeacon.net", 7001, true),
        };

        public static bool IsRbn(string host) { return (host ?? "").IndexOf("reversebeacon", StringComparison.OrdinalIgnoreCase) >= 0; }
    }

    internal static class KainosSpotting
    {
        public static event Action<KainosSpot> Spotted;
        public static event Action StatusChanged;

        public static volatile string Callsign = "";
        public static volatile string ClusterHost = "dxc.nc7j.com";
        public static volatile int ClusterPort = 7373;
        public static volatile bool ClusterOn, PotaOn;

        public static string ClusterStatus { get; private set; } = "Off";
        public static string PotaStatus { get; private set; } = "Off";

        private const string UserAgent = "Kainos/2.10.3.15 (+https://github.com/PNWHam-K7JUS/Kainos)";
        private const int PotaPollSeconds = 120;        // POTA's API runs on volunteers' goodwill: don't hammer it

        private static Thread _clusterThread, _potaThread;
        private static volatile bool _run;
        private static TcpClient _client;
        private static NetworkStream _stream;               // the logged-in connection, for sending spots (null when not)
        private static readonly object _sendLock = new object();
        private static readonly System.Collections.Generic.Dictionary<string, Tuple<double, DateTime>> _rbnSeen = new System.Collections.Generic.Dictionary<string, Tuple<double, DateTime>>();
        public static string LastSent { get; private set; } = "";
        private static string _clusterKey = "";
        private static readonly AutoResetEvent _wake = new AutoResetEvent(false);

        public static void Start()
        {
            if (_run) { Kick(); return; }
            _run = true;
            _clusterThread = new Thread(clusterLoop) { IsBackground = true, Name = "Kainos DX cluster" };
            _potaThread = new Thread(potaLoop) { IsBackground = true, Name = "Kainos POTA spots" };
            _clusterThread.Start();
            _potaThread.Start();
        }

        public static void Stop()
        {
            _run = false;
            try { _client?.Close(); } catch { }
            _wake.Set();
        }

        // settings changed: drop the cluster connection so it reconnects (or stays off) as set, and poll POTA now
        public static void Kick()
        {
            try { _client?.Close(); } catch { }
            _wake.Set();
        }

        private static void setStatus(bool cluster, string s)
        {
            if (cluster) ClusterStatus = s; else PotaStatus = s;
            StatusChanged?.Invoke();
        }

        private static void sleep(int seconds)
        {
            _wake.WaitOne(TimeSpan.FromSeconds(seconds));
        }

        // ---- DX cluster ----

        // "DX de W7XYZ:     14025.0  JA1ABC       CW 599 tnx                    2312Z"
        private static readonly Regex DxLine = new Regex(@"^DX de\s+([A-Z0-9/\-#]+):?\s+(\d+(?:\.\d+)?)\s+([A-Z0-9/]+)\s+(.*?)\s*(\d{4})Z", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static void clusterLoop()
        {
            int backoff = 15;
            while (_run)
            {
                if (!ClusterOn) { setStatus(true, "Off"); sleep(30); continue; }
                string call = (Callsign ?? "").Trim().ToUpperInvariant();
                if (call.Length < 3) { setStatus(true, "Needs your callsign (Setup, search \"callsign\")"); sleep(15); continue; }

                string host = ClusterHost, key = host + ":" + ClusterPort + "/" + call;
                _clusterKey = key;
                try
                {
                    setStatus(true, "Connecting to " + host);
                    using (TcpClient c = new TcpClient())
                    {
                        _client = c;
                        IAsyncResult ar = c.BeginConnect(host, ClusterPort, null, null);
                        if (!ar.AsyncWaitHandle.WaitOne(10000)) throw new TimeoutException("no answer");
                        c.EndConnect(ar);
                        NetworkStream s = c.GetStream();
                        s.ReadTimeout = 600000;     // a quiet cluster still sends spots within ten minutes
                        StringBuilder line = new StringBuilder();
                        byte[] buf = new byte[4096];
                        bool loggedIn = false;
                        string lastLine = "";
                        while (_run && ClusterOn && c.Connected && _clusterKey == key)
                        {
                            int n = s.Read(buf, 0, buf.Length);
                            if (n <= 0) break;
                            for (int i = 0; i < n; i++)
                            {
                                char ch = (char)buf[i];
                                if (ch == '\n' || ch == '\r')
                                {
                                    if (line.Length > 0) { lastLine = line.ToString(); handleClusterLine(lastLine); }
                                    line.Clear();
                                }
                                else if (ch >= ' ') line.Append(ch);
                            }
                            // the login prompt (usually without a line end; sometimes with)
                            if (!loggedIn && (isLoginPrompt(line.ToString()) || isLoginPrompt(lastLine)))
                            {
                                byte[] me = Encoding.ASCII.GetBytes(call + "\r\n");
                                lock (_sendLock) s.Write(me, 0, me.Length);
                                line.Clear();
                                loggedIn = true;
                                _stream = s;
                                setStatus(true, "Connected to " + host + " as " + call);
                                backoff = 15;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (_run && ClusterOn) setStatus(true, "Lost " + host + " (" + ex.Message + ")");
                }
                finally { _client = null; _stream = null; }

                if (!_run) break;
                // the server or callsign changed (the Server menu, Setup): straight back round, no waiting
                if (ClusterHost + ":" + ClusterPort + "/" + (Callsign ?? "").Trim().ToUpperInvariant() != key || !ClusterOn) continue;
                setStatus(true, "Reconnecting to " + host + " in " + backoff + " s");
                sleep(backoff);
                backoff = Math.Min(300, backoff * 2);
            }
        }

        // ---- sending a spot (GitHub #68) ----

        // A callsign as clusters take it: letters and digits with a digit in it, and / parts (DL/K7JUS/P)
        private static readonly Regex CallRx = new Regex(@"^(?=.*\d)[A-Z0-9]{1,8}(/[A-Z0-9]{1,8}){0,2}$", RegexOptions.Compiled);

        public static bool CanSpot { get { return _stream != null && !KainosCluster.IsRbn(ClusterHost); } }

        // the line a spot is sent as: "DX 14025.0 JA1ABC CW 599 tnx" (the cluster adds who spotted it and when)
        public static string SpotLine(double khz, string call, string comment)
        {
            string c = (comment ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
            // plain ASCII only, and clusters keep about 30 characters of the comment
            StringBuilder sb = new StringBuilder();
            foreach (char ch in c) if (ch >= ' ' && ch < 127) sb.Append(ch);
            c = Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
            if (c.Length > 30) c = c.Substring(0, 30).TrimEnd();
            return ("DX " + khz.ToString("0.0", CultureInfo.InvariantCulture) + " " + (call ?? "").Trim().ToUpperInvariant() + " " + c).TrimEnd();
        }

        // why a spot can't be sent as it is, or null
        public static string SpotProblem(double khz, string call)
        {
            if (KainosCluster.IsRbn(ClusterHost)) return "The Reverse Beacon Network only takes spots from its skimmers. Pick a DX cluster to spot to.";
            if (_stream == null)
            {
                if (!ClusterOn) return "Turn DX on first, so Kainos is connected to a cluster.";
                if ((Callsign ?? "").Trim().Length < 3) return "Kainos needs your callsign first: in Setup, search for \"callsign\".";
                return "Not connected to the cluster yet.";
            }
            if (!CallRx.IsMatch((call ?? "").Trim().ToUpperInvariant())) return "That doesn't look like a callsign.";
            if (khz < 100 || khz > 300000000) return "That frequency doesn't look right (it's in kHz, like 14025.0).";
            return null;
        }

        // sends it down the logged-in connection; returns why not, or null when it's gone. The cluster sends it back
        // as an ordinary spot, so it appears in the list like any other.
        public static string SendSpot(double khz, string call, string comment)
        {
            string why = SpotProblem(khz, call);
            if (why != null) return why;
            string line = SpotLine(khz, call, comment);
            try
            {
                byte[] b = Encoding.ASCII.GetBytes(line + "\r\n");
                NetworkStream s = _stream;
                if (s == null) return "Not connected to the cluster.";
                lock (_sendLock) s.Write(b, 0, b.Length);
                LastSent = line;
                setStatus(true, "Spot sent: " + line);
                return null;
            }
            catch (Exception ex) { return "Couldn't send it: " + ex.Message; }
        }

        private static bool isLoginPrompt(string s)
        {
            string t = s.Trim().ToLowerInvariant();
            return t.EndsWith("login:") || t.EndsWith("call:") || t.EndsWith("callsign:") || t.Contains("enter your call");
        }

        private static void handleClusterLine(string text)
        {
            Match m = DxLine.Match(text);
            if (!m.Success) return;
            double khz;
            if (!double.TryParse(m.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out khz)) return;
            string comment = m.Groups[4].Value.Trim();
            string hhmm = m.Groups[5].Value;
            // the Reverse Beacon Network sends the same station from every skimmer that hears it, hundreds a minute at
            // busy times: a call within 1 kHz of where it was last spotted counts once every 10 minutes
            if (KainosCluster.IsRbn(ClusterHost))
            {
                string dxCall = m.Groups[3].Value.ToUpperInvariant();
                lock (_rbnSeen)
                {
                    Tuple<double, DateTime> was;
                    if (_rbnSeen.TryGetValue(dxCall, out was) && Math.Abs(was.Item1 - khz) <= 1 && (DateTime.UtcNow - was.Item2).TotalMinutes < 10) return;
                    _rbnSeen[dxCall] = Tuple.Create(khz, DateTime.UtcNow);
                    if (_rbnSeen.Count > 5000) _rbnSeen.Clear();
                }
            }
            DateTime now = DateTime.UtcNow, utc;
            int h = int.Parse(hhmm.Substring(0, 2)), mi = int.Parse(hhmm.Substring(2, 2));
            utc = new DateTime(now.Year, now.Month, now.Day, Math.Min(23, h), Math.Min(59, mi), 0, DateTimeKind.Utc);
            if (utc > now.AddMinutes(5)) utc = utc.AddDays(-1);
            Spotted?.Invoke(new KainosSpot
            {
                Call = m.Groups[3].Value.ToUpperInvariant(),
                Hz = (long)Math.Round(khz * 1000),
                Mode = ModeFromText(comment),
                Info = comment,
                Spotter = m.Groups[1].Value.TrimEnd(':').ToUpperInvariant(),
                Source = "DX",
                Utc = utc,
            });
        }

        // ---- POTA ----

        private static void potaLoop()
        {
            using (HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) })
            {
                http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
                while (_run)
                {
                    if (!PotaOn) { setStatus(false, "Off"); sleep(30); continue; }
                    try
                    {
                        string json = http.GetStringAsync("https://api.pota.app/spot/activator").Result;
                        JArray a = JArray.Parse(json);
                        int count = 0;
                        foreach (JToken t in a)
                        {
                            if (t.Value<bool?>("invalid") == true) continue;
                            double khz;
                            if (!double.TryParse((string)t["frequency"] ?? "", NumberStyles.Float, CultureInfo.InvariantCulture, out khz) || khz <= 0) continue;
                            string reference = (string)t["reference"] ?? "", name = (string)t["name"] ?? "", where = (string)t["locationDesc"] ?? "";
                            string comments = (string)t["comments"] ?? "";
                            DateTime utc;
                            if (!DateTime.TryParse((string)t["spotTime"] ?? "", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out utc)) utc = DateTime.UtcNow;
                            Spotted?.Invoke(new KainosSpot
                            {
                                Call = ((string)t["activator"] ?? "").ToUpperInvariant(),
                                Hz = (long)Math.Round(khz * 1000),
                                Mode = ((string)t["mode"] ?? "").ToUpperInvariant(),
                                Info = ("POTA " + reference + " " + name + (where.Length > 0 ? " (" + where + ")" : "") + (comments.Length > 0 ? " " + comments : "")).Trim(),
                                Spotter = ((string)t["spotter"] ?? "").ToUpperInvariant(),
                                Source = "POTA",
                                Utc = utc,
                            });
                            count++;
                        }
                        setStatus(false, count + " activators on the air (every " + PotaPollSeconds / 60 + " min)");
                    }
                    catch (Exception ex)
                    {
                        Exception e = ex is AggregateException && ex.InnerException != null ? ex.InnerException : ex;
                        setStatus(false, "Couldn't reach POTA (" + e.Message + ")");
                    }
                    sleep(PotaPollSeconds);
                }
            }
        }

        // ---- modes ----

        // a mode named in a spot's text, else nothing (the console guesses from the frequency)
        public static string ModeFromText(string text)
        {
            string t = " " + (text ?? "").ToUpperInvariant() + " ";
            foreach (string m in new[] { "FT8", "FT4", "RTTY", "PSK", "JS8", "CW", "SSB", "USB", "LSB", "FM", "AM", "RADE", "FREEDV" })
                if (Regex.IsMatch(t, @"[\s,;]" + m + @"[\s,;0-9]")) return m;
            return "";
        }
    }
}
