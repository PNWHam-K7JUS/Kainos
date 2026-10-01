# Kainos

Kainos is a Software Defined Radio (SDR) application for Windows, maintained by Justin Cron, K7JUS.

Kainos is a fork of [Thetis](https://github.com/ramdor/Thetis), and builds on the Hermes Lite 2 edition of Thetis by Reid Campbell, MI0BOT ([OpenHPSDR-Thetis](https://github.com/mi0bot/OpenHPSDR-Thetis)). Nearly everything Kainos can do comes from the many years of work by the Thetis and OpenHPSDR community. See [Credits](#credits) below.

- Releases: https://github.com/PNWHam-K7JUS/Kainos/releases
- Current version: 2.10.3.15 (based on Thetis 2.10.3.15)

## Kainos and Thetis side by side

Kainos is installed and run separately from Thetis, so both can be on the same PC:

- The program is `Kainos.exe`, installed to `Program Files\OpenHPSDR\Kainos-HL2`
- Settings are kept in `%APPDATA%\OpenHPSDR\Kainos-x64` and the registry key `HKCU\Software\OpenHPSDR\Kainos-x64`
- Recordings are saved to `Music\Kainos`
- The Kainos installer will not upgrade or remove an existing Thetis install

The first time Kainos starts, if it finds an existing Thetis install, it offers once to copy your Thetis settings (databases, meters, skins and cmASIO settings) into Kainos. Your Thetis settings are not changed. A Thetis database can also be imported later from the Database Manager.

Kainos still identifies itself as Thetis to TCI and TCP CAT clients, so logging and contest software that supports Thetis keeps working. Thetis meter skins work in Kainos too.

## Building

Open `Project Files/Source/Thetis_VS2026.sln` in Visual Studio 2026, select the `Release | x64` configuration and build. The program is built to `Project Files/bin/x64/Release/Kainos.exe`.

## Credits

Kainos would not exist without the people who built Thetis and the software it grew from. With thanks to:

- Doug Wigley, W5WC: Thetis, ChannelMaster, UI and much more
- Richard Samphire, MW0LGE: Thetis UI, meters and much more
- Reid Campbell, MI0BOT: Thetis for the Hermes Lite 2
- Warren Pratt, NR0V: WDSP, the DSP engine at the heart of Thetis, and many other contributions
- Laurence Barker, G8NJJ: G2, Andromeda and protocols
- Phil, VK6PH; Bill Tracey, KD5TFD; Rick, N1GP; Bryan, W4WMT; Chris, W2PA; Joe, K5SO; and the many other contributors listed in the About window
- FlexRadio Systems: PowerSDR, from which Thetis was originally derived
- The OpenHPSDR and Apache Labs communities, the skin authors, and all the testers

The Thetis manuals and guides that ship with Kainos are the original Thetis documents and remain the work of their authors.

## License

Kainos is free software, licensed under the GNU General Public License version 2 or later. See [LICENSE](LICENSE). Like Thetis, it includes code under the dual-licensing statement in [LICENSE-DUAL-LICENSING](LICENSE-DUAL-LICENSING), which applies only to code written by Richard Samphire, MW0LGE.

## Upstream release history

Changes from the Thetis Hermes Lite 2 edition that Kainos is based on:

### 2.10.3.15 (2026-09-04)

- Upgraded to release 2.10.3.15 of official Thetis
- Corrected timeout of auto tune routine
- Updated alternate RX selection form
