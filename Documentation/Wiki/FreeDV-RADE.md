# FreeDV RADE

Built-in [FreeDV](https://freedv.org) **RADE** digital voice (the Radio Autoencoder, V1 and V2) on RX1, RX2 and transmit, ported from [Thetis-RADE](https://github.com/sv1eia/Thetis-RADE) by Christos Nikolaou, SV1EIA. No separate FreeDV program or virtual audio cables needed.

## Using it

1. Open **FreeDV** in the menu bar.
2. Pick **RX1** or **RX2** and press **RADE**. The receiver switches to DIGU or DIGL, and received RADE is decoded straight to speech.
3. Transmit as normal: your overs are sent as RADE, with your callsign in the end-of-over frame.

The FreeDV window shows sync, SNR, frequency offset, levels and the last callsign heard. Settings (callsign, levels, mic noise reduction, AGC and EQ) are in **Setup > DSP > FreeDV (RADE)**.

## On the panadapter

While a receiver has RADE on, its panadapter shows sync, SNR, level, clip and the last callsign at the top right. RADE readings can also be added to meter containers (**Setup > Appearance > Meters/Gadgets**).

## FreeDV Reporter

The **Reporter** button in the FreeDV window shows who's on RADE right now ([qso.freedv.org](https://qso.freedv.org)), by band or following your frequency. Double-click a station to tune to it. Stations transmitting show in red, just heard in green, and new messages in plum.

To appear on it yourself, tick **Report my station** in **Setup > DSP > FreeDV (RADE)**, with your callsign and grid square. Nothing about you is sent unless that box is ticked.
