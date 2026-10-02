@echo off
REM Kainos: builds the static libraries that ChannelMaster links for RADE (FreeDV) support.
REM Run once after cloning, and again only if one of these libraries changes. Needs Visual Studio 2026
REM (toolset v145) with "Desktop development with C++", and CMake (VS's bundled copy is fine).
REM
REM   opus_dnn   -> opus_dnn\build\x64\Release\opus.lib      (xiph/opus with DEEP_PLC, DRED and OSCE)
REM   radae_c    -> radae_c\build\x64\Release\rade.lib
REM   rnnoise    -> rnnoise\build\x64\Release\rnnoise.lib
REM   libebur128 -> libebur128\build\x64\Release\ebur128.lib
REM   WebRTC_AGC -> WebRTC_AGC\build\x64\Release\WebRTC_AGC.lib
REM
REM Usage: build_rade_libs.bat [Release|Debug]   (default Release)

setlocal
set CFG=%1
if "%CFG%"=="" set CFG=Release
set ROOT=%~dp0

for /f "usebackq tokens=*" %%i in (`"%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" -latest -prerelease -products * -requires Microsoft.Component.MSBuild -property installationPath`) do set VSDIR=%%i
if not defined VSDIR (echo Visual Studio not found & exit /b 1)
set MSBUILD="%VSDIR%\MSBuild\Current\Bin\MSBuild.exe"
set CMAKE=cmake
where cmake >nul 2>nul || set CMAKE="%VSDIR%\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe"

echo === opus_dnn (%CFG%)
%CMAKE% -S "%ROOT%opus_dnn" -B "%ROOT%opus_dnn\cmake-build" -G "Visual Studio 18 2026" -A x64 -T v145 ^
  -DOPUS_DEEP_PLC=ON -DOPUS_DRED=ON -DOPUS_OSCE=ON -DOPUS_BUILD_TESTING=OFF -DOPUS_BUILD_PROGRAMS=OFF || exit /b 1
%CMAKE% --build "%ROOT%opus_dnn\cmake-build" --config %CFG% -- -m -v:minimal -nologo || exit /b 1
if not exist "%ROOT%opus_dnn\build\x64\%CFG%" mkdir "%ROOT%opus_dnn\build\x64\%CFG%"
copy /y "%ROOT%opus_dnn\cmake-build\%CFG%\opus.lib" "%ROOT%opus_dnn\build\x64\%CFG%\" >nul || exit /b 1

REM rnnoise's model (about 75 MB) is the same one Thetis's NR3 uses, already in NR_Algorithms_x64
for %%f in (rnnoise_data.c rnnoise_data.h) do (
  if not exist "%ROOT%rnnoise\src\%%f" copy /y "%ROOT%NR_Algorithms_x64\src\rnnoise\src\%%f" "%ROOT%rnnoise\src\" >nul || exit /b 1
)

for %%p in ("radae_c\msvc\radae_c.vcxproj" "rnnoise\build\rnnoise.vcxproj" "libebur128\build\libebur128.vcxproj" "WebRTC_AGC\build\WebRTC_AGC.vcxproj") do (
  echo === %%~p
  %MSBUILD% "%ROOT%%%~p" -p:Configuration=%CFG% -p:Platform=x64 -v:minimal -nologo -m || exit /b 1
)

echo.
echo RADE libraries built (%CFG%).
endlocal
