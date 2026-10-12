# Kainos 1.0.10

Based on Thetis 2.10.3.15. See the [Kainos wiki](https://github.com/PNWHam-K7JUS/Kainos/wiki) for how to use everything below. Thanks to everyone who sent ideas and bug reports.

**New: Audio Scope colours**
- A **Colours** choice on the Audio Scope's right-click menu: **Standard** (as before, the default), **Kainos** (glowing trace and a gradient-filled spectrum), **Phosphor** (green, like an old CRT, with an afterglow), **Heat** (green, yellow and red by level) and **Spectrum** (a rainbow by frequency). In light mode the glow and afterglow are left out.

**Fixed**
- With no audio, the Audio Scope's auto scale no longer follows the silence down until the noise fills the view; it stops at -40 dBFS.
- The Audio Scope now moves smoothly: it drew about 21 frames a second at uneven spacing (31 to 62 ms), which showed as jitter and little pauses; it now draws a steady 30, in step with the screen, on its own thread, so it keeps going while the rest of the window is busy (for example while you move the mouse over the panadapter).
- The AF spectrum is steadier: it averages each frequency's level over about 0.2 s (it used to jump about with band noise every frame), and its auto scale no longer creeps down and jumps back up all the time: it goes up at once, but only comes down after 3 seconds of quieter audio, and glides there.

- Technical: `KainosAudioScope.cs` (`Themes`; Phosphor keeps an afterglow bitmap faded each frame; the glow is drawn through about one point every 3 px; the frames are paced by a thread waiting on `DwmFlush` instead of a Forms timer, whose 15.6 ms grain gave 31 / 47 / 62 ms frames, and drawn on that thread into a back picture copied to the window with `GetDC` (the console's sample rates and AF span are fetched on the UI thread every 100 ms; `_sync` keeps the UI thread's own painting apart); the AF columns are averaged as power with a 180 ms time constant by the clock, and the auto top has a target with a 3 s hold before stepping down, eased at 150 dB/s up and 20 dB/s down) and `consoleKainosAudioScope.cs` (menu, and the `col` setting).

## Installing

Run **Kainos-1.0.10-x64.msi**. It replaces any earlier Kainos version and keeps your settings. Kainos installs alongside Thetis and doesn't change it.
