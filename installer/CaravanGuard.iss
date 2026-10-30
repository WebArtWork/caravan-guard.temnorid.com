; Inno Setup script for the Windows installer: Temnorid-CaravanGuard-Setup.exe
; Build the player first (Unity: Temnorid > CaravanGuard > Build Windows Player), then compile this with Inno Setup:
;   "ISCC.exe" installer\CaravanGuard.iss
; The installer is written to Builds\Installer.

#define AppName "Temnorid Caravan Guard"
#define AppVersion "0.1.0"
#define AppExe "TemnoridCaravanGuard.exe"

[Setup]
AppId={{4B7C2E91-5D3A-4A68-9F10-8C2E6A1D7B54}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Temnorid
AppPublisherURL=https://caravan-guard.temnorid.com
DefaultDirName={autopf}\Temnorid Caravan Guard
DefaultGroupName=Temnorid Caravan Guard
DisableProgramGroupPage=yes
OutputDir=..\Builds\Installer
OutputBaseFilename=Temnorid-CaravanGuard-Setup
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequiredOverridesAllowed=dialog
UninstallDisplayIcon={app}\{#AppExe}
WizardStyle=modern

[Files]
Source: "..\Builds\Windows\TemnoridCaravanGuard\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Run]
Filename: "{app}\{#AppExe}"; Description: "Start {#AppName}"; Flags: nowait postinstall skipifsilent
