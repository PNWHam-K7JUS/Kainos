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
                if (call.Length < 3) { setStatus(true, "Needs your callsign"); sleep(15); continue; }

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
                                s.Write(me, 0, me.Length);
                                line.Clear();
                                loggedIn = true;
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
                finally { _client = null; }

                if (!_run) break;
                if (_clusterKey != key || !ClusterOn) continue;     // settings changed: straight back round
                setStatus(true, "Reconnecting to " + host + " in " + backoff + " s");
                sleep(backoff);
                backoff = Math.Min(300, backoff * 2);
            }
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
