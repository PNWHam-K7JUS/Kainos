/*  KainosCiv.cs

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
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading;

namespace Thetis
{
    // Band switching for an amplifier that follows an Icom radio over CI-V, such as the Xiegu GPA100 over Bluetooth.
    // The amp is the CI-V controller: about ten times a second it asks "the radio" (an IC-705 at address A4) for its
    // frequency (03) and its TX / RX state (1C 00), and switches band from the answers. Kainos answers as that radio:
    // the transmit frequency (VFO B in split, the HL2's own frequency with a transverter) and the real MOX state. It
    // also answers mode (04) and ID (19 00) in case an amp asks. Band switching only: the amp's PTT and ALC stay
    // wired (the 1C answer is truthful, but nothing should rely on it for T/R switching).
    //
    // CivResponder holds the protocol (testable without a port); KainosCivLink runs it on a serial port (the
    // Bluetooth link's outgoing COM port) on a thread of its own, reopening the port whenever the link drops.
    internal class CivResponder
    {
        public byte Address = 0xA4;                    // the radio's CI-V address (IC-705's default)
        public Func<long> FrequencyHz = () => 0;       // the transmit frequency
        public Func<bool> Transmitting = () => false;
        public int Polls;                              // answers sent
        public DateTime LastPoll = DateTime.MinValue;

        private readonly List<byte> _buf = new List<byte>();

        // bytes in, answers out
        public List<byte[]> Feed(byte[] data, int count)
        {
            List<byte[]> replies = new List<byte[]>();
            for (int i = 0; i < count; i++) _buf.Add(data[i]);
            if (_buf.Count > 4096) _buf.RemoveRange(0, _buf.Count - 1024);     // a stream that never frames
            while (true)
            {
                int start = -1;
                for (int i = 0; i + 1 < _buf.Count; i++) if (_buf[i] == 0xFE && _buf[i + 1] == 0xFE) { start = i; break; }
                if (start < 0) { if (_buf.Count > 1 && _buf[_buf.Count - 1] != 0xFE) _buf.Clear(); break; }
                int end = _buf.IndexOf(0xFD, start);
                if (end < 0) { if (start > 0) _buf.RemoveRange(0, start); break; }
                byte[] frame = _buf.GetRange(start, end - start + 1).ToArray();
                _buf.RemoveRange(0, end + 1);
                byte[] reply = Answer(frame);
                if (reply != null) replies.Add(reply);
            }
            return replies;
        }

        // FE FE <to> <from> <cmd> [data] FD: an answer to a question for us, or null
        public byte[] Answer(byte[] f)
        {
            if (f.Length < 6 || f[0] != 0xFE || f[1] != 0xFE || f[f.Length - 1] != 0xFD) return null;
            byte to = f[2], from = f[3], cmd = f[4];
            if (from == Address) return null;                       // our own frame, echoed by the link
            if (to != Address && to != 0x00) return null;           // for someone else
            int dataLen = f.Length - 6;
            byte[] data;
            switch (cmd)
            {
                case 0x03:                                          // read frequency
                    if (dataLen != 0) return null;                  // a 03 with data is someone's answer, not a question
                    data = Bcd(FrequencyHz());
                    break;
                case 0x1C:                                          // 1C 00: TX / RX
                    if (dataLen != 1 || f[5] != 0x00) return null;
                    data = new byte[] { 0x00, (byte)(Transmitting() ? 0x01 : 0x00) };
                    break;
                case 0x04:                                          // read mode: USB, filter 1
                    if (dataLen != 0) return null;
                    data = new byte[] { 0x01, 0x01 };
                    break;
                case 0x19:                                          // 19 00: ID
                    if (dataLen != 1 || f[5] != 0x00) return null;
                    data = new byte[] { 0x00, Address };
                    break;
                default:
                    return null;
            }
            Polls++;
            LastPoll = DateTime.UtcNow;
            byte[] r = new byte[6 + data.Length];
            r[0] = 0xFE; r[1] = 0xFE; r[2] = from; r[3] = Address; r[4] = cmd;
            Array.Copy(data, 0, r, 5, data.Length);
            r[r.Length - 1] = 0xFD;
            return r;
        }

        // 5 bytes of BCD, least significant first (14.074 MHz = 00 40 07 14 00)
        public static byte[] Bcd(long hz)
        {
            if (hz < 0) hz = 0;
            byte[] b = new byte[5];
            for (int i = 0; i < 5; i++)
            {
                int lo = (int)(hz % 10); hz /= 10;
                int hi = (int)(hz % 10); hz /= 10;
                b[i] = (byte)((hi << 4) | lo);
            }
            return b;
        }

        public static string Hex(byte[] b) { return string.Join(" ", b.Select(x => x.ToString("X2"))); }
    }

    internal class KainosCivLink
    {
        public readonly CivResponder Civ = new CivResponder();
        public string PortName;
        public bool Log;                                 // every frame in and out to KainosCivLog.txt
        public volatile string Status = "Off";
        public event Action StatusChanged;

        private Thread _thread;
        private volatile bool _run;
        private SerialPort _port;

        public bool Running { get { return _run; } }

        public void Start()
        {
            Stop();
            if (string.IsNullOrEmpty(PortName)) { setStatus("No COM port chosen"); return; }
            _run = true;
            _thread = new Thread(loop) { IsBackground = true, Name = "Kainos CI-V", Priority = ThreadPriority.AboveNormal };
            _thread.Start();
        }

        public void Stop()
        {
            _run = false;
            try { _port?.Close(); } catch { }
            if (_thread != null && _thread != Thread.CurrentThread) _thread.Join(2000);
            _thread = null;
            setStatus("Off");
        }

        private void setStatus(string s)
        {
            if (Status == s) return;
            Status = s;
            StatusChanged?.Invoke();
        }

        // opens the port, answers the amp until the link drops, then tries again every few seconds (the amp switched
        // off, out of range, or Bluetooth reconnecting)
        private void loop()
        {
            byte[] buf = new byte[512];
            while (_run)
            {
                try
                {
                    _port = new SerialPort(PortName, 19200, Parity.None, 8, StopBits.One) { ReadTimeout = 500, WriteTimeout = 1000 };
                    setStatus("Connecting to " + PortName + "...");
                    _port.Open();
                    setStatus("Waiting for the amp on " + PortName);
                    int pollsAtOpen = Civ.Polls;
                    while (_run)
                    {
                        int n;
                        try { n = _port.Read(buf, 0, buf.Length); }
                        catch (TimeoutException)
                        {
                            // quiet for a while: say so (the link can stay "open" with the amp switched off)
                            if ((DateTime.UtcNow - Civ.LastPoll).TotalSeconds > 3)
                                setStatus(Civ.Polls > pollsAtOpen ? "Connected on " + PortName + ", but the amp has gone quiet" : "Waiting for the amp on " + PortName);
                            continue;
                        }
                        if (n <= 0) continue;
                        if (Log) log("in ", buf, n);
                        foreach (byte[] r in Civ.Feed(buf, n))
                        {
                            _port.Write(r, 0, r.Length);
                            if (Log) log("out", r, r.Length);
                        }
                        if (Civ.Polls > pollsAtOpen) setStatus("Connected: the amp is following on " + PortName);
                    }
                }
                catch (Exception ex)
                {
                    if (!_run) break;
                    setStatus("Can't use " + PortName + " (" + ex.Message.TrimEnd('.') + "); trying again");
                }
                finally
                {
                    try { _port?.Close(); } catch { }
                    try { _port?.Dispose(); } catch { }
                    _port = null;
                }
                for (int i = 0; i < 30 && _run; i++) Thread.Sleep(100);       // 3 s before reopening
            }
        }

        private static readonly object _logLock = new object();
        private void log(string dir, byte[] b, int n)
        {
            try
            {
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpenHPSDR", "Kainos-x64", "KainosCivLog.txt");
                lock (_logLock)
                {
                    FileInfo fi = new FileInfo(path);
                    if (fi.Exists && fi.Length > 2 * 1024 * 1024) fi.Delete();         // keep it small
                    File.AppendAllText(path, DateTime.Now.ToString("HH:mm:ss.fff") + " " + dir + " " + CivResponder.Hex(b.Take(n).ToArray()) + Environment.NewLine);
                }
            }
            catch { }
        }

        // ---- finding the amp's Bluetooth port ----

        internal class PortInfo
        {
            public string Port, Name, DeviceId;
            public bool Bluetooth, Outgoing;            // Bluetooth serial; the outgoing one (it carries the device's address)
            public override string ToString() { return Port + (Name != null ? "  (" + Name + ")" : ""); }
        }

        // the COM ports, with their names, and which are Bluetooth links (Win32_PnPEntity)
        public static List<PortInfo> Ports()
        {
            List<PortInfo> list = new List<PortInfo>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT Name, PNPDeviceID FROM Win32_PnPEntity WHERE Name LIKE '%(COM%'"))
                    foreach (ManagementObject o in s.Get())
                    {
                        string name = (o["Name"] as string) ?? "", id = (o["PNPDeviceID"] as string) ?? "";
                        int a = name.LastIndexOf("(COM", StringComparison.OrdinalIgnoreCase), b = a < 0 ? -1 : name.IndexOf(')', a);
                        if (a < 0 || b < 0) continue;
                        string port = name.Substring(a + 1, b - a - 1);
                        bool bt = id.StartsWith("BTHENUM", StringComparison.OrdinalIgnoreCase);
                        string addr = bt ? btAddress(id) : null;
                        list.Add(new PortInfo { Port = port, Name = name.Substring(0, a).Trim(), DeviceId = id, Bluetooth = bt, Outgoing = addr != null && addr.Trim('0').Length > 0 });
                        seen.Add(port);
                    }
            }
            catch { }
            foreach (string p in SerialPort.GetPortNames())
                if (seen.Add(p)) list.Add(new PortInfo { Port = p });
            return list.OrderBy(p => p.Port.Length).ThenBy(p => p.Port).ToList();
        }

        // a Bluetooth serial port's device address: the 12 hex digits before "_C000..." in its device ID
        // (...&0002\8&...&0&AAAA1C041231_C00000000: outgoing; the incoming one's address is all zeros)
        private static string btAddress(string id)
        {
            int u = id.LastIndexOf('_');
            if (u < 12) return null;
            string a = id.Substring(u - 12, 12);
            return a.All(Uri.IsHexDigit) ? a.ToUpperInvariant() : null;
        }

        // the outgoing Bluetooth port of the paired device named like the amp (XG_GPA100), or null; why: what was found
        public static string FindAmpPort(string deviceName, out string why)
        {
            why = "";
            string mac = null;
            try
            {
                // the paired device itself: BTHENUM\DEV_<address>\... with its friendly name
                using (ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT Name, PNPDeviceID FROM Win32_PnPEntity WHERE PNPDeviceID LIKE 'BTHENUM\\\\DEV_%'"))
                    foreach (ManagementObject o in s.Get())
                    {
                        string name = (o["Name"] as string) ?? "", id = (o["PNPDeviceID"] as string) ?? "";
                        if (name.IndexOf(deviceName, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        int d = id.IndexOf("DEV_", StringComparison.OrdinalIgnoreCase);
                        if (d >= 0 && id.Length >= d + 16) mac = id.Substring(d + 4, 12).ToUpperInvariant();
                        break;
                    }
            }
            catch (Exception ex) { why = "Windows wouldn't list the Bluetooth devices (" + ex.Message + ")."; return null; }
            List<PortInfo> bt = Ports().Where(p => p.Bluetooth && p.Outgoing).ToList();
            if (mac != null)
            {
                PortInfo p = bt.FirstOrDefault(x => x.DeviceId.ToUpperInvariant().Contains(mac));
                if (p != null) { why = "Found " + deviceName + " on " + p.Port + "."; return p.Port; }
                why = deviceName + " is paired, but Windows hasn't made its serial port. Remove it in Bluetooth settings and pair it again.";
                return null;
            }
            if (bt.Count == 1) { why = deviceName + " isn't paired by that name, but " + bt[0].Port + " is the only outgoing Bluetooth serial port, so it's probably the amp."; return bt[0].Port; }
            why = deviceName + " isn't paired with this PC yet. Pair it in Windows's Bluetooth settings first (PIN 0000).";
            return null;
        }
    }
}
