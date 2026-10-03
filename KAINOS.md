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
  - Confirmed working on the radio (transmit into a dummy load, played back on the PC).

### FreeDV RADE (roadmap Phase 6)

Built-in FreeDV RADE (V1 and V2) on RX1, RX2 and the transmitter, ported from [Thetis-RADE](https://github.com/sv1eia/Thetis-RADE) by Christos Nikolaou, SV1EIA (source taken at commit `408f2b5`, his version 2.10.3.21). Everything from Thetis-RADE is ported except its diagnostics (bypass switches and log snapshots) and the console-face mirror controls.

- **Libraries** (`Project Files/lib`, built once by `build_rade_libs.bat` into each library's `build/x64/<config>`): `radae_c` (rade.lib, the C port of RADE), `opus_dnn` (xiph/opus at `940d4e5`, CMake with DEEP_PLC, DRED and OSCE: FARGAN and LPCNet features), `rnnoise`, `libebur128`, `WebRTC_AGC`, plus sources compiled straight into ChannelMaster: `r8brain` (pffft) and `freedv_text` (FreeDV-GUI's `rade_text` LDPC callsign codec and a codec2 slice). Thetis-RADE's repository leaves out opus's and rnnoise's `x86/` source folders (a broad `x86/` gitignore rule); Kainos restores them from upstream (opus `940d4e5`, rnnoise `70f1d25`, the commit its sources match) and `.gitignore` has exceptions for them. rnnoise's model is the one Thetis's NR3 already uses; the script copies it from `NR_Algorithms_x64`. `util/sanitizers.h` is Thetis-RADE's stub for freedv-gui's header.
- **ChannelMaster** (unchanged from Thetis-RADE): `radae.c` (the modem wrapper: resamplers, FIFOs, sync, end-of-over, meters), `radae_micdsp.c` (RNNoise, BS.1770 AGC and limiter, 3-band EQ before the encoder), `r8brain_wrap.cpp`. `pipe.c` calls `xradae_rx` on each receiver's audio after WDSP (decoded in place, so speakers, VAC and recordings all get speech) and `xradae_tx` on the mic before WDSP (the modem replaces the mic audio, so WDSP transmits it as DIGU/DIGL audio). `create_radae`/`destroy_radae` run in `create_pipe`/`destroy_pipe`.
- **C#:**
  - `kainosRade.cs`: the P/Invokes (`Rade`), and `Rade.OnMox`, called from `Audio.MOX`, which decides at key-down whether the over is RADE (RADE on, not TUN or 2-TONE, not a VFO B over with RX2 on) and sends begin-over / end-of-over and the MOX state to the modem.
  - `consoleRade.cs`: `SetRadeEnabled` (RX1 to DIGL below 10 MHz and DIGU above, DIGU on 60 m; NR1-4, NB2 and ANF off; RX1 back to its old mode when switched off; refuses while transmitting) and the PTT arbiter from Thetis-RADE. A RADE over is keyed as normal; its release, from the MOX button or any PTT source, is held off (`radeInterceptMox` re-asserts MOX) while the modem sends the end-of-over frame and 60 ms of silence, then 300 ms more, then the arbiter un-keys (3 s cap). The MOX letters turn orange meanwhile. It runs in `PollPTT` (`radePollTick`), which skips its normal work during the flush.
  - `setupRade.cs`: Setup > DSP > FreeDV (RADE), TS controls saved by name: callsign (8 characters, RADE's limit), V1/V2, mic and RX levels, mic RNNoise, AGC (target LUFS) and EQ. Applied by `applyRade()` from `ForceAllEvents` and on every change. RADE on/off itself is not saved: Kainos always starts with it off.
  - `frmFreeDV.cs`: the FreeDV window (menu bar > FreeDV), Kainos Audio style: RADE on/off, V1/V2, sync lamp, SNR (-10 to +40 dB bar), offset, last callsign heard and when, RX and mic meters with clip, Mic and RX knobs, mic processing toggles and a Settings button. It polls the modem every 50 ms.
  - Hooks in Thetis files (all marked `Kainos`): `audio.cs` (`Rade.OnMox`), `console.cs` (`PollPTT`, `chkMOX_CheckedChanged2`, and MI0BOT's HL2 digital-mode AF lock in `SetRX1Mode`, skipped while RADE owns RX1's AF), `radio.cs` (`RXOutputGain`: RX1's AF slider sets the decoded-speech level after the decoder, and WDSP's output stays at unity), `cmaster.cs` (`CMSetTXAPanelGain1`: mic/VAC gain held at unity while RADE transmits), `setup.cs`, `setupKainos.cs`, `consoleKainos.cs`.
- **FreeDV Reporter** (`Console/FreeDVReporter/`): Thetis-RADE's Socket.IO client for qso.freedv.org (`FreeDVReporterClient`, on .NET's `ClientWebSocket` and Newtonsoft.Json, no extra packages), its station list window (`FreeDVReporterForm`: band filter, Track RX1 / RX2 by band or frequency, double-click to tune, QSY requests, column choice, saved geometry) and `Maidenhead`, all nearly unchanged. `FreeDVReporterManager` is reduced to RX1 and given Kainos's rules:
  - connected while the Reporter window is open (Reporter button in the FreeDV window, lit while connected), and also while RADE is on with "Report my station" ticked;
  - publishes our station (role `report`: callsign, grid, frequency, TX state, decoded callsigns and SNR, message) only while RADE is on, "Report my station" is ticked and a callsign and grid are set; otherwise it connects as a viewer and sends nothing about us;
  - settings typed in Setup apply after a second's pause, and reconnects (the server fixes role and identity per session) run off the UI thread.
  - Settings on Setup > DSP > FreeDV (RADE): grid square, message, Report my station (off by default), Ignore QSY requests, Show times in UTC. `SetRadeEnabled` and `applyRade` call `FreeDVReporterManager.Update` / `Configure`.
  - Verified: the client connected to qso.freedv.org as a viewer and received the live station list (42 stations); the window renders it.
- **RX2:** the modem in `radae.c` already has a decoder per receiver and one encoder that follows the transmitting receiver (`SetRadaeTxRx`, with that receiver's V1/V2). Kainos adds:
  - `SetRadeEnabled(rx, on)` for both receivers (RX2 needs RX2 on, and its RADE goes off when RX2 is switched off); the encoder is on while either receiver has RADE on.
  - `Rade.OverReceiver` / `OverWouldBeRade`: a VFO B over with RX2 on is RX2's (RADE if RX2 has RADE on), any other over is RX1's. An over from a receiver without RADE goes out as plain voice.
  - RX2's AF slider sets RX2's decoded-speech level (`radio.cs`), MI0BOT's HL2 digital-mode AF lock in `SetRX2Mode` is skipped while RADE owns RX2, and NR/NB2/ANF are switched off on RX2.
  - Setup: RX2 version and RX2 level. The FreeDV window has RX1 / RX2 buttons; it collects the last callsign heard on both.
  - The reporter reports one station: RX1 when RX1 has RADE on, otherwise RX2 (its frequency, decodes and SNR).
- **TX processing during RADE overs:** Thetis's TX profile follows RX1's mode, so an RX2 RADE over while RX1 is in a voice mode would put the modem signal through the voice profile's processing. `Rade.OnMox` therefore switches off WDSP's TX compressor, CFC, overshoot control and EQ at the start of every RADE over and restores them from their settings (`RadioDSPTX`, Setup's CFC box) at un-key. The leveler and phase rotator are left alone (slow and linear). Thetis-RADE instead swaps RX1's mode for VFO B overs; Kainos doesn't, because that also toggles VAC auto-enable and the digital-mode settings mid-over.
- **Verification (RX2):** the round-trip harness, extended to two receivers on different versions, encoded and decoded on RX2 (V1: callsign "K7JUS", correlation 0.91, also at 5 dB SNR; V2: correlation 0.96) while RX1 on the other version never synced, and the same with the receivers swapped.
- **Meters and panadapter overlay** (Thetis-RADE's, unchanged): `MeterManager.cs`, `display.cs`, `ucMeter.cs` and `frmMeterDisplay.cs` take Thetis-RADE's RADE hunks verbatim. They were applied by a script that diffed each file against Thetis-RADE's copy, took every hunk except the ones that would undo Kainos's "Thetis" -> "Kainos" text, and turned `cmaster.GetRadae*` into `Rade.GetRadae*`; every hunk was RADE code (checked one by one).
  - Meters: `Reading.RADAE_*` (80 onwards, after the existing values) and `MeterType.RADAE_*` (before `LAST`) for sync, SNR, RX level, clip and the end-of-over callsign per receiver, and the TX mic level and clip; bar items with RADE scales (`generalScale` gains two optional arguments) and a callsign text item (`clsRadaeCallText`). The enums only gained values at the end, so saved meter layouts load unchanged.
  - Containers: "Hide if no RADE" (`ContainerHidesWhenRADENotEnabled`), saved with the container; `containerShouldHide` combines it with "Hide if RX not in use".
  - Overlay: `drawRadeOverlayDX2D` at the top right of each panadapter while that receiver has RADE on (sync, SNR, smoothed level bar, clip, callsign), and on the transmitting panadapter during MOX (mic level, clip).
  - Kainos side: `consoleRade.cs` feeds the readings from the meter loops (`radeMeterReadings(rx)`, `radeMeterReadingsTX()`, hooked in `console.cs`; idle values while RADE is off), provides the names the meter code uses (`RadaeRx1Enabled`, `RadaeRx2Enabled`, `RadaeEnabledChangedHandlers`, fired by `SetRadeEnabled`) and the overlay switches (`RadeMeasureRx1/Rx2/Tx`). `setupRade.cs` adds the switches (Setup > DSP > FreeDV (RADE) > RADE status on the panadapter, on by default), the "Hide if no RADE" box in the meter container options and `radeMeterBlock`, used by `setup.cs`'s `findIndexForInsertOfSpecialItem` (two hook lines) to list the RADE meters with the RX and TX meters.
  - Verification: builds; Kainos starts with existing meter containers and saves; the Setup controls render. The overlay and meters themselves are Thetis-RADE's code and are checked on the radio.
- **Kainos Audio** is bypassed automatically, since RADE runs in DIGU/DIGL. Thetis's own TX processing still follows the TX profile, so use a DIGU/DIGL profile with the EQ, leveler, CFC and compressor off.
- **Version 2** sends no callsign: its end-of-over frame carries no data bits.
- **Verification:** a test program compiled `radae.c` and the same libraries with a stand-in `pcm`, encoded 16 s of synthesized speech with `xradae_tx` (end-of-over sent as the arbiter does) and decoded it with `xradae_rx`. V1, clean: sync in 0.9 s, SNR about 32 dB, callsign "K7JUS" decoded, speech envelope correlation 0.91. With noise, V1 held sync to 0 dB SNR and decoded the callsign to 4 dB. V2: sync in 0.4 s, correlation 0.96. Block sizes 128 to 1024 all worked. The end-of-over flushed in 300 to 460 ms. Kainos started, ran and saved the new settings with RADE built in; the window and Setup tab were checked in off-screen renders. On-air testing is next.

### Kainos layout (roadmap Phase 7)

Kainos is the default layout (Setup > Appearance > Kainos > Layout): a fresh install, or settings brought over from Thetis (which has no such setting), start in it; Classic is the Thetis look. Settings saved by an earlier Kainos build (which defaulted to Classic) are switched to Kainos once, the first time this version starts (the hidden `txtKainosLayoutSet` marks it done); after that the user's choice is kept.

The console redesign agreed on the design canvas ("Kainos Console Concepts", concept E): the splash-screen colours, a left dock in place of the top controls, one SmartSDR-style slice flag per receiver, an AetherSDR-style right column of toggle tabs (METERS first, with OE3IDE's FTDX-5000 multimeter offered as a download on first use), and RX2 as a second panadapter. It is built in stages, each tested on the radio; Thetis's designer files are not changed, and Setup > Appearance > Kainos > Layout switches between **Classic** (the Thetis console exactly as the skin draws it) and **Kainos**.

- **Display scaling:** Thetis does not declare itself DPI-aware (the `dpiAware` entry in `app.manifest` is commented out), so Windows scales the whole program at 125 % / 150 %; at 1920×1080 and 150 % Kainos has 1280×720 to lay out in. The Kainos layout is designed to fit that. Making the program DPI-aware (sharper text) would affect every Thetis window and is left for a separate test.
- **Stage 1 (foundation):**
  - `KainosUI.cs`: the palette (navy background, panels, ice blue, gold for VFO A and selection, violet for VFO B, red only for TX) and `KainosToolStripRenderer` for the menu and status bars.
  - `consoleKainosLayout.cs`: `Console.KainosLayout`. Kainos mode replaces the skin's console background with the navy, gives the panels and group boxes the skin draws with a background image the panel colour, and renders the menu and status bars in the palette; every original (images, colours, renderers) is kept and put back in Classic. `KainosApplyTheme()` runs again after a skin is loaded (one hook line in `setup.cs`), because a skin brings back its own images.
  - Menu fix: the menu items Kainos adds in code ("Kainos Audio", "FreeDV") now take the Thetis items' text colour; they were drawn in the default black and looked disabled.
  - `setupKainosUI.cs`: Setup > Appearance > Kainos, Layout = Classic / Kainos (saved; applied by `applyKainosUI()` from `ForceAllEvents`).
  - The panadapter and waterfall colours are unchanged until the last stage.
- **Stage 2 (left dock):** `consoleKainosDock.cs`.
  - `KainosDock` stands in for Thetis's `panelPower` (POWER, RX2) and `panelOptions` (MON, TUN, MOX, 2TON, DUP, PS-A, xPA, REC, PLAY), plus VOX, in three groups. Each dock button is drawn by Kainos (`KainosUI.DrawButton`) and bound to the real Thetis `CheckBox`: a left click calls the box's `OnClick` (it toggles and runs Thetis's Click handler, e.g. `chkMOX_Click`), a right click calls its `OnMouseDown`/`OnMouseUp` with the right button (Thetis's settings shortcuts). The dock redraws on the boxes' `CheckedChanged`, `EnabledChanged`, `VisibleChanged` and `TextChanged`, and lists only the boxes Thetis shows (VOX lives on the phone panel, so it leaves in CW and digital modes).
  - Thetis's two panels are collapsed to zero size, not hidden, so Thetis can still show and hide them (its collapsed layouts, "Mon/Tune panel" options) and the dock follows their `Visible`; it covers the area from `panelPower` down to `panelSoundControls`, repositioned on their `LocationChanged` (Thetis's `ResizeConsole` moves them) and the console's `SizeChanged`. Classic restores their sizes; a skin load re-collapses them.
  - Status bar: Fwd (`calfwdpower`), SWR (`alex_swr`, red from 2.0) and ALC gain reduction (`WDSP.CalculateTXMeter(1, ALC_G)`), polled every 250 ms while transmitting, before Thetis's fill label. The Kainos status renderer keeps each status item's own colour.
  - `KainosUI.Scale` (Setup > Appearance > Kainos > UI scale, 75–200 %): sizes the dock's text and spacing.
  - Checked: Kainos and Classic layouts captured from the running program; Classic shows Thetis's panels and the skin unchanged.
- **Stage 3a (right column: METERS, BAND):** `consoleKainosColumn.cs`.
  - `KainosColumn` (a `Panel`) runs down the right side, `KainosUI.S(300)` wide: a row of toggle tabs (AetherSDR's applets), then the panels of the tabs that are on, scrolled with the mouse wheel when they don't fit. It is placed after every Thetis move (`LocationChanged` on the console's controls, the console's and `panelDisplay`'s `SizeChanged`), between any Thetis panels that still reach into that space, and narrows `panelDisplay` to end before it; Classic calls `ResizeConsole` so Thetis lays the console out again. Which tabs are on is saved (`txtKainosColumnTabs`, a hidden Setup box).
  - Thetis controls come in three ways: Kainos-drawn stand-ins (`KainosButtonGrid`, like the dock: band, mode and filter buttons, the filter shift Reset); Thetis controls that Thetis never moves, moved into the column and back in Classic (filter width / shift sliders, low / high boxes and their labels; Thetis only looks for the radio buttons inside `panelFilter`); and controls Thetis does move, pinned over the column (the meter container). Thetis's band, mode, filter and multimeter panels are collapsed to zero size while the column shows them; a resize or skin load that sizes one again is caught and re-collapsed.
  - METERS: a meter container of its own, created once the console is shown (after MeterManager has restored the saved containers, so it is never duplicated): `AddMeterContainer(1, false)`, an `ANANMM` multimeter, no title, automatic height; its ID is saved (`txtKainosMeterId`). MeterManager owns and saves it; Kainos sizes it to the column width, pins it in place and turns it off (`enableContainer`) in Classic or when the tab is off.
  - BAND: the band buttons of whichever band panel Thetis shows (HF, GEN or VHF, swapped by its VHF+ / HF buttons), mode, filter, then width, shift, low and high.
  - Checked: the column, METERS (a live multimeter at the column width) and BAND captured from the running program; a second start reused the same meter container.
- **Stage 3b (RX, TX, single-column dock):**
  - RX tab: Thetis's Master AF, RX1 AF, RX2 AF and AGC gain sliders (label above, slider full width), AGC mode, preamp or step attenuator (whichever Thetis shows), and squelch, moved into the column. TX tab: drive (and tune power when Thetis shows it), then the mode panel Thetis shows for the current mode (phone, CW, digital or FM: mic, COMP, VOX, DEXP, TX profile, RX/TX EQ, AV...), pinned. The column is `KainosUI.S(312)` wide; the mode panels are laid out for 336 pixels, so the shown one is scaled down to fit (place, size and font of the panel and everything in it, `kainosFitPanel`), with the exact originals kept and put back in Classic, after a skin load and when the UI scale changes.
  - The column's content area is a viewport `Panel` under the tab bar, so controls scrolled out of it are clipped; the column never changes the `Visible` of a Thetis control (Thetis shows RX2 AF only with RX2 on, swaps preamp and step attenuator...). Pinned controls are parked off screen when their tab is off or they are scrolled out.
  - Thetis's `ExpandDisplay` (also run at start-up) re-parents the AF, AGC and preamp controls to `panelSoundControls`: the column watches `ParentChanged` on every control it moved in, takes Thetis's new parent and place as the Classic home, and moves the control back.
  - Thetis's collapsed display (its Collapse menu) has its own layout: while it is collapsed the dock and column step aside (everything moved in goes home) and come back when it is expanded.
  - Tab state: "meters,band,-rx": tabs turned off are written with "-"; a tab not listed (new in a later version) starts on.
  - The dock is a single column (`KainosUI.S(80)` wide) from the top of the left side to the status bar, and the panadapter starts beside it (`panelDisplay.Left`, put back in Classic).
  - Checked: captured with all tabs on and with BAND off (TX shows drive and the phone panel).
- **Stage 3c (EQ, KAINOS AUDIO, FREEDV, MEMORY; METERS menu):** `consoleKainosTabs.cs`. New tabs start off (`AddSection(..., defaultOn: false)`; the tab state lists every tab, "-" for off).
  - EQ: RX EQ and TX EQ (clicks and right clicks on Thetis's `chkRXEQ` / `chkTXEQ`), Equalizer... (Thetis's EQ window).
  - KAINOS AUDIO: a chip per stage of the receive and transmit chains in the strip's chain order (final limiter last on transmit), on / off through `AetherStrip.Set(stage, 0, ...)` (Exciter through the AetherVoice Setup box), BYPASS, Open... (Kainos Audio on that side; right click on a stage does the same).
  - FREEDV: a status line (RADE off, or sync, SNR and the last callsign of the RADE receiver), RADE RX1 / RX2, FreeDV..., Reporter....
  - MEMORY: the first 12 memories (name or group, frequency, mode); a click calls `Console.RecallMemory`. Memories... opens Thetis's Memory window.
  - `KainosActionGrid` (code-driven Kainos buttons), `KainosTextLine`, `KainosMemoryList`.
  - METERS right-click menu (on the container and its display area): Multimeter / Cross needle / Magic eye (`RemoveMeterType` + `AddMeter`; the type is saved, `txtKainosMeterType`), Meter settings... (`ShowMultiMeterSetupTab`), Get the FTDX-5000 meter skin (OE3IDE)..., Hide meters.
  - FTDX-5000: offered once, when the METERS container is first created (`txtKainosFtdxOffered`), and from the menu. Kainos reads OE3IDE's skin list (the server Kainos lists) for the current link to "FTDX-5000 (multimeter)" and hands it to `ThetisSkinService.DownloadFile` as a meter skin; Setup's own download handler unpacks it into the Meters folder and refreshes the meters. Nothing of OE3IDE's is shipped with Kainos.
  - Checked: captured with the new tabs on, and with RX / TX only (the scaled phone panel).
- **Stage 4a (slice flag):** `consoleKainosFlag.cs`.
  - SmartSDR's slice flag on the panadapter beside the VFO A line: the letter (A, gold), antenna (from Thetis's RX antenna status), filter width (the selected filter, or the width), active DSP (NR / NB / SNB / ANF / BIN), TX (outlined on the transmit VFO, filled red while transmitting), the mode (RADE while RADE is on) and the frequency as Thetis shows it.
  - The panadapter is drawn by DirectX into `pnlDisplay`'s window; in Thetis's "flip" present mode a flip-model swap chain covers any child window over it, so the flag is a separate borderless window owned by the console (`WS_EX_NOACTIVATE` and `MA_NOACTIVATE`: it never takes the focus) that draws on top whatever the present mode.
  - Placed every 80 ms (and on the console's `Move` / `Resize`) with `HzToPixel((VFOAFreq - CentreFrequency) * 1e6)`, the conversion Thetis uses for its own filter overlay (it allows for CTUN, RIT, XIT, zoom and pan); left of the line, or right when there's no room; hidden when the VFO is off the panadapter, the console is minimised, the display is collapsed, or in Classic.
  - Checked: screen capture of the running program (the flag beside the VFO line).
- **Stage 4b (flag tabs, RX2 flag, wheel tuning, VFO tab):** `consoleKainosFlag.cs`.
  - Tabs under each flag: AUDIO (that receiver's AF slider, MUTE, BIN), DSP (its DSP buttons), MODE (mode and filter buttons), RIT/XIT (on/off, zero, the offsets), VAC (VAC1 / VAC2; right click opens their setup as in Thetis), FREEDV (RADE for that receiver, sync / SNR / last callsign, FreeDV...). A click opens the tab's drawer under the flag; a second click closes it.
  - `KainosSlider` drives a Thetis `PrettyTrackBar`: it sets `Value` and calls its `OnScroll`, as dragging Thetis's slider would.
  - RX2's flag (B, violet) is on RX2's panadapter, shown when RX2 is on and the display is split (RX2 has the lower half): `HzToPixel((VFOBFreq - CentreRX2Frequency) * 1e6, 2)`.
  - The mouse wheel over a frequency digit tunes by that digit (SmartSDR's); elsewhere on the flag by Thetis's tune step. Locked VFOs don't move. The wheel is marked handled so it doesn't reach the console's own wheel tuning (the column's scrolling likewise).
  - VFO tab, first in the right column: the same face for VFO A (and B while RX2 is on), without the tabs, with wheel tuning.
- **Stage 5 (S meter, VFO boxes gone, VFO SYNC tab, band / mode / filter drop-downs):**
  - An S meter bar under the frequency on every VFO face (flags and the VFO tab): Thetis's signal strength plus its calibration and preamp offsets (`RXOffset`), S1-S9 over the first 60% (S9 = -73 dBm, -93 dBm above 30 MHz, `Common.GetSMeterUnits`), S9 to +60 dB the rest; fast attack, slow decay; empty while that VFO transmits or the radio is off.
  - Thetis's VFO A and VFO B boxes are collapsed in Kainos layout. What they did is on the faces: click the frequency to type one (MHz, or kHz without a decimal point; Enter sets, Esc cancels; the flag window takes the keyboard only while typing), click the dim TX badge to make that VFO the transmit VFO (`chkVFOATX` / `chkVFOBTX`).
  - VFO SYNC tab: Thetis's own box between the VFO boxes (`grpVFOBetween`: VFO sync, tune step, lock, band stack, RX antenna, quick save / restore), pinned over the column and scaled to fit like the mode panels.
  - The panadapter and the dock start under the menu. The panadapter's bottom stays where Thetis puts it (`gr_display_basis.Y + gr_display_size_basis.Height + v_delta`); its pan and zoom sliders, which Thetis places from the panel's top, move down by what it has grown (and back in Classic).
  - BAND tab: band, mode and filter are drop-downs in one row (`KainosDropDown`: caption, the button that is on; the list ticks it, choosing is a real click; right click is a right click on the one that is on, so the filter's opens Thetis's filter editor). The width / shift / low / high controls stay under them.
  - The column ignores Kainos-collapsed panels when finding its top (Thetis sizes its multimeter box again for a moment on a resize).
  - Bottom panels: all collapsed (`kainosCollapse` takes a size function too). DSP (`panelDSP`): its buttons are in the flags' DSP tab, every one but MUTE (in AUDIO): NR, ANF, NB, SNB, BIN, MNF, +MNF. VFO (`panelVFO`): split, A>B, A<B, swap, zero beat, IF>V are a dock group (the dock takes any Thetis button now, check box or plain), RIT / XIT are on the flags, VAC1 / VAC2 in the dock. Display (`panelDisplay2`) and multi-RX (`panelMultiRX`): in the panadapter's bar; the multi-RX volume sliders are left out. The panadapter then reaches down to the status bar, or to whatever Thetis still shows under it (RX2's panels when RX2 is on).
  - The panadapter's bar (`consoleKainosBar.cs`), one row under the spectrum: PAN and Center, MAIN / SUB pan, SubRX, Swap, the display mode (a `KainosDropDown` bound to `comboDisplayMode`), AVG, Peak, CTUN, and ZOOM with ZTB / 0.5x / 1x / 2x / 4x at the right. Thetis's own pan / zoom row is shrunk to 1 x 1 under it, not zero: Thetis's skin loader sizes a radio button's image list from the button and an empty size is a fatal error. The compact `KainosSlider` follows its PrettyTrackBar on a timer (it has no value-changed event, and Thetis moves pan / zoom from the panadapter).
  - The info bar's two buttons (Blobs / Peak, or whatever actions they're set to) get Kainos buttons laid over them, bound to them: a click toggles Thetis's button, a right click opens its menu of actions; they follow its place, size and visibility.
  - PA tab: the PA profile (Thetis's label under the column, hidden in Kainos layout and put back in its place in Classic) as a drop-down bound to Setup's PA profile list (`Setup.KainosPAProfileCombo`; `KainosDropDown` can take its combo box from a source looked up when needed), and PA settings... (Setup's PA page, the label's right click). With it gone the column runs to the status bar.
  - The dock ignores meter containers, the column's pinned controls and collapsed panels when finding its top (the METERS container sits at the top left until it's attached), and is placed again when the meter is attached.
  - RIT/XIT tab: the RIT and XIT offsets are `KainosUpDown` rows (bound to `udRIT` / `udXIT`: - and + by its increment, the wheel the same).
  - VAC1 / VAC2 are a fourth group in the dock (right click opens their setup).

### Windows in the Kainos Audio look

- Every window Kainos opens (Setup, Memory, Equalizer, CWX, XVTRs, Linearity, Finder, About, the database manager...) is restyled as it opens, in both layouts (`KainosWindowTheme.cs`, a timer watching `Application.OpenForms`; again each time a window is shown and a moment later, since windows set colours of their own after they appear; controls added later are caught by `ControlAdded`). Dark title bar (DWM caption / text / border colours on Windows 11, immersive dark mode on 10), the Kainos Audio navy, light text, flat dark buttons (a skin's picture replaced), dark text / number boxes, owner-drawn drop-downs with Windows's dark arrow (`DarkMode_CFD`), dark lists and grids with dark scroll bars (`DarkMode_Explorer`), group boxes painted by Kainos (frame and title), and tabs drawn by Kainos (dark, the selected one gold, the strip and page edge painted over).
- Only default colours change (system colours, white, anything too dark to read): colours a window sets on purpose (status colours, colour pickers) stay. Skipped: the console, the splash, Kainos's own windows (Kainos Audio, AetherVoice, RTTY / CW, the flags), meter windows and borderless pop-ups. Windows's own message boxes can't be restyled.

### Sliders and menus

- Sliders the same on every PC (`consoleKainosSliders.cs`): Thetis's sliders (PrettyTrackBar) take their track (background image) and thumb (head image) from the skin, so they looked different with different skins. In Kainos layout the column's (RX, TX, filter) and the mode panels' sliders get Kainos's: a slim rounded track and a rounded thumb made to each slider's size (again when it's resized), on its parent's colour. The slider itself stays Thetis's (dragging, clicks, the wheel, right clicks, the limit bar). The skin's pictures are saved and put back in Classic; a skin loaded in Kainos layout is saved and covered again.
- Modern menus: the renderer (`KainosUI.cs`) draws the menu bar's items with a rounded hover pill (a gold outline while open), and drop-downs (the menu bar's and every Kainos context menu) as a flat panel with a soft outline, rounded inset hover rows, inset separators and gold ticks. Kainos layout also sets the menu bar's font (Segoe UI Semibold 9.5) and spacing, and roomier drop-down rows (also for menus filled later, as they open); Thetis's are put back in Classic.
- The 3D stack's navy fill hid the backdrop's logo: the logo is drawn again over the pasted stack (still under the grid, filter and trace).

### Panadapter colours

- In Kainos layout the receive panadapter takes the Kainos colours (`consoleKainosPanColours.cs`): navy grid lines (finer ones darker), ice labels, a bright ice trace over a deep blue fill, gold peak fill, ice filter shading and gold TX filter lines. The waterfall and the transmit colours keep their own. Thetis's colours (Setup > Appearance > Display, or the skin's) are saved and put back in Classic, or with Setup > Appearance > Kainos > Panadapter > Kainos colours off. A colour changed in Setup while Kainos's are showing is the user's and isn't put back over. Applied with the Kainos theme and again once the console is shown (Setup's start-up applies the user's colours too).

### Panadapter backdrop

- In Kainos layout the panadapter has the Kainos backdrop (AetherSDR's look): a dark navy gradient with the Kainos logo (the splash's flame, KAINOΣ and tag line, cut out by brightness) faint in the middle, under the grid, filter, VFO lines, the 3D stack and the trace. Setup > Appearance > Kainos > Panadapter: on / off, and the logo's strength (off, 6%, 12% default, 20%). It covers the panadapter's own area only, so in Panafall it stays out of the waterfall. The 3D stack is filled with the backdrop's navy over it. Drawn by `drawKainosBackdrop` (`displayKainos.cs`), the first call in `DrawPanadapterDX2D`; the logo bitmap is made once per render target.

### 3D stacked-trace panadapter

- The 3D button on the panadapter bar (right click: depth 20-80 traces, speed 5-20 a second, height 25-50% of the panadapter): behind the live trace, the last few seconds of traces stacked back into the distance (AetherSDR's stacked-trace panadapter), each older one a step up and to the right, dimmer (Kainos ice), and filled with the background so nearer traces hide the ones behind. Saved in a hidden Setup box (`txtKainos3D`).
- Colours (the right-click menu): the waterfall's by strength (default) or Kainos blue. By strength is one linear gradient brush with the waterfall's "enhanced" colour stops (a dim blue instead of its background at the bottom), from that receiver's waterfall low level to its high level (its AGC level when waterfall AGC is on), converted to the panadapter's y; the render target's transform applies to brushes too, so each trace, moved back up the stack, is coloured by its own levels. One brush per redraw of the stack, so no extra cost.
- Speed: a trace becomes a Direct2D path geometry once, when it's taken (the speed setting, not every frame), a point every third pixel (half the average of the five around it, half their highest, so the noise is calmer and the peaks stay); the whole stack is then drawn into an offscreen image (a compatible bitmap render target, remade if Thetis remakes its render target), and each frame only pastes that image. Drawing the filled geometries every frame cost about a third of the CPU (Direct2D tessellates fills on the CPU each draw); pasting the image costs about 8 percentage points on the HL2 PC. The history clears when the span, size, scale, decimation or TX/RX changes, and when the radio is powered off (nothing new is coming, so no stack is drawn). Two calls in `DrawPanadapterDX2D`: one first thing, before Thetis's grid, so the grid, the filter shading and the VFO / TX lines stay on top of the stack (it pastes the image); one after its clip is pushed, before the live trace (it takes traces and redraws the image). The drawing is in `displayKainos.cs`.

### RTTY (native)

- The RTTY menu item (next to CWX) opens a terminal. In Kainos layout it splits the screen: it docks under the panadapter, which gives up its height (`kainosRttyDockHeight`), and Pop out moves it to its own window (always a window in Classic).
- Audio: `kdigi.c` in ChannelMaster, spliced into `xpipe` (pipe.c) beside the RADE splices: `xkdigi_rx` copies each receiver's demodulated audio (left channel, before RADE) into a ring the console reads (`KDigiRxTap` / `KDigiRxRead`); `xkdigi_tx` replaces the mic audio with what the console writes (`KDigiTxEnable` / `KDigiTxWrite`) while sending, after the RADE and VAC splices, so Thetis's mic gain, processing, drive and ALC follow as for any audio. Single-reader / single-writer rings with interlocked counters.
- Modem (`KainosRtty.cs`, no UI): 45.45 baud (50, 75), 170 Hz shift (200, 425, 850), amateur Baudot (ITA2 letters, US figures), 1 start / 5 data / 1.5 stop. Receive: each tone mixed to zero and averaged over one bit, (mark - space) / (mark + space), characters framed from the start bit's edge and sampled at each bit's middle; a quality figure squelches and each character's bits must be clear on average (SQL sets both). Unshift on space. Send: phase-continuous AFSK, mark then LTRS to open, LTRS while idle, shifts as needed. Tones 2125 / 2295 Hz in the audio (centre 2210); mark is the lower tone on LSB / DIGL and the higher on USB / DIGU (REV swaps). Tested offline: clean copy to about 12 dB SNR in the signal's bandwidth, no characters from noise alone, 20 Hz mistuning copies.
- Terminal (`consoleKainosRtty.cs`): RX1 / RX2, shift, baud, REV, USOS, the M / S tuning bars with the quality line, SQL and TX level (dB); received text (sent text in red), type-ahead (Enter starts sending what's typed, then each key goes out as typed; Esc or RX returns to receive once the queue is sent; ABORT stops at once), their call (double-click a word in the text), macros CQ / ANS / 599 / 73 / MY with `{MY}` from the RADE callsign setting and `{CALL}`. Sending keys MOX; un-keying elsewhere stops it. Settings are kept in a hidden Setup box (`txtKainosRtty`).

- AFC (on by default; the AFC button): the audio decimated to 8 kHz, a 2048-point Hann FFT every 128 ms, averaged; the centre whose two tones (each the peak within 8 Hz) are strongest, both at least 9 dB over the median of the bins around them, within 150 Hz of the nominal 2210, and the tones move a third of the way there each time. Moving the tones doesn't reset the decoder. The tuning indicator shows the offset; sending uses the centre AFC found, so the reply lands on the other station. Tested offline: copy 20, 40 and 80 Hz off, 120 Hz off pulls in within about a second, nothing from noise.
- Markers: the mark (gold) and space (ice) tones as lines on the terminal's receiver's panadapter, where the decoder listens (AFC included), labelled near the bottom on their outer sides (the flag and the scale are at the top). `Display` is made partial (one word in display.cs) with one call after the RADE overlay in `DrawPanadapterDX2D`; the drawing is in `displayKainos.cs`, from `Console.KainosDigiMarkers(rx)` (the tones as offsets from the VFO).

### CW terminal (native)

- The CW menu item (beside CWX and RTTY) opens a terminal laid out like the RTTY one, docked under the panadapter or popped out (`consoleKainosTerminal.cs` places both; one terminal is open at a time, as they read the same receive tap).
- Decoder (`KainosCw.cs`, no UI): the receiver's audio mixed down from Thetis's CW pitch (`CWPitch`) and averaged over 8 ms, an envelope every millisecond smoothed over a fifth of a dot; keyed with hysteresis between a tracked peak and noise floor (the floor learnt quickly in the first 0.3 s, nothing keyed until then), only when the peak is clearly above the floor (SQL). Marks over two dots are dashes; gaps of 2.5 dots end a character, 6 a word. The dot length (speed) follows dots, dashes and clean element gaps, only from a clear signal and only so far at a time. Tested offline: clean copy at 12-40 WPM, 15% timing jitter, 40 Hz off pitch, moderate noise; nothing from noise alone.
- Sending goes through CWX's remote-message path (the one CAT's KY and TCI use): CWX keys the radio and times the elements; TX WPM is CWX's speed. Sent text is echoed in red as CWX starts each character (`CWXRemoteCharacterStartedHandlers`). Enter or TX sends the typed line, and while sending each key goes out as typed; Esc or STOP drops the rest of the queue; ABORT stops at once. Macros CQ / ANS / 599 / 73 / MY, as for RTTY. Needs CWL or CWU. Settings in a hidden Setup box (`txtKainosCw`).

### Terminal macros

- Six macro buttons in each terminal (RTTY and CW have their own sets): a click runs one, F1-F6 in the typing line run them, a right click opens the editor (`KainosMacros.cs`): label, text (`{MY}` your call from the RADE callsign setting, `{CALL}` the box beside the macros), and what a click does: send then back to receive, send and keep transmitting (RTTY), or put the text in the typing line. Reset to default puts back the built-in macro (CQ, ANS, 599, 73, QRZ, MY). Saved with the options as Base64 in hidden Setup boxes (`txtKainosRttyMacros`, `txtKainosCwMacros`).

### Built-in spotting

- DX cluster and POTA spots without another program (`KainosSpots.cs`, `consoleKainosSpots.cs`). On by default; both start once the console is shown and stop when it closes.
- DX cluster: telnet to `dxc.nc7j.com:7373` (NC7J's AR-Cluster) by default, logged in with the user's callsign when the login prompt comes (with or without a line end); `DX de` lines parsed (spotter, kHz, call, comment, time); reconnects with back-off (15 s to 5 min). The callsign is what Kainos already knows: the RADE callsign, else Thetis's TCI own callsign, else the Discord callsign (`Setup.KainosOtherCallsign`); a change is picked up and the cluster logs in again.
- POTA: `https://api.pota.app/spot/activator` every 2 minutes (POTA's API is unofficial and runs on its volunteers' goodwill: no hammering), identified as Kainos in the User-Agent.
- SOTA is not included: SOTA's API terms say no AI-generated software may connect without prior approval, and developers must join the SOTA Reflector's API-consumers group first.
- Spots go to Thetis's own spot display (`SpotManager2.AddSpot`: callsign tags with flags on the panadapter, click to tune, lifetime and maximum from Setup's TCI spot settings), DX in Kainos ice, POTA in green, with a mode from the spot or guessed from the band plan (FT8 / FT4 frequencies, CW segments, else SSB on the usual sideband). Kainos turns on Thetis's show-spots option (`Setup.KainosShowSpots`) when a source is on.
- SPOTS tab: each source's status; DX, POTA, Pan (spots on the panadapter), Band (this band only); the latest spots (time, kHz, call, mode and comment; click one to tune VFO A there in its mode). Settings in a hidden Setup box (`txtKainosSpots`: sources, cluster host and port, band filter).

### KiwiSDR (KIWI tab)

- Listen to a public KiwiSDR inside Kainos, as OpenHPSDR-Zeus and AetherSDR do (`KainosKiwi.cs`, `consoleKainosKiwi.cs`). The KIWI tab (off by default) lists the nearest receivers with a free channel that cover VFO A's frequency, by distance from the grid square in Setup > DSP > FreeDV (RADE); click one to listen. Follow (on) keeps it on VFO A's frequency and mode; Vol is its own volume; Stop disconnects. The audio plays on Windows's default output (NAudio). Receive only.
- Also: right-click a receiver to star it (favourites stay at the top, shown even when full); a search box (place, name, antenna, grid); with Follow off the TUNE line tunes the Kiwi itself (wheel 1 kHz, Shift 100 Hz, Ctrl 10 kHz; click to type kHz, or MHz under 100; the mode button steps USB / LSB / CW / AM); Out: picks the Windows playback device (a VAC cable feeds a decoder); the Kiwi's S reading under VFO A's on its flag ("K S5"). Favourites, output, Follow and volume are saved in a hidden Setup box (`txtKainosKiwi`).
- The list is rx.linkfanel.net's copy of kiwisdr.com/public (kiwisdr.com's page is behind a human check), fetched when the tab is first shown and on Refresh. Receivers behind kiwisdr.com's proxy are left out (the proxy answers browsers only).
- The client follows the KiwiSDR author's kiwiclient: WebSocket `/kiwi/<stamp>/SND` (newer firmware) or `/<stamp>/SND` (older; tried when the first is silent for 4 s), `SET auth t=kiwi p=`, `SET ident_user=<callsign> (Kainos)` so the owner sees who is listening, then on `sample_rate`: IMA-ADPCM compression, AGC, no squelch, mode / passband / frequency, keepalive each second; `SND` frames (flags, sequence, S-meter, audio) decoded with the ADPCM state carried across frames. The receiver's answers (full, password, time limit, down) are shown in the tab. Each owner decides who may connect and for how long.

### Installer

The installer's pictures are Kainos's: `binary/kainos_background.bmp` (the welcome and finish pages, 493 x 312: the splash's flame, KAINOΣ and tag line on a dark wave panel at the left, white on the right for the installer's text) and `binary/kainos_banner.bmp` (the other pages' banner, 493 x 58: a Kainos tile at the right). Both are made from `Console/Resources/kainos-splash.png` by `art-source/make_installer_art.py` (Python with Pillow). Thetis's `thetis_*.bmp` are left in place, unused, for easy merges.

The publisher is Justin Cron - K7JUS. The Add/Remove Programs comments credit Thetis (W5WC, MW0LGE, MI0BOT, NR0V), the OpenHPSDR community and PowerSDR. The installer is built with WiX Toolset 3.14 (and .NET Framework 3.5, which WiX 3 needs) after a Release build of Kainos. Visual Studio 2026's MSBuild doesn't find WiX 3's targets by itself, so give it the path:

```
MSBuild.exe "Project Files\Source\Thetis-Installer\Thetis-Installer.wixproj" /p:Configuration=Release /p:Platform=x64 "/p:WixTargetsPath=C:\Program Files (x86)\MSBuild\Microsoft\WiX\v3.x\Wix.targets"
```

It writes `Project Files/bin/Installers/Kainos-v2.10.3.x64.msi` (Kainos HL2, installing to `Program Files\OpenHPSDR\Kainos-HL2`, with its own upgrade codes, so it never upgrades or removes Thetis). First built 2 October 2026: the welcome page shows the Kainos artwork; not yet installed on a PC.

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
4. Search for `Kainos` and `// Kainos:` comments to find every Kainos-specific change. The RADE code in `ChannelMaster` (`radae*.c`, `r8brain_wrap.cpp`, the `pipe.c` hooks, `cmcomm.h` and `ChannelMaster.vcxproj`) is Thetis-RADE's and can be compared with it directly.
5. Build, then test RX, TX and PureSignal on the HL2 before merging back.

## Licensing

Thetis and WDSP are licensed under the GNU GPL "version 2 or later", and there are no v2-only files in `Project Files/Source`. AetherSDR is GPLv3. Thetis-RADE's code is GPL version 2 or later, and the RADE libraries are BSD, MIT and LGPL-2.1 (codec2 slice), all GPLv3-compatible. Since Phase 4 added AetherSDR-derived code (`aethervoice.c/.h`), Kainos as a whole is distributed under **GPLv3 or later**: the full text is in `LICENSE-GPL-3.0`, and `ReadMe.md` says so. Existing Thetis/WDSP files keep their "version 2 or later" notices; new Kainos files carry GPLv3-or-later headers. Keep every existing copyright notice and the MW0LGE dual-licensing statement (`LICENSE-DUAL-LICENSING`).
