# Kainos 1.0.0 — first release

Kainos is a Software Defined Radio application for Windows and the Hermes Lite 2, maintained by Justin Cron, K7JUS. Kainos 1.0.0 is based on [Thetis](https://github.com/ramdor/Thetis) 2.10.3.15, built on the Hermes Lite 2 edition by Reid Campbell, MI0BOT ([OpenHPSDR-Thetis](https://github.com/mi0bot/OpenHPSDR-Thetis)). Everything Thetis does, Kainos does; this release adds the following.

![Kainos main window](https://raw.githubusercontent.com/PNWHam-K7JUS/Kainos/main/Documentation/images/kainos-main-window.webp)

## What's new

### A new layout

- A dark, tidy console in the Kainos colours, and the layout Kainos starts in. Setup > Appearance > Kainos switches between the Kainos layout and the Classic (Thetis) look, and has a UI scale from 75% to 200%. The waterfall keeps Thetis's colours.
- **A Kainos panadapter background**: dark navy with the Kainos logo faint behind the trace. Turn it off, or set how strongly the logo shows, in Setup > Appearance > Kainos.
- **Slice flags** on the panadapter, SmartSDR style, for VFO A and (with RX2 on) VFO B: antenna, filter, active DSP, mode, frequency and an S meter.
  - Turn the mouse wheel over a digit to tune by that digit. Click the frequency to type one. Click TX to choose the transmit VFO.
  - Tabs under each flag open its audio, DSP, mode, RIT/XIT, VAC and FreeDV controls.
- **A right-hand column of tabs** you turn on and off: VFO, VFO sync, meters, band / mode / filter, RX, TX, PA profile, EQ, Kainos Audio, FreeDV and memories.
  - The meters tab holds an analog meter; OE3IDE's FTDX-5000 skin is offered on first run (downloaded from his skin server, not bundled). Right-click the meter to change its type or open its settings.
  - Band, mode and filter are drop-downs, to save height.
- **A left-hand column** with power, transmit (MOX, TUN, 2TON, MON, VOX, DUP, PS-A), VFO (split, A>B, A<B, swap, zero beat, IF>V), REC / PLAY and VAC buttons. Right-click works as in Thetis.
- **A bar under the panadapter** for pan, zoom, display mode, AVG / Peak / CTUN and multi-RX pan.
- Thetis's VFO boxes and bottom panels are gone in the Kainos layout, so the panadapter and waterfall use the full height. Everything is still there in Classic.

### 3D stacked-trace panadapter (experimental)

> **Experimental:** this is new and still being tuned. It may change in later releases, and it uses more CPU than the normal panadapter (about 8 percentage points on the test PC). Turn it off with the same button if your panadapter feels slow.

- Press **3D** on the bar under the panadapter: the last few seconds of the spectrum stack back behind the live trace like a mountain range, so you can see how signals come and go.
- The traces take the waterfall's colours by strength (blue through green and yellow to red), matching your waterfall settings, or Kainos blue.
- Right-click **3D** for the depth (20 to 80 traces), the speed (5 to 20 a second), the height and the colours.
- The filter, VFO lines and grid stay on top. The stack clears when you pan, zoom, resize, switch between transmit and receive, or power off.

### Built-in spotting

DX spots and POTA activators without installing anything else.

- **DX cluster**: Kainos connects to NC7J's cluster and logs in with your callsign automatically (the callsign in Setup > DSP > FreeDV (RADE)). It reconnects by itself if the connection drops.
- **POTA**: activators currently on the air, from POTA's spot feed, checked every two minutes.
- Spots appear on the panadapter as callsign tags with country flags (POTA in green); click one to tune to it.
- The **SPOTS** tab in the right column shows each source's status and the latest spots (click to tune), with buttons to turn each source on or off, show or hide spots on the panadapter, and list only your current band.

### Kainos Audio

Audio processing ported from [AetherSDR](https://github.com/aethersdr/AetherSDR), in one window with Receive and Transmit tabs (Kainos Audio in the menu bar):

- **Receive**, on every receiver: parametric EQ, gate, compressor, tube saturation and the AetherVoice exciter.
- **Transmit**: gate, parametric EQ, de-esser, compressor, tube saturation, AetherVoice, reverb and a final limiter, saved in each TX profile.
- Voice modes only; bypassed automatically in CW and digital modes.

### FreeDV RADE

Built-in [FreeDV](https://freedv.org) RADE digital voice (V1 and V2) on RX1, RX2 and the transmitter, ported from [Thetis-RADE](https://github.com/sv1eia/Thetis-RADE) by Christos Nikolaou, SV1EIA. No separate FreeDV program or virtual audio cables.

- Sync, SNR, level and the last callsign heard, in the FreeDV window, on the panadapter and in meters.
- **FreeDV Reporter**: see who is on RADE now ([qso.freedv.org](https://qso.freedv.org)), filtered by band or following your frequency; double-click to tune. Reporting your own station is off unless you tick it in Setup > DSP > FreeDV (RADE).

### RTTY

Built-in RTTY, no extra programs or cables. Open **RTTY** in the menu bar.

- The terminal opens under the panadapter (split screen) or in its own window: received text, type-ahead sending, macros and a mark / space tuning indicator.
- 45.45, 50 or 75 baud; 170, 200, 425 or 850 Hz shift; reverse; unshift on space.
- **AFC** follows the signal up to 150 Hz either side, and your reply is sent where it found the other station.
- The mark and space tones are marked on the panadapter.
- Use DIGL or LSB.

### CW

A built-in CW decoder and sender. Open **CW** in the menu bar.

- Decodes the receiver at your CW pitch and follows the sender's speed. Zero-beat the signal for best copy.
- Sends what you type, or your macros, through Thetis's CWX keyer at the speed you set.
- Use CWL or CWU.

### Macros

Both terminals have six macro buttons (CQ, ANS, 599, 73, QRZ, MY), each mode its own set. Right-click a button to edit it: its label, its text (`{MY}` is your call, `{CALL}` is theirs) and whether it sends and returns to receive, keeps transmitting, or puts the text in the typing line. F1-F6 send them.

## Installing

- Run **Kainos-1.0.0-x64.msi**. Windows will ask for administrator permission. Kainos needs the .NET Framework 4.8 (included with Windows 10 and 11).
- **Kainos installs alongside Thetis** and does not upgrade, change or remove it. It installs to `Program Files\OpenHPSDR\Kainos-HL2` and keeps its settings in `%APPDATA%\OpenHPSDR\Kainos-x64`.
- The first time Kainos starts, if it finds Thetis, it offers once to copy your Thetis settings (databases, meters, skins and cmASIO settings). Your Thetis settings are not changed. Kainos opens in its own layout either way; switch to Classic in Setup > Appearance > Kainos if you prefer the Thetis look.
- Kainos still identifies itself as Thetis to TCI and TCP CAT clients, so logging and contest software that supports Thetis keeps working. Thetis meter skins work too.

## Before you transmit

Test new transmit features into a dummy load first and check your signal on a second receiver: Kainos Audio's transmit chain, RADE, RTTY (set the RTTY TX level so ALC stays low, with COMP off) and CW.

## Known limitations

- This is the first Kainos release. The installer has been built and checked but not yet installed on many PCs.
- The 3D stacked-trace panadapter is experimental (see above).
- RTTY AFC covers 150 Hz either side of where you tune; tune roughly onto the signal first.
- The CW decoder listens at your CW pitch, so zero-beat the station (0 Beat) for best copy. Very weak or fading signals copy less well than with a dedicated decoder.

## Credits

Kainos would not exist without the people who built Thetis and the software it grew from: Doug Wigley W5WC, Richard Samphire MW0LGE, Reid Campbell MI0BOT, Warren Pratt NR0V (WDSP), Laurence Barker G8NJJ, and the many other Thetis and OpenHPSDR contributors listed in the About window; FlexRadio Systems for PowerSDR; Christos Nikolaou SV1EIA (Thetis-RADE) and David Rowe (RADE); the AetherSDR contributors (AetherVoice and the audio processing); and Ernst, OE3IDE, for his meter skins.

Kainos is free software under the GNU General Public License. The source is in this repository.
