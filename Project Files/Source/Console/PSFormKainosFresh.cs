/*  PSFormKainosFresh.cs

Kainos: PureSignal starts afresh after a QSY, or after a long spell of receive (GitHub #69, Misfit-138).

Moving to another frequency changes the antenna's impedance a little, and an LDMOS amplifier that has cooled during a
long receive behaves differently from a warm one. PureSignal then starts each new transmission from correction worked
out for the old conditions, and on AM especially it can take a while to settle. Misfit-138's cure was to turn PS-A off,
move, key up and turn PS-A on again while keyed. These do the same by themselves, in auto-calibrate mode only:

  - After a QSY: when the transmit VFO has moved more than 1 kHz since the last transmission ended.
  - After a long receive: when it's been more than the minutes set since the last transmission ended.

Either way, at the moment of keying up the correction is thrown away and auto-calibrate starts again (the same two
calls Thetis's own auto-attenuate uses: everything off and reset, then auto back on), so PureSignal takes a fresh
sample at once. Both are off by default; they're in the Linearity window (right-click PS-A), saved with its settings.

This file is part of Kainos. Copyright (C) 2026 Justin Cron K7JUS. GPLv3 or later.
*/

using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace Thetis
{
    public partial class PSForm
    {
        private GroupBoxTS _kfGroup;
        private CheckBoxTS _kfQsy, _kfIdle;
        private NumericUpDownTS _kfMinutes;
        private LabelTS _kfLast;
        private double _kfLastTxMHz = double.NaN;
        private DateTime _kfLastTxEnd = DateTime.MinValue;
        private System.Threading.Timer _kfResume;

        internal const int KainosFreshHeight = 60;      // what the group adds to the window's normal height

        // before Common.RestoreForm, so the form's own save and restore keep these settings
        private void kainosFreshOn()
        {
            _kfGroup = new GroupBoxTS { Name = "grpKainosPsFresh", Text = "Start afresh (auto-calibrate only)", Location = new Point(14, 297), Size = new Size(533, 56) };
            _kfQsy = new CheckBoxTS { Name = "chkKainosPsFreshQsy", Text = "After a QSY (moved more than 1 kHz)", Location = new Point(10, 18), AutoSize = true };
            _kfIdle = new CheckBoxTS { Name = "chkKainosPsFreshIdle", Text = "After", Location = new Point(250, 18), AutoSize = true };
            _kfMinutes = new NumericUpDownTS { Name = "udKainosPsFreshMinutes", Minimum = 1, Maximum = 60, Value = 5, Location = new Point(302, 16), Size = new Size(42, 20) };
            LabelTS minutes = new LabelTS { Text = "minutes of receive", Location = new Point(348, 19), AutoSize = true };
            _kfLast = new LabelTS { Text = "", Location = new Point(10, 37), Size = new Size(515, 15), ForeColor = SystemColors.GrayText };
            toolTip1.SetToolTip(_kfQsy, "When the transmit frequency has moved more than 1 kHz since the last transmission, PureSignal's\n" +
                "correction is thrown away as you key up, and auto-calibrate starts again with a fresh sample.\n" +
                "The antenna's impedance changes with frequency, so the old correction no longer fits it.");
            toolTip1.SetToolTip(_kfIdle, "When it's been this long since the last transmission, PureSignal starts afresh as you key up:\n" +
                "an LDMOS amplifier that has cooled during a long receive doesn't match the correction from when it was warm.");
            toolTip1.SetToolTip(_kfMinutes, "Minutes of receive after which PureSignal starts afresh");
            _kfGroup.Controls.AddRange(new Control[] { _kfQsy, _kfIdle, _kfMinutes, minutes, _kfLast });
            Controls.Add(_kfGroup);
            ClientSize = new Size(ClientSize.Width, ClientSize.Height + KainosFreshHeight);

            console.MoxPreChangeHandlers += kainosFreshPreMox;
            console.MoxChangeHandlers += kainosFreshMox;
        }

        // the frequency transmitted on (MHz)
        private double kainosTxMHz()
        {
            try { return console.VFOBTX ? console.VFOBFreq : console.VFOAFreq; }
            catch (Exception) { return double.NaN; }
        }

        // keying up: start afresh if it's been asked for and the QSY or the receive time says so
        private void kainosFreshPreMox(int rx, bool currentMox, bool expectedMox)
        {
            if (currentMox || !expectedMox) return;
            if (!_bPSRunning || _cmdstate != eCMDState.AutoCalibrate) return;      // auto-calibrate only
            string why = null;
            double now = kainosTxMHz();
            if (_kfQsy.Checked && !double.IsNaN(_kfLastTxMHz) && !double.IsNaN(now) && Math.Abs(now - _kfLastTxMHz) > 0.001)
                why = "QSY " + (_kfLastTxMHz).ToString("0.000", CultureInfo.InvariantCulture) + " → " + now.ToString("0.000", CultureInfo.InvariantCulture) + " MHz";
            else if (_kfIdle.Checked && _kfLastTxEnd != DateTime.MinValue && (DateTime.UtcNow - _kfLastTxEnd).TotalMinutes > (double)_kfMinutes.Value)
                why = ((int)(DateTime.UtcNow - _kfLastTxEnd).TotalMinutes) + " minutes of receive";
            if (why == null) return;
            kainosFreshStart(why);
        }

        private void kainosFreshMox(int rx, bool oldMox, bool newMox)
        {
            if (oldMox && !newMox)
            {
                _kfLastTxEnd = DateTime.UtcNow;
                _kfLastTxMHz = kainosTxMHz();
            }
        }

        // everything off and reset, then auto-calibrate on again a moment later (as the auto-attenuate does)
        private void kainosFreshStart(string why)
        {
            try
            {
                puresignal.SetPSControl(_txachannel, 1, 0, 0, 0);
                if (_kfResume != null) _kfResume.Dispose();
                _kfResume = new System.Threading.Timer(_ =>
                {
                    try { if (_cmdstate == eCMDState.AutoCalibrate) puresignal.SetPSControl(_txachannel, 0, 0, 1, 0); } catch (Exception) { }
                }, null, 40, System.Threading.Timeout.Infinite);
                string note = "Started afresh at " + DateTime.UtcNow.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + "Z: " + why;
                if (IsHandleCreated) BeginInvoke((Action)(() => _kfLast.Text = note)); else _kfLast.Text = note;
            }
            catch (Exception) { }
        }
    }
}
