# Kainos 1.0.3

A small update to [Kainos 1.0.0](https://github.com/PNWHam-K7JUS/Kainos/releases/tag/kainos-1.0.0), based on Thetis 2.10.3.15. See the 1.0.0 notes for everything Kainos adds. Thanks again to LB3AG for the suggestion ([issue #2](https://github.com/PNWHam-K7JUS/Kainos/issues/2)).

## Changed

- **Slice flags no longer hide spots.** The VFO flags on the panadapter covered the spots, TCI flags and skimmer markers drawn along its top. Now:
  - The flags are **see-through** while the mouse isn't over them, and solid when you point at them, while one of their tabs is open or while you type a frequency. Choose how see-through in Setup > Appearance > Kainos > Slice flags (Solid, 90%, 75%, 60% or 45%; 75% by default).
  - You can **drag a flag down** the panadapter by its face (anywhere but the frequency and the TX button). Kainos remembers where each flag is. Double-click the face to put it back at the top.

## Installing

Run **Kainos-1.0.3-x64.msi**. It replaces any earlier Kainos version and keeps your settings. Kainos installs alongside Thetis and doesn't change it.
