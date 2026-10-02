# Kainos development notes

This file records what Kainos changes compared with Thetis, and why, so that future work and upstream merges don't have to rediscover it. For the user-facing description, see [ReadMe.md](ReadMe.md). For how the Thetis code fits together, see [CODEMAP.md](CODEMAP.md).

## The project

Kainos is a fork of Thetis maintained by Justin Cron, K7JUS. It is based on the Hermes Lite 2 edition of Thetis by Reid Campbell, MI0BOT ([mi0bot/OpenHPSDR-Thetis](https://github.com/mi0bot/OpenHPSDR-Thetis)), at release **v2.10.3.15**.

The goal is a Thetis-based SDR application for the HL2 that adds AetherSDR-derived audio processing (AetherVoice on RX and TX) and built-in FreeDV, while staying close enough to Thetis to keep taking its updates.

## Repository layout

| Item | Purpose |
|---|---|
| `main` on GitHub (`kainos-main` locally) | Kainos main line |
| `feature/*` branches | One feature per branch, merged into `kainos-main` once it works |
| `upstream` remote | MI0BOT's HL2 Thetis. `git fetch upstream` brings in new Thetis releases |
| `baseline-working` tag | Unmodified Thetis v2.10.3.15 that builds in Visual Studio 2026 |
| `kainos-identity` tag | Thetis renamed to Kainos (end of roadmap Phase 2) |

Build `Project Files/Source/Thetis_VS2026.sln` as `Release | x64`. The program is built to `Project Files/bin/x64/Release/Kainos.exe`.

## What Kainos changes

### Identity

- The program is `Kainos.exe` (`AssemblyName` in `Thetis.csproj`), with Kainos in the title bar, About window, splash screen, dialogs, tooltips, startup log and Midi2Cat.
- New splash image (`Resources/kainos-splash.png`, 720x307). `splash.cs` now positions the status, progress and countdown overlay from the bottom edge (`layoutOverlay`), so any image size works.
- New icons: `Resources/kainos.ico` is the program icon (Σ at 16-24px, flame + Σ at 32-64px, full art at 128-256px). Every form with an embedded icon uses the Kainos window icon.
- The About window lists K7JUS as a contributor and checks `PNWHam-K7JUS/Kainos` (`main`, `version.json`) for new releases instead of upstream Thetis.

### Coexisting with Thetis

Kainos keeps everything separate so that both programs can be installed on the same PC:

| | Thetis | Kainos |
|---|---|---|
| Settings and databases | `%APPDATA%\OpenHPSDR\Thetis-x64` | `%APPDATA%\OpenHPSDR\Kainos-x64` |
| Registry (cmASIO, startup log) | `HKCU\Software\OpenHPSDR\Thetis-x64` | `HKCU\Software\OpenHPSDR\Kainos-x64` |
| Recordings | `Music\Thetis` | `Music\Kainos` |
| Firewall rules | `Thetis Allow ...` | `Kainos Allow ...` |
| Install folder | `OpenHPSDR\Thetis-HL2` | `OpenHPSDR\Kainos-HL2` |
| Installer UpgradeCode | Thetis's | New GUIDs, so installing Kainos never replaces Thetis |

The registry key name is shared between C# (`clsCMASIOConfig.cs`, `clsProgressLog.cs`) and C++ (`cmASIO/hostsample.cpp`, `lib/portaudio-19.7.0/src/hostapi/asio/pa_asio.cpp`). All of them must always use the same name.

### One-time Thetis settings import

`clsThetisSettingsImport.cs`: on first start, if Kainos has no settings of its own and a Thetis settings folder exists, Kainos offers once to copy the Thetis folder and registry settings. The answer is recorded in `thetis_import_offered.txt` in the Kainos settings folder. The offer is skipped when `-datapath:` is used.

### Compatibility with data saved under the old name

Some settings are saved with .NET `BinaryFormatter`, which records the assembly name. This includes the built-in country data (`Properties.Resources.cty`), meter settings and diversity memories. That data names the `Thetis` assembly, which no longer exists once the program is `Kainos.exe`. `TypeRenameBinder` in `common.cs` maps `Thetis` back to the current assembly. Without it, country lookups fail silently for everyone. Any new code that deserializes with `BinaryFormatter` must use `TypeRenameBinder.Create()`.

### AetherVoice on receive (roadmap Phase 4)

A C port of AetherSDR's exciter (`src/core/ClientPudu.h/.cpp`, commit `20d022d5`). AetherSDR calls the two bands "Poo" and "Doo"; Kainos uses its UI names, Body and Clarity.

| Piece | Where |
|---|---|
| DSP module | `wdsp/aethervoice.c/.h`, GPLv3. Double precision, WDSP `create`/`x`/`destroy` pattern, settings under the channel's `csDSP` lock |
| RXA wiring | `RXA.h` (`aethervoice` member), `RXA.c` (create, destroy, flush, rate and buffer-size hooks), `xrxa` runs it just before `xpanel`, so the volume control still comes after it |
| Exports | `SetRXAAetherVoiceRun`, `SetRXAAetherVoiceMode`, `SetRXAAetherVoiceBody`, `SetRXAAetherVoiceClarity` |
| C# | `dsp.cs` (P/Invoke), `radio.cs` `RadioDSPRX` (`RXAetherVoiceOn`, `RXAetherVoiceMode`, `SetRXAetherVoiceBody/Clarity`, included in `SyncAll` and `Copy`) |
| UI | `setupKainos.cs`: Setup > DSP > AetherVoice tab, built in code so `setup.designer.cs` stays identical to Thetis. Hooked in by two lines in `setup.cs` (constructor and `ForceAllEvents`). The settings apply to all four receivers |

| AetherVoice window | `frmAetherVoice.cs`: a Windows Forms recreation of AetherSDR's AetherVoice editor (`ClientPuduEditor`, `PooDooLogo`, `ClientCompKnob`): glowing logo driven by `GetRXAAetherVoiceWetRms`, Even/Odd and ON buttons, six knobs (drag, Shift for fine, wheel, double-click to reset). It holds no settings of its own: it reads and writes the Setup tab's controls |
| Console | `consoleKainos.cs`: the **Kainos Audio** menu item (see below), and an **AV** button below RX EQ on the phone-mode panel (click toggles, right-click opens the window). Hooked in by one line in the `Console` constructor |
| Skins | The AV button borrows RX EQ's skin images through `Skin.ImageAlias` (a small Kainos change in `Skin.cs`, `SetupCheckBoxImages`), so it matches every skin without new image files |

Design decisions:

- **One source of truth.** Setup's AetherVoice controls own the settings (and save them). The window and the AV button only change those controls, and `Setup.applyAetherVoiceRX` notifies the console, so all three stay in step.
- **Voice modes only.** `RadioDSPRX` runs it only in LSB, USB, DSB, AM, SAM, FM, AM_LSB and AM_USB, re-checking on every mode change, because an exciter would distort CW and digital audio (including audio decoded over VAC).
- **Mono and binaural.** Normally the panel copies I to both ears, so only I is processed. In binaural mode (panel `copy == 0`) I and Q are processed as left and right, sharing one low-band envelope like AetherSDR's stereo path.
- **Level.** The RX AGC normalises audio to a peak of 1.0, the same full-scale level AetherSDR feeds the exciter, so AetherSDR's settings behave the same here. Heavy settings can roughly triple the peak level.
- **Verification.** The port was compared sample by sample against AetherSDR's original code (both modes, mono and stereo, extreme and out-of-range settings): maximum difference 3e-4, from AetherSDR using `float`. Bypass is bit-exact. A real WDSP RX channel was also run through Kainos's own P/Invoke declarations, including the FM 192 kHz rate, buffer-size changes and binaural.

### AetherVoice on transmit (roadmap Phase 5)

A second instance of the same module runs in the TXA chain.

| Piece | Where |
|---|---|
| TXA wiring | `TXA.h`, `TXA.c`: `xtxa` runs it after the TX EQ and its meter, before FM pre-emphasis, the leveler, CFC, compressor, bandpass filters and ALC. The mic audio is mono in I after the panel (`SetTXAPanelSelect` swaps Q into I), so only I is processed |
| Exports | `SetTXAAetherVoiceRun/Mode/Body/Clarity`, `GetTXAAetherVoiceWetRms` |
| C# | `RadioDSPTX` (`TXAetherVoiceOn`, `TXAetherVoiceMode`, `SetTXAetherVoiceBody/Clarity`), voice modes only, re-checked on every TX mode change, included in `SyncAll` |
| Setup | A Transmit group on the AetherVoice tab (`chkAetherVoiceTX`, `udAetherVoiceTX...`), applied to `GetDSPTX(0)` |
| TX profiles | Eight columns (`AetherVoiceTXEnabled`, `AetherVoiceTXMode`, `AetherVoiceTXBodyDrive/Tune/Mix`, `AetherVoiceTXClarityTune/Harmonics/Mix`), hooked into `updateTXProfileInDB`, `loadTXProfile`, `checkTXProfileChanged2`, `getTXProfileChangeReport` and `highlightTXProfileSaveItems` by one line each |
| Window | RX / TX buttons switch the AetherVoice window between the two sets of Setup controls. On TX the logo glows only while transmitting |

Design decisions:

- **Profile columns are added when needed.** `database.cs` is unchanged. `ensureAetherVoiceTXColumns` adds the columns to the TX profile table the first time a profile is saved, and a profile without them (built-in profiles, or databases from Thetis or earlier Kainos builds) loads with AetherVoice off and the default settings. Thetis only rebuilds its database when the version changes, which Kainos doesn't do, so this can't rely on the database upgrade.
- **Placement contains the bandwidth.** Because the TX bandpass filters run after it, anything the exciter adds outside the TX filter is removed.
- **Verification.** A real WDSP TX channel (USB, 200-2900 Hz) driven through Kainos's own `RadioDSPTX`: with AetherVoice off at a normal level, unwanted energy (opposite sideband plus anything more than 500 Hz beyond the filter edges) was 147 dB below the wanted signal; overdriving the mic into the ALC with AetherVoice off gave 50 dB; AetherVoice on at heavy settings (both modes), with the ALC also working, gave 57 dB. The remaining spread comes from ALC gain changes, not the exciter. Switching to CWU stopped the exciter and USB restarted it. This is a simulation: on-air checks into a dummy load are still required.

### AetherTX channel strip (Phase 5b)

Users see these features as **Kainos Audio** (one menu item and window, with Receive and Transmit tabs). The code and these notes keep the AetherSDR-derived names (AetherTX, AetherRX, `AetherStrip`) so it stays clear where the processing comes from.

AetherSDR's channel strip on transmit: gate, de-esser, compressor (with drive, phase rotator and output limiter), tube, reverb and final limiter, around the AetherVoice exciter.

| Piece | Where |
|---|---|
| DSP | AetherSDR's own processors, **copied unchanged** into `wdsp/aethersdr/` (see its README for the source commit and how to update), compiled as C++17 inside `wdsp.dll`. `QtGlobal` is a stand-in for the one Qt type they use |
| Wrapper | `wdsp/aetherstrip.cpp/.h`: one instance per TX channel. `xtxa` runs gate, de-esser, compressor and tube (`xaetherstrip_pre`) before AetherVoice, then reverb and final limiter (`xaetherstrip_post`), all before the Thetis leveler, TX filter and ALC. Converts the I channel to float for AetherSDR and back |
| Exports | `SetTXAStripParam(channel, stage, param, value)` and `GetTXAStripMeter(channel, stage, meter)`; the stage/param/meter numbers are listed in `aetherstrip.h` |
| C# | `RadioDSPTX.SetTXStripParam` caches every value, re-sends them all in `SyncAll`, and enables stages only in voice modes (re-checked on every TX mode change). `AetherStripTX` (`frmAetherStrip.cs`) is the settings model, with AetherSDR's names, ranges and defaults, owned by the console (`console.AetherStripTX`) |
| Window | `frmAetherStrip.cs`, shown to users as **Kainos Audio Processing** and opened from the **Kainos Audio** menu item. Stage list with power lights on the left; each stage page has an ON button, its own mode buttons, AetherSDR-style knobs and a picture: transfer curves for the gate, compressor and final limiter (the same formulas as AetherSDR's `staticCurveGainDb`), the de-esser's band, a tube shaping curve (illustrative), the reverb tail, and level and gain-reduction meters while transmitting. The Exciter page edits the AetherVoice TX settings. BYPASS turns the whole chain off, AetherVoice TX included, without changing any settings |
| TX profiles | The whole strip is one profile column, `AetherStripTX` (`stage.param=value;...`), saved, loaded and compared through the same Setup helpers as AetherVoice TX. A profile without it loads with the strip off |

Design decisions:

- **All stages start off.** AetherSDR's final limiter defaults to on; `create_aetherstrip` turns it off.
- **Voice modes only**, like AetherVoice: a gate, compressor or reverb would wreck CW and digital signals.
- **Chain order is fixed** for now (AetherSDR lets you reorder it), and the EQ stage, REC/PLAY monitor and settings gear aren't ported yet.
- **Verification.** The wrapper's output is identical to driving AetherSDR's classes directly (difference 0.0, every stage on); all stages off is a bit-exact pass-through. On a real WDSP TX channel through `RadioDSPTX`, a -12 dB compressor makeup gave -12.0 dB in USB and 0.0 dB in DIGU (bypassed); every stage on stayed in range.

### AetherRX channel strip

AetherSDR's receive chain, on every receiver: gate, compressor and tube from the same AetherSDR processors as AetherTX, then the AetherVoice exciter. AetherSDR's AetherNR page isn't ported (Kainos has its own NR, NR2, NR3 and NR4), and the EQ stage is still to come.

| Piece | Where |
|---|---|
| WDSP | A second `aetherstrip` instance per receiver in `RXA.c`: `xaetherstrip_pre` runs just before `xaethervoice` and the volume panel. Binaural (panel `copy == 0`) processes I and Q as left and right, as AetherVoice RX does. Exports `SetRXAStripParam`, `GetRXAStripMeter` |
| C# | `RadioDSPRX.SetRXStripParam` caches and resyncs every value (`SyncAll`, and `Copy` between receivers) and enables stages only in voice modes. `AetherStrip` (the model, shared with AetherTX) pushes AetherRX settings to all four receivers |
| Window | One window (`frmAetherStrip`, shown as **Kainos Audio Processing**) holds both chains, with **RX** and **TX** tabs above the stage list (`SetSide`); the single **Kainos Audio** menu item (after Equalizer) opens it on the tab last used, and each tab remembers its last page. RX shows Gate, Compressor, Tube and Exciter (AetherVoice RX); meters follow RX1 while receiving; BYPASS acts on the tab shown and also bypasses that side's AetherVoice |
| Saving | AetherRX settings live in a hidden `TextBoxTS` (`txtAetherStripRX`) on the Setup AetherVoice tab, so Setup saves and restores them with the other options. The Setup tab is built before Setup has its console, so the model is connected on first use (`hookAetherStripRX`) |

Verification: `RadioDSPRX` sends the stage enable as 1 in USB and AM and 0 in DIGU, DIGL and CW; a -12 dB compressor makeup gives -12.0 dB at a real WDSP receiver's output; gate, compressor and tube together run cleanly in mono and binaural.

### Kainos Audio EQ

AetherSDR's parametric EQ (`ClientEq`, copied unchanged) as a strip stage on both sides, in AetherSDR's positions: transmit runs gate, **EQ**, de-esser, compressor, tube; receive runs **EQ**, gate, compressor, tube (`create_aetherstrip`'s `rx` flag picks the order).

- **Bands:** AetherSDR's default 10-band layout (high pass 40 Hz, low shelf 100 Hz, peaks at 200 Hz to 5 kHz, high shelf 8 kHz, low pass 12 kHz), all off and flat until shaped. Each band has frequency, gain, Q, type, on/off and slope; the EQ also has a master gain and a pass-band filter family (Butterworth, Chebyshev, Bessel, Elliptic). Parameter numbers: `AS_EQ_BAND0 + 6 * band + field` (`aetherstrip.h`).
- **Window:** the EQ page has a response graph (20 Hz to 20 kHz) drawn with AetherSDR's own `bandMagnitudeDb`, exported as `GetAetherEqBandMagnitudeDb`, and a numbered handle per band: drag for frequency and gain (or Q on pass bands), mouse wheel for Q, right-click to switch a band on or off; changing a band switches it on, as AetherSDR's editor does. Below: band buttons, Freq / Gain / Q / Slope knobs for the selected band (knobs that don't apply to its type are dimmed), master gain, type and family.
- **Saving:** `AetherStrip.Serialize` now writes only values that differ from the defaults, so a shaped EQ adds a few dozen characters to the TX profile or the RX options; older saved strips still load.
- **Verification:** the wrapper's output is identical to AetherSDR's classes in both chain orders, and the drawing function is identical to `ClientEq::bandMagnitudeDb`.

### Kainos Audio chain order

The chain can be reordered, as AetherSDR's can: drag a stage up or down the stage list (right-click the list to reset to AetherSDR's order). Final Output (the final limiter) always runs last.

- **DSP:** `xaetherstrip` runs the whole chain in its order and calls the AetherVoice exciter (`aethervoice.c`) at its slot (`AS_EXCITER`), converting the audio back to the WDSP buffer for it, then runs the final limiter. `SetTXAStripOrder` / `SetRXAStripOrder` change the order under the channel's DSP lock and refuse duplicates or moving the final limiter. With no strip stage on, only the exciter runs, straight on the WDSP buffer, so AetherVoice alone and everything-off stay bit-exact.
- **C#:** `AetherStrip.Order` (saved as `o=...` only when it differs from the default, so older saved strips still load), pushed by `RadioDSPTX.SetTXStripOrder` / `RadioDSPRX.SetRXStripOrder` and re-sent in `SyncAll`. The stage list is built from the order (`pagesFromOrder`).
- **Verification:** reference checks in the default and a custom order are exact; on a real WDSP TX channel, swapping tube and compressor changed the output by 16.8 dB and swapping back returned to within 0.25 dB.

### Kainos Audio history views and REC / PLAY

- **Curve / History:** each stage graph can switch to a 10-second scrolling history (input and output level, threshold or ceiling, gain reduction from the top), fed from the same 30 Hz meter polling as the bars (`StripViz.drawHistory`). Each page remembers its view.
- **REC / PLAY** (above BYPASS), after AetherSDR's monitor. Built on Thetis's own recorder (`clsAudioRecordPlayback`, `console.ARP`) rather than a new audio path:
  - REC records to `<Thetis audio folder>/kainosaudio/KainosAudioTX.wav` (or `...RX.wav`). On the TX tab it records the **transmitter output** (`AudioRecordTxSource.TransmitterOutputIQ`), so the recording includes Kainos Audio plus the Thetis leveler, TX filter and ALC; on SSB the left channel (I) is the processed voice. On the RX tab it records the receiver output. The user's recording source settings are switched only for the moment the recording starts, then restored.
  - PLAY plays the last recording for that tab through the PC output device set in Thetis's recording settings (`ARP.OutputPCDeviceID`); nothing is transmitted.
  - REC won't stop a recording Kainos Audio didn't start.
  - Not yet tested on the radio: it needs the live audio streams.

### Installer

The publisher is Justin Cron - K7JUS. The Add/Remove Programs comments credit Thetis (W5WC, MW0LGE, MI0BOT, NR0V), the OpenHPSDR community and PowerSDR. The installer has not been built or tested yet.

## Deliberately left as Thetis

| What | Why |
|---|---|
| The `Thetis` code namespace, project and solution file names | Invisible to users. Renaming 185+ files would make every upstream merge painful |
| GPL headers and "found on GitHub: ramdor/Thetis" lines | Required attribution under the GPL |
| TCI protocol name (`Thetis`) and the TCP CAT banner | Logging and contest software detects "Thetis" to enable features |
| Links to Thetis releases, Discord, skin servers and manuals | They point to real Thetis resources |
| Single-instance mutex name | Kainos still warns if Thetis is already running against the radio |
| "Min Thetis Version" for skins | Skins are versioned against Thetis, and Kainos uses the same version numbers |
| Installer banner/background images (`thetis_banner.bmp`, `thetis_background.bmp`) | Not replaced yet |

## Merging new Thetis releases

1. `git fetch upstream`, then merge the new release tag into a branch off `kainos-main`.
2. Expect conflicts mainly in user-visible strings, `.resx` files (form icons and the `, Kainos, Version=` assembly references), `Thetis.csproj`, and the few lines Kainos adds to Thetis files (`setup.cs`, `console.cs`, `radio.cs`, `dsp.cs`, `Skin.cs`, `RXA.c/.h`; search for `Kainos`). Keep the Kainos side for names, paths and icons, and the Thetis side for everything else.
3. Search the merged code for new user-facing "Thetis" text, new `Thetis-x64` registry or folder paths, and new `BinaryFormatter` deserialization (see above).
4. Search for `Kainos` and `// Kainos:` comments to find every Kainos-specific change.
5. Build, then test RX, TX and PureSignal on the HL2 before merging back.

## Licensing

Thetis and WDSP are licensed under the GNU GPL "version 2 or later", and there are no v2-only files in `Project Files/Source`. AetherSDR is GPLv3. Since Phase 4 added AetherSDR-derived code (`aethervoice.c/.h`), Kainos as a whole is distributed under **GPLv3 or later**: the full text is in `LICENSE-GPL-3.0`, and `ReadMe.md` says so. Existing Thetis/WDSP files keep their "version 2 or later" notices; new Kainos files carry GPLv3-or-later headers. Keep every existing copyright notice and the MW0LGE dual-licensing statement (`LICENSE-DUAL-LICENSING`).
