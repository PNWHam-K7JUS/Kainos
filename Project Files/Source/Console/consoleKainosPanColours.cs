/*  consoleKainosPanColours.cs

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

namespace Thetis
{
    // Kainos layout, the last of Phase 7: the panadapter in the Kainos colours (the waterfall keeps its own). Thetis's
    // receive panadapter colours (Setup > Appearance > Display) are swapped for Kainos's while Kainos layout is on and
    // put back in Classic: the grid and its labels, the trace and its fill, the filter shading and the TX filter
    // lines. Transmit colours are left alone. A colour changed in Setup while Kainos's are showing is the user's
    // choice and is kept (it isn't put back over).
    public partial class Console
    {
        private class KainosPanColour
        {
            public string Name;
            public Func<Color> Get;
            public Action<Color> Set;
            public Color Kainos;
        }

        private KainosPanColour[] _kpc;
        private readonly Dictionary<string, Color> _kpcSaved = new Dictionary<string, Color>();
        private bool _kpcOn;

        // Setup > Appearance > Kainos > Panadapter
        public bool KainosPanColours = true;

        private KainosPanColour[] kainosPanColourList
        {
            get
            {
                if (_kpc != null) return _kpc;
                Color ice = Color.FromArgb(0x7f, 0xb0, 0xcc), iceHi = Color.FromArgb(0xa8, 0xd8, 0xf0), gold = Color.FromArgb(0xd4, 0xad, 0x6a);
                _kpc = new[]
                {
                    // the grid: quiet navy lines, the finer ones darker
                    new KainosPanColour { Name = "grid", Get = () => Display.GridColor, Set = c => Display.GridColor = c, Kainos = Color.FromArgb(0x26, 0x3e, 0x54) },
                    new KainosPanColour { Name = "hgrid", Get = () => Display.HGridColor, Set = c => Display.HGridColor = c, Kainos = Color.FromArgb(0x26, 0x3e, 0x54) },
                    new KainosPanColour { Name = "gridfine", Get = () => Display.GridPenDark, Set = c => Display.GridPenDark = c, Kainos = Color.FromArgb(0x16, 0x27, 0x38) },
                    // the frequency and dB labels
                    new KainosPanColour { Name = "gridtext", Get = () => Display.GridTextColor, Set = c => Display.GridTextColor = c, Kainos = ice },
                    // the trace: a bright ice line over a deep blue fill
                    new KainosPanColour { Name = "line", Get = () => Display.DataLineColor, Set = c => Display.DataLineColor = c, Kainos = iceHi },
                    new KainosPanColour { Name = "fill", Get = () => Display.DataFillColor, Set = c => Display.DataFillColor = c, Kainos = Color.FromArgb(150, 0x1c, 0x4a, 0x70) },
                    new KainosPanColour { Name = "panfill", Get = () => Display.PanFillColor, Set = c => Display.PanFillColor = c, Kainos = Color.FromArgb(150, 0x1c, 0x4a, 0x70) },
                    new KainosPanColour { Name = "peaksfill", Get = () => Display.DataPeaksFillColor, Set = c => Display.DataPeaksFillColor = c, Kainos = Color.FromArgb(90, gold) },
                    // the receive filter shading, and the TX filter's lines
                    new KainosPanColour { Name = "filter", Get = () => Display.DisplayFilterColor, Set = c => Display.DisplayFilterColor = c, Kainos = Color.FromArgb(55, ice) },
                    new KainosPanColour { Name = "txfilter", Get = () => Display.DisplayFilterTXColor, Set = c => Display.DisplayFilterTXColor = c, Kainos = gold },
                };
                return _kpc;
            }
        }

        private static bool sameColour(Color a, Color b) { return a.ToArgb() == b.ToArgb(); }

        private void kainosPanColoursOn()
        {
            foreach (KainosPanColour p in kainosPanColourList)
            {
                Color cur = p.Get();
                if (!sameColour(cur, p.Kainos)) _kpcSaved[p.Name] = cur;      // the user's (or the skin's) colour, for Classic
                p.Set(p.Kainos);
            }
            _kpcOn = true;
        }

        private void kainosPanColoursOff()
        {
            if (!_kpcOn) return;
            foreach (KainosPanColour p in kainosPanColourList)
            {
                Color saved;
                // still Kainos's: put the user's back; changed in Setup meanwhile: theirs stays
                if (_kpcSaved.TryGetValue(p.Name, out saved) && sameColour(p.Get(), p.Kainos)) p.Set(saved);
            }
            _kpcSaved.Clear();
            _kpcOn = false;
        }

        // on with Kainos layout (and the setting), off otherwise
        internal void KainosPanColoursApply()
        {
            if (_kainosLayout && KainosPanColours) kainosPanColoursOn();
            else kainosPanColoursOff();
        }
    }
}
