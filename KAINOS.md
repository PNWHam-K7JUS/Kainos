# Kainos development notes

This file records what Kainos changes compared with Thetis, and why, so that future work and upstream merges don't have to rediscover it. For the user-facing description, see [ReadMe.md](ReadMe.md). For how the Thetis code fits together, see [CODEMAP.md](CODEMAP.md).

## Changelog

Newest first. Each version's section is written to be copied straight into a GitHub release or post. Full notes for each release are in `Documentation/ReleaseNotes/`.

### Kainos 1.0.14 (not yet released)

**New**
- **KiwiSDR: Mute, Hear radio / Hear Kiwi, and TX mute** (Discussion #54), in the KIWI tab, with Mute and Hear on the Kiwi's waterfall window too. **Hear Kiwi** mutes the radio and unmutes the Kiwi in one click, and **Hear radio** swaps back, so you can switch to the Kiwi when your own receiver is noisy. **TX mute** (on unless you turn it off) silences the Kiwi while you transmit, and for 1.5 s after you un-key, so you don't hear your own signal coming back a second late. Turn it off to listen to yourself on the Kiwi.

- Technical: the Kiwi's volume and mutes are now applied to its own samples (`VolumeWaveProvider16`). Its Vol used to set `WaveOutEvent.Volume`, which is `waveOutSetVolume`: the volume of Kainos's whole audio session in Windows, which Windows remembers. The Kiwi player now also sets that back to 100% when it starts. The TX mute follows `MoxChangeHandlers`, and its 1.5 s hold ends in the Kiwi's 300 ms tick, which drops what was buffered meanwhile.

**Fixed**
- **The radio kept transmitting after you let go of the foot switch** (GitHub #23), when a REC / PLAY recording was played out while keyed. Letting go stopped the playback, and stopping it put back everything it had changed, including MOX, part-way through un-keying, so the radio was keyed again. Your key-up now wins: playback stops and the radio goes back to receive.

- **AGC gain jumped up when Kainos was minimised or resized, then stepped back down** (GitHub #52), with Auto AGC on (the AGC Gain slider's green knob). While the window is minimised, the panadapter's noise-floor reading comes out about 12 dB low, and it takes a moment to settle after a resize or restore; Auto AGC followed it. Measured on an HL2: minimising took the AGC gain from 71 to 83 dB, then down in steps (80, 77, 75) and back to 71 on restore. Now it stays at 71. The grid that follows the noise floor no longer jumps either.

- **The right-hand control column wobbled, and could freeze Kainos** (GitHub #56), when its sections only just filled it, for example METERS + RX + KAINOS AUDIO at 110% UI scale. The column kept switching its scroll bar on and off, getting wider and then narrower, and the meter got taller and then shorter with it. The column now sizes the meter for the width it is about to have, so it settles at once. Tested by stepping the window height 600 to 1360 px: 1.0.13 stopped responding part-way, and 1.0.14 settled at every height.

- **Dragging the control column's scroll bar showed the meter twice, torn** (GitHub #48). The mouse wheel swaps the meter and the other pinned panels for still pictures while the column moves; dragging the scroll bar moved the real ones, and the meter (drawn by DirectX) showed at its old and new places at once. Dragging now uses the same pictures, and the real panels come back when you let go.

- **The VFO flag's S meter read well above the analog meters** (GitHub #49): S9+20 on the flag while the ANAN meter's needle sat on S9. The flag used the peak signal strength, frame by frame; the analog meters show the average by default. On a 20 m SSB station the peak ran a median 7 dB above the average, and up to 15 dB on voice peaks. The flag now uses the average too, and its reading rises fast and falls slowly like the bar, so it no longer flickers up on each voice peak. Checked side by side with the ANAN meter on 20 m: within an S unit.

- **The list of TX profile changes covered the orange "TX Profile modified" box** (GitHub #50). Clicking the box now opens the list just under it, lined up with its right-hand edge, so you can read what changed before saving.

- Technical: `KainosColumn.Gliding` is also true while the scroll bar's thumb is dragged (`ThumbDrag`, which raises `GlideStarting` / `GlideEnded`, also on lost mouse capture). `lblTXProfileWarning_Click` (setup.cs) placed the report at a fixed (right edge, y 34), but `KainosSetupShell` moves the warning down beside the page title; it now goes under `lblTXProfileWarning`.

- Technical: `kainosMetersMeasure` (consoleKainosColumn.cs) measured the meter container at its current height, which follows its previous width (it keeps its meters' shape). The two measures in `arrange` (with and without the scroll bar) then disagreed, and the container's `SizeChanged` started each new layout. It now scales the height to the width being measured.

- Technical: `tmrAutoAGC_Tick` (console.cs) uses up and drops the noise-floor readings while the window is minimised, and for 2 s after its size or state changes, so Auto AGC and grid-follow keep the last good floor. Found by logging `RXA_AGC_GAIN`, `GetRXAAGCTop`, `ptbRF` and `_lastRX1NoiseFloor` every 100 ms through a scripted resize / minimise / restore.

- Technical: found with 1.0.13's MOX trail in KainosErrors.txt: the un-key (`chk=0`) raised `MoxPreChangeHandlers`, `clsAudioRecordPlayback.OnPreMox` called `StopPlayback`, whose `storeRestoreSettings(false, true)` set `_console.MOX` back to its pre-playback value (on), a nested `chkMOX_CheckedChanged2` (`nested=1, chk=1`); the outer handler then read `chkMOX.Checked` as on and ran the key path. `OnPreMox` sets `_inPreMox` while it stops recording and playback, and the MOX restore is skipped while it's set.

**Modules**
- **SSTV 0.5.1**: the sync / black / white tone markers showed on the panadapter whether or not the SSTV window was open (seen on LSB voice), and came back after closing it. They're now drawn only while the SSTV window is open. A self-test checks it.

### Kainos 1.0.13

**New: NNR neural noise reduction**
- **NNR**, the neural noise reduction from WDSP 2.10 (Warren Pratt, NR0V), is now in Kainos. It's trained on speech: it takes the band noise down between and under the words and keeps the voice. In a test with speech in white noise it took the pauses down by about 25 dB and left the speech nearly unchanged.
- It has its **own NNR button** on each VFO flag's DSP tab, next to AUTO NOTCH, so it runs on its own or together with NR, NR2, NR3 or NR4. The flag's DSP summary shows **NNR** while it's on. Right-click the button for its settings.
- **Setup > DSP > NNR**: the model (**Standard**, or **Large**, which uses more CPU), the **reduction depth** (6 - 40 dB, 25 by default), and **Voice modes only**. NNR treats steady tones as noise, so by default it stands by in CW and the digital modes and only runs in LSB, USB, AM, SAM, DSB and FM (the button reads NNR · STANDBY).
- Before or after the AGC, it follows the other NRs (Setup > DSP > NR, NRs/ANF Position).
- It adds about 50 ms of delay to the receive audio while it's on. One receiver with the standard model takes about 15 % of one CPU core (the large model about 35 %).

- Technical: `wdsp/nnr.c`, `nnet.c`, `nnio.c` and the two built-in models (`nnr_model_0.c`, `nnr_model_1.c`) are WDSP 2.10's, taken from AetherSDR's patched copy (its standalone accessors and the `n->df` guard in `setAlpha_nnet` / `setKnee_nnet`). Kainos keeps WDSP 1.29; NNR is wired in beside NR3/NR4: created in `create_rxa` (position 1, 16 kHz model rate, 512-point FFT), `xnnr` after `xsbnr` at both positions, and the rate, size, buffer, flush and destroy calls. `RXAbp1Check` takes an `nnr_run` argument and `RXAbp1Set` checks NNR, so bp1 runs with gain 2 while NNR is on. `SetRXANNRRun` sets bp1 the way `SetRXARNNRRun` does, and `SetRXANNRPosition` moves bp1 to NNR's position like every other NR (without that, bp1 stayed before the AGC and silenced the receiver). `dprintf` added to `utilities.c`. `consoleKainosNnr.cs` (new) holds the per-receiver on/off (saved in a hidden `txtKainosNnr`) and a 250 ms tick that sends only changes to RX1, RX1's sub-receiver and RX2, re-sending the model after WDSP rebuilds the block. `setupKainosNnr.cs` (new) is the Setup page. The models are built the first time NNR is switched on in a channel (`nnr_load`, about 8 ms and 9 MB; until then `xnnr` passes audio through), so the ten RX channels Kainos creates don't carry about 90 MB they may never use; `calc_nnr` rebuilds them after a rate or size change only once they're loaded. The first channel's FFTW planning adds about 0.7 s at start.

**New: more KiwiSDRs, and your own**
- The KIWI tab now lists **about twice as many receivers**: the half of the public list that's reached through kiwisdr.com's proxy (`<name>.proxy.kiwisdr.com`) was left out, as their address only answers browsers. Kainos now reaches them on the port the receiver itself answers on (8073).
- The search box also matches a receiver's address, which often carries the owner's call (kr6la.proxy.kiwisdr.com, say).
- **Show more** under the list adds ten receivers at a time (before, only the nearest eight could be seen); a new search or Refresh starts again from eight.
- **Add Kiwi** (GitHub discussion #43): type the address of a receiver that isn't listed (for example `kiwi.example.com:8073`). Kainos checks it (its status page gives its place, antenna, users and position for the distance), lists it first with a star, and checks it again on each refresh. Right-click one you've added to remove it.

**New: a dot on the flag's tabs** (GitHub #33)
- A small dot on a VFO flag's **DSP**, **RIT/XIT** or **FREEDV** tab shows that something on it is on (NR, NB, ANF, AUTO NOTCH, NNR ...; RIT or XIT; RADE on that receiver), with the drawer closed.

**Documentation**
- The whole transmit audio chain, in order, and where Kainos Audio sits in it (after Thetis's TX EQ, before the leveler, CFC, compressor and ALC), is on the Kainos Audio wiki page, linked from the ReadMe (GitHub #37).

**Fixed**
- **AUTO NOTCH didn't notch anything on the air** (new in 1.0.12): it looked for carriers in the mirror image of the passband you hear, so a carrier in USB was looked for where the opposite sideband would be. Tested on the air on 40 m: a broadcast carrier 1 kHz into USB is found and notched within a couple of seconds (AUTO NOTCH · 1), and LSB speech on 80 m isn't touched.
- **External PTT that stayed keyed after you let go** (GitHub #23): when Kainos finds the radio's PTT released but transmit still on, with the MOX button already off, it now finishes the un-key itself within a quarter of a second, instead of staying on the air. It's logged in KainosErrors.txt with the last few MOX changes (when, from where, and whether each one finished), so the cause can be found from an error report.
- **CW and RTTY macros lost on restart** (GitHub #30): with the CW (or RTTY) squelch set to anything but its default, restoring it at start-up saved that window's settings part-way through loading, which wiped the settings loaded after it: the CW and RTTY F-key macros, and also spots, Kiwi, the audio scope, compact view, the flag tabs and the docked windows' heights. They now all come back.
- **The CW and RTTY windows jumped to the end** with every new character once they'd filled (GitHub #30), so you couldn't scroll back to read. They now follow new text only while you're at the bottom; scrolled up, the view and anything you've selected stay put. Old text is trimmed a whole line at a time.
- **REC / PLAY went out through Kainos Audio** (GitHub #41): Setup > Audio > Recording, "During playback temporarily", has **Disable Kainos Audio** (on by default), so a recording played out on transmit skips the Kainos Audio chain (gate, EQ, compressor, AetherVoice, plug-in ...) and goes out as it was recorded. It comes back when playback stops; your BYPASS setting isn't touched.

- Technical: #23: `chkMOX_CheckedChanged2` (`consoleKainosPtt.cs`) now wraps Thetis's handler (renamed `chkMOX_CheckedChanged2_core`), counting its depth and recording each change (time, check state, `_mox` before / after, PTT mode, thread, the first three non-System callers, and whether it threw). 1.0.12's log showed `mode=MIC` with nothing holding PTT: the handler resets the mode to NONE first when it un-keys, so it never ran, i.e. chkMOX was off while `_mox` stayed on, and PollPTT's `chkMOX.Checked = false` changes nothing. `kainosPttWatch` runs the handler itself after 250 ms in that state when none of PollPTT's guards hold and the handler isn't running. #41: `AetherStrip.PlaybackHold` (not saved, not shown on BYPASS) and `Bypassed` (BYPASS or the hold), used by the stage pushes, AetherVoice TX and the VST plug-in; `clsAudioRecordPlayback` sets it with the other playback settings ("KAINOS") and always clears it on restore. The checkbox is added in `setupKainos.cs`; the group's rows are 20 px apart to fit it. #30: `applyKainosUI` (`setupKainosUI.cs`) sets `_kainosApplyingUI` while it hands the hidden boxes to the console, and the `KainosSettingsChanged` copy-back skips while it's set (`CwLoadSettings` / `RttyLoadSettings` setting the squelch fired `cwSave` / `rttySave`, which copied the still-empty later values into their boxes). AUTO NOTCH: `KainosAutoNotcher.Step` negates Q before its FFT; in WDSP's frequencies (nbp0's passband edges, the notch database) a USB tone at +f is at -f in a plain FFT of I + jQ (found on the air with a temporary log of the tap's spectrum: the carrier at -1012 Hz, -22 dB, and +1012 Hz at the noise). Kiwi: `KiwiDirectory.NormalUrl` / `IsProxy` (`KainosKiwi.cs`) give proxied listings `:8073` (port 80 answers with a 307 for browsers; 8073 accepts the WebSocket), `FetchStatus` reads a receiver's `/status` into the same fields as the list; `consoleKainosKiwi.cs` keeps the added ones (`own=` in the Kiwi settings), lists them first, re-checks them on Refresh, and `KainosKiwiList` grows by ten rows per "Show more" (`ArrangeSections`). #33: `KainosFlagTabActive` (`consoleKainosFlag.cs`) and a dot in the flag's accent colour on the tab. `KainosRxText.cs` (new): the CW and RTTY panes' append, which checks the vertical scroll position first and, when scrolled up, appends with redraw off and scrolls back to the same first line (`EM_LINESCROLL`), restoring the selection.

**Modules**
- **Digital Modes 0.10.3**: the RX / TX markers stayed on the panadapter after the FT8 window was closed, in every mode (GitHub #42). The pane's timer kept putting them back; they're now drawn only while the window is open. A self-test checks it.
- **Digital Modes 0.10.3**: a dashed line across the waterfall where each period starts, with its UTC time (GitHub #45).

### Kainos 1.0.12

1.0.11 was never released on its own: everything that was to be in it is here.

**New: a new look for Setup**
- **Setup** now works like a modern settings window: a **Search settings** box along the top (Ctrl+F), a list of pages down the left in place of the two rows of tabs (Setup's sections as headings, which open to show their pages), and the page's name over the page. Typing in the search box shows only the pages that mention what you typed; Enter opens the first and briefly highlights the setting. Up and Down step through the pages.
- The pages have more room: Setup is a little bigger, and each page's sections are spread over it, so the busy pages (Transmit, DSP Options, Display General ...) are less cramped.
- Every Kainos window (Setup, XVTRs, Memory, CWX, Equalizer ...) gets the full dark look: dark check boxes and radio buttons with a gold tick, dark number boxes with dark arrows, rounded section frames with their titles in ice blue, tabs as rounded "pills", grid headings without light lines, and a dark title bar (which some windows had lost).
- Windows no longer flash up in the standard light look for a moment before turning dark: they're styled before they appear, and shown only once they've been drawn.

- Technical: `KainosSetupShell.cs` (new) wraps Setup's own pages: the tab strips of `tcSetup` and its second row are hidden (flat, 1 px) and each tab control sits in a page-sized `Holder` panel, offset so only the page shows; the list (`NavList`, drawn by Kainos, scrolling itself; its text is drawn with `TextRenderer`, which ignores the scroll transform, so the text rectangles are offset by hand) chooses the tab, so `ShowSetupTab` and every handler work as before. Setup's controls all move together (so the spare ones Thetis keeps beyond its edges stay out of sight), with a `MinimumSize` against Setup's saved size; the page area is 1.14 times Setup's and each page's controls are placed proportionally from their original bounds (`spread`, again on each page's `Resize`). Search matches the text of every control on a page. `KainosWindowTheme.cs`: a `WH_CALLWNDPROC` hook styles a window on `WM_SHOWWINDOW` and DWM-cloaks it until it has painted; check boxes, radio buttons and up-down buttons are painted over Windows's light glyphs; group boxes are rounded with bold titles; tab rows are flat buttons drawn as pills (no flat separators) with everything outside the page painted; the title bar colours are set again on `HandleCreated`, `Shown` and `Activated`.

**New: modules**
- **Setup > Modules** lists add-on features you can download when you want them, instead of everyone getting them in the installer. Install one and Kainos downloads it from GitHub and checks it before installing. Turn it on or off, or remove it, from the same page. **Install from file...** is there for testing a module before it's published.
- The RTTY, CW and module windows docked under the panadapter can be **made taller or shorter**: drag the bar between them and the panadapter (the panadapter and waterfall give up or take back the height). Each one remembers its own height (a short one like the Voice Keyer and a tall one like SSTV don't share); double-click the bar for its standard height.
- A module's window lives where the RTTY and CW terminals do: docked under the panadapter, or popped out into its own window. A digital mode appears in the **Modes** menu; a tool (the Voice Keyer) has a button in the left-hand dock and an item in **Tools**.
- **One digital mode at a time.** RTTY, CW and the modules (FT8 / FT4, SSTV) share the receiver's audio, so opening one closes whichever had it.
- A module can draw part of its window **at the screen's own resolution**. At 125 % / 150 % display scaling Windows enlarges Kainos as a picture, which blurs fine detail; the FT8/FT4 waterfall is now sharp.
- **Three modules to start with**, in Setup > Modules:
  - **Digital Modes**: FT8, FT4, JT65, JT9 and WSPR, and FT2 (experimental). It decodes with WSJT-X's own decoder (included) and sends straight through Kainos, with no virtual audio cables or CAT. It has band activity, automatic QSO sequencing and an ADIF log.
  - **SSTV**: receives Martin, Scottie, Robot 36 and PD pictures (the mode found by itself, the slant corrected, the sender's callsign read, kept in a gallery), and sends them: a one-time station setup makes your CQ, Reply and 73 pictures, and Reply answers in the sender's mode.
  - **Voice Keyer (DVK)**: 12 recordings on F1 - F12, recorded from your mic and sent through your whole transmit chain, so they sound like you. Preview, repeat CQ with a listening gap, WAV import and export. It opens from its **DVK** button in the left-hand dock.
- Modules can record your mic, play a preview through your speakers and use the F-keys (the Voice Keyer does).
- A module that's a tool rather than a mode can put a button at the bottom of the **left-hand dock** (the Voice Keyer's **DVK**), with an item in the **Tools** menu (on the menu bar in the Classic layout). The button lights while the tool is open.

- Technical (modules): `ModuleApi/KainosModuleApi.cs` is a new project, built to `Kainos.ModuleApi.dll` and shipped next to `Kainos.exe` (also in the installer). It holds the interfaces a module uses: `IKainosModule`, `IKainosHost`, `IKainosRadio`, `IKainosAudio`, `IKainosTheme`, `IKainosPane` and `IKainosMenuItem`. `KainosModules.cs` reads `modules.json` from `main`, downloads from GitHub hosts only, checks the SHA-256, rejects zip paths that leave the module's folder, unpacks to `Modules\<id>\<version>`, and loads the module with `Assembly.LoadFrom`. A new version starts at the next run, because a loaded DLL can't be replaced. `consoleKainosModules.cs` holds `KainosModuleHost` and the console side: Modes menu items (with a Classic menu-bar copy), panes placed by `kainosTermPlace`, and the `kdigi` taps. A module taking the receive tap closes RTTY and CW, and opening RTTY or CW calls the module's `released`. Markers are drawn by `drawKainosModuleMarkersDX2D`. `setupKainosModules.cs` is the Setup page; the state lives in the hidden `txtKainosModules`. See `Documentation/Modules.md`.
- Technical (crisp module drawing): `ModuleApi/KainosCrispHost.cs`, a public control in `Kainos.ModuleApi.dll`. A module puts it in its pane in place of a control. When the process is DPI-unaware and the system DPI is above 96, the control goes into a borderless `Form` created inside `SetThreadDpiAwarenessContext(PER_MONITOR_AWARE_V2)` (WS_EX_NOACTIVATE, WS_EX_TOOLWINDOW). That form is owned by the placeholder's form and positioned from `GetWindowRect` read in a PMv2 scope (physical pixels). It follows the placeholder's and its ancestors' moves, resizes, visibility and re-parenting, with a 100 ms poll as a backstop. It is clipped with `SetWindowRgn` to what the ancestors show and hidden when the placeholder isn't visible or the owner is minimised. A click activates the owner (`WM_MOUSEACTIVATE` → `MA_NOACTIVATE`). The approach first tried was `SetThreadDpiHostingBehavior(MIXED)` in `Main` plus a PMv2 child control, but the child still comes out DPI-unaware: Windows doesn't let a per-monitor-aware child into an unaware or system-aware parent (`DPI_HOSTING_BEHAVIOR` remarks). Additive, so `KainosModuleApi.Version` stays 1; a module using it sets `minKainos` 1.0.11. Nothing else in Kainos changes, and Kainos itself stays DPI-unaware.
- Technical (terminal height): `KainosTermGrip` (consoleKainosTerminal.cs) is the 6 px bar (`KainosTermGap`) between `panelDisplay` and the docked terminal. While it's dragged only the bar moves; on release `KainosSetTermHeight` sets the height and calls `positionKainosColumn` once, since resizing the panadapter rebuilds its DirectX surfaces. The height is in design px, from 140 up to whatever leaves the panadapter 160 px (`KainosTermMaxHeight`, applied again when the window shrinks), and is saved in the hidden `txtKainosTermHeight`. The module pane's panel and the bar are `IKainosTerminal`, so `kainosDisplayBottom` looks past them as it does past RTTY's and CW's. Before this, a docked module pane was treated as an obstacle under the panadapter.
- Technical (docked heights): `_kainosTermHeights` (consoleKainosTerminal.cs), keyed "rtty", "cw" or "m.<module id>" (`kainosTermKey`, the one docked now); `KainosSetTermHeight` sets the docked one's (the standard height is kept as no entry). Saved in `txtKainosTermHeight` as "rtty=300;m.sstv=480"; a plain number from before 1.0.12 is given to rtty, cw, m.sstv and m.digimodes. A module pane's content can implement `IKainosPaneHeight` (new in 1.0.12): its `PreferredHeight` is its default (and what a double-click on the bar goes back to), its `MinimumHeight` how short it may be dragged (40 - 140), and its popped-out window may be that short too (`kainosPaneWindowMin`).
- Technical (module voice API): `IKainosVoice` in `Kainos.ModuleApi.dll`, a new interface beside the unchanged ones (API version stays 1; a module asks with `host as IKainosVoice` and sets `minKainos` 1.0.12). `kdigi.c`: `xkdigi_mic` copies the transmit input in `xpipe` (TX position 0) after VAC and before `xradae_tx` / `xkdigi_tx` (`KDigiMicTap` / `Rate` / `Read`); `xkdigi_preview` adds a ring to RX1's audio before it's copied to `rbuff` (`KDigiPreviewEnable` / `Rate` / `Write` / `Queued` / `Clear`). `Console_KeyDown` offers F1 - F12 and Esc without modifiers to `KainosModuleKey` (consoleKainosModules.cs) after the Kainos text-box check; the newest handler first. A stopping module's mic tap, preview and keys are released in `KainosModuleHost.Shutdown`.
- Technical (module dock buttons): `IKainosDock.AddDockButton(label, menuText, tip, click)`, also new in 1.0.12. The dock (consoleKainosDock.cs) has a last group of `KainosToolSlots` (4) hidden check boxes of Kainos's own (`kainosToolTargets`); `KainosAddModuleDockButton` (consoleKainosModules.cs) claims a free one (its action in `Tag`, shown), and adds a menu item through `KainosAddModuleMenuItem(..., tools: true)`, which goes in the Tools menu. `KainosModuleDockItem` lights the button and ticks the item together; it's null when all slots are taken, and the module falls back to `AddMenuItem`. Removed with the module.
- Technical (module reinstall): `KainosModuleManager.installZip` unpacks into `freeFolder` (KainosModules.cs): Modules\<id>\<version>, or "<version>-2", "-3" ... when that's the folder in use (its DLL is loaded until Kainos closes); the state saves the folder name beside the version (`"folder"`, read back by `Load`, the version used when it's missing), and `cleanUp` removes the old one at the next start.

**New: the right-hand column, band plans and temperature**
- The tabs along the top of the right-hand column can be **put on one row**, to give the column more room: click the small **▴** chip after the last tab (or right-click a tab, **Tabs on one row**). As many tabs as fit are shown, in your order (drag your favourites to the front), then a **+4 ▾** chip saying how many more there are. Click it to show them all again. The chip is outlined in gold when an open tab is among the hidden ones. It's remembered.
- **US Advanced class** is in the licence band plans (GitHub #28): its CW and data from 3.525, 7.025, 14.025 and 21.025 MHz, and phone from 3.700, 7.125, 14.175 and 21.225 MHz; the other bands as General.
- **The radio's temperature in °F** (GitHub #28): Setup > Appearance > Kainos, **Temperature in °F**, or click the temperature in the status bar to switch.

- Technical (#28): `KainosLicences.Countries` gains "Advanced" for the United States, `KainosBandPlan.unitedStates` its segments (FCC 97.301 / 97.305); `chkKainosFahrenheit` (setupKainosWizard.cs) and `Console.KainosTemperatureText` (consoleKainosSupport.cs) for the status bar's HL2 temperature, whose click toggles the box.
- Technical (tabs on one row): `KainosColumn.layoutTabs` (consoleKainosColumn.cs) places the chips that fit and gives the rest an empty `TabRect` (not drawn, clicked or dropped on), then `_toggleRect` for the chip that switches; `TabsOnOneRow` raises `TabsChanged`, so the console saves it and lays out again. Saved as `!onerow` at the end of the tab state (`txtKainosColumnTabs`), which an older Kainos ignores.

**New: filters**
- **AUTO NOTCH** (on each VFO flag's DSP tab) finds steady carriers and heterodynes in the receiver's passband and notches them, up to eight at once. Thetis's ANF follows only one or two tones. A carrier is notched after it has held steady for about a second and a half, and the notch is lifted about a second after it goes. Notches stay put while you tune. It only works in voice modes (LSB, USB, AM, SAM, DSB): in CW and the digital modes the steady tones are the signal. Your manual notches aren't changed and don't need to be on. The flag shows **AN** while it's on, and the button shows how many carriers are notched.
- **PEAK** in the RTTY window puts two narrow peaks on the mark and space tones and cuts the noise and QRM around and between them, for your ears and for the decoder. The peaks follow the shift, the baud rate and AFC. When PEAK is off or the window closes, the receiver goes back to whatever Setup had.

- Technical (auto notch): `wdsp/kntap.c` copies each channel's I/Q at the DSP rate in `xrxa` just before `nbp0`, so a carrier is still visible after it's notched (`KainosNotchTapRun` / `Read` / `Info`; `Info` gives the rate, the notch database's tune + shift, and `nbp0`'s passband). The notch database (`nbp.h`) has a second list, `kn` / `kcenter` / `kwidth`, set by `KainosNBPSetAutoNotches`. `calc_nbp_impulse` and `calc_nbp_lightweight` design with the manual notches only while `fnfrun` is on, plus the auto list always (`nbp_gather_notches`). Thetis's notch pages and `SaveNotchesToDatabase` read only the manual list. `consoleKainosAutoNotch.cs` runs on a 100 ms UI timer: a 4096-point Hann FFT, peaks 15 dB over the passband median and 12 dB down three bins out, followed at RF (15 Hz match) with a presence of 0.9 x last + 0.1 when seen, notched (50 Hz wide) above 0.8 and lifted below 0.3, at most eight. RX1's notches also go to its sub-receiver. Saved in the hidden `txtKainosAutoNotch`.
- Technical (RTTY PEAK): `rttyPeakSync` (consoleKainosRtty.cs, every UI tick) drives `RXADollyRun` / `Freq0` / `Freq1` on the RTTY receiver to the demodulator's centre ± shift / 2, with `SetRXAmpeakFilBw` at max(60, 1.6 x baud) Hz. It saves the Setup values when it takes the receiver and puts them back when it lets go (the widths back to WDSP's 75 Hz). The Dolly filter runs before the kdigi tap, so the decoder hears it too.

**Fixed**
- **Turning spots off on the panadapter is remembered** (GitHub #29). With DX cluster or POTA spotting on, Kainos put the spots back on the panadapter at every start, whatever you'd set. They're now shown when you turn DX or POTA on, and stay as you leave them.
- Technical (#29): `kainosSpotsStart` (consoleKainosSpots.cs) no longer calls `kainosShowSpotsOnPanadapter(true)`; `kainosSpotsChanged(turnedOn)` does, only when the DX or POTA button has just turned one on. `chkShowTCISpots` (Setup > TCI), which the Pan button and Setup set, is what's saved.
- **Ctrl + mouse wheel on a slider in the right-hand column moves only the slider** (GitHub #32). It used to scroll the column as well, unless the column was already at the end. The same with "adjust first": the wheel on a slider no longer scrolls the column too.
- Technical (#32): a wheel message a slider leaves unhandled goes on to its parent, the column's viewport, whose `OnMouseWheel` scrolled. `KainosWheelFilter.ColumnScrolls` (consoleKainosColumn.cs) now decides there as the filter does: no scrolling when the wheel is adjusting (Ctrl, or the wheel alone with "adjust first") and the control under the pointer uses it.
- **Opening one digital mode un-ticks the one it replaces.** Opening SSTV after FT8 / FT4 (or any module after another, or RTTY / CW) closed FT8's window but left it ticked in the Modes menu, and the module still thought it was open. It's now closed properly, and the Voice Keyer's F-keys stop when another window takes its place.
- Technical (module pane replaced): `KainosTerminalOpening` (consoleKainosModules.cs) closes another module's pane with `KainosModulePane.CloseForAnother`, which raises `Closed` as the user's close does, so the module runs its own close (stops, un-ticks, releases its keys). The modules' own "receive audio lost" handler had returned early, as their pane already looked closed.
- **Compact view: the right-hand column takes clicks again** (GitHub #32). When it slid in from the edge, its buttons, drop-downs, sliders and check boxes ignored clicks (the mouse wheel and the left-hand dock worked).
- Technical (#32, compact view): a click activates the column's edge window, which focused its first control, the VFO SYNC panel's wheel-tune box; Thetis's `HideFocus` sends that box's focus to `btnHidden` in the console, which took the activation straight back, and the click was lost. `KainosEdgeForm.FocusSink` (consoleKainosCompact.cs) is now the edge window's active control, set again as it's shown, and `HideFocus` keeps focus in an edge window. Found by logging activation and the mouse in the edge window.
- **MUTE now mutes** (GitHub #19). On a Hermes Lite 2 you usually hear the radio through your PC's sound card (VAC), and MUTE (on the VFO flags, the AUDIO tab, anywhere else) left that audio playing. MUTE now silences VAC too. If you feed VAC to another program and want it to keep receiving while you mute the speaker, untick **Mute will mute VAC 1** / **VAC 2** in Setup > Audio > Options.
- Technical: Thetis's `chkVAC1WillMute` / `chkVAC2WillMute` now default on (setup.designer.cs), and `Setup.KainosCheckVacMute` (setupKainosWizard.cs, run from `InitConsole` beside `KainosCheckRadioModel`) switches them on once for existing installs, remembered in the hidden `chkKainosVacMuteChecked`.
- **The TX tab's panel no longer disappears while you scroll** (GitHub #20). With TX and AUDIO SCOPE both open in the side column, scrolling down so TX's top part went out of view hid its mode panel (the mic, compressor and VOX controls) entirely. It now stays, cut off neatly at the column's edge like everything else. The same goes for VFO SYNC.
- Technical: `kainosPin` (consoleKainosColumn.cs) used to move a pinned panel off screen unless it was wholly inside the viewport. It now places a partly visible panel and clips it with a window `Region` (`kainosClip`, removed again by `kainosUnpin`). The METERS container still goes all or nothing, since it's a DirectX surface. `kainosStandInPictures` now tests the middle of the part that shows, so a panel that's cut off still gets its picture for the scroll glide and doesn't jitter.
- **The TX tab's digital-mode panel has the Kainos look.** In DIGU / DIGL the TX tab showed Thetis's VAC panel shrunk to fit: small sliders, white drop-downs and boxed groups. It's now laid out like the phone panel, with full-width RX and TX gain sliders, Kainos drop-downs for the transmit profile and VAC sample rate, and a STEREO button. With RX2 on, **VAC: RX1** / **VAC: RX2** buttons choose which receiver VAC carries. Classic is unchanged.
- Technical: `consoleKainosDigital.cs` (new), `kainosDigitalLayout` / `kainosDigitalRestore`, called from `kainosTxMeasure` / `kainosTxArrange` / `kainosUnfitPanels` beside the phone panel's. Thetis's `lblRXGain` / `lblTXGain` and `lblVACRXIndicator` / `lblVACTXIndicator` are reused as the captions since they keep themselves up to date; the combo boxes, group boxes and RX1 / RX2 radio buttons are moved out of sight and driven through `KainosDropDown` and `KainosActionGrid`.
- **Choosing a skin no longer fails part-way** (GitHub #22). At 125 % display scaling the Kainos layout can make a button wider than Windows lets a skin picture be (256 px), and loading a skin stopped there with an error, after which no other skin could be chosen until Kainos was restarted. Such a button now simply goes without the skin's picture, and a skin that does fail is logged in ErrorLog.txt instead of stopping everything after it.
- Technical: `Skin.kainosNoImageList` (Skin.cs) skips `ImageList` set-up for check boxes, buttons and radio buttons wider or taller than 256 px; `comboAppSkin_SelectedIndexChanged` (setup.cs) catches and logs a failed `Skin.Restore`, so `_skinChanging` is cleared and, at start-up, the rest of `ForceAllEvents` still runs.
- **A note when external PTT stays keyed** (GitHub #23). If the radio says your PTT is released but Kainos is still transmitting from it half a second later, Kainos now writes a line to KainosErrors.txt saying what was holding it (the 2-tone test, MOX, PureSignal, a RADE end-of-over ...). Nothing about keying changes. If it happens to you, **Report a bug** (top right of the menu bar) includes that line, which shows what to fix.
- Technical: `kainosPttWatch` (consoleKainosPtt.cs, new) runs at the top of each `PollPTT` tick, before its guards. When `_mox` is on in `PTTMode.MIC` and the PTT bit of `nativeGetDotDashPTT` is clear for over 500 ms (3.5 s while a RADE end-of-over is running), it logs the raw PTT bits, `_current_ptt_mode`, `_manual_mox`, 2-tone, tune, PureSignal, the RADE state, `_disable_ptt`, `_rx_only`, `_tx_inhibit`, QSK, the Ganymede PA flag and the radio model, once per over.
- **The METERS tab's meter stays hidden when you transmit** (GitHub #26). With METERS turned off (for example because you use a meter in its own window instead), the meter appeared at the top left over the menus and buttons on every transmit and stayed there until METERS was turned on and off again. It now stays hidden.
- Technical: `MeterManager.OnMox` shows a disabled container that has Show on TX / RX set, so it re-enabled the container Kainos had switched off with `enableContainer(false)`, at its last docked place. `MeterManager.KainosHoldOff` keeps a set of containers Kainos has switched off, which `OnMox` now leaves alone; `kainosMeterEnable` (consoleKainosColumn.cs) sets it with every on / off of the METERS container.
- **The slice flags on the panadapter follow the UI scale** (GitHub #27). After changing Setup > Appearance > Kainos > UI scale, a flag on the panadapter kept its old size inside a window of the new size: at 75 % it was cut off on the right (the frequency and the tab row), and at 125 % / 150 % it sat in the corner of a dark box, with the frequency running into the mode and the S meter cut off. It now redraws at the new size straight away, an open tab's drawer included. The **Blobs** / **Peak** buttons under the panadapter also kept their old-size label until the mouse passed over them, and above 100 % their label was too big for them; they're redrawn, with a label that fits.
- Technical: `KainosFlagForm` sized its `KainosFlagView` (and drawer) only when it was made, compacted or a tab opened, while `placeKainosFlag` sized the form from `PreferredSize2` every time. `KainosFlagForm.FitToScale`, called by `placeKainosFlag`, lays the view out again when its size no longer matches `PreferredFlagSize`, and rebuilds an open drawer at the new scale. The `KainosUI.ScaleChanged` handler (consoleKainosColumn.cs) now ends with `Invalidate(true)`, since a Kainos control whose bounds don't change (the info bar's proxy buttons) isn't repainted by the layout; and `KainosButtonGrid` caps its label at 60 % of the button's height, as a proxy keeps the Thetis button's height while the scale grows.
- **The bar under the panadapter stops growing at 125 %.** Above that UI scale its sliders, buttons and drop-down stay at their 125 % size, leaving the room to the panadapter; the rest of Kainos keeps scaling.
- Technical: `KainosUI.Capped(max)` caps `KainosUI.Scale` / `KainosUI.S` for the current thread until disposed (thread-static, so the panadapter's render thread is unaffected). `positionKainosBar` lays the bar out inside `Capped(KainosUI.BarMaxScale)`, and `KainosSlider`, `KainosButtonGrid`, `KainosActionGrid` and `KainosDropDown` have a `MaxScale` they paint within (the slider's mouse handling too), set to 1.25 for the bar's controls.
- **RADE can use USB / LSB instead of DIGU / DIGL** (GitHub #21). New **Mode** choice in Setup > DSP > FreeDV (RADE): **Digital (DIGU / DIGL)**, as before, or **Voice (USB / LSB)**, for radios whose own mic jack isn't used in the digital modes (the ANAN G2), so you no longer have to switch to USB by hand each time. The TX compressor, CFC and EQ are still bypassed during RADE overs either way.
- Technical: `comboRadeMode` (setupRade.cs) sets `Console.RadeVoiceModes`, which `SetRadeEnabled` (consoleRade.cs) uses to pick USB / LSB or DIGU / DIGL for the receiver, with the same upper / lower split (upper from 10 MHz and on 60 m).
- **The audio scope can show speech** (GitHub #25). New timebases of **200 ms, 500 ms, 1 s and 2 s** for watching SSB, as AetherSDR does. From 200 ms the waveform rolls (newest on the right) and is drawn as the signal's envelope, so speech shows its shape instead of a scribble. The quick buttons are now 2 ms, 10 ms, 50 ms, 200 ms and 1 s; every timebase is on the right-click menu. The trigger still holds the wave still up to 100 ms.
- Technical: `KainosScopeTap` keeps 131072 samples (about 2.7 s at 48 kHz) and `Latest` copies only the samples a frame needs into the end of the view's arrays (two `Array.Copy`s around the ring). `KainosScopeView`: `Timebases` runs to 2000 ms with `ButtonTimebases` and `TimebaseText`; at `RollFrom` (200 ms) and above the trigger is skipped, only one view is fetched, and `drawEnvelope` fills each pixel column from its lowest to its highest sample.
- **Switch to the I/O board's Alt RX input from the VFO flag** (GitHub #7). On a Hermes Lite 2 with the I/O board, click the antenna on VFO A's flag (ANT1) to receive on the board's receive-only input on this band; it shows **ALT RX** in ice blue, and a click puts it back. It's remembered for each band, the same setting as **Alt Rx** in Setup > General > Ant/Filters > Antenna. Transmit stays on the main antenna. The HL2 has one receiver input, so RX2 hears the same antenna.
- Technical: `Console.KainosToggleAltRx` / `KainosAltRxOn` (consoleKainosFlag.cs) tick the band's "Alt Rx" box through `Setup.KainosSetAltRx` (setupKainosWizard.cs), so Thetis's `ProcessAlexAntCheckBox` sets the receive-only antenna and `Alex.UpdateAlexAntSelection` switches the I/O board (`REG_RF_INPUTS`) as before. Offered only when `HL2IOBoardPresent` (detected while the radio runs) and the band has an Alt Rx box; switching it on clears "Tx Ant", which would otherwise hide it; not while transmitting.
- **A note when the HL2 I/O board isn't found.** If Kainos can't recognise the I/O board when the radio powers on (it then unticks **HL2 I/O Board** in Setup > General > H/W Select), it writes what the board answered to KainosErrors.txt, so **Report a bug** shows whether the board answered at all.
- Technical: `UpdateIOBoard` (console.cs) logs through `kainosIoBoardLog` when the hardware-version read (0x41) isn't version 1: the raw reply (`IOBoard.KainosVersionReply`, recorded by `readResponse`), then a read of the controller's firmware registers (0x1d, 9 and 10). All FF from both means nothing on the HL2's I2C bus answers. The 20 ms timeouts are logged too.

### Kainos 1.0.10

**New: Audio Scope colours**
- A **Colours** choice on the Audio Scope's right-click menu: **Standard** (as before, the default), **Kainos** (glowing trace and a gradient-filled spectrum), **Phosphor** (green, like an old CRT, with an afterglow), **Heat** (green, yellow and red by level) and **Spectrum** (a rainbow by frequency). In light mode the glow and afterglow are left out.

**Fixed**
- A new install of Kainos set the radio model to **Hermes** (a different board) instead of **Hermes Lite 2**, Thetis's own default: the HL2 ran with Hermes's hardware handling, Setup had no HL2 Options page, and its OC Control page was titled "Hermes Ctrl". New installs now start as a Hermes Lite 2, the setup wizard sets it too, and an existing install still set to Hermes is moved to Hermes Lite 2 once, with a message saying so.
- Typing a frequency in the **VFO** tab of the right-hand column didn't work (GitHub #15; the VFO flags were fine). Kainos's own text boxes now get the keys they're typed: the main window previews every key for Thetis's keyboard shortcuts and only let Thetis's own boxes through, so Enter never reached the VFO tab's entry (and moved the focus away, which cancelled it), and with the shortcuts on, letters and Space could fire shortcuts while typing in Kainos's boxes (Memory, Spots, Kiwi and others).
- N1MM+'s spectrum (panafall) was shifted by the CW pitch in CW modes (GitHub #14; 550 Hz low in CWU with a 550 Hz pitch). This came from Thetis 2.10.3.15, where the pitch was applied twice.
- In SPLIT, **MUTE** in VFO B's Audio drawer didn't mute B (GitHub #12). B is heard through RX1's sub-receiver (LISTEN), and the button muted RX2, which isn't running then. It now mutes the sub-receiver, keeping the LISTEN AF level for unmuting; moving LISTEN AF unmutes it (when Thetis's "AF sliders unmute" option is on), and turning split off clears it.
- In the Kainos layout the whole window froze for about a fifth of a second every second (since 1.0.9): the panadapter's cursor marks stopped following the mouse, and everything else paused with them. Two causes: a picture of the meter, taken once a second for smooth scrolling, made Windows repaint the whole window, and the window's background was a full-size picture (about 190 ms to paint at 4K) rather than just a colour. The meter's picture is now taken only as you start scrolling the column, and the Kainos layout's background is plain colour. Thetis's skin backgrounds (Classic) also paint several times faster.
- With no audio, the Audio Scope's auto scale no longer follows the silence down until the noise fills the view; it stops at -40 dBFS.
- The Audio Scope now moves smoothly: it drew about 21 frames a second at uneven spacing (31 to 62 ms), which showed as jitter and little pauses; it now draws a steady 30, in step with the screen, on its own thread, so it keeps going while the rest of the window is busy (for example while you move the mouse over the panadapter).
- The AF spectrum is steadier: it averages each frequency's level over about 0.2 s (it used to jump about with band noise every frame), and its auto scale no longer creeps down and jumps back up all the time: it goes up at once, but only comes down after 3 seconds of quieter audio, and glides there.

- Technical: `KainosAudioScope.cs` (`Themes`; Phosphor keeps an afterglow bitmap faded each frame; the glow is drawn through about one point every 3 px; the frames are paced by a thread waiting on `DwmFlush` instead of a Forms timer, whose 15.6 ms grain gave 31 / 47 / 62 ms frames, and drawn on that thread into a back picture copied to the window with `GetDC` (the console's sample rates and AF span are fetched on the UI thread every 100 ms; `_sync` keeps the UI thread's own painting apart); the AF columns are averaged as power with a 180 ms time constant by the clock, and the auto top has a target with a 3 s hold before stepping down, eased at 150 dB/s up and 20 dB/s down) and `consoleKainosAudioScope.cs` (menu, and the `col` setting).
- Technical (the freeze): `consoleKainosColumn.cs` takes the pinned panels' pictures on `KainosColumn.GlideStarting` instead of a 1 s timer (`PrintWindow` with `PW_RENDERFULLCONTENT` renders the whole top-level window); `console.cs` `resizeBackgroundImage` skips the window-sized picture when the Kainos layout's plain background is in use (`kainosPlainBackground`) and makes the skin's one `Format32bppPArgb`. Found with a UI-thread watchdog that logged the stack whenever the UI thread stalled over 80 ms.
- Technical (GitHub #12, #14, #15): `consoleKainosSupport.cs` `KainosTypingInOwnBox` (the focused control is a plain `TextBoxBase` or `NumericUpDown`, not Thetis's `TextBoxTS` / `NumericUpDownTS`), checked first in `Console_KeyDown` and `Console_KeyPress`. `N1MM.cs`: no extra pitch shift, since `Display.CentreFreqRX1/RX2` is the DDS frequency, which already includes it (`UpdateVFOAFreq`), as the panadapter's own scale does (`vfo + getCWSideToneShift`). `consoleKainosFlag.cs` `KainosSetSubMute` / `KainosToggleMute`, applied in `ptbRX1Gain_Scroll` with RX1's mute.
- Technical (radio model): `setup.cs` defaults a blank database to `HERMES LITE`; `KainosWizardAnswers.Model` (always `HERMES LITE`) is applied first in `KainosWizardApply`, since the HL2 Options page only exists for that model; `Setup.KainosCheckRadioModel` runs once (`chkKainosModelChecked`, so a later deliberate change sticks), in `InitConsole` straight after the Setup form is created, so MI0BOT's HL2 start-up (attenuator range, the menus it removes, antenna labels) runs for the new model; the message is shown once the console is up.

### Kainos 1.0.9

**New: compact view**
- In the Kainos layout, **Collapse** in the menu bar is now **Compact**: the left dock and the right-hand column tuck away at the window's edges, the panadapter fills the window (full screen or a small window), and the VFO flags stay. Point at the left or right edge and the dock or the column slides in over the panadapter; move away and it slides back. **Expand** puts everything back. Thetis's own collapsed layout is still there in Classic.
- Kainos remembers whether you were in the compact view.
- Technical: `consoleKainosCompact.cs`. The panadapter is drawn by DirectX, which covers controls placed over it, so the dock and the column slide in as owned windows (`KainosEdgeForm`), taking the column's pinned Thetis panels (mode panel, VFO SYNC, meter) with them; `CollapseToolStripMenuItem_Click` hands the menu item to `KainosCompactMenu` in the Kainos layout.

**New (experimental): VST3 plugins in Kainos Audio**
- Kainos Audio's transmit chain has a **Plugin** stage: choose a VST3 plugin installed on your PC (or browse for a `.vst3`), open its own window, and drag the stage anywhere in the chain like the others. It runs in voice modes only (never in digital modes or CW), Kainos Audio's BYPASS takes it out too, and Thetis's leveler, compressor and ALC still come after it. Your plugin and its settings are remembered.
- Technical: `KainosVst.dll` (`Source/KainosVst`), a host built on Steinberg's VST3 SDK 3.8.1 (MIT licence, the `Source/vst3sdk` submodule), built with CMake by the console project. WDSP's strip has a plugin slot (`AS_PLUGIN`): `xaetherstrip` calls the host's `kv_process` on the chain's float audio, in place, and `SetTXAStripPlugin` swaps it under the DSP lock. `RadioDSPTX.SetTXStripPlugin` attaches it only in voice modes. The plugin's state (component and controller) is saved as base64 with the options, a moment after it changes in the plugin's window. Receive and running plugins in a separate process (so a crashing plugin can't take Kainos down) are still to come.

**New: a tidier menu bar (Audio, Modes, Tools)**
- In the Kainos layout the menu bar is now **Setup, Memory, Audio, Modes, Tools, Compact, About**. **Modes** has FreeDV, RTTY, CW and CWX (RTTY and CW ticked while their terminal is open); **Tools** has XVTRs, Linearity, Radio Astronomy (RA) and Finder.
- **Audio** replaces Equalizer and Kainos Audio: **Kainos Audio: Receive** and **Transmit**, **VST3 Plugin** (straight to the transmit chain's plugin page), **Equalizer**, and shortcuts to the **VAC 1 / VAC 2**, **VOX / DEXP** and **CFC** settings. Classic keeps Thetis's menu bar as it was (`consoleKainosAudioMenu.cs`; the grouped items are hidden while it's on, and the new entries call the same handlers).

**New: band switching for the Xiegu GPA100 over Bluetooth**
- Kainos can tell a Xiegu GPA100 the band over Bluetooth, so the amp switches by itself: **Setup > Serial / Network / Midi CAT > Amplifier (CI-V)**, with step-by-step setup, a button that opens Windows's Bluetooth settings, and **Find GPA100**, which finds the amp's COM port for you. Kainos sends the transmit frequency (VFO B in split, and the HL2's own frequency with a transverter), and picks the amp up again by itself if it's switched off or goes out of range. Band switching only: keep the amp's ACC1 cable for PTT and ALC. Other amplifiers that follow an Icom radio over CI-V can work too. See the [Amplifier Band Switching](https://github.com/PNWHam-K7JUS/Kainos/wiki/Amplifier-Band-Switching) wiki page.
- Technical: `KainosCiv.cs`. The amp is the CI-V controller: it polls "an IC-705" (A4) about 10 times a second with `03` (frequency) and `1C 00` (TX / RX). `CivResponder` answers those (and `04`, `19 00`) from a snapshot the console takes on the UI thread every 100 ms (frequency) and in `MoxChangeHandlers` (MOX); `KainosCivLink` runs it on the Bluetooth link's outgoing COM port on its own thread, reopening it every 3 s after an error. **Find GPA100** matches the paired `XG_GPA100`'s address (`BTHENUM\DEV_...` in `Win32_PnPEntity`) to the outgoing Bluetooth serial port's device ID (the incoming port's address is all zeros). The protocol is checked offline against the frames the amp sent in the hardware test; the GPA100 gives no power or SWR over Bluetooth.

**Fixes**
- The menu bar said **Collapse** instead of **Compact** in the Kainos layout (Thetis renames it as it starts up).
- The right-hand column scrolls smoothly: the mouse wheel glides it instead of jumping, a touchpad scrolls in proportion (it used to jump a whole step for every small movement), and the column is drawn once per step instead of control by control.
- Menus in the Kainos layout could show light for a moment before turning dark. Their windows now start dark, and they no longer lay themselves out again each time they open. Windows's own menus (a text box's right-click Cut / Copy / Paste) are dark too, on Windows 10 1903 and later.

### Kainos 1.0.8

**New: hear VFO B in split**
- In split, the VFO B flag (and VFO B in the right-hand column's VFO tab) has a **LISTEN** button: click it to hear VFO B along with VFO A, for example to hear the DX's pileup while you listen to the DX. It uses Thetis's second receiver inside RX1's span (the SubRX button), so the **MAIN** and **SUB** sliders under the panadapter set which side you hear each one on. VFO B starts at VFO A's volume; set its own level with **LISTEN AF** in VFO B's AUDIO drawer. If VFO B is too far from VFO A for it to reach, the button says **TOO FAR** (GitHub issue #4).

**New: AUDIO SCOPE tab**
- A new **AUDIO SCOPE** tab in the right-hand column (off by default; turn it on from the tab chips) shows what you're hearing as a waveform: RX1's audio, including VFO B when LISTEN is on, and your processed transmit signal while you transmit (marked **TX**, in red). The trace holds still on the signal and scales itself to fit, with the peak level in dBFS. Choose 2, 5, 10 or 20 ms across (50 and 100 ms on the right-click menu), and click the scope to hold the trace. It only runs while the tab is on screen.
- **AF spectrum**: right-click the scope for **Waveform**, **AF spectrum** or **Both**. The spectrum shows your audio from 0 to 3 kHz on SSB (3.5 kHz on digital, 6 kHz on AM, 8 kHz on FM, and 1 kHz around your pitch on CW, with the pitch marked), with your receive filter's passband shaded, the strongest frequency and its level, and a peak-hold line. While you transmit it shows your transmit signal's spectrum, handy for setting up the TX EQ.
- The right-click menu also has a fixed scale (0 to -50 dBFS full scale) in place of the automatic one, the trigger on or off, left, right or both channels, smoothing, peak hold and an **AF waterfall** under the spectrum (its colours follow the noise floor, so signals stand out). Your choices are kept. See the [Audio Scope](https://github.com/PNWHam-K7JUS/Kainos/wiki/Audio-Scope) wiki page.
- Technical: it reads ChannelMaster's scope tap, the one Thetis's Scope display uses (`KainosScopeTap`, fed from `DoScope.xscope` in `cmaster.cs`). `CMSetScopeRun` keeps the tap running while either Thetis's display or the tab wants it, and Thetis's scope arrays are only filled when its display does.

**New: pin the VFO flags anywhere, or turn them off**
- Drag a VFO flag **sideways** and it stays where you drop it on the panadapter, instead of following its VFO, marked with a gold pin. Drag it again to move it; click the pin, or double-click its face, to put it back on its VFO. Dragging straight down still moves it down its VFO line as before (GitHub issue #11).
- **Setup > Appearance > Kainos > Slice flags** has a new choice, **Off (VFO tab only)**: no flags on the panadapter, just the VFO line. The right-hand column's VFO tab then gets the flags' tab row, and AUDIO, DSP, MODE, RIT/XIT, VAC and FREEDV open in the column.
- Technical: pinned places are saved as fractions of the panadapter's free room (`KainosFlagSettings`, now "dropA;dropB;compactA;compactB;pinA;pinB"); flag opacity 0 is "off" (`KainosFlagOpacity`), and `KainosBuildDrawer` takes a width for the column.

**New: choose what the mouse wheel does over the right-hand column**
- **Setup > Appearance > Kainos > Mouse wheel**: **Scrolls the column** (as now; hold Ctrl to change a slider) or **Adjusts the control under it**, where the wheel changes the slider, list, drop-down or number under the pointer and scrolls the column anywhere else, and Ctrl+wheel always scrolls the column (GitHub issue #4).

**Fixes**
- The squelch level in FM (and RX2's) went back to 100 at every start. It now keeps the level you set (GitHub issue #10). This came from Thetis: at start-up it stored the squelch slider's default as the level before the saved one was put on it; Kainos now puts the saved level on the slider first (`kainosSquelchSlidersFromSaved`).
- **Report a bug** showed "Screen: (InvalidOperationException)" instead of the screen size and scaling.
- Windows's Installed apps list showed Kainos as version 1.0.1 whatever version was installed: the installer kept its version from the 1.0.1 build. It now shows the real version.
- With RX2 on, Thetis's RX2 controls appeared in a strip under the panadapters. They're now hidden in the Kainos layout, giving the panadapters the room: RX2's mode, filter and DSP are on VFO B's flag, and its pan, squelch, AGC and AGC gain are now in flag B's **AUDIO** and **DSP** drawers.

### Kainos 1.0.7

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

### Kainos 1.0.6

**New: setup wizard for the Hermes Lite 2**
- A few questions and Kainos sets itself up: your callsign and grid square (used for spots, FreeDV RADE and the FreeDV Reporter), your country and licence class, and which HL2 boards you have (N2ADR filter board, HL2 I/O board, built-in PA, band data for an amplifier).
- **Audio page:** the HL2 has no audio output of its own, so receive audio and your microphone go through the PC. Pick your speakers and microphone, and play a test tone to check.
- **Look page:** Kainos or Classic layout, UI scale, and which right-hand column tabs start open.
- A summary shows exactly what will change before anything is set.
- Offered once after updating (choose "Not now" to skip), and available any time from Setup > Appearance > Kainos > **Run setup wizard**.
- On a new install with Thetis on the PC, the first-run question becomes a choice: set up for my HL2, import my Thetis settings, or skip.

### Kainos 1.0.5

**Changed**
- A cleaner TX tab in voice modes: Mic, Comp and VOX are full-width sliders like Master AF, the Transmit Profile is a drop-down right under them with Low / High on one row, and the buttons are in two rows of four.
- The TX tab's buttons and the squelch bar have the same rounded look as the rest of Kainos, gold while on.

### Kainos 1.0.4

**Fixed**
- Band, Mode and Filter drop-downs did nothing for some people who imported their Thetis settings (the Legacy Items "Hide ... button grid" options).
- The right-hand column could be squeezed or blank after importing Thetis settings, hiding the RX volume and squelch so it seemed there was no audio. Docked Thetis meter containers are now hidden in the Kainos layout; you no longer need to delete them.

### Kainos 1.0.3

**Changed**
- The VFO flags on the panadapter no longer hide spots, TCI flags or skimmer markers: they're see-through until you point at them (Setup > Appearance > Kainos > Slice flags), and you can drag them down the panadapter. Double-click a flag to put it back.

### Kainos 1.0.2

**Fixed**
- SPLT (split) didn't work. With split on, the VFO tab shows a VFO B box marked SPLIT, and VFO B's marker appears on the panadapter.
- Part of the right-hand column could go blank. Any cause is now written to `KainosErrors.txt`.

### Kainos 1.0.1

**Fixed**
- The squelch bar and the transmit panel's buttons are now clean Kainos controls instead of squashed skin pictures.

### Kainos 1.0.0

The first release: the Kainos layout (slice flags, right-hand column of tabs, left-hand button column), Kainos Audio, FreeDV RADE, built-in RTTY and CW, DX cluster and POTA spots, KiwiSDR listening, and an experimental 3D panadapter.

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
  - `KainosDock` stands in for Thetis's `panelPower` (POWER, RX2) and `panelOptions` (MON, TUN, MOX, 2TON, DUP, PS-A, xPA, REC, PLAY), plus VOX, in three groups. Each dock button is drawn by Kainos (`KainosUI.DrawButton`) and bound to the real Thetis `CheckBox`: a left click goes through `KainosUI.Press`, which calls the box's `OnClick` (it toggles and runs Thetis's Click handler, e.g. `chkMOX_Click`) and then its `OnMouseClick`, as a real click does (SPLT has `AutoCheck` off and toggles in its MouseClick handler; the column's buttons and drop-downs and the tab chips press the same way), a right click calls its `OnMouseDown`/`OnMouseUp` with the right button (Thetis's settings shortcuts). The dock redraws on the boxes' `CheckedChanged`, `EnabledChanged`, `VisibleChanged` and `TextChanged`, and lists only the boxes Thetis shows (VOX lives on the phone panel, so it leaves in CW and digital modes).
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
- Buttons: the squelch bar (`chkSquelch`, whose skin picture was squashed to the column's width) and the mode panels' toggle buttons (MIC, COMP, VOX, DEXP, RX EQ, TX EQ, AV, TX FL...) are drawn flat in Kainos colours in Kainos layout, gold text and outline while on; the skin's pictures and colours are saved and put back in Classic, and a skin loaded in Kainos layout is saved and covered again.
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

### TX section (phone modes)

- In voice modes the TX tab lays out Thetis's phone panel for the column (`consoleKainosPhone.cs`) instead of scaling it down as a block: Mic, Comp, VOX and DEXP (whichever Thetis shows) are a caption and value over a full-width slider like Master AF, VOX and DEXP keeping their level bars; the transmit profile is a Kainos drop-down under them (the AM list in AM / SAM, the main list otherwise); the TX filter's Low / High share a row; and MIC, COMP, VOX, DEXP, RX EQ, TX EQ, AV and TX FL are two rows of four. The panel's layout is recorded first and put back in Classic.
- The mode panels' buttons and the squelch bar are painted with `KainosUI.DrawButton` (rounded, gold while on), like the dock's.

### Column resilience and split

- The right-hand column measures and arranges each section on its own (`ArrangeSections`); a section that throws is laid out empty and logged once to `%APPDATA%\OpenHPSDR\Kainos-x64\KainosErrors.txt`, so one failure can't blank the rest of the column. The analog meter is capped at 3/4 of the column width tall (GitHub issue #1).
- The slice flags on the panadapter are see-through while the mouse isn't over them (Setup > Appearance > Kainos > Slice flags: Solid, 90, 75 (default), 60 or 45%; `KainosFlagOpacity`) and solid while it is, while a tab's drawer is open and while typing a frequency. Drag a flag by its face (not the frequency or TX) to move it down its panadapter; double-click the face to put it back. The drop is saved per flag (`KainosFlagSettings`, hidden `txtKainosFlags`), so spots, TCI flags and skimmer markers along the top stay visible (GitHub issue #2).
- Thetis meter containers docked on the console (usually brought over with the Thetis settings import) are hidden in Kainos layout and shown again in Classic (`kainosHideThetisMeters`): they sat over the column and squeezed it, hiding the RX tab's volume and squelch. Floating containers are left alone. The column's own meter is never one of them.
- The band, mode and filter drop-downs list their Thetis buttons by the buttons' own Visible setting (`KainosUI.OwnVisible`), so they work with Setup > Appearance > Legacy Items "Hide band / mode / filter button grid" on (those hide the whole Thetis panel); the band list follows Thetis's HF / VHF / GEN choice.
- With SPLT on and RX2 off, the VFO tab shows the B face (marked SPLIT, or QUICK SPLIT) and the violet B flag sits on RX1's panadapter at VFO B's frequency (`KainosSplitB` in `consoleKainosFlag.cs`).

### Setup wizard

- `KainosSetupWizard.cs` (window), `consoleKainosWizard.cs` (when it runs), `setupKainosWizard.cs` (reading and applying through Setup's own controls, so Thetis's handlers run as if the boxes were clicked).
- First run with a Thetis install and no Kainos settings: the old import prompt is now `KainosFirstRunChoice` (set up for the HL2, import the Thetis settings, or skip), before the settings load; the answer waits in `kainos_wizard_pending.txt` for the console.
- Once the console is up: after "set up" or "import" the wizard runs; otherwise it is offered once (Welcome page, Not now) to anyone it hasn't run for, including everyone updating. Setup > Appearance > Kainos > Run setup wizard runs it again. State in the hidden `txtKainosWizard` ("", "done", "later", "skipped").
- Pages: Station (callsign and grid square, the FreeDV RADE fields that spots and the Reporter use; country and licence class, saved in `txtKainosLicence` for the band plans), Hardware (N2ADR filter board = the N2ADR preset `chkHERCULES`, HL2 I/O board, built-in PA, Band Volts; advanced: CL1 10 MHz, CL2, TX latency, PTT hang), Audio (VAC 1 on or off, audio system, speakers and microphone from PortAudio's lists, as Setup's; a test tone through NAudio on the matching Windows device; the HL2 has no audio output of its own), Look (layout, UI scale, which right-hand column tabs are open: `KainosColumn.TabList` / `SetOn`), Summary (only what changes). Nothing is set until Apply, which also saves.

### VFO SYNC tab

- `consoleKainosSync.cs` lays out Thetis's `grpVFOBetween` for the column (as consoleKainosPhone.cs does the phone panel): rows for VFO Sync / Rx Ant, VFO lock A / B, tune step, quick memory, Save / Restore, and band stack with the quick recall pad; Thetis's labels moved away and Kainos captions in their place; the group box's frame painted over in Kainos layout; text alignment and the recorded layout put back in Classic. Its buttons are in `kainosStyledButtons`, so they're drawn rounded.
- The console, like Setup, keeps its controls in a list keyed by name: every control Kainos adds to a Thetis panel needs a unique Name, or Kainos stops at startup ("An item with the same key has already been added").

### Readable windows (issue #3)

- `KainosWindowTheme`: brighter `Text` / `TextMid` / `TextDim`. `watchContrast` on labels, buttons, text boxes, panels and group boxes: text light on a light background (`effectiveBack`) becomes dark, and goes back when the background does (watched through BackColorChanged, the container's too). `paintDisabled` redraws a disabled label's, check box's or radio button's text in a readable grey (Windows draws it darker than the background), with GDI+ as Windows does so it fits as before.
- `ucRadioList` (Thetis): a dark palette when the colour behind it is dark (the theme makes it transparent). Setup's meter and MMIO lists draw their text in the list's ForeColor rather than black.

### Hearing VFO B in split

- `KainosListenB` / `KainosToggleListenB` (consoleKainosFlag.cs) are Thetis's MultiRX (`chkEnableMultiRX`, SubRX in the bar): with RX2 off its sub-receiver is on VFO B, inside RX1's span. The flag view draws a LISTEN pill on VFO B's identity row while `KainosSplitB` (on the panadapter flag and the VFO tab's face), red TOO FAR when VFO B is more than 45 % of `SampleRateRX1` from `CentreFrequency`.

### Column scrolling and one-line flags

- `KainosScrollBar` (consoleKainosColumn.cs): shown when the open sections are taller than the column; `ArrangeSections` measures at full width first and again, narrower, when the bar is needed. `KainosWheelFilter` (an application message filter) turns WM_MOUSEWHEEL over the column (found with WindowFromPoint) into column scrolling, unless Ctrl is held.
- One-line flags: a click on the flag's letter (`BadgeClicked`) calls `KainosFlagForm.SetCompact`, which closes any open drawer; `KainosFlagView.paintCompact` draws the line. Saved per flag in `KainosFlagSettings` ("dropA;dropB;compactA;compactB").

### KiwiSDR waterfall

- `KiwiWaterfallClient` (KainosKiwi.cs): the Kiwi's W/F stream at the same address, path and stamp as the sound stream (`KiwiClient.WsBase` / `Prefix` / `Stamp`), so it shares that receiver slot; `SET zoom=Z start=` and `cf=` (older and newer firmware), `wf_comp=0`, `wf_speed=3`; each line is 1024 bytes after a 16-byte header, dB above -255. Zoom 0 is the receiver's whole bandwidth (from its `bandwidth` message), each step halves it.
- `KainosKiwiWaterfall` / `KiwiSpectrumView`: spectrum (a third of the height), scale, waterfall (a 1024 x 300 bitmap, newest line on top, colours from an auto noise floor, the 3D panadapter's palette). Click tunes where the pointer is (in CW that's the signal; Thetis's VFO in CW is the signal and the Kiwi's dial sits a CW pitch below); wheel tunes the dial by a step to suit the span; Ctrl + wheel zooms.
- Phase 3: `KiwiSpectrumView.drawBandPlan` (the console's band plan segments, uses and spot frequencies, `KainosBandPlan.ColourOf`) and `drawSpots` (`KainosSpotsBetween`, up to three rows of call tags, a click on one is `KainosTuneToSpot`); right-click `KiwiViewMenu` (contrast: the colours' dB range 80 / 60 / 40; speed: `wf_speed` 1 / 3 / 4); zoom per band (`_kiwiZoomByBand`, by the tuned frequency's band, set on zoom and applied on a band change). Saved in the Kiwi settings: `wfc`, `wfs`, `zooms`.
- Main view: `kainosKiwiMainView`, a second `KiwiSpectrumView` laid over the panadapter area (pnlDisplay's screen rectangle in console coordinates) while Kainos layout is on, the HL2 is off and a Kiwi is being listened to (`KiwiOnPanadapter`, "Main view" in the KIWI tab); the one W/F stream feeds both views. Follow B (`_kiwiFollowB`): VFO B, with RX2's mode when RX2 is on; saved as `followb`. The KIWI tab's tick follows and runs the waterfall even with the tab closed.
- `consoleKainosKiwiWaterfall.cs`: the window, a 300 ms tick that starts the stream when the listened-to Kiwi is ready (or changes), re-centres when the tuned frequency is beyond 40 % of the span from the centre, and titles the window. Tuning: Follow on sets VFO A; Follow off `KiwiTuneOwn`.
- The column's wheel filter now works by the column's area of the main window, so Thetis panels placed over the column (the TX tab's phone panel, VFO SYNC) don't take the wheel.

### Column tab order

- `KainosColumn` keeps the user's tab order (`_order`), saved in the same tab state as which tabs are open (`KainosColumnTabs`: the keys in the order shown). It's applied again whenever a section is added (some, like KIWI and SPOTS, are added after the state loads); tabs it doesn't list follow in the order they were added (`Section.Added`, also what Reset the order goes back to).
- A tab chip pressed and moved more than 6 px is dragged (a gold marker where it would drop, `dropIndex`); without the move, the release toggles it, as the press used to. Right-click: Move up / down / to the top / to the bottom, Reset the order. A section's title in the column (`HeaderRect`, painted on the viewport) can be dragged the same way: the viewport passes its mouse to `HeaderDown` / `HeaderMove` / `HeaderUp`, which drop before the first open section whose title is below the pointer, with a gold line there; right-clicking a title opens the same menu.

### SWR sweep

- `KainosSwrSweep.cs` (window, plot, saved sweeps as CSV in `%APPDATA%\OpenHPSDR\Kainos-x64\Sweeps`), `consoleKainosSwr.cs` (the sweep). The dock's SWR button presses a Button of Kainos's own (`kainosSwrButton`), which opens the window.
- The sweep uses Thetis's TUN: split must be off with VFO A transmitting. It sets the tune power source to fixed and pulsed tune off, finds the tune power giving 0.5 to 1 W from `alex_fwd`, then steps VFO A (about 110 ms to settle, 60 ms of readings averaged), working SWR out from `alex_fwd` / `alex_rev`. It stops at SWR over 5:1 three steps running (Thetis ignores SWR protection at low tune power), output over 1.2 W, no output, or TUN going off; afterwards TUN, tune power, its source, pulsed tune and VFO A are put back. Band edges are Region 2 for the United States and Canada (or no country set), Region 1 / 3 otherwise.

### Safety nets and light mode

- `consoleKainosSupport.cs`: items added to the Setup menu (Back up settings now, Run setup wizard, Light mode, Check for updates, Report a bug; Database Manager's item renamed to say it holds profiles, backups and restore) and a right-aligned Report a bug on the menu bar.
- Back up: `SaveOptions`, `DB.WriteDB` (the file, as on exit), then `DBMan.TakeBackup` with a dated description.
- Light mode: Setup's `chkKainosLightMode` (Appearance > Kainos); on, it saves the display FPS and 3D in `txtKainosLightSaved` and sets 20 fps (at most) and 3D off; off puts them back.
- Updates: once a day after the startup wizard check (`txtKainosUpdate`: last check date, skipped version), `version.json` on GitHub `main` against `KainosVersion.Number` (`Common.CompareVersions`); a newer one shows `KainosUpdateNotice` with `Documentation/ReleaseNotes/kainos-<version>.md` from GitHub.
- Report a bug: `KainosBugReport` (KainosSupport.cs) builds a GitHub new-issue link (title, `bug` label, body with the diagnostics; shortened if the address would be too long) and opens it in the browser. Diagnostics read the real screen size and scaling (`GetDeviceCaps` DESKTOPHORZRES), and name Windows 11 by build number (its registry still says Windows 10).

### Licence-aware band plan

- `KainosBandPlan.cs` also holds what each part of each band is used for (`Use`: the ARRL plan for Region 2, a simplified IARU Region 1 plan otherwise) and the popular spot frequencies (`Spot`); the band's labels are those, its colour the privileges.
- `KainosBandPlan.cs`: privilege segments (MHz, kind: all modes, CW and data, CW only, not yours, band edges only) by country and licence class. Built in: the United States from FCC 97.301 / 97.305 (Technician, General, Amateur Extra; 160 to 10 m and 6 m; 60 m's channels left out) and Canada by qualification (Basic: 6 m here; Basic with Honours and Advanced: every band, no mode sub-bands). Other countries: Region 1 / 3 band edges only. "Not yours" fills the rest of each band.
- `consoleKainosBandPlan.cs` turns the segments into pixel spans with the same `HzToPixel` the slice flags use; `drawKainosBandPlanDX2D` (displayKainos.cs, called from `DrawPanadapterDX2D` after the RTTY markers) fills a 20 px see-through band along the bottom (the top has the scale, slice flags and spot tags) with a solid top edge, a divider where segments meet, and the mode written in each segment (`measureStringDX2D` picks the longest wording that fits); not while transmitting, and only in Kainos layout.
- Setup > Appearance > Kainos > Band plan (`grpKainosBandPlan`, `chkKainosBandPlan`): the country and licence lists are kept in `txtKainosLicence`, shared with the setup wizard (each updates the other). Setup saves every named control in a list keyed by name, so every control added to Setup must have a unique Name (two unnamed ones crash Setup at startup).

### Modules

- These are downloadable add-ons, kept out of the installer and listed in `modules.json` at the repository root. The full design and how to write a module are in `Documentation/Modules.md`.
- `Kainos.ModuleApi.dll` is the only thing a module compiles against. Its assembly version stays 1.0.0.0 while `KainosModuleApi.Version` is 1. A module's `module.json` `api` must match exactly, so raise the version when an interface changes. Adding a class (`KainosCrispHost`) doesn't change the version; the module sets `minKainos` instead.
- The receive tap (`kdigi.c`) has a single reader. The RTTY and CW terminals and the modules take turns: `KainosTerminalOpening` closes the other terminals, and `kainosModulesLoseTap` tells a module that its tap has gone.
- The first module, FT8/FT4, lives in its own repository (`kainos-module-digimodes`):
  - It decodes with WSJT-X's `jt9` once per period, from a 12 kHz WAV of that period.
  - It packs messages with WSJT-X's `ft8code`.
  - The CRC, LDPC and GFSK are a C# port of ft8_lib (MIT). Its self-test checks the tones against `ft8code`, and the decodes through `jt9` (FT8 down to -19 dB, FT4 to -15 dB, DT 0.0).

### Installer

Versions: Kainos has its own version, major.minor.patch (1.0.0 the first release; new features raise the minor number, fixes the patch number), set in one place, `Console/KainosVersion.cs`. The title bar and About window show it with the Thetis version it's based on ("Kainos 1.0.0 (Thetis 2.10.3.15)"); the installer build reads it from that file for the installer's version and its file name (`Kainos-1.0.0-x64.msi`). The installer replaces any other Kainos version, newer or older (`AllowDowngrades`: the test builds before 1.0.0 were numbered 2.10.3.15). The About window's update check compares this version with `ReleaseVersion` in `version.json` on GitHub `main`. A release: bump `KainosVersion.cs`, add release notes in `Documentation/ReleaseNotes/kainos-<version>.md`, set `version.json`'s `ReleaseVersion` and `ReleaseName` to match, build, tag `kainos-<version>` and publish the GitHub release with the MSI.

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
