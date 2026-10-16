; Inno Setup script for VideoSqueeze. Build with build-installer.ps1.

#define AppName "VideoSqueeze"
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

[Setup]
AppId={{6F3B2A1E-9C4D-4E8B-A7F2-5D1C0B9E8A43}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Mark
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
; Per-user install by default (no admin prompt); the user can still choose "all users".
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=installer
OutputBaseFilename=VideoSqueeze-Setup-{#AppVersion}
SetupIconFile=app.ico
UninstallDisplayIcon={app}\VideoSqueeze.exe
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "build\VideoSqueeze.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\VideoSqueeze.exe"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\VideoSqueeze.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\VideoSqueeze.exe"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
