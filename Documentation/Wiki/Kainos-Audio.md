# Kainos Audio

Audio processing for the voices you hear and your own transmitted voice, ported from [AetherSDR](https://github.com/aethersdr/AetherSDR). Open it from the menu bar's **Audio** menu (**Kainos Audio: Receive** or **Kainos Audio: Transmit**; in the Classic layout, **Kainos Audio** in the menu bar); it has **Receive** and **Transmit** tabs.

## Receive

On every receiver: parametric EQ, gate, compressor, tube saturation and the **AetherVoice** exciter. Saved with your settings.

## Transmit

Gate, parametric EQ, de-esser, compressor, tube saturation, AetherVoice, reverb and a final limiter. Saved in each **TX profile**.

## Where it sits in the transmit chain

Kainos Audio runs inside the transmit DSP, after Thetis's EQ and before the leveler, CFC, compressor and ALC. So anything the strip adds is still caught by the leveler, the TX filter and the ALC. Your mic goes through, in this order:

1. **VOX / DEXP** (downward expander and VOX)
2. **Mic gain**
3. **Phase rotator**
4. **TX EQ** (Thetis's equalizer)
5. **Kainos Audio** (the transmit strip). By default: gate, parametric EQ, de-esser, compressor, tube, AetherVoice, reverb. You can change the order in the Kainos Audio window; a VST3 plug-in goes wherever you put it. The **final limiter** always runs last.
6. **FM pre-emphasis** (FM only, if set before the leveler)
7. **Leveler**
8. **CFC** (with its post-CFC EQ)
9. **TX filter**
10. **COMP** (the compressor/processor)
11. **CESSB** (overshoot control)
12. **ALC**
13. **AM / FM modulator** (in those modes)
14. **TUNE and 2-TONE** are generated here, after all the processing above
15. **PureSignal** correction, then out to the radio

What reaches step 1 is your mic, or instead: VAC audio, a REC / PLAY recording, the Voice Keyer, FreeDV RADE or the RTTY and digital modes' own audio. RADE overs skip the compressor, CFC and EQ, and Kainos Audio is bypassed in CW and the digital modes. A REC / PLAY recording skips Kainos Audio too while **Disable Kainos Audio** is ticked (Setup > Audio > Recording; on by default).

## AetherVoice

AetherVoice adds low-end body and high-end clarity to voice. Turn it on with the **AV** button (in the TX tab, and on the flag), right-click **AV** for a small AetherVoice window, or set it in **Setup > DSP > AetherVoice**.

## Good to know

- Everything works in **voice modes only** and is bypassed automatically in CW and digital modes, so decoders are never affected.
- On transmit, **test into a dummy load** and check your signal on a second receiver before using it on the air.
