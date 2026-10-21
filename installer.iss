; Inno Setup script for VideoSqueeze. Build with build-installer.ps1.

#define AppName "VideoSqueeze"
#ifndef AppVersion
  #define AppVersion "1.2.0"
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

[Registry]
; Explorer right-click "Squeeze with VideoSqueeze" (Windows 11: under "Show more options"). Keep in sync with MainForm.VideoExtensions.
Root: HKA; Subkey: "Software\Classes\Directory\shell\VideoSqueeze"; ValueType: string; ValueName: "MUIVerb"; ValueData: "Squeeze with VideoSqueeze"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Directory\shell\VideoSqueeze"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\VideoSqueeze.exe"
Root: HKA; Subkey: "Software\Classes\Directory\shell\VideoSqueeze"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"
Root: HKA; Subkey: "Software\Classes\Directory\shell\VideoSqueeze\command"; ValueType: string; ValueName: ""; ValueData: """{app}\VideoSqueeze.exe"" ""%1""""
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mp4\shell\VideoSqueeze"; ValueType: string; ValueName: "MUIVerb"; ValueData: "Squeeze with VideoSqueeze"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mp4\shell\VideoSqueeze"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\VideoSqueeze.exe"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mp4\shell\VideoSqueeze"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mp4\shell\VideoSqueeze\command"; ValueType: string; ValueName: ""; ValueData: """{app}\VideoSqueeze.exe"" ""%1""""
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mkv\shell\VideoSqueeze"; ValueType: string; ValueName: "MUIVerb"; ValueData: "Squeeze with VideoSqueeze"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mkv\shell\VideoSqueeze"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\VideoSqueeze.exe"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mkv\shell\VideoSqueeze"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mkv\shell\VideoSqueeze\command"; ValueType: string; ValueName: ""; ValueData: """{app}\VideoSqueeze.exe"" ""%1""""
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mov\shell\VideoSqueeze"; ValueType: string; ValueName: "MUIVerb"; ValueData: "Squeeze with VideoSqueeze"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mov\shell\VideoSqueeze"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\VideoSqueeze.exe"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mov\shell\VideoSqueeze"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mov\shell\VideoSqueeze\command"; ValueType: string; ValueName: ""; ValueData: """{app}\VideoSqueeze.exe"" ""%1""""
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.avi\shell\VideoSqueeze"; ValueType: string; ValueName: "MUIVerb"; ValueData: "Squeeze with VideoSqueeze"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.avi\shell\VideoSqueeze"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\VideoSqueeze.exe"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.avi\shell\VideoSqueeze"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.avi\shell\VideoSqueeze\command"; ValueType: string; ValueName: ""; ValueData: """{app}\VideoSqueeze.exe"" ""%1""""
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.m4v\shell\VideoSqueeze"; ValueType: string; ValueName: "MUIVerb"; ValueData: "Squeeze with VideoSqueeze"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.m4v\shell\VideoSqueeze"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\VideoSqueeze.exe"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.m4v\shell\VideoSqueeze"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.m4v\shell\VideoSqueeze\command"; ValueType: string; ValueName: ""; ValueData: """{app}\VideoSqueeze.exe"" ""%1""""
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.webm\shell\VideoSqueeze"; ValueType: string; ValueName: "MUIVerb"; ValueData: "Squeeze with VideoSqueeze"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.webm\shell\VideoSqueeze"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\VideoSqueeze.exe"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.webm\shell\VideoSqueeze"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.webm\shell\VideoSqueeze\command"; ValueType: string; ValueName: ""; ValueData: """{app}\VideoSqueeze.exe"" ""%1""""
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.wmv\shell\VideoSqueeze"; ValueType: string; ValueName: "MUIVerb"; ValueData: "Squeeze with VideoSqueeze"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.wmv\shell\VideoSqueeze"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\VideoSqueeze.exe"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.wmv\shell\VideoSqueeze"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.wmv\shell\VideoSqueeze\command"; ValueType: string; ValueName: ""; ValueData: """{app}\VideoSqueeze.exe"" ""%1""""
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.flv\shell\VideoSqueeze"; ValueType: string; ValueName: "MUIVerb"; ValueData: "Squeeze with VideoSqueeze"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.flv\shell\VideoSqueeze"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\VideoSqueeze.exe"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.flv\shell\VideoSqueeze"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.flv\shell\VideoSqueeze\command"; ValueType: string; ValueName: ""; ValueData: """{app}\VideoSqueeze.exe"" ""%1""""
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.ts\shell\VideoSqueeze"; ValueType: string; ValueName: "MUIVerb"; ValueData: "Squeeze with VideoSqueeze"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.ts\shell\VideoSqueeze"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\VideoSqueeze.exe"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.ts\shell\VideoSqueeze"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.ts\shell\VideoSqueeze\command"; ValueType: string; ValueName: ""; ValueData: """{app}\VideoSqueeze.exe"" ""%1""""
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.m2ts\shell\VideoSqueeze"; ValueType: string; ValueName: "MUIVerb"; ValueData: "Squeeze with VideoSqueeze"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.m2ts\shell\VideoSqueeze"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\VideoSqueeze.exe"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.m2ts\shell\VideoSqueeze"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.m2ts\shell\VideoSqueeze\command"; ValueType: string; ValueName: ""; ValueData: """{app}\VideoSqueeze.exe"" ""%1""""
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mts\shell\VideoSqueeze"; ValueType: string; ValueName: "MUIVerb"; ValueData: "Squeeze with VideoSqueeze"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mts\shell\VideoSqueeze"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\VideoSqueeze.exe"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mts\shell\VideoSqueeze"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mts\shell\VideoSqueeze\command"; ValueType: string; ValueName: ""; ValueData: """{app}\VideoSqueeze.exe"" ""%1""""
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mpg\shell\VideoSqueeze"; ValueType: string; ValueName: "MUIVerb"; ValueData: "Squeeze with VideoSqueeze"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mpg\shell\VideoSqueeze"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\VideoSqueeze.exe"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mpg\shell\VideoSqueeze"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mpg\shell\VideoSqueeze\command"; ValueType: string; ValueName: ""; ValueData: """{app}\VideoSqueeze.exe"" ""%1""""
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mpeg\shell\VideoSqueeze"; ValueType: string; ValueName: "MUIVerb"; ValueData: "Squeeze with VideoSqueeze"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mpeg\shell\VideoSqueeze"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\VideoSqueeze.exe"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mpeg\shell\VideoSqueeze"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mpeg\shell\VideoSqueeze\command"; ValueType: string; ValueName: ""; ValueData: """{app}\VideoSqueeze.exe"" ""%1""""
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mxf\shell\VideoSqueeze"; ValueType: string; ValueName: "MUIVerb"; ValueData: "Squeeze with VideoSqueeze"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mxf\shell\VideoSqueeze"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\VideoSqueeze.exe"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mxf\shell\VideoSqueeze"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mxf\shell\VideoSqueeze\command"; ValueType: string; ValueName: ""; ValueData: """{app}\VideoSqueeze.exe"" ""%1""""
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.3gp\shell\VideoSqueeze"; ValueType: string; ValueName: "MUIVerb"; ValueData: "Squeeze with VideoSqueeze"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.3gp\shell\VideoSqueeze"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\VideoSqueeze.exe"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.3gp\shell\VideoSqueeze"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.3gp\shell\VideoSqueeze\command"; ValueType: string; ValueName: ""; ValueData: """{app}\VideoSqueeze.exe"" ""%1""""

[Run]
Filename: "{app}\VideoSqueeze.exe"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
