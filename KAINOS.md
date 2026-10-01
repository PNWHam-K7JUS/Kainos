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
2. Expect conflicts mainly in user-visible strings, `.resx` files (form icons and the `, Kainos, Version=` assembly references) and `Thetis.csproj`. Keep the Kainos side for names, paths and icons, and the Thetis side for everything else.
3. Search the merged code for new user-facing "Thetis" text, new `Thetis-x64` registry or folder paths, and new `BinaryFormatter` deserialization (see above).
4. Search for `Kainos` and `// Kainos:` comments to find every Kainos-specific change.
5. Build, then test RX, TX and PureSignal on the HL2 before merging back.

## Licensing

Thetis and WDSP are licensed under the GNU GPL "version 2 or later", and there are no v2-only files in `Project Files/Source`. AetherSDR is GPLv3. Once AetherSDR code is added (roadmap Phase 4), Kainos as a whole will be distributed under GPLv3, and `ReadMe.md` must be updated to say so. Keep every existing copyright notice and the MW0LGE dual-licensing statement (`LICENSE-DUAL-LICENSING`).
