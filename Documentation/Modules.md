# Kainos modules

A module adds a feature to Kainos that not everyone needs, such as FT8/FT4. Modules are not part of the installer. Kainos downloads a module from GitHub only when the user installs it in **Setup > Modules**, and runs it only while it is turned on. This page is for anyone writing a module. The first one is [kainos-module-digimodes](https://github.com/PNWHam-K7JUS/kainos-module-digimodes).

## How it fits together

```
 Setup > Modules ──reads──> modules.json (this repository, main branch)
        │                     id, version, url (a GitHub release zip), sha256, api, minKainos
        │ Install
        ▼
 download zip ─> check SHA-256 ─> unpack to %APPDATA%\OpenHPSDR\Kainos-x64\Modules\<id>\<version>\
        │ turned on (and at every start once the console is shown)
        ▼
 Assembly.LoadFrom(<entry>) ─> new <the class implementing IKainosModule> ─> Start(IKainosHost)
```

| Piece | Where |
|---|---|
| The interface (`IKainosModule`, `IKainosHost` …) | `Project Files/Source/ModuleApi/KainosModuleApi.cs`, built to `Kainos.ModuleApi.dll`, which ships next to `Kainos.exe` |
| List, download, unpack, enable, load | `Console/KainosModules.cs` (`KainosModuleManager`) |
| What the host provides: menu items, panes, audio, radio, markers | `Console/consoleKainosModules.cs` (`KainosModuleHost`) |
| The Setup page | `Console/setupKainosModules.cs` |
| The list of published modules | `modules.json` at the root of this repository |

## Writing a module

1. Create a .NET Framework 4.8 class library that references `Kainos.ModuleApi.dll` with **Copy Local off**, because Kainos supplies it.
2. Add one public class that implements `IKainosModule`:
   - In `Start(IKainosHost host)`, add your menu item and your pane. `Start` must return quickly, so do any slow work on your own thread.
   - In `Stop()`, stop your threads.
   - `Dispose()` is called after `Stop()`.
3. Put a `module.json` at the root of the zip:

```json
{
  "id": "digimodes",                     // lower-case letters, digits, - and _ (also the folder name)
  "name": "Digital Modes (FT8 / FT4)",
  "description": "...",
  "version": "0.1.0",
  "api": 1,                              // KainosModuleApi.Version it was built against
  "minKainos": "1.0.11",
  "entry": "Kainos.DigiModes.dll",       // the DLL with the IKainosModule class
  "author": "...", "license": "GPL-3.0-or-later", "homepage": "https://github.com/..."
}
```

4. Zip `module.json`, the DLL and anything else the module needs. Subfolders are fine; the digital-modes module has a `decoder/` folder.

## What the host offers (API version 1)

- **Menu.** `AddMenuItem` adds the item to Kainos's **Modes** menu, and to the menu bar in Classic layout. Each item has `Checked`, `Enabled`, `Text` and `Remove`.
- **Pane.** `CreatePane` gives the module a pane that lives where the RTTY and CW terminals do: docked under the panadapter, or popped out into its own window. Opening one closes the others.
- **Audio.** These are the same digital audio taps the RTTY and CW terminals use (`kdigi.c`):
  - `AcquireRx(rx)` and `ReadRx` give you a copy of a receiver's demodulated audio (usually 48 kHz, mono).
  - Between `TxStart` and `TxStop`, audio written with `TxWrite` replaces the mic, so mic gain, the TX filter, drive and ALC still apply.
  - Only one user can have the receive tap at a time. If the RTTY or CW terminal opens, Kainos calls the module's `released` callback.
- **Radio.**
  - Read: `PowerOn`, `Transmitting`, `TxReceiver`, `VfoHz(rx)` and `Mode(rx)`.
  - Change: `TuneVfo`, `SetMode` (Thetis mode names such as "DIGU") and `SetMox`.
  - Key the radio only while sending through `IKainosAudio`.
- **Panadapter markers.** `SetMarkers(rx, …)` draws lines at audio offsets from the VFO, each with a label and a colour.
- **Settings.** `GetSetting` and `SetSetting` are per module and saved with Kainos's own settings.
- **Files.** `DataFolder` is a writable folder for the module's own files. `Log` writes to `DataFolder\module.log`.
- **Look.** `Theme` gives the Kainos palette, the UI scale, `DrawButton`, `Font` and `Style` (which applies the dark look to a window).
- **Station.** `Callsign` and `Grid` are the user's own, from Setup > DSP > FreeDV (RADE).
- **Crisp drawing.** `KainosCrispHost` (a control, not part of `IKainosHost`) shows a module's control at the screen's own resolution while Kainos stays DPI-unaware. Put `new KainosCrispHost(content)` in the pane where the content would go. At 125 % / 150 % the content then lives in a borderless per-monitor-aware window of its own, kept over the placeholder, and Windows doesn't stretch it. The content works in screen pixels and multiplies its design sizes by `KainosCrispHost.DpiScale(this)`. It should invalidate whole rather than by rectangle, and give any `ToolTip` `ShowAlways = true`. The file's header has the details. Windows can't host a per-monitor-aware child inside Kainos's DPI-unaware window, even with mixed DPI hosting, which is why it uses a window of its own. Added in 1.0.11, so a module that uses it sets `minKainos`.
- **Voice** (`IKainosVoice`, added in 1.0.12). A module asks for it with `host as IKainosVoice`; it's null on an older Kainos, so a module that needs it sets `minKainos` to 1.0.12. The interfaces above are unchanged, so the API version stays 1.
  - `MicStart` and `ReadMic` give the mic as Kainos has it: the transmit input after VAC (radio mic, sound card, VAC or ASIO), before the mic gain. Played back through `TxWrite`, which goes in at the same place, a recording gets the mic gain and the whole transmit chain (EQ, Kainos Audio, AetherVoice) as the live voice does.
  - Between `PreviewStart` and `PreviewStop`, audio written with `PreviewWrite` is mixed into RX1's audio, so it's heard wherever the radio is (and on VAC and TCI receive audio).
  - `SetKeyHandler` passes F1 - F12 and Esc, pressed in the main window without Ctrl, Alt or Shift and not while typing in a text box, to the module (on the UI thread); it returns true if it used the key. Ctrl+F1 - F10 stay CWX's.
- **Dock button** (`IKainosDock`, added in 1.0.12, asked for like `IKainosVoice`). `AddDockButton(label, menuText, tip, click)` puts a button at the bottom of the left-hand dock, for a module that's a tool rather than a mode, with an item in the Tools menu (on the menu bar in the Classic layout). Its `Checked` lights the button and ticks the item. There are four slots; it returns null when they're taken, so fall back to `AddMenuItem`.
- **Pane height** (`IKainosPaneHeight`, added in 1.0.12). A pane's content can implement it: `PreferredHeight` is the height (design px) it's docked at until the user drags the bar (and what a double-click puts back), `MinimumHeight` how short it may go (down to 40; 140 without it). Each docked pane keeps its own height.
- **UI thread.** `BeginInvoke` runs an action on the UI thread. Any host member that touches the window marshals to the UI thread itself.

When a module stops, Kainos cleans up after it. It removes the module's menu items and panes, releases the audio tap and markers, stops TX, the mic tap, the preview and its keys, and un-keys the radio if the module had keyed it.

## Updating and removing

A loaded DLL can't be unloaded while Kainos is running. Because of that:

- **Update.** The new version is unpacked into its own folder and used from the next start; Setup says "Restart Kainos to use x".
- **Remove.** If the module never ran this session, its folder is deleted at once; otherwise it is deleted at the next start.

Old version folders are cleaned up at start-up.

## Publishing a version

1. Build the zip. The digital-modes module's `build.ps1` builds it, runs its self-test, and writes `dist/registry-entry.json` with the zip's SHA-256 and size.
2. Create a GitHub release in the module's repository and attach the zip.
3. Add or update the module's entry in `modules.json` here, including its `url`, `sha256` and `size`, then push to `main`.

## Rules Kainos enforces

- **Download source.** Downloads come from GitHub hosts only, and must match the SHA-256 in `modules.json`.
- **Zip contents.** Kainos refuses a zip with no `module.json`, with no entry DLL, or with files that would land outside its own folder.
- **Interface version.** The `api` field must equal `KainosModuleApi.Version`. Change the version when an interface changes in a way older modules can't cope with; adding members is a change. A new class (such as `KainosCrispHost`) isn't: older modules never see it. A module that uses one sets `minKainos` to the first Kainos that has it.
- **Install from file.** The **Install from file...** button on the Setup page skips the list and the checksum. It is meant for testing, and warns that a module runs with Kainos's own access to the PC.

## Licensing

Kainos is GPLv3 or later. A module runs inside Kainos's process and implements Kainos's interface, so release modules as GPLv3-compatible open source.
