# Building from Source

1. Clone the repository with its submodule (Steinberg's VST3 SDK, for VST3 plugins):
   `git clone --recurse-submodules https://github.com/PNWHam-K7JUS/Kainos.git`
   (Already cloned? `git submodule update --init --recursive`.)
2. Open `Project Files/Source/Thetis_VS2026.sln` in **Visual Studio 2026**, with the **Desktop development with C++** workload (it includes CMake).
3. Choose **Release | x64** and build. The program is built to `Project Files/bin/x64/Release/Kainos.exe`.

The VST3 host, `KainosVst.dll` (`Project Files/Source/KainosVst`), is built with CMake as part of the console project's build (in `Project Files/bin/kvst`) and copied next to `Kainos.exe`. Without the submodule the build skips it, and Kainos runs without VST3 plugins.

The installer is the `Thetis-Installer` project (WiX Toolset 3.14). It takes its version from `Console/KainosVersion.cs`.

How Kainos differs from Thetis, and why, is described in [KAINOS.md](https://github.com/PNWHam-K7JUS/Kainos/blob/main/KAINOS.md).

## Contributing

Bug reports and ideas are welcome on [GitHub Issues](https://github.com/PNWHam-K7JUS/Kainos/issues), or with **Report a bug** in Kainos.
