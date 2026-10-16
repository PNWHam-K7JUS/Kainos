/*  KainosSetupShell.cs

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
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Thetis
{
    // Setup in the style of AetherSDR's Radio Setup: a "Search settings" box along the top, the pages in a list down
    // the left (Setup's tabs are its headings, their tabs its pages; the heading of the page shown is open), and the
    // page itself on the right under its name. Setup's own pages are used as they are: the tab strips of Setup and of
    // its second row are hidden and the list chooses the tab, so every setting, handler and ShowSetupTab works as
    // before. Applied by KainosWindowTheme each time Setup is themed (it's idempotent).
    internal static class KainosSetupShell
    {
        private class Shell
        {
            public Setup Form;
            public TabControl Top;
            public TextBox Search;
            public Panel SearchHost;
            public Panel NavHost;
            public NavList Nav;
            public PageTitle Title;
            public Point OrigTop;
            public int Dx, Dy;
            public readonly Dictionary<Control, Point> Moved = new Dictionary<Control, Point>();     // and where Setup had each
            public Size OrigTopSize;
            // the pages' controls as Setup laid them out, and where Kainos last put them (spread over the bigger page)
            public readonly Dictionary<Control, Size> PageSize0 = new Dictionary<Control, Size>();
            public readonly Dictionary<Control, Rectangle> Orig = new Dictionary<Control, Rectangle>();
            public readonly Dictionary<Control, Point> Placed = new Dictionary<Control, Point>();
        }

        private static readonly Dictionary<Setup, Shell> _shells = new Dictionary<Setup, Shell>();

        private const int Pad = 8, SearchH = 28, NavW = 196, Gap = 8, TitleH = 40;
        // the page area, bigger than Setup's own by this much, with the sections spread over it (more room between them)
        private const float Grow = 1.14f;

        public static void Apply(Setup f)
        {
            if (f == null || f.IsDisposed || f.TabSetup == null) return;
            Shell s;
            if (!_shells.TryGetValue(f, out s))
            {
                s = build(f);
                if (s == null) return;
                _shells[f] = s;
                f.Disposed += (o, e) => _shells.Remove(f);
            }
            layout(s);
            hideStrips(s);
            foreach (TabPage top in s.Top.TabPages) spread(s, top);
            s.Nav.Rebuild();
            shotsStart(s);      // TEMPORARY
        }

        // TEMPORARY: KAINOS_SETUP_SHOTS=<folder> saves pictures of some pages and a search, for review
        private static bool _shots;
        private static void shotsStart(Shell s)
        {
            string dir = Environment.GetEnvironmentVariable("KAINOS_SETUP_SHOTS");
            if (string.IsNullOrEmpty(dir) || _shots) return;
            _shots = true;
            string[][] plan = {
                new[] { "General", "H/W Select" }, new[] { "General", "Options" }, new[] { "Audio", "" }, new[] { "Display", "" },
                new[] { "DSP", "" }, new[] { "Transmit", "" }, new[] { "Appearance", "Kainos" }, new[] { "Serial", "Amplifier" },
                new[] { "?search", "vox" } };
            int i = -1;
            Timer t = new Timer { Interval = 1500 };
            t.Tick += (o, e) =>
            {
                try
                {
                    if (i >= 0)
                    {
                        Rectangle r = Screen.PrimaryScreen.Bounds;
                        using (Bitmap b = new Bitmap(r.Width, r.Height))
                        {
                            using (Graphics g = Graphics.FromImage(b)) g.CopyFromScreen(r.Location, Point.Empty, r.Size);
                            b.Save(System.IO.Path.Combine(dir, "setup_" + i + ".png"));
                        }
                    }
                    i++;
                    if (i >= plan.Length) { t.Stop(); return; }
                    s.Form.Activate();
                    string[] step = plan[i];
                    if (step[0] == "?search") { s.Search.Text = step[1]; s.Search.Focus(); s.Nav.OpenFirstMatch(); return; }
                    TabPage top = s.Top.TabPages.Cast<TabPage>().FirstOrDefault(p => p.Text.StartsWith(step[0], StringComparison.OrdinalIgnoreCase));
                    if (top == null) return;
                    TabControl sub = SubOf(top);
                    TabPage page = sub == null ? top : sub.TabPages.Cast<TabPage>().FirstOrDefault(p => step[1].Length > 0 && p.Text.IndexOf(step[1], StringComparison.OrdinalIgnoreCase) >= 0) ?? (sub.TabCount > 0 ? sub.TabPages[0] : top);
                    Open(s, top, page);
                }
                catch (Exception ex) { System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "setup_error.txt"), ex.ToString()); t.Stop(); }
            };
            t.Start();
        }

        private static int S(Control c, int px) { return (int)Math.Round(px * c.DeviceDpi / 96.0); }

        private static Shell build(Setup f)
        {
            Shell s = new Shell { Form = f, Top = f.TabSetup };
            s.OrigTop = s.Top.Location;
            s.OrigTopSize = s.Top.Size;
            foreach (TabPage top in s.Top.TabPages) remember(s, top);

            s.Search = new TextBox
            {
                Name = "kainosSetupSearch",
                BorderStyle = BorderStyle.None,
                BackColor = KainosWindowTheme.FieldBg,
                ForeColor = KainosWindowTheme.Text,
                Font = new Font("Segoe UI", 10f),
            };
            // the box: a dark rounded frame around a borderless text box (a plain text box's border is light)
            s.SearchHost = new Panel { Name = "kainosSetupSearchHost", BackColor = KainosWindowTheme.WindowBg, Tag = "kainos" };
            s.SearchHost.Paint += (o, e) =>
            {
                Rectangle r = new Rectangle(0, 0, s.SearchHost.Width - 1, s.SearchHost.Height - 1);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath path = roundRect(r, 5 * s.SearchHost.DeviceDpi / 96f))
                {
                    using (Brush b = new SolidBrush(KainosWindowTheme.FieldBg)) e.Graphics.FillPath(b, path);
                    using (Pen p = new Pen(s.Search.Focused ? KainosWindowTheme.Accent : KainosWindowTheme.Border)) e.Graphics.DrawPath(p, path);
                }
            };
            s.SearchHost.Controls.Add(s.Search);
            s.SearchHost.Click += (o, e) => s.Search.Focus();
            s.Search.GotFocus += (o, e) => s.SearchHost.Invalidate();
            s.Search.LostFocus += (o, e) => s.SearchHost.Invalidate();
            s.Search.HandleCreated += (o, e) => SendMessage(s.Search.Handle, 0x1501 /* EM_SETCUEBANNER */, (IntPtr)1, "Search settings (Ctrl+F)");
            s.Search.TextChanged += (o, e) => s.Nav.Filter(s.Search.Text);
            s.Search.KeyDown += (o, e) =>
            {
                if (e.KeyCode == Keys.Enter) { s.Nav.OpenFirstMatch(); e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.Escape) { s.Search.Text = ""; e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.Down) { s.Nav.Focus(); e.SuppressKeyPress = true; }
            };

            s.NavHost = new Panel { Name = "kainosSetupNavHost", BackColor = KainosWindowTheme.PanelBg, Tag = "kainos" };
            s.Nav = new NavList(s) { Name = "kainosSetupNav", Dock = DockStyle.Fill };
            s.NavHost.Controls.Add(s.Nav);

            s.Title = new PageTitle { Name = "kainosSetupTitle" };

            f.Controls.Add(s.SearchHost);
            f.Controls.Add(s.NavHost);
            f.Controls.Add(s.Title);

            // Ctrl+F: to the search box
            f.KeyPreview = true;
            f.KeyDown += (o, e) => { if (e.Control && e.KeyCode == Keys.F) { s.Search.Focus(); s.Search.SelectAll(); e.SuppressKeyPress = true; } };

            // the page shown, whichever way it was chosen (the list, ShowSetupTab, Thetis's own code)
            EventHandler changed = (o, e) => { s.Nav.Rebuild(); s.Title.Set(s); };
            s.Top.SelectedIndexChanged += changed;
            ControlEventHandler pagesChanged = (o, e) => f.BeginInvoke(new Action(() => { if (!f.IsDisposed) { hideStrips(s); s.Nav.Rebuild(); s.Title.Set(s); } }));
            s.Top.ControlAdded += pagesChanged;
            s.Top.ControlRemoved += pagesChanged;
            foreach (TabControl sub in subTabs(s))
            {
                sub.SelectedIndexChanged += changed;
                sub.ControlAdded += pagesChanged;
                sub.ControlRemoved += pagesChanged;
            }
            return s;
        }

        // ---- the pages, spread over the bigger area ----

        // a page and its controls as Setup laid them out (and any tab control's pages within it, which grow too)
        private static void remember(Shell s, TabPage page)
        {
            if (s.PageSize0.ContainsKey(page)) return;
            s.PageSize0[page] = page.ClientSize;
            page.Resize += (o, e) => spread(s, page);       // a page takes its new size a moment after its tab control
            foreach (Control c in page.Controls)
            {
                s.Orig[c] = c.Bounds;
                TabControl tc = c as TabControl;
                if (tc != null) foreach (TabPage p in tc.TabPages) remember(s, p);
            }
        }

        // each control at the same proportional place on the bigger page; sections keep their size, so only the gaps
        // between them grow. A control Thetis has moved since is taken from where it put it. The tab control of
        // Setup's second row (held, filling the page) is left to fill it.
        private static void spread(Shell s, TabPage page)
        {
            Size s0;
            bool known = s.PageSize0.TryGetValue(page, out s0);
            Size s1 = page.ClientSize;
            if (!known || s0.Width < 50 || s0.Height < 50) return;
            float fx = s1.Width / (float)s0.Width, fy = s1.Height / (float)s0.Height;
            if (fx < 1.01f && fy < 1.01f) return;
            fx = Math.Min(fx, Grow + 0.05f); fy = Math.Min(fy, Grow + 0.05f);
            foreach (Control c in page.Controls.Cast<Control>().ToList())
            {
                if (c is Holder)        // Setup's second row: its pages are spread in their turn
                {
                    foreach (TabControl sub in c.Controls.OfType<TabControl>()) foreach (TabPage p in sub.TabPages) spread(s, p);
                    continue;
                }
                if (c.Dock != DockStyle.None)
                {
                    // a tab control filling the page grows with it: its pages are spread in their turn
                    if (c is TabControl) foreach (TabPage p in ((TabControl)c).TabPages) spread(s, p);
                    continue;
                }
                Rectangle o;
                Point placed;
                if (!s.Orig.TryGetValue(c, out o)) continue;
                if (s.Placed.TryGetValue(c, out placed) && c.Location != placed)
                {
                    // Thetis moved it (in Setup's own layout): from there
                    o = new Rectangle(c.Location, o.Size);
                    s.Orig[c] = o;
                }
                Point want = new Point((int)Math.Round(o.X * fx), (int)Math.Round(o.Y * fy));
                TabControl tc = c as TabControl;
                if (tc != null)
                {
                    // a tab control on the page (a third row): bigger by as much, its pages spread too
                    Size big = new Size((int)Math.Round(o.Width * fx), (int)Math.Round(o.Height * fy));
                    if (tc.Size != big) tc.Size = big;
                    if (c.Location != want) c.Location = want;
                    s.Placed[c] = want;
                    foreach (TabPage p in tc.TabPages) spread(s, p);
                    continue;
                }
                if (c.Size != o.Size && (c.Anchor & (AnchorStyles.Left | AnchorStyles.Right)) == (AnchorStyles.Left | AnchorStyles.Right)
                    || c.Size != o.Size && (c.Anchor & (AnchorStyles.Top | AnchorStyles.Bottom)) == (AnchorStyles.Top | AnchorStyles.Bottom))
                    c.Size = o.Size;      // stretched by its anchors as the page grew: back to Setup's size
                if (c.Location != want) c.Location = want;
                s.Placed[c] = want;
            }
        }

        // the second row: a tab control filling a top page (General > H/W Select, Options ...)
        internal static TabControl SubOf(TabPage top)
        {
            return top.Controls.OfType<TabControl>().FirstOrDefault()
                   ?? top.Controls.OfType<Holder>().SelectMany(h => h.Controls.OfType<TabControl>()).FirstOrDefault();     // held (below)
        }
        private static IEnumerable<TabControl> subTabs(Shell s) { return s.Top.TabPages.Cast<TabPage>().Select(SubOf).Where(t => t != null); }

        // a tab control with no visible tabs: flat, one pixel high, nothing drawn by the theme; and held in a panel the
        // size of its page, offset so only the page shows (the strip and the edges Windows draws round the page, light
        // whatever the colours, fall outside the panel)
        private static void hideStrips(Shell s)
        {
            foreach (TabControl tc in new[] { s.Top }.Concat(subTabs(s)).ToList())
            {
                tc.Tag = "kainos-shell";
                if (tc.DrawMode != TabDrawMode.Normal) tc.DrawMode = TabDrawMode.Normal;
                if (tc.Appearance != TabAppearance.FlatButtons) tc.Appearance = TabAppearance.FlatButtons;
                if (tc.SizeMode != TabSizeMode.Fixed) tc.SizeMode = TabSizeMode.Fixed;
                if (tc.ItemSize != new Size(0, 1)) tc.ItemSize = new Size(0, 1);
                hold(tc, tc != s.Top);      // the second row fills its page (and grows with it)
            }
        }

        private class Holder : Panel { }

        private static void hold(TabControl tc, bool fillPage)
        {
            Holder h = tc.Parent as Holder;
            if (h == null)
            {
                Control parent = tc.Parent;
                if (parent == null) return;
                h = new Holder { Name = "kainosHold_" + tc.Name, Bounds = tc.Bounds, Anchor = tc.Anchor, BackColor = KainosWindowTheme.WindowBg, Tag = "kainos" };
                int z = parent.Controls.GetChildIndex(tc);
                parent.SuspendLayout();
                parent.Controls.Add(h);
                parent.Controls.SetChildIndex(h, z);
                h.Dock = fillPage ? DockStyle.Fill : tc.Dock;       // a tab control that filled its page: the holder does now
                tc.Dock = DockStyle.None;
                tc.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                h.Controls.Add(tc);
                parent.ResumeLayout();
                h.Resize += (o, e) => fit(tc);
                tc.SelectedIndexChanged += (o, e) => fit(tc);
                // the page moves up once Windows has shrunk the hidden strip: fit again then
                tc.Layout += (o, e) => fit(tc);
                tc.HandleCreated += (o, e) => tc.BeginInvoke(new Action(() => fit(tc)));
            }
            fit(tc);
            if (tc.IsHandleCreated) tc.BeginInvoke(new Action(() => fit(tc)));
        }

        // the tab control placed so its page fills the holder exactly
        private static void fit(TabControl tc)
        {
            Holder h = tc.Parent as Holder;
            if (h == null) return;
            Rectangle page = tc.SelectedTab != null ? tc.SelectedTab.Bounds : tc.DisplayRectangle;
            int l = page.Left, tp = page.Top, r = tc.Width - page.Right, b = tc.Height - page.Bottom;
            if (l < 0 || tp < 0 || r < 0 || b < 0 || l > 40 || tp > 40 || r > 40 || b > 40) return;      // not laid out yet
            Rectangle want = new Rectangle(-l, -tp, h.Width + l + r, h.Height + tp + b);
            if (tc.Bounds != want) tc.Bounds = want;
        }

        // search on top, the list on the left, the page's name over the page; everything else of Setup's moves with the
        // page (OK / Cancel / Apply, the TX profile warning, and the spare controls Thetis keeps out of sight beyond its
        // edges, which stay out of sight)
        private static void layout(Shell s)
        {
            Setup f = s.Form;
            f.SuspendLayout();
            try
            {
                int pad = S(f, Pad), gap = S(f, Gap), navW = S(f, NavW), searchH = S(f, SearchH), titleH = S(f, TitleH);
                int contentLeft = pad + navW + gap, contentTop = pad + searchH + gap;
                int dx = contentLeft - s.OrigTop.X, dy = contentTop + titleH - s.OrigTop.Y;
                int contentW = (int)Math.Round(s.OrigTopSize.Width * Grow), contentH = (int)Math.Round(s.OrigTopSize.Height * Grow);
                int extraW = contentW - s.OrigTopSize.Width, extraH = contentH - s.OrigTopSize.Height;
                Func<Point, Point> place = o => new Point(o.X + dx + (o.X > s.OrigTop.X + s.OrigTopSize.Width / 2 ? extraW : 0),
                                                          o.Y + dy + (o.Y >= s.OrigTop.Y + s.OrigTopSize.Height - 2 ? extraH : 0));
                foreach (Control c in f.Controls.Cast<Control>().ToList())
                {
                    if (c == s.SearchHost || c == s.NavHost || c == s.Title) continue;
                    Control key = c is Holder && c.Controls.Count > 0 ? c.Controls[0] : c;     // a held tab control: by the tab control
                    Point orig;
                    if (s.Moved.TryGetValue(key, out orig))
                    {
                        Point now = c.Location;
                        if (now == orig) c.Location = place(orig);      // Setup put it back
                        continue;
                    }
                    s.Moved[key] = c.Location;
                    c.Location = place(c.Location);
                }
                Control topBox = s.Top.Parent is Holder ? s.Top.Parent : (Control)s.Top;
                if (topBox.Size != new Size(contentW, contentH)) topBox.Size = new Size(contentW, contentH);
                s.Dx = dx; s.Dy = dy;
                // Setup's own area, as it was: its tabs and the buttons under them
                int bottom = topBox.Bottom;
                foreach (string n in new[] { "btnOK", "btnCancel", "btnApply" })
                {
                    Control b = f.Controls[n];
                    if (b != null) bottom = Math.Max(bottom, b.Bottom);
                }
                s.SearchHost.SetBounds(pad, pad, navW + gap + contentW, searchH);
                int ins = S(f, 10);
                s.Search.SetBounds(ins, (searchH - s.Search.PreferredHeight) / 2, s.SearchHost.Width - ins * 2, s.Search.PreferredHeight);
                s.NavHost.SetBounds(pad, contentTop, navW, Math.Max(S(f, 200), bottom - contentTop));
                s.Title.SetBounds(contentLeft, contentTop, contentW, titleH);
                Size want = new Size(contentLeft + contentW + pad, bottom + pad);
                // at least this big (Setup's saved size, put back later, would cut the page off), and no bigger, so the
                // spare controls beyond the edges stay out of sight
                Size frame = f.Size - f.ClientSize;
                Size min = new Size(want.Width + frame.Width, want.Height + frame.Height);
                if (f.MinimumSize != min) f.MinimumSize = min;
                if (f.ClientSize != want) f.ClientSize = want;
                s.Title.Set(s);
            }
            finally { f.ResumeLayout(); }
        }

        private static void Open(Shell s, TabPage top, TabPage page)
        {
            if (s.Top.SelectedTab != top) s.Top.SelectedTab = top;
            TabControl sub = SubOf(top);
            if (sub != null && page != top && sub.SelectedTab != page) sub.SelectedTab = page;
            s.Nav.Rebuild();
            s.Title.Set(s);
        }

        // ---- searching: the text of every control on a page ----

        internal static bool PageMatches(TabPage page, string q)
        {
            if (page.Text.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return FirstMatch(page, q) != null;
        }

        internal static Control FirstMatch(Control parent, string q)
        {
            foreach (Control c in parent.Controls)
            {
                if (!(c is TextBoxBase) && !(c is UpDownBase) && !(c is ComboBox) && !string.IsNullOrEmpty(c.Text)
                    && c.Text.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) return c;
                if (c.HasChildren)
                {
                    Control inner = FirstMatch(c, q);
                    if (inner != null) return inner;
                }
            }
            return null;
        }

        // show where the match is: a moment in the highlight colour
        internal static void Flash(Control c)
        {
            if (c == null) return;
            Color was = c.BackColor;
            c.BackColor = KainosWindowTheme.Selection;
            Timer t = new Timer { Interval = 1600 };
            t.Tick += (o, e) => { t.Stop(); t.Dispose(); if (!c.IsDisposed) c.BackColor = was; };
            t.Start();
        }

        private static GraphicsPath roundRect(Rectangle r, float rad)
        {
            GraphicsPath p = new GraphicsPath();
            float d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, string l);
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] private static extern int SetWindowTheme(IntPtr hwnd, string app, string idList);

        // ---- the page's name, over the page ----

        private class PageTitle : Control
        {
            private string _heading = "", _page = "";
            public PageTitle()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                BackColor = KainosWindowTheme.WindowBg;
                Tag = "kainos";
            }
            public void Set(Shell s)
            {
                TabPage top = s.Top.SelectedTab;
                if (top == null) return;
                TabControl sub = SubOf(top);
                string heading = sub != null ? top.Text : "", page = sub != null && sub.SelectedTab != null ? sub.SelectedTab.Text : top.Text;
                if (heading == _heading && page == _page) return;
                _heading = heading; _page = page;
                Invalidate();
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.Clear(BackColor);
                float k = DeviceDpi / 96f;
                using (Font big = new Font("Segoe UI Semibold", 15f * k, FontStyle.Regular, GraphicsUnit.Pixel))
                using (Font small = new Font("Segoe UI", 9.5f * k, FontStyle.Bold, GraphicsUnit.Pixel))
                {
                    float x = 2 * k;
                    if (_heading.Length > 0)
                    {
                        string h = _heading.ToUpperInvariant() + "  ›";
                        SizeF hs = g.MeasureString(h, small);
                        using (Brush b = new SolidBrush(KainosWindowTheme.TextDim)) g.DrawString(h, small, b, x, (Height - hs.Height) / 2f + 1 * k);
                        x += hs.Width + 4 * k;
                    }
                    SizeF ps = g.MeasureString(_page, big);
                    using (Brush b = new SolidBrush(KainosWindowTheme.Text)) g.DrawString(_page, big, b, x, (Height - ps.Height) / 2f);
                }
                using (Pen p = new Pen(KainosWindowTheme.Border)) g.DrawLine(p, 0, Height - 1, Width, Height - 1);
            }
        }

        // ---- the list: headings (Setup's tabs) and their pages ----

        private class NavList : Control
        {
            private class Row { public TabPage Top, Page; public bool Heading; public Rectangle Bounds; }

            private readonly Shell _s;
            private readonly List<Row> _rows = new List<Row>();
            private string _filter = "";
            private Row _hover;

            public NavList(Shell s)
            {
                _s = s;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
                BackColor = KainosWindowTheme.PanelBg;
                Tag = "kainos";
                Cursor = Cursors.Hand;
            }

            private int rowH { get { return (int)Math.Round(30 * DeviceDpi / 96.0); } }
            private int headH { get { return (int)Math.Round(34 * DeviceDpi / 96.0); } }

            public void Filter(string q)
            {
                _filter = (q ?? "").Trim();
                Rebuild();
            }

            public void OpenFirstMatch()
            {
                Row r = _rows.FirstOrDefault(x => !x.Heading);
                if (r == null) return;
                Open(_s, r.Top, r.Page);
                if (_filter.Length > 0) Flash(FirstMatch(r.Page, _filter));
            }

            // the rows: every heading; the pages of the heading shown (or, searching, every page that matches)
            public void Rebuild()
            {
                _rows.Clear();
                TabPage current = _s.Top.SelectedTab;
                int rowW = Width - barRoom;
                int y = (int)Math.Round(6 * DeviceDpi / 96.0);
                foreach (TabPage top in _s.Top.TabPages)
                {
                    TabControl sub = SubOf(top);
                    List<TabPage> pages = sub != null ? sub.TabPages.Cast<TabPage>().ToList() : new List<TabPage> { top };
                    List<TabPage> shown;
                    if (_filter.Length > 0)
                    {
                        shown = pages.Where(p => PageMatches(p, _filter)).ToList();
                        if (shown.Count == 0 && top.Text.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    }
                    else shown = top == current ? pages : new List<TabPage>();
                    _rows.Add(new Row { Top = top, Page = top, Heading = true, Bounds = new Rectangle(0, y, rowW, headH) });
                    y += headH;
                    if (sub == null && _filter.Length == 0) continue;       // a single page: the heading is the page
                    foreach (TabPage p in shown)
                    {
                        _rows.Add(new Row { Top = top, Page = p, Bounds = new Rectangle(0, y, rowW, rowH) });
                        y += rowH;
                    }
                }
                _contentH = y + (int)Math.Round(8 * DeviceDpi / 96.0);
                // keep the page shown in view
                Row sel = _rows.FirstOrDefault(isSelected);
                if (sel != null && (sel.Bounds.Top < _offset || sel.Bounds.Bottom > _offset + Height))
                    _offset = sel.Bounds.Top - Height / 3;
                clamp();
                Invalidate();
            }

            // ---- scrolling: the list moves itself, with a slim bar on the right ----
            private int _offset, _contentH, _dragFrom = -1, _dragOffset;
            private int barRoom { get { return (int)Math.Round(10 * DeviceDpi / 96.0); } }
            private void clamp() { _offset = Math.Max(0, Math.Min(_offset, Math.Max(0, _contentH - Height))); }
            private Rectangle thumb()
            {
                if (_contentH <= Height || Height <= 0) return Rectangle.Empty;
                float k = DeviceDpi / 96f;
                int track = Height - (int)(8 * k);
                int th = Math.Max((int)(30 * k), track * Height / _contentH);
                int ty = (int)(4 * k) + (track - th) * _offset / Math.Max(1, _contentH - Height);
                return new Rectangle(Width - (int)(7 * k), ty, (int)(4 * k), th);
            }
            protected override void OnResize(EventArgs e) { base.OnResize(e); clamp(); Invalidate(); }

            private bool isSelected(Row r)
            {
                TabPage top = _s.Top.SelectedTab;
                if (r.Top != top) return false;
                TabControl sub = SubOf(top);
                if (r.Heading) return sub == null;
                return sub != null && sub.SelectedTab == r.Page;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.Clear(BackColor);
                paintThumb(g);
                g.TranslateTransform(0, -_offset);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float k = DeviceDpi / 96f;
                using (Font headFont = new Font("Segoe UI", 10.5f * k, FontStyle.Bold, GraphicsUnit.Pixel))
                using (Font pageFont = new Font("Segoe UI", 12.5f * k, FontStyle.Regular, GraphicsUnit.Pixel))
                {
                    foreach (Row r in _rows)
                    {
                        bool sel = isSelected(r), hover = r == _hover;
                        if (r.Heading)
                        {
                            bool open = r.Top == _s.Top.SelectedTab || _filter.Length > 0;
                            Rectangle hr = new Rectangle(r.Bounds.X + (int)(6 * k), r.Bounds.Y + (int)(3 * k), r.Bounds.Width - (int)(12 * k), r.Bounds.Height - (int)(6 * k));
                            if (sel || hover)
                                using (GraphicsPath path = rounded(hr, 5 * k))
                                using (Brush b = new SolidBrush(sel ? KainosWindowTheme.Selection : KainosWindowTheme.ButtonHover)) g.FillPath(b, path);
                            if (sel) using (Brush b = new SolidBrush(KainosWindowTheme.Gold)) g.FillRectangle(b, hr.X, hr.Y + 4 * k, 3 * k, hr.Height - 8 * k);
                            Color tc = sel ? KainosWindowTheme.Gold : open ? KainosWindowTheme.TextMid : KainosWindowTheme.TextDim;
                            TextRenderer.DrawText(g, r.Top.Text.ToUpperInvariant(), headFont, new Rectangle(hr.X + (int)(12 * k), hr.Y, hr.Width - (int)(30 * k), hr.Height), tc,
                                                  TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                            // a chevron: open or closed
                            if (SubOf(r.Top) != null && _filter.Length == 0)
                            {
                                float cx = hr.Right - 12 * k, cy = hr.Y + hr.Height / 2f;
                                using (Pen p = new Pen(tc, 1.6f * k))
                                    if (open) g.DrawLines(p, new[] { new PointF(cx - 4 * k, cy - 2 * k), new PointF(cx, cy + 2 * k), new PointF(cx + 4 * k, cy - 2 * k) });
                                    else g.DrawLines(p, new[] { new PointF(cx - 2 * k, cy - 4 * k), new PointF(cx + 2 * k, cy), new PointF(cx - 2 * k, cy + 4 * k) });
                            }
                        }
                        else
                        {
                            Rectangle pr = new Rectangle(r.Bounds.X + (int)(14 * k), r.Bounds.Y + (int)(2 * k), r.Bounds.Width - (int)(20 * k), r.Bounds.Height - (int)(4 * k));
                            if (sel || hover)
                                using (GraphicsPath path = rounded(pr, 5 * k))
                                using (Brush b = new SolidBrush(sel ? KainosWindowTheme.Selection : KainosWindowTheme.ButtonHover)) g.FillPath(b, path);
                            if (sel) using (Brush b = new SolidBrush(KainosWindowTheme.Gold)) g.FillRectangle(b, pr.X, pr.Y + 4 * k, 3 * k, pr.Height - 8 * k);
                            TextRenderer.DrawText(g, r.Page.Text, pageFont, new Rectangle(pr.X + (int)(14 * k), pr.Y, pr.Width - (int)(18 * k), pr.Height),
                                                  sel ? Color.White : KainosWindowTheme.Text,
                                                  TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                        }
                    }
                }
            }

            private static GraphicsPath rounded(Rectangle r, float rad)
            {
                GraphicsPath p = new GraphicsPath();
                float d = rad * 2;
                p.AddArc(r.X, r.Y, d, d, 180, 90);
                p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                p.CloseFigure();
                return p;
            }

            private void paintThumb(Graphics g)
            {
                Rectangle th = thumb();
                if (th.IsEmpty) return;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath path = rounded(th, th.Width / 2f))
                using (Brush b = new SolidBrush(_dragFrom >= 0 ? KainosWindowTheme.TextMid : KainosWindowTheme.Border)) g.FillPath(b, path);
                g.SmoothingMode = SmoothingMode.None;
            }

            private Row rowAt(Point p) { return _rows.FirstOrDefault(r => r.Bounds.Contains(new Point(p.X, p.Y + _offset))); }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                if (_dragFrom >= 0)
                {
                    Rectangle th = thumb();
                    int track = Height - th.Height;
                    if (track > 0) { _offset = _dragOffset + (e.Y - _dragFrom) * (_contentH - Height) / track; clamp(); Invalidate(); }
                    return;
                }
                Row r = rowAt(e.Location);
                if (r != _hover) { _hover = r; Invalidate(); }
            }

            protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = null; Invalidate(); }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                Focus();
                Rectangle th = thumb();
                if (!th.IsEmpty && e.X >= Width - barRoom)
                {
                    if (th.Contains(e.Location)) { _dragFrom = e.Y; _dragOffset = _offset; Capture = true; }
                    else { _offset += (e.Y < th.Top ? -1 : 1) * Height * 3 / 4; clamp(); }       // page up / down
                    Invalidate();
                    return;
                }
                Row r = rowAt(e.Location);
                if (r == null) return;
                if (r.Heading)
                {
                    // a heading: its page shown last time (or its first)
                    TabControl sub = SubOf(r.Top);
                    Open(_s, r.Top, sub != null ? (sub.SelectedTab ?? (sub.TabCount > 0 ? sub.TabPages[0] : r.Top)) : r.Top);
                }
                else
                {
                    Open(_s, r.Top, r.Page);
                    if (_filter.Length > 0) Flash(FirstMatch(r.Page, _filter));
                }
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                base.OnMouseUp(e);
                if (_dragFrom >= 0) { _dragFrom = -1; Capture = false; Invalidate(); }
            }

            protected override void OnMouseWheel(MouseEventArgs e)
            {
                _offset -= Math.Sign(e.Delta) * rowH * 2;
                clamp();
                Invalidate();
            }

            // up / down through the pages shown
            protected override bool IsInputKey(Keys k) { return k == Keys.Up || k == Keys.Down || base.IsInputKey(k); }
            protected override void OnKeyDown(KeyEventArgs e)
            {
                base.OnKeyDown(e);
                if (e.KeyCode != Keys.Up && e.KeyCode != Keys.Down) return;
                List<Row> pages = _rows.Where(r => !r.Heading || SubOf(r.Top) == null).ToList();
                int i = pages.FindIndex(isSelected);
                int n = Math.Max(0, Math.Min(pages.Count - 1, i + (e.KeyCode == Keys.Down ? 1 : -1)));
                if (n >= 0 && n < pages.Count) Open(_s, pages[n].Top, pages[n].Page);
                e.Handled = true;
            }
        }
    }
}
