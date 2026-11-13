# Kainos 1.0.17

Based on Thetis 2.10.3.15. See the [Kainos wiki](https://github.com/PNWHam-K7JUS/Kainos/wiki) for how to use everything below. Almost everything in this release came from your reports and ideas: thank you, especially Misfit-138, Sam NR4A, Sven N2VEN, ke3ci, szproton and ruediger-moeller.

**Fixed: 6 m on ANAN radios.** Kainos starts out set up as a Hermes Lite 2, whose top frequency is 38.4 MHz, and choosing an ANAN never put that back. So on a G2 or G2E, VFO A stopped at 38.400 when you tuned 6 m (GitHub #60). Other radios now get the usual 61.44 MHz. On a Hermes Lite 2 itself, which really does stop at 38.4 MHz, picking 6 m now tells you so (it needs a transverter) instead of half going there.

**New**
- **Custom filters** (GitHub #79): right-click a filter button (the FILTER drop-down, or the VFO flag's MODE drawer) for **Edit the 2.4k filter...**: the filter editor opens on that filter, to rename it and set its width and shift, as in Thetis. There's also **Reset the filters to their defaults...**.
- **PureSignal can start afresh after a QSY or a long receive** (GitHub #69), in the Linearity window (right-click PS-A), both off by default. **After a QSY** (the transmit frequency moved more than 1 kHz since the last transmission) or **after N minutes of receive** (an LDMOS amplifier that has cooled), the old correction is thrown away as you key up and auto-calibrate starts again with a fresh sample. Auto-calibrate mode only.
- **AGC in the VFO flag's DSP drawer** (GitHub #87): AGC mode and AGC GAIN for VFO A too, handy in the Compact view. Right-click the gain for automatic AGC (the knob turns green); right-click its name for Setup's AGC page.
- **NNR's reduction depth in the VFO flag's DSP drawer** (GitHub #82), 6 - 40 dB with the mouse wheel or - / +.
- **The MAIN and SUB balance sliders have a centre** (GitHub #83): a mark in the middle, a gold knob when centred, a catch at the centre while dragging, and right-click (or double-click) to centre.
- **Easier-to-read S meter numbers on the VFO flag** (GitHub #89): bright white, a size larger.
- **RTTY: 450 Hz shift**, for the German weather service's (DWD) weather RTTY at 50 baud.

**Fixed**
- **The Master AF slider forgot its setting** and came back at 30 after every restart (GitHub #91), as did the other sliders in the right-hand column.
- **The FreeDV Reporter showed no stations, or wouldn't open** (GitHub #66), on PCs where another program has installed an older copy of a library Kainos uses (Newtonsoft.Json). Your call now shows on the Reporter again.
- **Meter containers came up with EQ, Leveler, CFC and Compression greyed out** (GitHub #74), as if those were switched off, until one was switched or the container's background reset. (A Thetis bug.)
- **A module installed in Setup > Modules was forgotten if you then closed Setup with Cancel**, and so were some Kainos settings changed elsewhere (RTTY, CW, the column's tabs) if Setup was opened and cancelled afterwards.
- **Setup's headings in the list on the left wouldn't close** (GitHub #81): clicking an open heading now folds it away.

**Modules** (from Setup > Modules: update each one that offers a new version)
- **Olivia / MFSK 0.1.1**: "OLIVIA 8/250" no longer stays on the panadapter in every mode (GitHub #86). `<RX>` and `<TX>` typed in the box work as in the messages (GitHub #80). The received text follows the newest line, stays put while you scroll back to read, and has a **Clear** button (GitHub #75).
- **PSK 0.2.1**, **DominoEX / THOR 0.1.1**: the same panadapter marker and scrolling fixes.
- **Hell 0.1.1**: the same panadapter marker fix.

## Installing

Run **Kainos-1.0.17-x64.msi**. It replaces any earlier Kainos version and keeps your settings. Kainos installs alongside Thetis and doesn't change it.

Updating Kainos doesn't update the modules: open **Setup > Modules** afterwards and click **Update** on each one that has a new version.
