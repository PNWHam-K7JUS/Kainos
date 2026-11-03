# Getting Started

## The first start

If Thetis is on the same PC, Kainos asks how you'd like to start:

- **Set up for my Hermes Lite 2** (recommended): fresh settings and a few questions.
- **Import my Thetis settings:** copies your Thetis databases, meters, skins and cmASIO settings (Thetis isn't changed), then asks the same questions. Close Thetis first.
- **Skip:** Kainos's default settings.

## The setup wizard

The wizard sets Kainos up for your radio: a Hermes Lite 2, an ANAN or another HPSDR radio. Nothing changes until you press **Apply** on the last page. It's offered once after installing or updating, and you can run it again any time from **Setup > Appearance > Kainos > Run setup wizard** or the **Setup** menu.

| Page | What it asks |
|---|---|
| **Station** | Your callsign and grid square (used for spots, FreeDV RADE and the FreeDV Reporter), your country and licence class (used by the [[Band Plan]]). |
| **Radio** | Which radio you have (the model in Setup > General > H/W Select). Once Kainos has connected to your radio, this page shows which board it reported and the models that go with it. **BrickSDR and Brick2:** choose **HERMES**. They report a Hermes II board, but set up as an ANAN-10E (as Thetis settings brought over often are) PureSignal won't correct. |
| **Hardware** | For a Hermes Lite 2: which boards it has (the N2ADR filter board, the HL2 I/O board, the built-in PA, and band data for an external amplifier; clock and latency options under *Show advanced options*). For an ANAN or other radio: where its own options are in Setup, and how to get PureSignal correcting. |
| **Audio** | The HL2 has no speaker output of its own, so receive audio and your microphone go through the PC. Pick your speakers and microphone, and press **Play a test tone**. Other radios have their own audio; tick this only to use the PC's instead. |
| **Look** | Kainos or Classic layout, UI scale, light mode, and which right-hand column tabs start open. |
| **Summary** | Exactly what will change. Press **Apply**. Leave *Then open the first-transmit check* ticked to test your transmitter next. |

## The first-transmit check

**Tools > First-transmit check** runs two short tests into a dummy load (or a tuned antenna):

- **Send a carrier:** TUN for 3 seconds at your tune power. It reports the power out and SWR, or what to check if nothing comes out. A high SWR stops it.
- **Talk test:** switch to USB or LSB, press it and talk for 6 seconds. It transmits at 10 % drive and says whether your microphone is too quiet, too loud (the ALC) or right.

## PureSignal

If PureSignal (**PS-A**) won't correct, transmit a two-tone (**2TON**) at your usual full drive with PS-A on, then press **Use GetPk** in **Tools > Linearity**. Some radios measure well away from the default SetPk, and PureSignal can't correct until it matches. In the Kainos layout, the **PS** light in the status bar shows how it's doing: green correcting, amber feedback too strong or too weak, red no correction. Hover over it for what to do.

## Connecting to the radio

Press **POWER** at the top of the left-hand column. If Kainos doesn't find your radio, see **Setup > General > H/W Select** (Discover) and [[Troubleshooting and FAQ]].

## Next

Have a look at [[The Kainos Layout]] to find your way around.
