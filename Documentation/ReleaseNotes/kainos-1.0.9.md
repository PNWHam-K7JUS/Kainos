# Kainos 1.0.9

Based on Thetis 2.10.3.15. See the [Kainos wiki](https://github.com/PNWHam-K7JUS/Kainos/wiki) for how to use everything below. Thanks to everyone who sent ideas and bug reports.

**New: compact view**
- In the Kainos layout, **Collapse** in the menu bar is now **Compact**: the left dock and the right-hand column tuck away at the window's edges, the panadapter fills the window (full screen or a small window), and the VFO flags stay. Point at the left or right edge and the dock or the column slides in over the panadapter; move away and it slides back. **Expand** puts everything back. Thetis's own collapsed layout is still there in Classic.
- Kainos remembers whether you were in the compact view.
- Technical: `consoleKainosCompact.cs`. The panadapter is drawn by DirectX, which covers controls placed over it, so the dock and the column slide in as owned windows (`KainosEdgeForm`), taking the column's pinned Thetis panels (mode panel, VFO SYNC, meter) with them; `CollapseToolStripMenuItem_Click` hands the menu item to `KainosCompactMenu` in the Kainos layout.

**New (experimental): VST3 plugins in Kainos Audio**
- Kainos Audio's transmit chain has a **Plugin** stage: choose a VST3 plugin installed on your PC (or browse for a `.vst3`), open its own window, and drag the stage anywhere in the chain like the others. It runs in voice modes only (never in digital modes or CW), Kainos Audio's BYPASS takes it out too, and Thetis's leveler, compressor and ALC still come after it. Your plugin and its settings are remembered.
- Technical: `KainosVst.dll` (`Source/KainosVst`), a host built on Steinberg's VST3 SDK 3.8.1 (MIT licence, the `Source/vst3sdk` submodule), built with CMake by the console project. WDSP's strip has a plugin slot (`AS_PLUGIN`): `xaetherstrip` calls the host's `kv_process` on the chain's float audio, in place, and `SetTXAStripPlugin` swaps it under the DSP lock. `RadioDSPTX.SetTXStripPlugin` attaches it only in voice modes. The plugin's state (component and controller) is saved as base64 with the options, a moment after it changes in the plugin's window. Receive and running plugins in a separate process (so a crashing plugin can't take Kainos down) are still to come.

**New: a tidier menu bar (Audio, Modes, Tools)**
- In the Kainos layout the menu bar is now **Setup, Memory, Audio, Modes, Tools, Compact, About**. **Modes** has FreeDV, RTTY, CW and CWX (RTTY and CW ticked while their terminal is open); **Tools** has XVTRs, Linearity, Radio Astronomy (RA) and Finder.
- **Audio** replaces Equalizer and Kainos Audio: **Kainos Audio: Receive** and **Transmit**, **VST3 Plugin** (straight to the transmit chain's plugin page), **Equalizer**, and shortcuts to the **VAC 1 / VAC 2**, **VOX / DEXP** and **CFC** settings. Classic keeps Thetis's menu bar as it was (`consoleKainosAudioMenu.cs`; the grouped items are hidden while it's on, and the new entries call the same handlers).

**New: band switching for the Xiegu GPA100 over Bluetooth**
- Kainos can tell a Xiegu GPA100 the band over Bluetooth, so the amp switches by itself: **Setup > Serial / Network / Midi CAT > Amplifier (CI-V)**, with step-by-step setup, a button that opens Windows's Bluetooth settings, and **Find GPA100**, which finds the amp's COM port for you. Kainos sends the transmit frequency (VFO B in split, and the HL2's own frequency with a transverter), and picks the amp up again by itself if it's switched off or goes out of range. Band switching only: keep the amp's ACC1 cable for PTT and ALC. Other amplifiers that follow an Icom radio over CI-V can work too. See the [Amplifier Band Switching](https://github.com/PNWHam-K7JUS/Kainos/wiki/Amplifier-Band-Switching) wiki page.
- Technical: `KainosCiv.cs`. The amp is the CI-V controller: it polls "an IC-705" (A4) about 10 times a second with `03` (frequency) and `1C 00` (TX / RX). `CivResponder` answers those (and `04`, `19 00`) from a snapshot the console takes on the UI thread every 100 ms (frequency) and in `MoxChangeHandlers` (MOX); `KainosCivLink` runs it on the Bluetooth link's outgoing COM port on its own thread, reopening it every 3 s after an error. **Find GPA100** matches the paired `XG_GPA100`'s address (`BTHENUM\DEV_...` in `Win32_PnPEntity`) to the outgoing Bluetooth serial port's device ID (the incoming port's address is all zeros). The protocol is checked offline against the frames the amp sent in the hardware test; the GPA100 gives no power or SWR over Bluetooth.

**Fixes**
- The menu bar said **Collapse** instead of **Compact** in the Kainos layout (Thetis renames it as it starts up).
- The right-hand column scrolls smoothly: the mouse wheel glides it instead of jumping, a touchpad scrolls in proportion (it used to jump a whole step for every small movement), and the column is drawn once per step instead of control by control.
- Menus in the Kainos layout could show light for a moment before turning dark. Their windows now start dark, and they no longer lay themselves out again each time they open. Windows's own menus (a text box's right-click Cut / Copy / Paste) are dark too, on Windows 10 1903 and later.

## Installing

Run **Kainos-1.0.9-x64.msi**. It replaces any earlier Kainos version and keeps your settings. Kainos installs alongside Thetis and doesn't change it.
