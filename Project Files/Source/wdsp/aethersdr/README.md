# AetherSDR channel-strip processors

These files are copied **unchanged** from [AetherSDR](https://github.com/aethersdr/AetherSDR) `src/core/`, commit `20d022d5`, by the AetherSDR contributors, and are licensed under the GNU GPL version 3. Kainos compiles them into `wdsp.dll` as C++17 and drives them through `wdsp/aetherstrip.cpp`.

| File | Processor |
|---|---|
| `ClientGate` | Downward expander / noise gate |
| `ClientEq` | Parametric EQ (up to 16 bands) |
| `ClientDeEss` | De-esser |
| `ClientComp` (+ `ClientPhaseRotator`) | Compressor with drive, phase rotator and output limiter |
| `ClientTube` | Tube saturation |
| `ClientReverb` | Reverb (Freeverb) |
| `ClientFinalLimiter` | Final brickwall limiter |

`QtGlobal` is a Kainos stand-in for the one Qt header these files include (`ClientFinalLimiter` uses its `quint64` type), so they build without Qt.

To update from a newer AetherSDR, copy the same files over these, keep `QtGlobal`, rebuild, and check the compiler output and the parameter mapping in `aetherstrip.cpp` against the new headers.
