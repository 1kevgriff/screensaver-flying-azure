; Inno Setup script for the Flying Azure screensaver.
; Produces a single FlyingAzure-Setup-x64.exe that installs the .scr into System32
; (so it appears in Windows' Screen Saver Settings) and registers an uninstaller.
;
; Build:  ISCC.exe /DAppVersion=1.2.3 installer\FlyingAzure.iss
; The .scr is expected at build\FlyingAzure-Screensaver\FlyingAzure.scr (staged by package.ps1).

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

#define MyAppName "Flying Azure Screensaver"
#define MyAppPublisher "Kevin Griffin"

[Setup]
AppId={{B7A4F2E1-9C3D-4A6B-8E2F-1D5C7A9B3E04}
AppName={#MyAppName}
AppVersion={#AppVersion}
AppPublisher={#MyAppPublisher}
; The screensaver lives in System32, so there is no app folder to choose.
DefaultDirName={autopf}\FlyingAzure
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..
OutputBaseFilename=FlyingAzure-Setup-x64
UninstallDisplayIcon={sys}\FlyingAzure.scr
UninstallDisplayName={#MyAppName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

[Files]
; Install into System32 so the saver shows up in Screen Saver Settings.
Source: "..\build\FlyingAzure-Screensaver\FlyingAzure.scr"; DestDir: "{sys}"; Flags: ignoreversion

[Tasks]
Name: "activate"; Description: "Set Flying Azure as my current screen saver (10-minute idle)"

[Registry]
Root: HKCU; Subkey: "Control Panel\Desktop"; ValueType: string; ValueName: "SCRNSAVE.EXE"; ValueData: "{sys}\FlyingAzure.scr"; Flags: uninsdeletevalue; Tasks: activate
Root: HKCU; Subkey: "Control Panel\Desktop"; ValueType: string; ValueName: "ScreenSaveActive"; ValueData: "1"; Tasks: activate
Root: HKCU; Subkey: "Control Panel\Desktop"; ValueType: string; ValueName: "ScreenSaveTimeOut"; ValueData: "600"; Tasks: activate

[Run]
Filename: "{sys}\FlyingAzure.scr"; Parameters: "/c"; Description: "Configure Flying Azure now"; Flags: postinstall skipifsilent nowait unchecked

[UninstallDelete]
Type: files; Name: "{sys}\FlyingAzure.scr"
