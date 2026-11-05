# Kainos 1.0.15

Based on Thetis 2.10.3.15. See the [Kainos wiki](https://github.com/PNWHam-K7JUS/Kainos/wiki) for how to use everything below. Thanks to everyone who sent ideas and bug reports, and to Ezio F5MWA and Anton for finding why PureSignal wouldn't correct on a Brick2.

**New: setting up is easier**
- **The setup wizard now sets up ANAN, BrickSDR and other HPSDR radios too**, not only the Hermes Lite 2. A new Radio page picks the model, shows which board your radio reported the last time it connected (gold if the model you chose doesn't go with it), and tells BrickSDR and Brick2 owners to choose **HERMES**: set up as an ANAN-10E, as Thetis settings brought over often are, PureSignal won't correct (GitHub #36).
- **First-transmit check** (Tools > First-transmit check, and offered at the end of the wizard): two short tests into a dummy load. **Send a carrier** keys TUN for 3 seconds and reports the power out and SWR, or what to check if nothing comes out. **Talk test** keys MOX at 10 % drive for 6 seconds while you talk, and says whether your microphone is too quiet, too loud or right.
- **PureSignal: Use GetPk** (Tools > Linearity, GitHub #36). Sets SetPk to the peak PureSignal measured, for radios that measure well away from the default. The button turns gold when the two are more than 15 % apart.
- **A PureSignal light in the status bar**, while PS-A is on: green when it's correcting, amber when the feedback is too strong or weak, red when there's no correction. Hover to see why; click for the Linearity window.

**New: other**
- **Keep VAC on/off when the TX profile changes** (GitHub #59), in Setup > Transmit, off by default. Changing mode can load another TX profile, which could turn VAC off; with this on, VAC 1 and VAC 2 stay as they are.
- **Your own meter containers show in the right-hand column** (GitHub #61), each with a tab of its own (METER 2, METER 3, or named from its Notes), instead of disappearing after a restart.
- **Modules can send CW and talk to each other** (`IKainosKeyer`), which lets the Logbook's contest page send CQ, the exchange and TU from Enter and F1 - F3: CW through Kainos's keyer, phone through the Voice Keyer.
- **The Modes menu is now Digi Modes.**
- **Experimental modules are marked in Setup > Modules**, so you know which ones haven't been tested on the air yet.

**Fixed**
- **A meter you couldn't move covered the power button in Classic** (GitHub #70). The Kainos layout's own meter (which has no title bar) came back at the top left when Kainos started in Classic. It now stays off in Classic.
- **A harmless I/O board line in KainosErrors.txt looked like a fault** (GitHub #71). "I/O board check at power-on: no reply" is now written only with the HL2 I/O board box ticked; without a board, no reply is expected.
- **The FreeDV Reporter button could do nothing** (GitHub #66). It now says why if the window can't open, makes a fresh window when needed, and brings it onto a screen and in front.
- **A bright band across the waterfall when you stop transmitting** (GitHub #67). The waterfall now waits a moment after un-keying, until the spectrum no longer holds your own transmission.
- **QuickRecord's PLAY drove AM to only half power** (GitHub #63). A new **Full level** option (Setup > Audio > Recording, on by default) scales the recording so its loudest moment is just below full scale, so it drives the transmitter as your microphone does and still sounds as recorded.
- **The "ErrorLog.txt has been updated" message asked you to email Thetis's author** (GitHub #65). It now points to Report a bug, for the Kainos GitHub.
- **"Value of '32' is not valid" on a Hermes Lite 2** (GitHub #62) when changing mode with RX2's attenuator at 32 dB.
- **The CW and RTTY windows' scroll bar jumped to the top once the window filled** (GitHub #64), and the text could stop following.
- **The Xiegu GPA100 (or another CI-V amplifier) didn't reconnect when Kainos was reopened.** If yours shows "No COM port chosen", pick the port (or click Find GPA100) once more.

**Modules** (from Setup > Modules)
- **Digital Modes 0.11.0**: a **Continents** filter for band activity (GitHub #44), and new modes **Q65**, **FST4**, **FST4W** and **JT4**, all decoded by WSJT-X's jt9 (included). These four, like FT2, are marked experimental: they pass the self-test against WSJT-X's own encoders and decoder, but haven't been on the air yet.
- **Logbook 0.6.2** (new): a log with callsign lookups (QRZ, HamQTH), uploads to LoTW, eQSL, QRZ Logbook and Club Log (each off until you turn it on), POTA spots and activations, 37 contests with dupe checking, scores and Cabrillo, DXCC / WAS / WAZ / grid awards, and a search box (Ctrl+F). The digital-mode modules' QSOs come into it by themselves.
- **Voice Keyer 0.3.0**: plays a slot when another module asks (the Logbook's contest messages).
- New and **experimental** keyboard modes, compatible with fldigi but not yet tested on the air against it (reports welcome):
  - **PSK 0.2.0** (GitHub #38): PSK31 - PSK500, QPSK31 - QPSK500, PSK-R and 8PSK, with a browser that reads every PSK signal in the passband at once.
  - **Hell 0.1.0** (GitHub #38): Feld Hell, Slow Hell, Hell x5 / x9, FSK-Hell 245 / 105 and Hell 80.
  - **DominoEX / THOR 0.1.0**: DominoEX Micro - 88 and THOR Micro - 100.
  - **Olivia / MFSK 0.1.0** (GitHub #38): Olivia and Contestia (4/125 to 64/2000) and MFSK4 - MFSK128L.

## Installing

Run **Kainos-1.0.15-x64.msi**. It replaces any earlier Kainos version and keeps your settings. Kainos installs alongside Thetis and doesn't change it.

The modules come from **Setup > Modules**. The Logbook's contest messages need Kainos 1.0.15; the other modules also run on Kainos 1.0.12 and later.
