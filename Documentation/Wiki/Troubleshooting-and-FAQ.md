# Troubleshooting and FAQ

## No receive audio

The HL2 has no speaker output of its own: audio goes through the PC (VAC 1).

- Run the setup wizard's **Audio** page (**Setup > Run setup wizard**), pick your speakers and press **Play a test tone**.
- Check **Master AF** and **RX1 AF** in the **RX** tab, and that squelch (**SQL**) isn't on.

## The Band, Mode or Filter lists are empty

Thetis's **Setup > Appearance > Legacy Items** options "Hide band / mode / filter button grid" (often brought over from Thetis) used to empty them. Kainos 1.0.4 and later work whatever those options are; on older versions, untick them.

## My Thetis meter containers disappeared

Thetis meter containers **docked** on the main window are hidden in the Kainos layout (they used to cover the right-hand column). Make one a **separate window** (**Setup > Appearance > Meters/Gadgets**) to use it in the Kainos layout; the Classic layout shows them all.

## Kainos doesn't find my radio

- Check the HL2 is on and on the same network.
- **Setup > General > H/W Select**: press **Discover**, and check the network settings there.
- Only one program can use the radio at a time: close Thetis (or anything else connected to it).

## Kainos is slow on my PC

Turn on **Light mode** (Setup menu), and turn off the 3D panadapter.

## A KiwiSDR disconnects me straight away

The receiver may be full, or allow one connection per address. Try another one from the list.

## Where are my settings?

`%APPDATA%\OpenHPSDR\Kainos-x64`. Errors Kainos catches are written to `KainosErrors.txt` there; the bug report includes the latest lines.

## Can RX2 use a different antenna from RX1?

No: the HL2 has one receiver input, so both receivers share the selected antenna. See [[HL2 I/O board antennas|HL2 IO Board Antennas]].

## Is there a Linux or Mac version?

Not at the moment. Kainos is built on Thetis, which is a Windows program.
