; Inno Setup script for VideoSqueeze. Build with build-installer.ps1.

#define AppName "VideoSqueeze"
#ifndef AppVersion
  #define AppVersion "1.6.0"
#endif

[Setup]
AppId={{6F3B2A1E-9C4D-4E8B-A7F2-5D1C0B9E8A43}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=TNT24
AppPublisherURL=https://github.com/TNT24/VideoSqueeze
AppUpdatesURL=https://github.com/TNT24/VideoSqueeze/releases
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
; Only offer to close VideoSqueeze itself. Explorer holds VideoSqueezeMenu.dll, which [Code] moves aside instead.
CloseApplicationsFilter=*.exe

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "build\VideoSqueeze.exe"; DestDir: "{app}"; Flags: ignoreversion
; Windows 11 right-click menu: the handler Explorer loads and the sparse package that registers it (see shell\).
Source: "build\VideoSqueezeMenu.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "build\VideoSqueezeMenu.msix"; DestDir: "{app}"; Flags: ignoreversion
; The package's self-signed certificate, trusted machine-wide by [Code] TrustCertificate before the package is added.
Source: "build\TNT24.cer"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\VideoSqueeze.exe"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\VideoSqueeze.exe"; Tasks: desktopicon

[Registry]
; Classic Explorer right-click "Squeeze with VideoSqueeze", for Windows 10 and Windows 11's "Show more options".
; The Windows 11 modern menu comes from VideoSqueezeMenu.msix ([Run]). Keep in sync with MainForm.VideoExtensions
; and shell\AppxManifest.xml.
Root: HKA; Subkey: "Software\Classes\Directory\shell\VideoSqueeze"; ValueType: string; ValueName: "MUIVerb"; ValueData: "Squeeze with VideoSqueeze"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Directory\shell\VideoSqueeze"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\VideoSqueeze.exe"
Root: HKA; Subkey: "Software\Classes\Directory\shell\VideoSqueeze"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"
Root: HKA; Subkey: "Software\Classes\Directory\shell\VideoSqueeze\command"; ValueType: string; ValueName: ""; ValueData: """{app}\VideoSqueeze.exe"" ""%1"""
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mp4\shell\VideoSqueeze"; ValueType: string; ValueName: "MUIVerb"; ValueData: "Squeeze with VideoSqueeze"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mp4\shell\VideoSqueeze"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\VideoSqueeze.exe"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mp4\shell\VideoSqueeze"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mp4\shell\VideoSqueeze\command"; ValueType: string; ValueName: ""; ValueData: """{app}\VideoSqueeze.exe"" ""%1"""
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mkv\shell\VideoSqueeze"; ValueType: string; ValueName: "MUIVerb"; ValueData: "Squeeze with VideoSqueeze"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mkv\shell\VideoSqueeze"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\VideoSqueeze.exe"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mkv\shell\VideoSqueeze"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mkv\shell\VideoSqueeze\command"; ValueType: string; ValueName: ""; ValueData: """{app}\VideoSqueeze.exe"" ""%1"""
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mov\shell\VideoSqueeze"; ValueType: string; ValueName: "MUIVerb"; ValueData: "Squeeze with VideoSqueeze"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mov\shell\VideoSqueeze"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\VideoSqueeze.exe"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mov\shell\VideoSqueeze"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mov\shell\VideoSqueeze\command"; ValueType: string; ValueName: ""; ValueData: """{app}\VideoSqueeze.exe"" ""%1"""
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.avi\shell\VideoSqueeze"; ValueType: string; ValueName: "MUIVerb"; ValueData: "Squeeze with VideoSqueeze"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.avi\shell\VideoSqueeze"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\VideoSqueeze.exe"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.avi\shell\VideoSqueeze"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.avi\shell\VideoSqueeze\command"; ValueType: string; ValueName: ""; ValueData: """{app}\VideoSqueeze.exe"" ""%1"""
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.m4v\shell\VideoSqueeze"; ValueType: string; ValueName: "MUIVerb"; ValueData: "Squeeze with VideoSqueeze"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.m4v\shell\VideoSqueeze"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\VideoSqueeze.exe"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.m4v\shell\VideoSqueeze"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.m4v\shell\VideoSqueeze\command"; ValueType: string; ValueName: ""; ValueData: """{app}\VideoSqueeze.exe"" ""%1"""
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.webm\shell\VideoSqueeze"; ValueType: string; ValueName: "MUIVerb"; ValueData: "Squeeze with VideoSqueeze"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.webm\shell\VideoSqueeze"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\VideoSqueeze.exe"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.webm\shell\VideoSqueeze"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.webm\shell\VideoSqueeze\command"; ValueType: string; ValueName: ""; ValueData: """{app}\VideoSqueeze.exe"" ""%1"""
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.wmv\shell\VideoSqueeze"; ValueType: string; ValueName: "MUIVerb"; ValueData: "Squeeze with VideoSqueeze"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.wmv\shell\VideoSqueeze"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\VideoSqueeze.exe"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.wmv\shell\VideoSqueeze"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.wmv\shell\VideoSqueeze\command"; ValueType: string; ValueName: ""; ValueData: """{app}\VideoSqueeze.exe"" ""%1"""
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.flv\shell\VideoSqueeze"; ValueType: string; ValueName: "MUIVerb"; ValueData: "Squeeze with VideoSqueeze"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.flv\shell\VideoSqueeze"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\VideoSqueeze.exe"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.flv\shell\VideoSqueeze"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.flv\shell\VideoSqueeze\command"; ValueType: string; ValueName: ""; ValueData: """{app}\VideoSqueeze.exe"" ""%1"""
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.ts\shell\VideoSqueeze"; ValueType: string; ValueName: "MUIVerb"; ValueData: "Squeeze with VideoSqueeze"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.ts\shell\VideoSqueeze"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\VideoSqueeze.exe"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.ts\shell\VideoSqueeze"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.ts\shell\VideoSqueeze\command"; ValueType: string; ValueName: ""; ValueData: """{app}\VideoSqueeze.exe"" ""%1"""
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.m2ts\shell\VideoSqueeze"; ValueType: string; ValueName: "MUIVerb"; ValueData: "Squeeze with VideoSqueeze"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.m2ts\shell\VideoSqueeze"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\VideoSqueeze.exe"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.m2ts\shell\VideoSqueeze"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.m2ts\shell\VideoSqueeze\command"; ValueType: string; ValueName: ""; ValueData: """{app}\VideoSqueeze.exe"" ""%1"""
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mts\shell\VideoSqueeze"; ValueType: string; ValueName: "MUIVerb"; ValueData: "Squeeze with VideoSqueeze"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mts\shell\VideoSqueeze"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\VideoSqueeze.exe"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mts\shell\VideoSqueeze"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mts\shell\VideoSqueeze\command"; ValueType: string; ValueName: ""; ValueData: """{app}\VideoSqueeze.exe"" ""%1"""
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mpg\shell\VideoSqueeze"; ValueType: string; ValueName: "MUIVerb"; ValueData: "Squeeze with VideoSqueeze"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mpg\shell\VideoSqueeze"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\VideoSqueeze.exe"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mpg\shell\VideoSqueeze"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mpg\shell\VideoSqueeze\command"; ValueType: string; ValueName: ""; ValueData: """{app}\VideoSqueeze.exe"" ""%1"""
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mpeg\shell\VideoSqueeze"; ValueType: string; ValueName: "MUIVerb"; ValueData: "Squeeze with VideoSqueeze"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mpeg\shell\VideoSqueeze"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\VideoSqueeze.exe"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mpeg\shell\VideoSqueeze"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mpeg\shell\VideoSqueeze\command"; ValueType: string; ValueName: ""; ValueData: """{app}\VideoSqueeze.exe"" ""%1"""
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mxf\shell\VideoSqueeze"; ValueType: string; ValueName: "MUIVerb"; ValueData: "Squeeze with VideoSqueeze"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mxf\shell\VideoSqueeze"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\VideoSqueeze.exe"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mxf\shell\VideoSqueeze"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mxf\shell\VideoSqueeze\command"; ValueType: string; ValueName: ""; ValueData: """{app}\VideoSqueeze.exe"" ""%1"""
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.3gp\shell\VideoSqueeze"; ValueType: string; ValueName: "MUIVerb"; ValueData: "Squeeze with VideoSqueeze"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.3gp\shell\VideoSqueeze"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\VideoSqueeze.exe"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.3gp\shell\VideoSqueeze"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.3gp\shell\VideoSqueeze\command"; ValueType: string; ValueName: ""; ValueData: """{app}\VideoSqueeze.exe"" ""%1"""

[Run]
; Windows 11 right-click menu: register the sparse package for the user who ran Setup. Its certificate has to be
; trusted machine-wide first (TrustCertificate: one UAC prompt, the first time only).
Filename: "powershell.exe"; Parameters: "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command ""Add-AppxPackage -Path '{app}\VideoSqueezeMenu.msix' -ExternalLocation '{app}' -ForceUpdateFromAnyVersion"""; MinVersion: 10.0.22000; Flags: runhidden waituntilterminated runasoriginaluser; BeforeInstall: TrustCertificate; StatusMsg: "Adding VideoSqueeze to Explorer's right-click menu..."
Filename: "{app}\VideoSqueeze.exe"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
; In-app updates run Setup silently after closing the app; start it again afterwards.
Filename: "{app}\VideoSqueeze.exe"; Flags: nowait runasoriginaluser; Check: WizardSilent

[UninstallRun]
Filename: "powershell.exe"; Parameters: "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command ""Get-AppxPackage -Name TNT24.VideoSqueeze | Remove-AppxPackage"""; MinVersion: 10.0.22000; Flags: runhidden waituntilterminated; RunOnceId: "RemoveExplorerMenu"

[Code]
// Explorer keeps the menu DLL loaded once the menu has been shown, so it can't be overwritten or deleted
// without restarting Explorer, but Windows does let it be renamed or moved.
const
  MenuDll = 'VideoSqueezeMenu.dll';

procedure DeleteOldMenuDlls(const Dir: String);
var
  FindRec: TFindRec;
begin
  if FindFirst(Dir + '\' + MenuDll + '.*.old', FindRec) then
  try
    repeat
      DeleteFile(Dir + '\' + FindRec.Name);
    until not FindNext(FindRec);
  finally
    FindClose(FindRec);
  end;
end;

// Adds TNT24.cer to the machine's Trusted People unless it's there already. If the UAC prompt is declined, adding
// the package fails quietly and the entry stays under "Show more options".
procedure TrustCertificate;
var
  Cer: String;
  ResultCode: Integer;
begin
  Cer := ExpandConstant('{app}\TNT24.cer');
  if Exec('powershell.exe', '-NoProfile -NonInteractive -Command "if (Test-Path (''Cert:\LocalMachine\TrustedPeople\'' + (Get-PfxCertificate ''' + Cer + ''').Thumbprint)) { exit 0 } else { exit 1 }"',
          '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0) then
    Exit;
  if IsAdmin then
    Exec('certutil.exe', '-addstore TrustedPeople "' + Cer + '"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode)
  else
    ShellExec('runas', 'certutil.exe', '-addstore TrustedPeople "' + Cer + '"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

function TimeStamp: String;
begin
  Result := GetDateTimeString('yyyymmddhhnnsszzz', #0, #0);
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Dll: String;
begin
  if CurStep = ssInstall then
  begin
    DeleteOldMenuDlls(ExpandConstant('{app}'));
    Dll := ExpandConstant('{app}\' + MenuDll);
    if FileExists(Dll) and not DeleteFile(Dll) then
      RenameFile(Dll, Dll + '.' + TimeStamp + '.old');
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Dll: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    // Whatever Explorer still holds goes to %TEMP% so the install folder can be removed.
    DeleteOldMenuDlls(ExpandConstant('{app}'));
    Dll := ExpandConstant('{app}\' + MenuDll);
    if FileExists(Dll) and not DeleteFile(Dll) then
      RenameFile(Dll, ExpandConstant('{%TEMP}\') + MenuDll + '.' + TimeStamp + '.old');
    RemoveDir(ExpandConstant('{app}'));
  end;
end;
