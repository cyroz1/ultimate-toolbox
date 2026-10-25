; UltimateWinUI installer (Inno Setup 6)
; Build: iscc /DAppVersion=1.0.3 /DPublishDir=<dotnet publish output> UltimateWinUI.iss
; CI passes both defines; the defaults below work for a local publish to ..\artifacts\publish\win-x64

#ifndef AppVersion
  #define AppVersion "1.0.3"
#endif
#ifndef PublishDir
  #define PublishDir "..\artifacts\publish\win-x64"
#endif
#ifndef OutName
  #define OutName "UltimateWinUI-Setup-" + AppVersion
#endif
#ifndef ArchAllowed
  #define ArchAllowed "x64compatible"
#endif
#ifndef In64Bit
  #define In64Bit "x64compatible"
#endif

[Setup]
AppId={{8F3B0C6A-9A41-4E2E-B5E7-3C2D1A0F9E77}
AppName=UltimateWinUI
AppVersion={#AppVersion}
AppPublisher=UltimateWinUI contributors
DefaultDirName={autopf}\UltimateWinUI
DefaultGroupName=UltimateWinUI
DisableProgramGroupPage=yes
OutputBaseFilename={#OutName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed={#ArchAllowed}
ArchitecturesInstallIn64BitMode={#In64Bit}
UninstallDisplayIcon={app}\UltimateWinUI.exe

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional icons:"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\UltimateWinUI"; Filename: "{app}\UltimateWinUI.exe"
Name: "{autodesktop}\UltimateWinUI"; Filename: "{app}\UltimateWinUI.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\UltimateWinUI.exe"; Description: "Launch UltimateWinUI"; Flags: nowait postinstall skipifsilent
