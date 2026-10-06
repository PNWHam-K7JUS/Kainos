/*  KainosBandPlan.cs

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

using System.Collections.Generic;
using System.Linq;

namespace Thetis
{
    // Licence-aware band plans: where the operator may transmit, by country and licence class (chosen in the setup
    // wizard or Setup > Appearance > Kainos), drawn as a strip along the top of the panadapter (displayKainos.cs).
    // Only privileges Kainos is sure of are built in: the United States (FCC Part 97, HF and 6 m; 60 m's channels
    // are left out) and Canada (by qualification; Canada has no mode sub-bands in law). Elsewhere only the band
    // edges are shown, marked as such. It's a guide: the operator is responsible for knowing their privileges.
    internal static class KainosBandPlan
    {
        internal enum Kind { CwOnly, CwData, AllModes, NotYours, BandOnly }

        internal struct Seg
        {
            public double Lo, Hi;       // MHz
            public Kind Kind;
            public Seg(double lo, double hi, Kind k) { Lo = lo; Hi = hi; Kind = k; }
        }

        // what the strip's colours mean, for Setup's legend
        public const string Legend = "Gold: all modes (phone too)   Ice: CW and data   Violet: CW only   Red: in the band, not your privileges   Grey: band edges only";

        private static readonly double[][] UsBands =
        {
            new[] { 1.800, 2.000 }, new[] { 3.500, 4.000 }, new[] { 7.000, 7.300 }, new[] { 10.100, 10.150 }, new[] { 14.000, 14.350 },
            new[] { 18.068, 18.168 }, new[] { 21.000, 21.450 }, new[] { 24.890, 24.990 }, new[] { 28.000, 29.700 }, new[] { 50.000, 54.000 },
        };
        private static readonly double[][] Region1Bands =
        {
            new[] { 1.810, 2.000 }, new[] { 3.500, 3.800 }, new[] { 7.000, 7.200 }, new[] { 10.100, 10.150 }, new[] { 14.000, 14.350 },
            new[] { 18.068, 18.168 }, new[] { 21.000, 21.450 }, new[] { 24.890, 24.990 }, new[] { 28.000, 29.700 }, new[] { 50.000, 52.000 },
        };

        // the United States: Amateur Extra, General and Technician (FCC 97.301 and 97.305)
        private static List<Seg> unitedStates(string licence)
        {
            List<Seg> s = new List<Seg>();
            if (licence == "Amateur Extra")
            {
                s.Add(new Seg(1.800, 2.000, Kind.AllModes));
                s.Add(new Seg(3.500, 3.600, Kind.CwData)); s.Add(new Seg(3.600, 4.000, Kind.AllModes));
                s.Add(new Seg(7.000, 7.125, Kind.CwData)); s.Add(new Seg(7.125, 7.300, Kind.AllModes));
                s.Add(new Seg(10.100, 10.150, Kind.CwData));
                s.Add(new Seg(14.000, 14.150, Kind.CwData)); s.Add(new Seg(14.150, 14.350, Kind.AllModes));
                s.Add(new Seg(18.068, 18.110, Kind.CwData)); s.Add(new Seg(18.110, 18.168, Kind.AllModes));
                s.Add(new Seg(21.000, 21.200, Kind.CwData)); s.Add(new Seg(21.200, 21.450, Kind.AllModes));
                s.Add(new Seg(24.890, 24.930, Kind.CwData)); s.Add(new Seg(24.930, 24.990, Kind.AllModes));
                s.Add(new Seg(28.000, 28.300, Kind.CwData)); s.Add(new Seg(28.300, 29.700, Kind.AllModes));
            }
            else if (licence == "General")
            {
                s.Add(new Seg(1.800, 2.000, Kind.AllModes));
                s.Add(new Seg(3.525, 3.600, Kind.CwData)); s.Add(new Seg(3.800, 4.000, Kind.AllModes));
                s.Add(new Seg(7.025, 7.125, Kind.CwData)); s.Add(new Seg(7.175, 7.300, Kind.AllModes));
                s.Add(new Seg(10.100, 10.150, Kind.CwData));
                s.Add(new Seg(14.025, 14.150, Kind.CwData)); s.Add(new Seg(14.225, 14.350, Kind.AllModes));
                s.Add(new Seg(18.068, 18.110, Kind.CwData)); s.Add(new Seg(18.110, 18.168, Kind.AllModes));
                s.Add(new Seg(21.025, 21.200, Kind.CwData)); s.Add(new Seg(21.275, 21.450, Kind.AllModes));
                s.Add(new Seg(24.890, 24.930, Kind.CwData)); s.Add(new Seg(24.930, 24.990, Kind.AllModes));
                s.Add(new Seg(28.000, 28.300, Kind.CwData)); s.Add(new Seg(28.300, 29.700, Kind.AllModes));
            }
            else if (licence == "Technician")
            {
                s.Add(new Seg(3.525, 3.600, Kind.CwOnly));
                s.Add(new Seg(7.025, 7.125, Kind.CwOnly));
                s.Add(new Seg(21.025, 21.200, Kind.CwOnly));
                s.Add(new Seg(28.000, 28.300, Kind.CwData)); s.Add(new Seg(28.300, 28.500, Kind.AllModes));
            }
            else return null;
            // 6 m, for every class: CW only at the bottom, everything above
            s.Add(new Seg(50.000, 50.100, Kind.CwOnly)); s.Add(new Seg(50.100, 54.000, Kind.AllModes));
            return withNotYours(s, UsBands);
        }

        // Canada: Basic only above 30 MHz (6 m here); Basic with Honours and Advanced on all the HF bands (Canada
        // regulates power and qualification, not mode sub-bands)
        private static List<Seg> canada(string licence)
        {
            List<Seg> s = new List<Seg>();
            if (licence == "Basic") s.Add(new Seg(50.000, 54.000, Kind.AllModes));
            else if (licence == "Basic with Honours" || licence == "Advanced")
                foreach (double[] b in UsBands) s.Add(new Seg(b[0], b[1], Kind.AllModes));
            else return null;
            return withNotYours(s, UsBands);
        }

        // the rest of each band, where the operator may not transmit
        private static List<Seg> withNotYours(List<Seg> mine, double[][] bands)
        {
            List<Seg> all = new List<Seg>(mine);
            foreach (double[] b in bands)
            {
                double at = b[0];
                foreach (Seg s in mine.Where(x => x.Hi > b[0] && x.Lo < b[1]).OrderBy(x => x.Lo))
                {
                    if (s.Lo > at + 1e-9) all.Add(new Seg(at, s.Lo, Kind.NotYours));
                    at = System.Math.Max(at, s.Hi);
                }
                if (at < b[1] - 1e-9) all.Add(new Seg(at, b[1], Kind.NotYours));
            }
            return all.OrderBy(x => x.Lo).ToList();
        }

        // ---- what each part of the band is used for (voluntary band plans, simplified): the ARRL plan for Region 2
        // (the United States, Canada, or no country set), the IARU Region 1 plan elsewhere. Labels, and the popular
        // spot frequencies (FT8, FT4, WSPR, PSK31, SSTV, AM, QRP) ----

        internal struct Use
        {
            public double Lo, Hi;
            public string Label;
            public Use(double lo, double hi, string label) { Lo = lo; Hi = hi; Label = label; }
        }

        internal struct Spot
        {
            public double MHz;
            public string Label;
            public Spot(double mhz, string label) { MHz = mhz; Label = label; }
        }

        private static readonly Use[] UseRegion2 =
        {
            new Use(1.800, 1.840, "CW"), new Use(1.840, 1.843, "DIGITAL"), new Use(1.843, 2.000, "SSB"),
            new Use(3.500, 3.570, "CW"), new Use(3.570, 3.600, "DIGITAL"), new Use(3.600, 4.000, "SSB"),
            new Use(7.000, 7.070, "CW"), new Use(7.070, 7.125, "DIGITAL"), new Use(7.125, 7.300, "SSB"),
            new Use(10.100, 10.130, "CW"), new Use(10.130, 10.150, "DIGITAL"),
            new Use(14.000, 14.070, "CW"), new Use(14.070, 14.0995, "DIGITAL"), new Use(14.0995, 14.1005, "BEACONS"), new Use(14.1005, 14.150, "CW / DIGITAL"), new Use(14.150, 14.350, "SSB"),
            new Use(18.068, 18.100, "CW"), new Use(18.100, 18.1095, "DIGITAL"), new Use(18.1095, 18.1105, "BEACONS"), new Use(18.1105, 18.168, "SSB"),
            new Use(21.000, 21.070, "CW"), new Use(21.070, 21.110, "DIGITAL"), new Use(21.110, 21.1495, "CW"), new Use(21.1495, 21.1505, "BEACONS"), new Use(21.1505, 21.200, "CW"), new Use(21.200, 21.450, "SSB"),
            new Use(24.890, 24.910, "CW"), new Use(24.910, 24.9295, "DIGITAL"), new Use(24.9295, 24.9305, "BEACONS"), new Use(24.9305, 24.990, "SSB"),
            new Use(28.000, 28.070, "CW"), new Use(28.070, 28.190, "DIGITAL"), new Use(28.190, 28.300, "BEACONS"), new Use(28.300, 29.000, "SSB"),
            new Use(29.000, 29.200, "AM"), new Use(29.200, 29.300, "SSB"), new Use(29.300, 29.510, "SATELLITE"), new Use(29.510, 29.700, "FM"),
            new Use(50.000, 50.100, "CW / BEACONS"), new Use(50.100, 50.300, "SSB"), new Use(50.300, 50.600, "DIGITAL"), new Use(50.600, 51.000, "DIGITAL / OTHER"), new Use(51.000, 54.000, "FM"),
        };

        private static readonly Use[] UseRegion1 =
        {
            new Use(1.810, 1.838, "CW"), new Use(1.838, 1.843, "DIGITAL"), new Use(1.843, 2.000, "SSB"),
            new Use(3.500, 3.570, "CW"), new Use(3.570, 3.600, "DIGITAL"), new Use(3.600, 3.800, "SSB"),
            new Use(7.000, 7.040, "CW"), new Use(7.040, 7.060, "DIGITAL"), new Use(7.060, 7.200, "SSB"),
            new Use(10.100, 10.130, "CW"), new Use(10.130, 10.150, "DIGITAL"),
            new Use(14.000, 14.070, "CW"), new Use(14.070, 14.099, "DIGITAL"), new Use(14.099, 14.101, "BEACONS"), new Use(14.101, 14.125, "DIGITAL"), new Use(14.125, 14.350, "SSB"),
            new Use(18.068, 18.095, "CW"), new Use(18.095, 18.109, "DIGITAL"), new Use(18.109, 18.111, "BEACONS"), new Use(18.111, 18.168, "SSB"),
            new Use(21.000, 21.070, "CW"), new Use(21.070, 21.149, "DIGITAL"), new Use(21.149, 21.151, "BEACONS"), new Use(21.151, 21.450, "SSB"),
            new Use(24.890, 24.915, "CW"), new Use(24.915, 24.929, "DIGITAL"), new Use(24.929, 24.931, "BEACONS"), new Use(24.931, 24.990, "SSB"),
            new Use(28.000, 28.070, "CW"), new Use(28.070, 28.190, "DIGITAL"), new Use(28.190, 28.225, "BEACONS"), new Use(28.225, 29.000, "SSB"),
            new Use(29.000, 29.200, "AM"), new Use(29.200, 29.300, "DIGITAL"), new Use(29.300, 29.510, "SATELLITE"), new Use(29.510, 29.700, "FM"),
            new Use(50.000, 50.100, "CW / BEACONS"), new Use(50.100, 50.300, "SSB"), new Use(50.300, 50.500, "DIGITAL"), new Use(50.500, 52.000, "FM / OTHER"),
        };

        private static readonly Spot[] SpotsCommon =
        {
            new Spot(1.840, "FT8"), new Spot(3.573, "FT8"), new Spot(3.575, "FT4"), new Spot(7.074, "FT8"), new Spot(7.0475, "FT4"),
            new Spot(10.136, "FT8"), new Spot(10.140, "FT4"), new Spot(14.074, "FT8"), new Spot(14.080, "FT4"), new Spot(18.100, "FT8"), new Spot(18.104, "FT4"),
            new Spot(21.074, "FT8"), new Spot(21.140, "FT4"), new Spot(24.915, "FT8"), new Spot(24.919, "FT4"), new Spot(28.074, "FT8"), new Spot(28.180, "FT4"),
            new Spot(50.313, "FT8"), new Spot(50.318, "FT4"),
            new Spot(3.5686, "WSPR"), new Spot(7.0386, "WSPR"), new Spot(10.1387, "WSPR"), new Spot(14.0956, "WSPR"), new Spot(18.1046, "WSPR"),
            new Spot(21.0946, "WSPR"), new Spot(24.9246, "WSPR"), new Spot(28.1246, "WSPR"),
            new Spot(14.070, "PSK31"), new Spot(21.070, "PSK31"), new Spot(28.120, "PSK31"),
            new Spot(14.230, "SSTV"), new Spot(21.340, "SSTV"), new Spot(28.680, "SSTV"),
            new Spot(3.560, "QRP"), new Spot(7.030, "QRP"), new Spot(14.060, "QRP"), new Spot(21.060, "QRP"), new Spot(28.060, "QRP"),
        };

        private static readonly Spot[] SpotsRegion2 =
        {
            new Spot(3.845, "SSTV"), new Spot(7.171, "SSTV"), new Spot(3.885, "AM"), new Spot(7.290, "AM"), new Spot(14.286, "AM"), new Spot(29.000, "AM"),
            new Spot(7.070, "PSK31"),
        };

        private static readonly Spot[] SpotsRegion1 =
        {
            new Spot(3.735, "SSTV"), new Spot(7.165, "SSTV"), new Spot(7.040, "PSK31"),
        };

        public static Use[] UsesFor(string country) { return isRegion2(country) ? UseRegion2 : UseRegion1; }
        public static Spot[] SpotsFor(string country) { return SpotsCommon.Concat(isRegion2(country) ? SpotsRegion2 : SpotsRegion1).ToArray(); }
        private static bool isRegion2(string country) { return string.IsNullOrEmpty(country) || country == "United States" || country == "Canada"; }

        // the plan for a country and licence class, and whether it's the full privileges (false: band edges only)
        public static List<Seg> For(string country, string licence, out bool privileges)
        {
            List<Seg> s = null;
            if (country == "United States") s = unitedStates(licence);
            else if (country == "Canada") s = canada(licence);
            privileges = s != null;
            if (s != null) return s;
            bool r2 = string.IsNullOrEmpty(country) || country == "United States" || country == "Canada";
            return (r2 ? UsBands : Region1Bands).Select(b => new Seg(b[0], b[1], Kind.BandOnly)).ToList();
        }
    }
}
