"""Architecture adaptation for ARM64 Windows.

Nothing is disabled on ARM64. Instead, where an x86/x64 step in a script
literally cannot work on ARM64, the app runs a replacement body
("arm64Body" in the catalog) that does the ARM64-working equivalent, or
explains in the log why there is no equivalent and what to use instead.

ARM64_BODIES: full replacement bodies per script, used for every option.
For driver tooling the ARM64 reality is that GPU drivers ship through
Windows Update / the device maker (Snapdragon/Adreno), so the equivalent
action is opening Windows Update or Device Manager instead of downloading
discrete NVIDIA/AMD/Intel packages that do not exist for ARM64.

ARM64_BODY_SWAPS: small in-place replacements where only a payload URL
differs and the rest of the script is unchanged:
  - Visual C++ 2015-2022 redistributable -> Microsoft's official ARM64
    build (the 2005-2013 packs have no ARM64 builds and install under
    emulation, so they are left alone).
  - 7-Zip -> the official ARM64 installer from 7-zip.org (26.04). The
    scripts only download it to 7zip.exe and run it with /S, which the
    ARM64 installer accepts the same way.

Everything else in the library is registry, policy, bcdedit or user-mode
tooling that behaves the same on ARM64 (x86/x64 user-mode downloads run
under emulation).
"""

_WU = (
    'Write-Host "GPU drivers on ARM64 Windows arrive through Windows Update and the device maker. Opening Windows Update."\n'
    'Start-Process "ms-settings:windowsupdate"\n'
)

ARM64_BODIES = {
    "5 Graphics/1 Driver Clean.ps1": (
        'Write-Host "ARM64 replacement: DDU does not support ARM64 or Adreno GPUs, so nothing was removed."\n'
        'Write-Host "On ARM64 the display driver is managed by Windows and the device maker. To remove or roll it back, uninstall the display device in Device Manager. Opening Device Manager."\n'
        'Start-Process "devmgmt.msc"\n'
    ),
    "5 Graphics/2 Driver Updated Install.ps1": (
        'Write-Host "ARM64 replacement: there are no separate NVIDIA, AMD or Intel driver packages for ARM64 Windows, so no driver was downloaded."\n'
        + _WU
    ),
    "5 Graphics/3 Driver Updated Install & Settings.ps1": (
        'Write-Host "ARM64 replacement: there are no separate NVIDIA, AMD or Intel driver packages or vendor settings apps for ARM64 Windows, so nothing was installed."\n'
        + _WU
    ),
    "5 Graphics/4 Driver Debloat Install & Settings.ps1": (
        'Write-Host "ARM64 replacement: there are no discrete GPU driver packages to debloat on ARM64 Windows, so nothing was installed."\n'
        + _WU
    ),
    "5 Graphics/5 Nvidia Settings.ps1": (
        'Write-Host "ARM64 replacement: NVIDIA driver profiles do not exist on ARM64 Windows, so no profile was imported."\n'
        'Write-Host "The graphics settings that do apply on ARM64 live in Advanced graphics settings. Opening them."\n'
        'Start-Process "ms-settings:display-advancedgraphics"\n'
    ),
    "5 Graphics/6 Amd Settings.ps1": (
        'Write-Host "ARM64 replacement: AMD Adrenalin profiles for discrete GPUs do not exist on ARM64 Windows, so no profile was imported."\n'
        'Write-Host "The graphics settings that do apply on ARM64 live in Advanced graphics settings. Opening them."\n'
        'Start-Process "ms-settings:display-advancedgraphics"\n'
    ),
    "5 Graphics/7 Intel Settings.ps1": (
        'Write-Host "ARM64 replacement: Intel graphics driver settings do not exist on ARM64 Windows, so nothing was applied."\n'
        'Write-Host "The graphics settings that do apply on ARM64 live in Advanced graphics settings. Opening them."\n'
        'Start-Process "ms-settings:display-advancedgraphics"\n'
    ),
    "5 Graphics/8 Hdcp.ps1": (
        'Write-Host "ARM64 replacement: this tweak writes registry keys for discrete GPU drivers, which do not exist on ARM64 Windows. Nothing was changed."\n'
    ),
    "5 Graphics/9 P0 State.ps1": (
        'Write-Host "ARM64 replacement: the P0 state tweak writes registry keys for discrete GPU drivers, which do not exist on ARM64 Windows. Nothing was changed."\n'
    ),
    "5 Graphics/10 Msi Mode.ps1": (
        'Write-Host "ARM64 replacement: MSI mode in this library targets discrete GPU devices, which do not exist on ARM64 Windows. Nothing was changed."\n'
    ),
    "4 Installers/2 MSI Afterburner.ps1": (
        'Write-Host "ARM64 replacement: MSI Afterburner depends on its RTCore64 kernel driver, which cannot load on ARM64 Windows, so it was not installed."\n'
        'Write-Host "For FPS and performance monitoring on ARM64, use the Xbox Game Bar overlay (Win+G) or Task Manager."\n'
    ),
    "7 Hardware/4 Controller Overclock.ps1": (
        'Write-Host "ARM64 replacement: the controller overclock installs the hidusbf kernel driver, which has no ARM64 build and cannot load on ARM64 Windows. Nothing was changed."\n'
    ),
    "8 Advanced/7 ReBar Force.ps1": (
        'Write-Host "ARM64 replacement: Resizable BAR forcing works through NVIDIA driver profiles, and there is no NVIDIA driver on ARM64 Windows. Nothing was changed."\n'
    ),
    "8 Advanced/14 Ulps.ps1": (
        'Write-Host "ARM64 replacement: ULPS is a setting of the AMD discrete GPU driver, which does not exist on ARM64 Windows. Nothing was changed."\n'
    ),
    "8 Advanced/15 Driver Whql Secure Boot Bypass.ps1": (
        'Write-Host "ARM64 replacement: this bypass targets x64 driver signing. ARM64 Windows only loads native ARM64 signed drivers, so the bypass was not applied."\n'
    ),
}

_7ZIP_X64 = "https://github.com/FR33THYFR33THY/Ultimate/releases/download/Files/7zip.exe"
_7ZIP_ARM64 = "https://www.7-zip.org/a/7z2604-arm64.exe"

ARM64_BODY_SWAPS = {
    "5 Graphics/12 C++.ps1": [
        (
            'IWR "https://github.com/FR33THYFR33THY/Ultimate/releases/download/Files/vcredist2015_2017_2019_2022_x64.exe" -OutFile "$env:SystemRoot\\Temp\\vcredist2015_2017_2019_2022_x64.exe"',
            'IWR "https://aka.ms/vs/17/release/vc_redist.arm64.exe" -OutFile "$env:SystemRoot\\Temp\\vcredist2015_2017_2019_2022_x64.exe"',
        ),
    ],
    "4 Installers/1 Installers.ps1": [(_7ZIP_X64, _7ZIP_ARM64)],
    "5 Graphics/3 Driver Updated Install & Settings.ps1": [(_7ZIP_X64, _7ZIP_ARM64)],
    "5 Graphics/11 DirectX.ps1": [(_7ZIP_X64, _7ZIP_ARM64)],
}
