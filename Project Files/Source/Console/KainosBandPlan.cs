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
