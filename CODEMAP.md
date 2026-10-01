# Kainos code map

Notes on how the Thetis code that Kainos is built on fits together, written while working through roadmap Phase 3. Line numbers are approximate and will drift; search for the names instead. For what Kainos has changed, see [KAINOS.md](KAINOS.md).

All paths are under `Project Files/Source/`.

## The big picture

Kainos is three layers that talk through DLL calls:

```
 HL2 radio ──UDP──> ChannelMaster.dll ──> wdsp.dll ──> ChannelMaster.dll ──> speakers / VAC / ASIO
 (Protocol 1)       networking, routing    the DSP       audio mixing
                          ▲                   ▲
                          │ P/Invoke          │ P/Invoke
                    Kainos.exe (C#): UI, settings, CAT/TCI, meters, display
```

| Layer | Language | Folder | Built to |
|---|---|---|---|
| UI and control | C# (.NET Framework 4.8, WinForms) | `Console/` | `bin/x64/Release/Kainos.exe` |
| DSP engine (WDSP, by NR0V) | C | `wdsp/` | `bin/x64/Release/wdsp.dll` |
| Radio I/O and audio routing (ChannelMaster) | C | `ChannelMaster/` | `bin/x64/Release/ChannelMaster.dll` |
| ASIO audio | C++ | `cmASIO/` | `bin/x64/Release/cmASIO.dll` |
| PortAudio | C | `../lib/portaudio-19.7.0` | `bin/x64/Release/PA19.dll` |

**Build output gotcha:** the build log prints `wdsp.vcxproj -> ...\Project Files\build\x64\Release\wdsp.dll`, but that's only the intermediate folder (`.lib`, `.pdb`). The linker's `OutputFile` writes the DLL straight into `bin/x64/Release/`, next to `Kainos.exe`, which is the copy that gets loaded. A DLL's timestamp only changes when its own source changes, so an old date on `wdsp.dll` is normal if WDSP wasn't touched.

## C# side (`Console/`)

| File | Size | What it is |
|---|---|---|
| `console.cs` | ~55k lines | The main window class `Console` (namespace `Thetis`). `Main()` (~line 1477) does early checks, the single-instance check, FFT wisdom, then creates `Console`. The constructor (~598) sets the settings folder, shows the splash, creates components, loads the database (`DBMan.LoadDB`), then calls `InitConsole()` (~1777). Mode changes: `SetRX1Mode()` (~34769). Power: `PowerOn` property (~19928) |
| `console.Designer.cs` | ~8k | Layout of the main window (designer generated) |
| `setup.cs` / `setup.designer.cs` | ~38k / ~78k | The Setup window, every tab. Most settings live here as properties that push values into `radio`/`console` |
| `radio.cs` | ~4.4k | `Radio`, `RadioDSPRX` (~270) and `RadioDSPTX` (~2471). C# wrapper objects holding each DSP setting, and calling WDSP when it changes |
| `dsp.cs` | | `WDSP` static class: ~250 `[DllImport("wdsp.dll")]` declarations, plus `WDSP.id(thread, subrx)` mapping receivers to WDSP channel numbers |
| `cmaster.cs`, `ivac.cs`, `HPSDR/NetworkIOImports.cs` | | P/Invoke declarations for ChannelMaster (mixing, VAC, network) |
| `database.cs`, `clsDBMan.cs` | ~12k | Settings database (an XML DataSet) and the Database Manager. TX profiles are the `TXProfile` table (`AddTXProfileTable`, ~4303) |
| `display.cs`, `PanDisplay.cs` | ~12k | Panadapter and waterfall drawing (DirectX). Leave until last (roadmap Phase 7) |
| `MeterManager.cs` | ~45k | The MultiMeter system and meter skins |
| `CAT/CATCommands.cs`, `TCIServer.cs` | | CAT and TCI remote control |
| `eqform.cs` | | RX/TX equalizer window: a good, self-contained example of UI → DSP |
| `common.cs` | | Helpers: `SaveForm`/`RestoreForm`, `TypeRenameBinder`, version helpers |

### How a DSP setting flows (example: RX EQ on/off)

1. UI: `eqform.cs` sets `console.radio.GetDSPRX(0, 0).RXEQOn = enable`.
2. `radio.cs`, `RadioDSPRX.RXEQOn`: caches the value, and if it changed calls `WDSP.SetRXAEQRun(WDSP.id(thread, subrx), value)`.
3. `dsp.cs`: `[DllImport("wdsp.dll", EntryPoint = "SetRXAEQRun", CallingConvention = Cdecl)]`.
4. `wdsp/eq.c`: `PORT void SetRXAEQRun(int channel, int run)` takes the channel's `csDSP` lock and sets `rxa[channel].eqp.p->run`.

A new DSP feature (AetherVoice) follows the same four steps.

### How settings are saved

- **Setup controls** are saved automatically by `Common.SaveForm`/`RestoreForm` (`common.cs` ~368/425), keyed by control name. This works only for the Thetis control types ending in `TS` (`CheckBoxTS`, `NumericUpDownTS`, `ComboBoxTS`, `RadioButtonTS`, `TextBoxTS`, `TrackBarTS`…), so new controls should use those types and stable names.
- **TX profiles** are rows in the `TXProfile` table. A new per-profile setting needs a column (`t.Columns.Add(...)` in `AddTXProfileTable`), defaults in each built-in profile, and save/load code in `setup.cs` (search for `TXEQEnabled` as the pattern).

## WDSP (`wdsp/`)

Each receiver or transmitter is a WDSP **channel** (`channel.c`, `OpenChannel`). Data is processed in blocks of `dsp_size` **complex** samples: interleaved `double` I/Q pairs, even for audio after demodulation.

### Channel rates (set in `ChannelMaster/cmaster.c`)

| | Input rate | DSP rate | DSP buffer size |
|---|---|---|---|
| Receive (RXA) | radio sample rate | **48 kHz**, or **192 kHz in FM** (`SetRX1Mode` → `WDSP.SetDSPSamplerate`) | 4096 at start, then per-mode setting from Setup (`DSPPhoneRXBuffer`, …) via `SetDSPBuffsize` |
| Transmit (TXA) | mic/VAC rate | **96 kHz** | per-mode setting (`DSPPhoneTXBuffer`, …) |

A module must therefore cope with the rate and buffer size changing while running. That's what the `setSamplerate_*`/`setSize_*` hooks are for.

### RXA chain (`RXA.c`, `xrxa`)

In order: input shift and resampler → generator → ADC meter → notch/bandpass (`nbp0`, `bpsnba`) → S-meter → **demodulators** (`amd`, `fmd`, FM squelch) → `snba` → **RX EQ** (`eqp`) → noise reduction position 0 (ANF, NR, NR2/`emnr`, NR3/`rnnr`, NR4/`sbnr`) → bandpass `bp1` → carrier removal (`cbl`, pre-AGC option) → **AGC** (`wcpagc`) → noise reduction position 1 (same modules, post-AGC option) → `bp1` → AGC meter → siphon (display tap) → `cbl` → CW peaking filters (`doublepole`, `matched`, `gaussian`, `speak`, `mpeak`) → syllabic squelch → **`panel`** (volume, pan, binaural) → AM squelch → output resampler.

**AetherVoice RX (Kainos, Phase 4):** `xaethervoice` runs between `ssql` and `panel`, so it sees demodulated, noise-reduced, AGC-levelled audio, and the volume control still works after it. See KAINOS.md.

### TXA chain (`TXA.c`, `xtxa`)

In order: input resampler → generator → `panel` (mic gain) → phase rotator → mic meter → downward expander → **TX EQ** (`eqp`) → EQ meter → FM pre-emphasis → **leveler** → CFC (continuous frequency compressor + post-EQ) → bandpass `bp0` → **COMP** compressor → `bp1` → CESSB overshoot control → `bp2` → **ALC** → AM/FM modulators → output generator (TUN, two-tone) → up-slew → ALC meter → siphon → **PureSignal** correction (`iqc`) → compensating FIR → output resampler.

**AetherVoice TX insertion point (Phase 5):** after `eqp`/EQ meter and before the leveler, matching the roadmap ("after the mic EQ, before the compressor/CFC, leveler, bandpass and ALC").

### Anatomy of a module (pattern: `eq.c`, `eqp`)

| Piece | EQ example |
|---|---|
| State struct in the `.h` | `typedef struct _eqp { int run; int size; double* in; double* out; double samplerate; ... } eqp, *EQP;` |
| `create_` | `create_eqp(run, size, ..., in, out, ..., samplerate)` |
| `destroy_`, `flush_` | `destroy_eqp`, `flush_eqp` |
| `x` (process one block) | `xeqp(a)`: if `run` process, else copy `in` → `out` (`size * sizeof(complex)`) |
| `setBuffers_`, `setSamplerate_`, `setSize_` | Called when buffers, rate or size change |
| Exported setters | `PORT void SetRXAEQRun(int channel, int run)`, wrapped in `EnterCriticalSection(&ch[channel].csDSP)` |

Wiring a new module into RXA touches: the `rxa` struct in `RXA.h`, then `create_rxa`, `destroy_rxa`, `flush_rxa`, `xrxa`, `setDSPSamplerate_rxa` and `setDSPBuffsize_rxa` in `RXA.c` (search for `eqp` and copy each spot). Also include the header in `comm.h` and add the files to `wdsp.vcxproj`. TXA has the same set of functions in `TXA.c`.

## ChannelMaster (`ChannelMaster/`)

| File | What it does |
|---|---|
| `networkproto1.c` | Protocol 1 (the HL2 uses it). `MetisReadThreadMainLoop_HL2` receives IQ and mic samples; `WriteMainLoop_HL2` sends TX IQ and control frames |
| `netInterface.c`, `network.c` | Exported control functions (frequencies, attenuator, PTT…) and protocol plumbing (`sendOutbound`) |
| `cmbuffs.c` (`Inbound`) | Buffers incoming samples and calls `xcmaster(stream)` once a block is ready |
| `cmaster.c` (`xcmaster`) | **The per-block routing.** Receiver: noise blankers → panadapter (`Spectrum0`) → `fexchange0` (= WDSP RXA) → audio mixer. Transmitter: ASIO/TCI input → VOX/expander → `fexchange0` (= WDSP TXA) → sidetone → monitor mix → TX gain → interleave → `OutBound` |
| `pipe.c` (`xpipe`) | Hooks at position 0 (before WDSP) and 1 (after WDSP) on every stream. A possible tap point for FreeDV (Phase 6) |
| `aamix.c` | Audio mixer combining receivers to the outputs |
| `ivac.c` | VAC (virtual audio cable) in/out |
| `sidetone.c`, `vox.c`, `txgain.c` | CW sidetone, VOX/anti-VOX, TX gain and amp protection |

`fexchange0(channel, in, out, &error)` is the single call where ChannelMaster hands a block to WDSP and gets the processed block back.

## Startup sequence (simplified)

1. `Main()`: creates the settings folder, runs the single-instance check, then creates `Console` and calls `Application.Run`.
2. `Console()` constructor, in order: DLL version checks (`checkVersions`), settings folder (and the one-time Thetis import), splash, PortAudio init thread, `InitializeComponent()`, database load (`DBMan.LoadDB`), then `radio = new Radio(AppDataPath)` (~858). The `Radio` constructor calls `cmaster.CMCreateCMaster()`, which creates ChannelMaster and opens the WDSP channels (`OpenChannel`).
3. `InitConsole()` (~1777) continues setup: restoring saved settings into the UI and DSP.
4. Power on (`PowerOn = true`) starts the network threads and the radio streams.
