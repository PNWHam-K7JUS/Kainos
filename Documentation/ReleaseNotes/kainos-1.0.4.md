# Kainos 1.0.4

A fix update to [Kainos 1.0.0](https://github.com/PNWHam-K7JUS/Kainos/releases/tag/kainos-1.0.0), based on Thetis 2.10.3.15. See the 1.0.0 notes for everything Kainos adds. Thanks to everyone in the Hermes Lite 2 group who reported these.

## Fixed

- **Band, Mode and Filter drop-downs did nothing** for some people who imported their Thetis settings. Thetis's Setup > Appearance > Legacy Items "Hide band / mode / filter button grid" options emptied them. The drop-downs now work whatever those options are.
- **The right-hand column could be squeezed or blank after importing Thetis settings**, hiding the RX tab's volume and squelch (SQL), so it seemed there was no receive audio. Thetis meter containers docked on the main window sat where the column goes. In the Kainos layout they are now hidden (the Classic layout shows them again). Meter containers in their own windows are left alone, so you can still use one in the Kainos layout by making it a separate window (Setup > Appearance > Meters/Gadgets). You no longer need to delete your containers.

## Installing

Run **Kainos-1.0.4-x64.msi**. It replaces any earlier Kainos version and keeps your settings. Kainos installs alongside Thetis and doesn't change it.
