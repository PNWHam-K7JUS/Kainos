# Installing

## What you need

- Windows 10 or 11, 64-bit.
- A Hermes Lite 2 on your network.

## Install

1. Download **Kainos-<version>-x64.msi** from the [Releases page](https://github.com/PNWHam-K7JUS/Kainos/releases).
2. Run it. A newer version installs over an older one and keeps your settings.

## Kainos and Thetis side by side

Kainos keeps everything separate from Thetis, so you can have both on the same PC:

| | Thetis | Kainos |
|---|---|---|
| Program | `Thetis.exe` | `Kainos.exe` (in `Program Files\OpenHPSDR\Kainos-HL2`) |
| Settings | `%APPDATA%\OpenHPSDR\Thetis-x64` | `%APPDATA%\OpenHPSDR\Kainos-x64` |

The Kainos installer doesn't change or remove Thetis. Run only one of them at a time with the radio.

## Updating

Kainos checks for a new version once a day and shows what's new, with **Download**, **Later** and **Skip this version**. You can also check any time from **Setup > Check for updates**. See [[Backups and Updates]].

## Uninstalling

Use **Settings > Apps** in Windows. Your settings folder (`%APPDATA%\OpenHPSDR\Kainos-x64`) is left in place; delete it too if you want a completely fresh start next time.
