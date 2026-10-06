# Kainos 1.0.6

An update to [Kainos 1.0.0](https://github.com/PNWHam-K7JUS/Kainos/releases/tag/kainos-1.0.0), based on Thetis 2.10.3.15. See the 1.0.0 notes for everything Kainos adds.

## New: setup wizard for the Hermes Lite 2

A few questions and Kainos sets itself up for your HL2:

- **Your station:** callsign and grid square (used for DX cluster spots, FreeDV RADE and the FreeDV Reporter), and your country and licence class (Kainos will use these for licence-aware band plans in a later version).
- **Your HL2:** which boards you have: the N2ADR filter board, the HL2 I/O board, the built-in power amplifier, and band data (Band Volts) for an external amplifier. Clock and latency options are under "Show advanced options".
- **Audio:** the HL2 has no speaker or headphone output of its own, so receive audio and your microphone go through the PC. Pick your speakers and microphone, and play a test tone to check. If you've had no receive audio, this is the page to look at.
- **Look and feel:** Kainos or Classic layout, UI scale, and which right-hand column tabs start open.
- **Summary:** exactly what will change, before anything is set. Nothing changes until you press Apply.

The wizard is offered once after updating (choose "Not now" to skip it), and you can run it any time from Setup > Appearance > Kainos > **Run setup wizard**.

On a new install with Thetis on the same PC, the first-run question is now a choice: set up for my HL2, import my Thetis settings, or skip.

## Installing

Run **Kainos-1.0.6-x64.msi**. It replaces any earlier Kainos version and keeps your settings. Kainos installs alongside Thetis and doesn't change it.
