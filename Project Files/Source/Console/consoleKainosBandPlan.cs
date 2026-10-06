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

using System.Collections.Generic;

namespace Thetis
{
    // The licence-aware band plan (KainosBandPlan.cs) for the display: the segments for the chosen country and
    // licence class, and where they fall on a receiver's panadapter
    public partial class Console
    {
        internal bool KainosBandPlanOn = true;
        internal bool KainosBandPlanPrivileges { get; private set; }
        private volatile List<KainosBandPlan.Seg> _kainosBandPlan;

        internal void KainosBandPlanSet(string country, string licence)
        {
            bool priv;
            _kainosBandPlan = KainosBandPlan.For(country, licence, out priv);
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
    }
}
