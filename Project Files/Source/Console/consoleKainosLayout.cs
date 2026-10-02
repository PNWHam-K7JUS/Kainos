/*  consoleKainosLayout.cs

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

using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Thetis
{
    // The Kainos layout (roadmap Phase 7). Classic is the Thetis console exactly as the skin draws it; Kainos
    // restyles it in the splash-screen colours, and later stages add the left dock, the tabbed right column and
    // the slice flags. Nothing in Thetis's own designer files changes: switching back to Classic restores
    // everything Kainos changed.
    public partial class Console
    {
        private bool _kainosLayout;
        private Image _kainosSkinBackground;                 // the skin's background, kept while Kainos mode replaces it
        private Bitmap _kainosBackground;
        private Color _kainosSavedStatusBack, _kainosSavedBack;
        private bool _kainosBackSaved;
        private ToolStripRenderer _kainosSavedMenuRenderer, _kainosSavedStatusRenderer;
        private readonly Dictionary<ToolStripItem, Color> _kainosSavedItemColours = new Dictionary<ToolStripItem, Color>();
        // panels whose skin background image Kainos mode replaced: the image and colour, to put back in Classic
        private readonly Dictionary<Control, KeyValuePair<Image, Color>> _kainosSavedPanels = new Dictionary<Control, KeyValuePair<Image, Color>>();

        public bool KainosLayout
        {
            get { return _kainosLayout; }
            set
            {
                if (_kainosLayout == value) return;
                _kainosLayout = value;
                KainosApplyTheme();
            }
        }

        // Applies (or removes) the Kainos look. Also called after a skin is loaded, since loading a skin puts its
        // own background back.
        public void KainosApplyTheme()
        {
            if (_kainosLayout) applyKainosTheme();
            else restoreClassicTheme();
        }

        private void applyKainosTheme()
        {
            // console background: the splash navy instead of the skin's image (the skin's image is kept for Classic)
            if (_kainosBackground == null)
            {
                _kainosBackground = new Bitmap(8, 8);
                using (Graphics g = Graphics.FromImage(_kainosBackground)) g.Clear(KainosUI.Bg);
            }
            if (CachedBackgroundImage != _kainosBackground)
            {
                _kainosSkinBackground = CachedBackgroundImage;
                CachedBackgroundImage = _kainosBackground;
            }
            if (!_kainosBackSaved) { _kainosSavedBack = BackColor; _kainosBackSaved = true; }
            BackColor = KainosUI.Bg;

            // panels the skin draws with a background image (the grey and metal plates behind button groups)
            themePanels(this);

            // menu bar and status bar
            if (_kainosSavedMenuRenderer == null)
            {
                _kainosSavedMenuRenderer = menuStrip1.Renderer;
                _kainosSavedStatusRenderer = statusStripMain.Renderer;
                _kainosSavedStatusBack = statusStripMain.BackColor;
            }
            KainosToolStripRenderer renderer = new KainosToolStripRenderer();
            menuStrip1.Renderer = renderer;
            statusStripMain.Renderer = renderer;
            statusStripMain.BackColor = KainosUI.Surface;
            foreach (ToolStripItem item in menuItemsAndStatusLabels())
            {
                if (!_kainosSavedItemColours.ContainsKey(item)) _kainosSavedItemColours[item] = item.ForeColor;
                item.ForeColor = KainosUI.Text;
            }
            kainosDockOn();             // stage 2: consoleKainosDock.cs
            menuStrip1.Invalidate();
            statusStripMain.Invalidate();
            Invalidate(true);
        }

        private void restoreClassicTheme()
        {
            kainosDockOff();
            if (_kainosSkinBackground != null || CachedBackgroundImage == _kainosBackground)
            {
                CachedBackgroundImage = _kainosSkinBackground;
                _kainosSkinBackground = null;
            }
            if (_kainosSavedMenuRenderer != null)
            {
                menuStrip1.Renderer = _kainosSavedMenuRenderer;
                statusStripMain.Renderer = _kainosSavedStatusRenderer;
                statusStripMain.BackColor = _kainosSavedStatusBack;
                _kainosSavedMenuRenderer = _kainosSavedStatusRenderer = null;
            }
            if (_kainosBackSaved) { BackColor = _kainosSavedBack; _kainosBackSaved = false; }
            foreach (KeyValuePair<Control, KeyValuePair<Image, Color>> kv in _kainosSavedPanels)
            {
                kv.Key.BackgroundImage = kv.Value.Key;
                kv.Key.BackColor = kv.Value.Value;
            }
            _kainosSavedPanels.Clear();
            foreach (KeyValuePair<ToolStripItem, Color> kv in _kainosSavedItemColours) kv.Key.ForeColor = kv.Value;
            _kainosSavedItemColours.Clear();
            matchKainosMenuItems();
            menuStrip1.Invalidate();
            statusStripMain.Invalidate();
            Invalidate(true);
        }

        // Panels and group boxes with a skin background image get the Kainos panel colour instead. A skin loaded
        // in Kainos mode sets its images again, so they are saved again here (the newest skin's images win).
        private void themePanels(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                if ((c is Panel || c is GroupBox) && c.BackgroundImage != null)
                {
                    _kainosSavedPanels[c] = new KeyValuePair<Image, Color>(c.BackgroundImage, c.BackColor);
                    c.BackgroundImage = null;
                    c.BackColor = KainosUI.Panel;
                }
                if (c.HasChildren) themePanels(c);
            }
        }

        private IEnumerable<ToolStripItem> menuItemsAndStatusLabels()
        {
            foreach (ToolStripItem item in menuStrip1.Items) yield return item;
            foreach (ToolStripItem item in statusStripMain.Items) yield return item;
        }

        // The menu items Kainos adds in code take the same text colour as Thetis's own items (the designer sets
        // Thetis's to white; without this ours are drawn in the default black and look disabled)
        internal void matchKainosMenuItems()
        {
            Color c = setupToolStripMenuItem.ForeColor;
            if (kainosAudioToolStripMenuItem != null) kainosAudioToolStripMenuItem.ForeColor = c;
            if (freeDVToolStripMenuItem != null) freeDVToolStripMenuItem.ForeColor = c;
        }
    }
}
