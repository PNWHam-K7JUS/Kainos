# Audio Scope

See what you're hearing, and what you're sending: a waveform and an audio spectrum of RX1's audio in the right-hand column. *(Kainos 1.0.8 and later.)*

## Turning it on

Click **AUDIO SCOPE** in the tab chips at the top of the right-hand column. It's off until you turn it on. Like any tab, you can drag it to where you want it in the column.

It only runs while it's on screen: when the tab is off, scrolled out of view or Kainos is minimised, it costs nothing.

## What it shows

**Receiving**, it shows RX1's audio, including VFO B when **LISTEN** is on (see [[VFO Flags]]). **Transmitting**, it switches to your transmit signal after the TX EQ, compressor and ALC, marked **TX** in red.

### Waveform

The audio as a trace, held still on the signal so you can see its shape.

- **2 / 5 / 10 / 20 ms** buttons set how much time is across the view (50 and 100 ms are on the right-click menu, good for seeing speech as syllables).
- The height scales itself to the signal, and the top right shows the **peak level** in dBFS.
- If the left and right channels differ (for example LISTEN with VFO A and VFO B panned apart), the right channel is drawn as a second, violet trace.

### AF spectrum

The audio's frequencies, with:

- your **receive filter's passband** shaded;
- the **strongest frequency** and its level at the top right, handy for tones and CW;
- a gold **peak-hold** line that slowly falls back;
- an optional **waterfall** underneath.

The span follows the mode:

| Mode | Span |
|---|---|
| SSB | 0 to 3 kHz |
| Digital (DIGU, DIGL) | 0 to 3.5 kHz |
| AM, SAM, DSB | 0 to 6 kHz |
| FM | 0 to 8 kHz |
| CW | 1 kHz around your CW pitch, with the pitch marked |

While you transmit, the spectrum shows your transmit signal on your sideband: a quick way to see what the TX EQ and compressor are doing, and how wide your signal is.

## Controls

- **Click** the scope to **hold** the picture (HOLD shows bottom left); click again to carry on.
- **Right-click** for the options:

| Option | What it does |
|---|---|
| **Waveform / AF spectrum / Both** | What the tab shows. Both puts the waveform above the spectrum. |
| **Scale** | **Auto**, or a fixed full scale from 0 to -50 dBFS, to compare levels. |
| **Timebase** | 2 to 100 ms across the waveform. |
| **Trigger** | On: the waveform holds still. Off: it runs freely, better for noise. |
| **Channels** | Left and right, left only or right only. |
| **AF peak hold** / **AF smoothing** / **Clear peak** | The spectrum's peak line and averaging. |
| **AF waterfall** | A waterfall under the spectrum. |
| **Hold** | The same as clicking the scope. |

Your choices are kept.

## Good to know

- The audio is taken after the **RX1 AF** volume, so the levels in dBFS follow your volume. The scale adjusts for that; to compare levels over time, set a fixed **Scale**.
- It shows RX1 only. RX2 isn't shown.
- It uses the same audio feed as Thetis's own **Scope** display modes, and the two can run together.
- In **light mode** it draws at half the rate.
