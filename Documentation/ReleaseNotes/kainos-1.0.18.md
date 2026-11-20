# Kainos 1.0.18

Based on Thetis 2.10.3.15. See the [Kainos wiki](https://github.com/PNWHam-K7JUS/Kainos/wiki) for how to use everything below. Almost everything in this release came from your reports and ideas: thank you, especially Sam NR4A, Misfit-138, Juha NI2M, F4GMS, W3UB, WB2QLL and devonian.

**New**
- **Spot to the DX cluster, and choose the cluster** (GitHub #68). The SPOTS tab has **Spot...**: a small window with the frequency, their call and a comment. It shows the exact line the cluster gets, under your callsign, and sends nothing until you press Send. Right-click a spot in the list to spot that station again. **Server** picks the cluster: NC7J (as before), W3LPL, VE7CC, K1TTT, the Reverse Beacon Network (receive only), or any other address.
- **PureSignal: start afresh on every TX** (GitHub #95). The Linearity window's Start afresh row (right-click PS-A) has a third choice, **On every TX**. Auto-calibrate only.
- **AM carrier level on the TX tab** (GitHub #117). In AM and SAM, the TX tab has **AM CARRIER**: Setup > Transmit's carrier level, with the mouse wheel or - / +.
- **Brighter text** (GitHub #94). Setup > Appearance > Kainos > **Brighter text** shows Kainos's captions, tab names, hints and status lines in near white instead of grey.
- **Kiwi: TX hold after the un-key can be set** (Discussion #54). The KIWI tab has **TX hold** under Vol, 0 - 3000 ms, 300 ms to start with (it was a fixed 1.5 s, which cut off the start of the reply).
- **NNR in the DSP menu** (GitHub #116): Classic's DSP menus have NNR after NR4.
- **cmASIO from Thetis** (GitHub #102). If Thetis is set up for cmASIO and Kainos isn't, Kainos asks once at start-up whether to use the same settings. Thetis is not changed.

**Fixed**
- **Classic layout: the left column's sliders were gone or out of place, and Pan and Zoom sat on the waterfall** after a switch from the Kainos layout (GitHub #93). Classic now looks as it does when Kainos starts in it, however often you switch.
- **Panadapter colours set in Setup didn't last in the Kainos layout** (GitHub #112): Kainos's own colours came back at every start. A colour you change in Setup > Appearance > RX Display now stays yours. To have all of Kainos's colours back, untick and tick **Kainos colours** in Setup > Appearance > Kainos.
- **CW and RTTY: F1 - F6 did nothing unless the typing box had the focus** (GitHub #99). They now work wherever the focus is while the CW or RTTY pane is open, docked or popped out.
- **A floating meter container could vanish behind Kainos** (GitHub #101) with pin-on-top off. It now stays above Kainos (but not above other programs).
- **A red attenuator box over the right-hand column while transmitting** (GitHub #105), after the column had scrolled. It now moves with the S-ATT box.

**Tips**
- **A skin's display picture** (GitHub #100) is covered by Kainos's navy background. Untick **Kainos background** in Setup > Appearance > Kainos to see it.
- **RTTY at half power?** The RTTY pane's own **TX** level starts at -10 dB; set it to 0 dB for full power.

**Modules** (from Setup > Modules: update each one that offers a new version)
- **Olivia / MFSK 0.1.2** (GitHub #105): starts at 1500 Hz, where fldigi and most Olivia nets are. The typing box has a scroll bar, and the received text no longer leaves stale blocks behind.

## Installing

Run **Kainos-1.0.18-x64.msi**. It replaces any earlier Kainos version and keeps your settings. Kainos installs alongside Thetis and doesn't change it.

Updating Kainos doesn't update the modules: open **Setup > Modules** afterwards and click **Update** on each one that has a new version.
