/*  KainosModuleApi.cs

The contract between Kainos and its downloadable modules. A module is a .NET Framework 4.8 class library that
references this assembly (Kainos.ModuleApi.dll, shipped with Kainos) and has one public class implementing
IKainosModule. Kainos never references a module; a module only sees Kainos through IKainosHost.

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
using System.Drawing;
using System.Windows.Forms;

namespace Kainos.Modules
{
    public static class KainosModuleApi
    {
        // Raised when an interface below changes in a way an older module can't cope with. A module's module.json
        // names the version it was built for ("api"); Kainos loads it only if that matches.
        public const int Version = 1;
    }

    // What a module implements. Kainos makes one instance after loading the module, calls Start on the UI thread
    // once the console is up (or when the module is enabled in Setup), and Stop then Dispose when it's disabled,
    // removed or Kainos closes. Start must return quickly: do slow work on a thread of your own.
    public interface IKainosModule : IDisposable
    {
        void Start(IKainosHost host);
        void Stop();
    }

    // Everything Kainos offers a module. Members may be called from any thread unless noted; the ones that touch
    // the window marshal to the UI thread themselves.
    public interface IKainosHost
    {
        int ApiVersion { get; }
        string KainosVersion { get; }

        string ModuleId { get; }
        string ModuleFolder { get; }        // where the module's files were unpacked (read-only for the module)
        string DataFolder { get; }          // a writable folder of the module's own (logs, temp files, its data)

        string Callsign { get; }            // the station's callsign and grid square (Setup > DSP > FreeDV (RADE))
        string Grid { get; }

        IKainosRadio Radio { get; }
        IKainosAudio Audio { get; }
        IKainosTheme Theme { get; }

        // An item in the Modes menu (and the menu bar in Classic layout). UI thread.
        IKainosMenuItem AddMenuItem(string text, string toolTip, Action click);

        // A pane that lives where the RTTY and CW terminals do: docked under the panadapter in Kainos layout (it takes
        // its height from the panadapter), or in its own window (popped out, and always in Classic). UI thread.
        IKainosPane CreatePane(string title, Control content);

        // Lines on a receiver's panadapter (rx 0 = RX1, 1 = RX2) at audio offsets from its VFO; null clears them.
        void SetMarkers(int rx, KainosMarker[] markers);

        // Settings saved with Kainos's own (a key per setting, per module)
        string GetSetting(string key, string fallback);
        void SetSetting(string key, string value);

        // A line in the module's log file (DataFolder\module.log)
        void Log(string message);

        // Runs an action on the UI thread
        void BeginInvoke(Action action);
    }

    public interface IKainosRadio
    {
        bool PowerOn { get; }
        bool Transmitting { get; }          // MOX, from any source (the module's own, the MOX button, a foot switch...)
        int TxReceiver { get; }             // the receiver whose VFO transmits: 0 (VFO A) or 1 (VFO B)

        double VfoHz(int rx);
        void TuneVfo(int rx, double hz);

        // "USB", "LSB", "DIGU", "DIGL", "CWU", "CWL", "AM", "FM", ... (Thetis's mode names)
        string Mode(int rx);
        void SetMode(int rx, string mode);

        // Keys or un-keys the radio. A module keys only while it's sending audio through IKainosAudio.
        void SetMox(bool on);
    }

    // The digital-mode audio taps (shared with Kainos's RTTY and CW terminals: one user at a time)
    public interface IKainosAudio
    {
        // Takes a receiver's demodulated audio (a copy, after the noise reduction and AGC, before the volume). Closes
        // the RTTY or CW terminal if one has it. `released` is called (UI thread) if something else takes it later.
        bool AcquireRx(int rx, Action released, out string problem);
        void ReleaseRx();
        int RxRate { get; }                 // samples a second (48000 usually; changes with the receiver's mode)
        int ReadRx(float[] dst, int max);   // what has arrived since the last read, mono, about +-1.0

        // Transmit: while started, the mic audio is replaced by what's written here, so the radio's mic gain, TX
        // filter, drive and ALC apply as for any audio. Keep about 0.2 s queued ahead.
        int TxRate { get; }
        void TxStart();
        int TxWrite(float[] src, int n);    // returns how many were taken (the queue is ~2.7 s)
        int TxQueued { get; }
        void TxClear();
        void TxStop();
    }

    // Voice: what a voice keyer needs. Added in Kainos 1.0.12 alongside the interfaces above (which are unchanged, so
    // modules built for them keep working): a module asks for it with `host as IKainosVoice`, which is null on an
    // older Kainos, and sets minKainos to 1.0.12 if it can't do without it.
    public interface IKainosVoice
    {
        // The mic as Kainos has it: the transmit input after VAC (radio mic, sound card, VAC or ASIO), before the mic
        // gain and the transmit chain. Played back through IKainosAudio's transmit feed, which goes in at the same
        // place, a recording gets the mic gain, EQ, Kainos Audio and AetherVoice exactly as the live voice does.
        void MicStart();
        void MicStop();
        int MicRate { get; }                // samples a second (48000 usually)
        int ReadMic(float[] dst, int max);  // what has arrived since the last read, mono, about +-1.0

        // Preview: audio mixed into RX1's audio, so it's heard wherever the radio is (and on VAC / TCI receive audio)
        int PreviewRate { get; }
        void PreviewStart();
        int PreviewWrite(float[] src, int n);   // returns how many were taken (the queue is ~2.7 s)
        int PreviewQueued { get; }
        void PreviewClear();
        void PreviewStop();

        // F1 - F12 and Esc pressed in Kainos's main window without Ctrl, Alt or Shift (and not while typing in a text
        // box). The handler runs on the UI thread and returns true if it used the key. Null stops.
        void SetKeyHandler(Func<Keys, bool> handler);
    }

    // The dock: a button down the left-hand side, for a module that's a tool rather than a mode (the Voice Keyer).
    // Added in Kainos 1.0.12, like IKainosVoice: ask with `host as IKainosDock` (null on an older Kainos). The button
    // goes in the dock's tools group (Kainos layout), with an item in the Tools menu (the menu bar in Classic layout).
    // Its Checked lights the button and ticks the item. A few slots are kept for modules: null when they're all taken,
    // so fall back to AddMenuItem.
    public interface IKainosDock
    {
        IKainosMenuItem AddDockButton(string label, string menuText, string toolTip, Action click);
    }

    // Kainos's look, so a module's windows match the console
    public interface IKainosTheme
    {
        Color Bg { get; }           // deep navy
        Color Surface { get; }      // bars and columns
        Color Panel { get; }
        Color Raised { get; }       // buttons, menus
        Color Line { get; }         // borders, grid
        Color Text { get; }
        Color Dim { get; }
        Color Faint { get; }
        Color Ice { get; }          // active controls
        Color Gold { get; }         // selected, VFO A
        Color Violet { get; }       // VFO B
        Color Tx { get; }           // transmit only
        Color Selected { get; }     // behind a lit button

        float Scale { get; }        // Setup > Appearance > Kainos > UI scale
        int S(float designPx);      // a size at the UI scale

        Font Font(float px, bool bold);
        void DrawButton(Graphics g, RectangleF r, string text, bool on, bool enabled, bool hover, float fontPx);
        void Style(Control root);   // gives a module's own window or controls the Kainos dark look
    }

    public interface IKainosMenuItem
    {
        bool Checked { get; set; }
        bool Enabled { get; set; }
        string Text { get; set; }
        void Remove();
    }

    // A pane's content can implement this (Kainos 1.0.12; an older Kainos ignores it): the height (design px) it's
    // docked at until the user drags it, and how short it may be dragged. Without it: 236, and 140 at least.
    public interface IKainosPaneHeight
    {
        int PreferredHeight { get; }
        int MinimumHeight { get; }
    }

    public interface IKainosPane
    {
        bool IsOpen { get; }
        bool Popped { get; set; }   // in its own window rather than docked
        void Open();
        void Close();
        event EventHandler Closed;  // the user closed its window
    }

    public sealed class KainosMarker
    {
        public double OffsetHz;     // from the VFO, as heard (negative below it on LSB / DIGL)
        public string Label;
        public Color Colour;

        public KainosMarker() { }
        public KainosMarker(double offsetHz, string label, Color colour) { OffsetHz = offsetHz; Label = label; Colour = colour; }
    }
}
