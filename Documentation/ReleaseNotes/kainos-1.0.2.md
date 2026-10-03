# Kainos 1.0.2

A fix update to [Kainos 1.0.0](https://github.com/PNWHam-K7JUS/Kainos/releases/tag/kainos-1.0.0), based on Thetis 2.10.3.15. See the 1.0.0 notes for everything Kainos adds. Thanks to LB3AG for reporting these ([issue #1](https://github.com/PNWHam-K7JUS/Kainos/issues/1)).

## Fixed

- **SPLT (split) didn't work.** Clicking SPLT in the left dock now turns split on and off. With split on (and RX2 off), the VFO tab shows a VFO B box marked SPLIT (or QUICK SPLIT), and VFO B's marker appears on the panadapter.
- **Part of the right column could go blank.** The analog meter can no longer grow tall enough to push the sections below it out of view, and each section is laid out on its own, so a problem in one can't blank the rest. If it ever happens, Kainos writes the cause to `%APPDATA%\OpenHPSDR\Kainos-x64\KainosErrors.txt`; please attach that file to a GitHub issue.

## Installing

Run **Kainos-1.0.2-x64.msi**. It replaces any earlier Kainos version and keeps your settings. Kainos installs alongside Thetis and doesn't change it.
