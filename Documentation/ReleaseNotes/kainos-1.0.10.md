# Kainos 1.0.10

Based on Thetis 2.10.3.15. See the [Kainos wiki](https://github.com/PNWHam-K7JUS/Kainos/wiki) for how to use everything below. Thanks to everyone who sent ideas and bug reports.

**New: Audio Scope colours**
- A **Colours** choice on the Audio Scope's right-click menu: **Standard** (as before, the default), **Kainos** (glowing trace and a gradient-filled spectrum), **Phosphor** (green, like an old CRT, with an afterglow), **Heat** (green, yellow and red by level) and **Spectrum** (a rainbow by frequency). In light mode the glow and afterglow are left out.

**Fixed**
- With no audio, the Audio Scope's auto scale no longer follows the silence down until the noise fills the view; it stops at -40 dBFS.
- The Audio Scope now moves smoothly: it drew about 21 frames a second at uneven spacing (31 to 62 ms), which showed as jitter and little pauses; it now draws a steady 30, in step with the screen.

- Technical: `KainosAudioScope.cs` (`Themes`; Phosphor keeps an afterglow bitmap faded each frame; the glow is drawn through about one point every 3 px; the frames are paced by a thread waiting on `DwmFlush` instead of a Forms timer, whose 15.6 ms grain gave 31 / 47 / 62 ms frames) and `consoleKainosAudioScope.cs` (menu, and the `col` setting).

## Installing

Run **Kainos-1.0.10-x64.msi**. It replaces any earlier Kainos version and keeps your settings. Kainos installs alongside Thetis and doesn't change it.
