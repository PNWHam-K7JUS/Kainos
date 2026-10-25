# KiwiSDR

Listen to [KiwiSDR](http://kiwisdr.com) receivers around the world from inside Kainos, with their own panadapter and waterfall, even with your HL2 off.

## Listening

1. Open the **KIWI** tab in the right-hand column. It lists the nearest public KiwiSDRs (search by place, call, antenna or address).
2. **Click** a receiver to listen. The tab shows its status; the VFO A flag shows the Kiwi's signal reading.
3. **Vol** sets its volume and **Out** chooses the output device. **Stop** ends it.

The list shows the nearest eight with a free channel that cover your frequency. **Show more** under it adds ten at a time; a new search or **Refresh** starts again from eight. Right-click a receiver to make it a favourite (★, listed first).

**A receiver that isn't listed?** Press **Add Kiwi** and type its address as you'd open it in a browser (for example `kiwi.example.com:8073`; 8073 is used when there's no port). Kainos checks it and adds it at the top of the list, and checks it again on each refresh. Right-click one you've added to remove it.

## Follow

The **Follow** button steps through:

- **Follow A:** the Kiwi tunes wherever VFO A is, in its mode.
- **Follow B:** the Kiwi follows VFO B (with RX2's mode when RX2 is on), so it can listen on B while your HL2 is on A.
- **Off:** tune the Kiwi on its own from the KIWI tab.

In CW the Kiwi is tuned so signals sound at your own CW pitch.

## The Kiwi's panadapter and waterfall

Press **Waterfall** for a window with the Kiwi's live spectrum and waterfall, your tuned frequency and passband, spots and the [[Band Plan]].

- **Click** a signal to tune to it, or use the **mouse wheel**. Click a spot's tag to tune to that station.
- **Ctrl + wheel** or **Zoom - / +** zoom from the whole 0-30 MHz down to a couple of kHz; **Centre** brings it back to your frequency. Each band remembers its zoom.
- **Right-click** for the waterfall's **Contrast** and **Speed**.

## The Kiwi as your radio

With **Main view** on (the default), when your HL2 is **off** and you're listening to a Kiwi, the Kiwi fills the main panadapter area. Your VFO flag, the band and mode lists and the mouse wheel all tune it. Power the HL2 on and its own panadapter comes back.

## Etiquette

KiwiSDRs are run by volunteers with a few listening slots each. Kainos identifies you to the owner with your callsign. Please don't leave a Kiwi connected when you're not listening.
