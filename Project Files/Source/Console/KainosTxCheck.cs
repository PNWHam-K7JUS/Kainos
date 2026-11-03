/*  KainosTxCheck.cs

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
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Thetis
{
    // The first-transmit check window (Tools > First-transmit check, and after the setup wizard): a carrier test (power
    // out, SWR) and a talk test (the microphone's level, the ALC), each a few seconds into a dummy load, with a plain
    // verdict. The tests themselves are in consoleKainosTxCheck.cs.
    internal class KainosTxCheck : Form
    {
        private const int CarrierSeconds = 3, TalkSeconds = 6;
        private readonly Console _console;
        private readonly CheckBox _load;
        private readonly KainosWizardButton _carrier, _talk, _close;
        private readonly Label _carrierLive, _carrierResult, _talkLive, _talkResult;
        private bool _running, _stop;

        public KainosTxCheck(Console console)
        {
            _console = console;
            Text = "First-transmit check";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = KainosWindowTheme.WindowBg;
            ForeColor = KainosWindowTheme.Text;
            Font = new Font("Segoe UI", 9.5f);
            AutoScaleDimensions = new SizeF(96f, 96f);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(600, 500);

            Controls.Add(new Label { Text = "First-transmit check", Location = new Point(24, 16), Size = new Size(552, 30), Font = new Font("Segoe UI", 15f, FontStyle.Bold), ForeColor = KainosUI.GoldHi });
            label("Two short tests: does power come out, and does Kainos hear your microphone? Each transmits for a few seconds " +
                  "on VFO A, and you can stop it at any time.", 54, 40, false);
            _load = new CheckBox { Text = "I'm connected to a dummy load (or an antenna tuned for this frequency)", Location = new Point(24, 100), AutoSize = true, ForeColor = KainosWindowTheme.Text, Font = new Font(Font, FontStyle.Bold) };
            Controls.Add(_load);

            heading("1.  Power out and SWR", 140);
            label("Sends Thetis's tune carrier (TUN) for " + CarrierSeconds + " seconds at your tune power.", 166, 22, true);
            _carrier = new KainosWizardButton("Send a carrier") { Location = new Point(24, 192), Size = new Size(150, 30), Accent = true };
            _carrier.Click += (s, e) => run(true);
            Controls.Add(_carrier);
            _carrierLive = label("", 198, 22, true, 190, 386);
            _carrierResult = label("", 230, 62, false);

            heading("2.  Your microphone", 300);
            label("Switch to USB or LSB, press Talk test and talk as you would on the air (\"testing, one two three\") for " + TalkSeconds +
                  " seconds. It transmits at " + Console.KainosMicTestDrive + " % drive.", 326, 40, true);
            _talk = new KainosWizardButton("Talk test") { Location = new Point(24, 370), Size = new Size(150, 30), Accent = true };
            _talk.Click += (s, e) => run(false);
            Controls.Add(_talk);
            _talkLive = label("", 376, 22, true, 190, 386);
            _talkResult = label("", 408, 50, false);

            _close = new KainosWizardButton("Close") { Location = new Point(476, 458), Size = new Size(100, 30) };
            _close.Click += (s, e) => Close();
            Controls.Add(_close);
            KeyPreview = true;
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) { if (_running) _stop = true; else Close(); } };
            FormClosing += (s, e) => { if (_running) { _stop = true; e.Cancel = true; } };
        }

        private Label label(string text, int y, int h, bool dim, int x = 24, int w = 552)
        {
            Label l = new Label { Text = text, Location = new Point(x, y), Size = new Size(w, h), ForeColor = dim ? KainosWindowTheme.TextMid : KainosWindowTheme.Text };
            Controls.Add(l);
            return l;
        }

        private void heading(string text, int y)
        {
            Controls.Add(new Label { Text = text, Location = new Point(24, y), Size = new Size(552, 24), ForeColor = KainosUI.Gold, Font = new Font(Font.FontFamily, 11f, FontStyle.Bold) });
        }

        private async void run(bool carrier)
        {
            KainosWizardButton b = carrier ? _carrier : _talk;
            if (_running) { if (b.Text == "Stop") _stop = true; return; }
            Label live = carrier ? _carrierLive : _talkLive, result = carrier ? _carrierResult : _talkResult;
            if (!_load.Checked)
            {
                result.ForeColor = KainosUI.Gold;
                result.Text = "Connect a dummy load (or a tuned antenna) and tick the box above first.";
                return;
            }
            _running = true;
            _stop = false;
            string was = b.Text;
            b.Text = "Stop";
            b.Invalidate();
            result.Text = "";
            Console.TxCheckResult r;
            try
            {
                Action<string> show = t => { if (!IsDisposed) live.Text = t; };
                r = carrier ? await _console.KainosTxCarrierTest(CarrierSeconds, () => _stop, show)
                            : await _console.KainosTxMicTest(TalkSeconds, () => _stop, show);
            }
            catch (Exception ex) { r = new Console.TxCheckResult { Verdict = "The test stopped: " + ex.Message }; }
            finally
            {
                _running = false;
                b.Text = was;
                b.Invalidate();
            }
            if (IsDisposed) return;
            result.ForeColor = r.Ok ? Color.FromArgb(0x6c, 0xc2, 0x7a) : KainosUI.Gold;
            result.Text = (r.Ok ? "✓  " : "!  ") + r.Verdict;
        }
    }
}
