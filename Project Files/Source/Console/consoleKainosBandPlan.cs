/*  consoleKainosBandPlan.cs

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

using System;
using System.Collections.Generic;
using System.Linq;

namespace Thetis
{
    // The licence-aware band plan (KainosBandPlan.cs) for the display: the segments for the chosen country and
    // licence class, and where they fall on a receiver's panadapter
    public partial class Console
    {
        internal bool KainosBandPlanOn = true;
        internal bool KainosBandPlanPrivileges { get; private set; }
        private volatile List<KainosBandPlan.Seg> _kainosBandPlan;
        private volatile KainosBandPlan.Use[] _kainosBandUse;
        private volatile KainosBandPlan.Spot[] _kainosBandSpots;

        internal void KainosBandPlanSet(string country, string licence)
        {
            bool priv;
            _kainosBandPlan = KainosBandPlan.For(country, licence, out priv);
            _kainosBandUse = KainosBandPlan.UsesFor(country);
            _kainosBandSpots = KainosBandPlan.SpotsFor(country);
            KainosBandPlanPrivileges = priv;
        }

        // pixel spans (left, right, kind) on rx's panadapter, W wide; null when it's off (or in Classic)
        internal List<KeyValuePair<float[], KainosBandPlan.Kind>> KainosBandPlanSpans(int rx, int W)
        {
            List<KainosBandPlan.Seg> plan = _kainosBandPlan;
            if (!KainosBandPlanOn || !_kainosLayout || plan == null || W <= 0) return null;
            double centre = rx == 1 ? CentreFrequency : CentreRX2Frequency;
            List<KeyValuePair<float[], KainosBandPlan.Kind>> spans = new List<KeyValuePair<float[], KainosBandPlan.Kind>>();
            foreach (KainosBandPlan.Seg s in plan)
            {
                float x0 = HzToPixel((float)((s.Lo - centre) * 1e6), rx), x1 = HzToPixel((float)((s.Hi - centre) * 1e6), rx);
                if (x1 < 0 || x0 > W) continue;
                spans.Add(new KeyValuePair<float[], KainosBandPlan.Kind>(new[] { System.Math.Max(0, x0), System.Math.Min(W, x1) }, s.Kind));
            }
            return spans;
        }

        // for the Kiwi view (its own frequency span): the segments, uses and spot frequencies; null when it's off
        internal List<KainosBandPlan.Seg> KainosBandPlanSegments { get { return KainosBandPlanOn ? _kainosBandPlan : null; } }
        internal KainosBandPlan.Use[] KainosBandUses { get { return KainosBandPlanOn ? _kainosBandUse : null; } }
        internal KainosBandPlan.Spot[] KainosBandSpotFreqs { get { return KainosBandPlanOn ? _kainosBandSpots : null; } }

        private float kainosBandX(double mhz, int rx)
        {
            double centre = rx == 1 ? CentreFrequency : CentreRX2Frequency;
            return HzToPixel((float)((mhz - centre) * 1e6), rx);
        }

        // what each part of the band in view is used for: pixel span, label, and whether it's outside the operator's
        // privileges (the band's colour shows those)
        internal List<Tuple<float, float, string, bool>> KainosBandUseSpans(int rx, int W)
        {
            KainosBandPlan.Use[] uses = _kainosBandUse;
            List<KainosBandPlan.Seg> plan = _kainosBandPlan;
            if (!KainosBandPlanOn || !_kainosLayout || uses == null || W <= 0) return null;
            List<Tuple<float, float, string, bool>> list = new List<Tuple<float, float, string, bool>>();
            foreach (KainosBandPlan.Use u in uses)
            {
                float x0 = kainosBandX(u.Lo, rx), x1 = kainosBandX(u.Hi, rx);
                if (x1 < 0 || x0 > W) continue;
                // "not yours" judged at the middle of the part that's on screen
                float a = Math.Max(0, x0), b = Math.Min(W, x1);
                double mid = u.Lo + (u.Hi - u.Lo) * (((a + b) / 2 - x0) / Math.Max(1f, x1 - x0));
                bool notYours = plan != null && plan.Any(s => s.Kind == KainosBandPlan.Kind.NotYours && mid >= s.Lo && mid < s.Hi);
                list.Add(Tuple.Create(x0, x1, u.Label, notYours));
            }
            return list;
        }

        // the popular spot frequencies in view (FT8, FT4, WSPR ...): x and label, left to right
        internal List<KeyValuePair<float, string>> KainosBandSpots(int rx, int W)
        {
            KainosBandPlan.Spot[] spots = _kainosBandSpots;
            if (!KainosBandPlanOn || !_kainosLayout || spots == null || W <= 0) return null;
            List<KeyValuePair<float, string>> list = new List<KeyValuePair<float, string>>();
            foreach (KainosBandPlan.Spot s in spots)
            {
                float x = kainosBandX(s.MHz, rx);
                if (x >= 0 && x <= W) list.Add(new KeyValuePair<float, string>(x, s.Label));
            }
            list.Sort((p, q) => p.Key.CompareTo(q.Key));
            return list;
        }
    }
}
