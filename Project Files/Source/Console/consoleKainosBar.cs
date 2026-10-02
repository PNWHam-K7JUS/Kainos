/*  consoleKainosBar.cs

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
using System.Linq;
using System.Windows.Forms;

namespace Thetis
{
    // Kainos layout, stage 5: the panadapter's bottom bar. One row under the spectrum, in Kainos's style, in place of
    // Thetis's pan / zoom row and its display, multi-RX and mixer panels under the panadapter (collapsed):
    //   PAN [Center] | MAIN pan, SUB pan, [SubRX] [Swap] | display mode, [AVG] [Peak] [CTUN] ... ZOOM [ZTB 0.5x 1x 2x 4x]
    // Every part is bound to the Thetis control it replaces (sliders set the PrettyTrackBar and raise its Scroll,
    // buttons are real clicks, the display mode sets the combo box), so Thetis's own handlers do the work.
    public partial class Console
    {
        private Panel _kainosBar;
        private KainosSlider _kbPan, _kbPanMain, _kbPanSub, _kbZoom;
        private KainosButtonGrid _kbCenter, _kbRx, _kbDisp, _kbZoomButtons;
        private KainosDropDown _kbMode;
        private KainosButtonGrid _kbInfo1, _kbInfo2;

        // Thetis's row (inside panelDisplay) that the bar takes over
        private Control[] kainosBarReplaces
        {
            get
            {
                return new Control[] { lblDisplayPan, ptbDisplayPan, btnDisplayPanCenter, lblDisplayZoom, ptbDisplayZoom,
                                       radDisplayZoom05, radDisplayZoom1x, radDisplayZoom2x, radDisplayZoom4x, btnDisplayZTB };
            }
        }

        private void kainosBarOn()
        {
            if (_kainosBar == null)
            {
                _kainosBar = new Panel { Name = "kainosBar", BackColor = KainosUI.Surface };
                _kbPan = new KainosSlider(ptbDisplayPan, "PAN", true);
                _kbCenter = new KainosButtonGrid(1, KainosUI.Tone.Ice);
                _kbPanMain = new KainosSlider(ptbPanMainRX, "MAIN", true);
                _kbPanSub = new KainosSlider(ptbPanSubRX, "SUB", true);
                _kbRx = new KainosButtonGrid(2, KainosUI.Tone.Ice);
                _kbMode = new KainosDropDown(comboDisplayMode);
                _kbDisp = new KainosButtonGrid(3, KainosUI.Tone.Ice);
                _kbZoom = new KainosSlider(ptbDisplayZoom, "ZOOM", true);
                _kbZoomButtons = new KainosButtonGrid(5, KainosUI.Tone.Gold);
                _kainosBar.Controls.AddRange(new Control[] { _kbPan, _kbCenter, _kbPanMain, _kbPanSub, _kbRx, _kbMode, _kbDisp, _kbZoom, _kbZoomButtons });
            }
            _kbCenter.SetTargets(new ButtonBase[] { btnDisplayPanCenter });
            _kbRx.SetTargets(new ButtonBase[] { chkEnableMultiRX, chkPanSwap });
            _kbDisp.SetTargets(new ButtonBase[] { chkDisplayAVG, chkDisplayPeak, chkFWCATU });
            _kbZoomButtons.SetTargets(new ButtonBase[] { btnDisplayZTB, radDisplayZoom05, radDisplayZoom1x, radDisplayZoom2x, radDisplayZoom4x });
            if (_kainosBar.Parent != panelDisplay) panelDisplay.Controls.Add(_kainosBar);
            // 1 x 1 under the bar, not zero: Thetis's skin loader sizes a radio button's image list from the button,
            // and an empty size there is a fatal error
            foreach (Control c in kainosBarReplaces) kainosCollapse(c, full => new Size(1, 1));
            _kainosBar.Visible = true;
            kainosInfoBarOn();
        }

        private void kainosBarOff()
        {
            if (_kainosBar != null) _kainosBar.Visible = false;
            if (_kbInfo1 != null) { _kbInfo1.Visible = false; _kbInfo2.Visible = false; }
        }

        // The info bar's two buttons (Blobs and Peak by default; Thetis lets each be set to other actions) in Kainos's
        // style: Kainos buttons laid over Thetis's, bound to them (a click toggles Thetis's, a right click opens its
        // menu of actions), following their place, size and visibility
        private void kainosInfoBarOn()
        {
            if (infoBar == null) return;
            if (_kbInfo1 == null)
            {
                _kbInfo1 = new KainosButtonGrid(1, KainosUI.Tone.Ice);
                _kbInfo2 = new KainosButtonGrid(1, KainosUI.Tone.Ice);
                foreach (KeyValuePair<KainosButtonGrid, CheckBox> kv in new[] { new KeyValuePair<KainosButtonGrid, CheckBox>(_kbInfo1, infoBar.Button1), new KeyValuePair<KainosButtonGrid, CheckBox>(_kbInfo2, infoBar.Button2) })
                {
                    KainosButtonGrid proxy = kv.Key;
                    CheckBox target = kv.Value;
                    proxy.SetTargets(new ButtonBase[] { target });
                    EventHandler follow = (s, e) => kainosInfoProxyPlace(proxy, target);
                    target.LocationChanged += follow;
                    target.SizeChanged += follow;
                    target.VisibleChanged += follow;
                    infoBar.Controls.Add(proxy);
                }
            }
            kainosInfoProxyPlace(_kbInfo1, infoBar.Button1);
            kainosInfoProxyPlace(_kbInfo2, infoBar.Button2);
        }

        private void kainosInfoProxyPlace(KainosButtonGrid proxy, CheckBox target)
        {
            bool on = _kainosBar != null && _kainosBar.Visible && target.Visible;
            if (on)
            {
                proxy.BackColor = infoBar.BackColor;
                proxy.SetBounds(target.Left, target.Top + 1, target.Width, Math.Max(1, target.Height - 2));
                proxy.BringToFront();
            }
            if (proxy.Visible != on) proxy.Visible = on;
        }

        // the bar fills the bottom of panelDisplay under the info bar; the sliders share what the fixed parts leave
        private void positionKainosBar()
        {
            if (_kainosBar == null || !_kainosBar.Visible) return;
            int h = KainosUI.S(24);
            int top = Math.Max(0, panelDisplay.Height - h - 2);
            if (infoBar != null && infoBar.Visible && infoBar.Bottom <= top + 2) top = Math.Max(top, infoBar.Bottom + 1);
            _kainosBar.SetBounds(0, top, panelDisplay.Width, panelDisplay.Height - top);
            _kainosBar.BringToFront();

            int gap = KainosUI.S(6), sep = KainosUI.S(16), y = (_kainosBar.Height - h) / 2;
            int center = KainosUI.S(58), rx = KainosUI.S(112), mode = KainosUI.S(118), disp = KainosUI.S(150), zoomButtons = KainosUI.S(210);
            int fixedW = gap + center + sep + gap * 2 + rx + sep + mode + gap + disp + sep + gap + zoomButtons + gap;
            // pan 190, main / sub 120 each, zoom 170 when there's room; less (to a minimum) when there isn't
            float want = KainosUI.S(190) + KainosUI.S(120) * 2 + KainosUI.S(170);
            float f = Math.Max(0.35f, Math.Min(1f, (_kainosBar.Width - fixedW - gap * 4) / want));
            int pan = (int)(KainosUI.S(190) * f), sub = (int)(KainosUI.S(120) * f), zoom = (int)(KainosUI.S(170) * f);

            int x = gap;
            _kbPan.SetBounds(x, y, pan, h); x += pan + gap;
            _kbCenter.SetBounds(x, y, center, h); x += center + sep;
            _kbPanMain.SetBounds(x, y, sub, h); x += sub + gap;
            _kbPanSub.SetBounds(x, y, sub, h); x += sub + gap;
            _kbRx.SetBounds(x, y, rx, h); x += rx + sep;
            _kbMode.SetBounds(x, y, mode, h); x += mode + gap;
            _kbDisp.SetBounds(x, y, disp, h);
            int r = _kainosBar.Width - gap;
            _kbZoomButtons.SetBounds(r - zoomButtons, y, zoomButtons, h); r -= zoomButtons + gap;
            _kbZoom.SetBounds(r - zoom, y, zoom, h);
        }
    }
}
