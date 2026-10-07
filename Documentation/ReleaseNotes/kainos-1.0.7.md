# Kainos 1.0.7

The biggest Kainos update yet, based on Thetis 2.10.3.15. See the [1.0.0 notes](https://github.com/PNWHam-K7JUS/Kainos/releases/tag/kainos-1.0.0) for everything Kainos adds, and the new [Kainos wiki](https://github.com/PNWHam-K7JUS/Kainos/wiki) for how to use it. Thanks to everyone on GitHub and Facebook who reported bugs and asked for features.

**New: SWR sweep**
- An **SWR** button in the left-hand column opens the sweep window. Pick a band (or your own range) and Kainos steps a low-power carrier (1 W at most) across it, plotting SWR as it goes.
- Shows the lowest SWR and where it is, and the 2:1 bandwidth.
- **Save** sweeps and tick earlier ones to lay them over the current one, to compare antennas or watch one change over time. **Export CSV** for a spreadsheet.
- Before each sweep, a reminder to turn off any antenna tuner, put any amplifier in bypass or off, and connect an antenna or dummy load. The sweep stops by itself if SWR goes above 5:1 or the output above 1 W, and puts your frequency and tune settings back afterwards.

**New: KiwiSDR panadapter and waterfall**
- A **Waterfall** button in the KIWI tab opens the KiwiSDR you're listening to as a panadapter and waterfall: its live spectrum, your tuned frequency and passband, and a frequency scale.
- Click a signal to tune to it, or use the mouse wheel; Ctrl + wheel or the Zoom buttons zoom from the whole 0-30 MHz down to a couple of kHz. The view follows your frequency.
- **Kiwi as your radio:** with the HL2 off, the Kiwi you're listening to fills the main panadapter area, and the VFO flag, band and mode lists and the mouse wheel all tune it. Power the HL2 on and its own panadapter comes back. Turn this off with **Main view** in the KIWI tab.
- **Follow** now steps through **Follow A**, **Follow B** and off, so the Kiwi can listen on VFO B while the HL2 is on VFO A. Following keeps working with the KIWI tab closed.
- Tuning from the Kiwi's view tunes the VFO being followed (A or B), or the Kiwi on its own with Follow off.
- The Kiwi's view shows DX cluster and POTA spots as call tags (click one to tune to it) and the same band plan as the main panadapter. Right-click it for the waterfall's **Contrast** and **Speed**; the zoom you choose on each band is remembered.

**New: arrange the right-hand column your way**
- Drag a section by its title in the column (RX, METERS, BAND ...), or its tab at the top of the column, to a new place. A gold marker shows where it will land.
- Or right-click a tab for Move up, Move down, Move to the top or bottom, and Reset the order.
- Your order is saved; tabs added in future versions go at the end.

**New: safety nets**
- **Report a bug** at the top right of the menu bar (and in the Setup menu): describe what happened and Kainos opens a new GitHub issue with diagnostics filled in (versions, Windows, screen, layout, radio, recent errors). Nothing is sent until you press Submit on GitHub; you can also copy it all to paste elsewhere.
- **Setup > Back up settings now**: one click backs up your current settings. Restore a backup, or keep several settings profiles, in Setup > Database Manager.
- **Update notice**: once a day Kainos checks for a new version and, if there is one, shows what's new with Download, Later and Skip this version. Setup > Check for updates checks right away.

**New: light mode**
- For slower PCs: turns the 3D panadapter off and lowers the display to 20 frames a second. In the Setup menu and Setup > Appearance > Kainos, and suggested by the setup wizard on PCs with 4 or fewer processor threads. Turning it off puts your frame rate and 3D back.

**New: licence-aware band plan**
- A band along the bottom of the panadapter shows what each part of the band is used for (CW, DIGITAL, SSB, BEACONS, AM, SATELLITE, FM, from the ARRL band plan, or IARU Region 1's outside the Americas), with small tags above it at the popular spot frequencies (FT8, FT4, WSPR, PSK31, SSTV, AM, QRP).
- Its colour shows where you may transmit: gold all modes, ice CW and data, violet CW only, red out of privileges (labelled "OUT OF PRIVILEGES" there). It's see-through, so signals still show.
- Uses the country and licence class from the setup wizard, or set them in Setup > Appearance > Kainos > Band plan (where it can also be turned off).
- Full privileges are built in for the United States (Technician, General, Amateur Extra; HF and 6 m) and Canada; elsewhere the band edges are shown. It's a guide: always check your own licence.

**New: easier scrolling in the right-hand column**
- A scroll bar down the right-hand side when the open tabs don't all fit: drag it, or click above or below it.
- The mouse wheel anywhere over the column now scrolls the column instead of changing whatever slider is under the pointer. Hold Ctrl to use the wheel on a slider or list (GitHub issue #4).

**Changed: the VFO SYNC tab in the Kainos look**
- VFO Sync, Rx Ant, VFO lock A / B, tune step, quick memory (Save / Restore) and the band stack are now in rows like the rest of the right-hand column, with Kainos's rounded buttons instead of the old scaled-down Thetis panel. The Classic layout is unchanged.

**New: one-line VFO flags**
- Click the A or B on a VFO flag to shrink it to one line (mode, frequency and TX), as in SmartSDR; click it again for the full flag. The frequency still tunes with the wheel and can be clicked to type one (GitHub issue #4).

**Fixed**
- Setup and the other windows were hard to read in places (GitHub issue #3): text is now brighter (near white, with clearer group titles), greyed-out options are a readable grey instead of dark blue, the radio list on Setup > General no longer shows light text on a light highlight, the Meters/Gadgets lists show their names in white, and any text that ends up light on a light box (like the "TX Profile modified" notice) is switched to dark.
- The FreeDV Reporter's station list was hard to read (white rows behind light text): it's now dark like the rest of Kainos, with darker red / green / plum shades for stations transmitting, just heard, or with a new message, and its tool bar is dark too.
- The VFO flag's MODE tab could open empty (no mode or filter buttons) when Thetis's Legacy Items "Hide mode / filter button grid" options were on (GitHub issue #6).
- KiwiSDR in CW, following VFO A: the Kiwi was off by the CW pitch. It now hears a signal on your VFO at your own CW pitch (GitHub issue #5).

## Installing

Run **Kainos-1.0.7-x64.msi**. It replaces any earlier Kainos version and keeps your settings. Kainos installs alongside Thetis and doesn't change it.
