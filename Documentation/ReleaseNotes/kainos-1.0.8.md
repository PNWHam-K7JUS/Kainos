# Kainos 1.0.8

Based on Thetis 2.10.3.15. See the [1.0.0 notes](https://github.com/PNWHam-K7JUS/Kainos/releases/tag/kainos-1.0.0) for everything Kainos adds, and the [Kainos wiki](https://github.com/PNWHam-K7JUS/Kainos/wiki) for how to use it. Thanks to everyone on GitHub and Facebook who reported bugs and asked for features.

**New: hear VFO B in split**
- In split, the VFO B flag (and VFO B in the right-hand column's VFO tab) has a **LISTEN** button: click it to hear VFO B along with VFO A, for example to hear the DX's pileup while you listen to the DX. It uses Thetis's second receiver inside RX1's span (the SubRX button), so the **MAIN** and **SUB** sliders under the panadapter set which side you hear each one on. VFO B starts at VFO A's volume; set its own level with **LISTEN AF** in VFO B's AUDIO drawer. If VFO B is too far from VFO A for it to reach, the button says **TOO FAR** (GitHub issue #4).

**New: AUDIO SCOPE tab**
- A new **AUDIO SCOPE** tab in the right-hand column (off by default; turn it on from the tab chips) shows what you're hearing as a waveform: RX1's audio, including VFO B when LISTEN is on, and your processed transmit signal while you transmit (marked **TX**, in red). The trace holds still on the signal and scales itself to fit, with the peak level in dBFS. Choose 2, 5, 10 or 20 ms across (50 and 100 ms on the right-click menu), and click the scope to hold the trace. It only runs while the tab is on screen.
- **AF spectrum**: right-click the scope for **Waveform**, **AF spectrum** or **Both**. The spectrum shows your audio from 0 to 3 kHz on SSB (3.5 kHz on digital, 6 kHz on AM, 8 kHz on FM, and 1 kHz around your pitch on CW, with the pitch marked), with your receive filter's passband shaded, the strongest frequency and its level, and a peak-hold line. While you transmit it shows your transmit signal's spectrum, handy for setting up the TX EQ.
- The right-click menu also has a fixed scale (0 to -50 dBFS full scale) in place of the automatic one, the trigger on or off, left, right or both channels, smoothing, peak hold and an **AF waterfall** under the spectrum (its colours follow the noise floor, so signals stand out). Your choices are kept. See the [Audio Scope](https://github.com/PNWHam-K7JUS/Kainos/wiki/Audio-Scope) wiki page.
- Technical: it reads ChannelMaster's scope tap, the one Thetis's Scope display uses (`KainosScopeTap`, fed from `DoScope.xscope` in `cmaster.cs`). `CMSetScopeRun` keeps the tap running while either Thetis's display or the tab wants it, and Thetis's scope arrays are only filled when its display does.

**New: pin the VFO flags anywhere, or turn them off**
- Drag a VFO flag **sideways** and it stays where you drop it on the panadapter, instead of following its VFO, marked with a gold pin. Drag it again to move it; click the pin, or double-click its face, to put it back on its VFO. Dragging straight down still moves it down its VFO line as before (GitHub issue #11).
- **Setup > Appearance > Kainos > Slice flags** has a new choice, **Off (VFO tab only)**: no flags on the panadapter, just the VFO line. The right-hand column's VFO tab then gets the flags' tab row, and AUDIO, DSP, MODE, RIT/XIT, VAC and FREEDV open in the column.
- Technical: pinned places are saved as fractions of the panadapter's free room (`KainosFlagSettings`, now "dropA;dropB;compactA;compactB;pinA;pinB"); flag opacity 0 is "off" (`KainosFlagOpacity`), and `KainosBuildDrawer` takes a width for the column.

**New: choose what the mouse wheel does over the right-hand column**
- **Setup > Appearance > Kainos > Mouse wheel**: **Scrolls the column** (as now; hold Ctrl to change a slider) or **Adjusts the control under it**, where the wheel changes the slider, list, drop-down or number under the pointer and scrolls the column anywhere else, and Ctrl+wheel always scrolls the column (GitHub issue #4).

**Fixes**
- The squelch level in FM (and RX2's) went back to 100 at every start. It now keeps the level you set (GitHub issue #10). This came from Thetis: at start-up it stored the squelch slider's default as the level before the saved one was put on it; Kainos now puts the saved level on the slider first (`kainosSquelchSlidersFromSaved`).
- **Report a bug** showed "Screen: (InvalidOperationException)" instead of the screen size and scaling.
- Windows's Installed apps list showed Kainos as version 1.0.1 whatever version was installed: the installer kept its version from the 1.0.1 build. It now shows the real version.
- With RX2 on, Thetis's RX2 controls appeared in a strip under the panadapters. They're now hidden in the Kainos layout, giving the panadapters the room: RX2's mode, filter and DSP are on VFO B's flag, and its pan, squelch, AGC and AGC gain are now in flag B's **AUDIO** and **DSP** drawers.

## Installing

Run **Kainos-1.0.8-x64.msi**. It replaces any earlier Kainos version and keeps your settings. Kainos installs alongside Thetis and doesn't change it.
