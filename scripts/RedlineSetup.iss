; Redline Gaming Optimizer - Windows Installer (Inno Setup 6)
; Build: ISCC.exe scripts\RedlineSetup.iss

#define MyAppName "Redline Gaming Optimizer"
#define MyAppVersion "9.24"
#define MyAppPublisher "Tobias Immisch"
#define MyAppExeName "Redline Gaming Optimizer.exe"
#define MyAppSource "..\publish\win-x64\GamingBooster_Pro.exe"
[Setup]
AppId={{A7B3C9E1-4F2D-4A8B-9C0E-REDLINE-GAMING-01}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
DisableDirPage=auto
UsePreviousAppDir=yes
CloseApplications=force
CloseApplicationsFilter=GamingBooster_Pro.exe,Redline Gaming Optimizer.exe
RestartApplications=no
OutputDir=..\dist
OutputBaseFilename=Redline_Gaming_Optimizer_Setup_v{#MyAppVersion}
SetupIconFile=
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\{#MyAppExeName}
VersionInfoVersion={#MyAppVersion}.0.0
VersionInfoProductVersion={#MyAppVersion}
AppMutex=RedlineGamingOptimizerMutex

[Languages]
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[InstallDelete]
Type: files; Name: "{app}\GamingBooster_Pro.exe"
Type: files; Name: "{app}\GamingBooster_Pro.dll"
Type: files; Name: "{app}\GamingBooster_Pro.runtimeconfig.json"
Type: files; Name: "{app}\GamingBooster_Pro.deps.json"

[Files]
Source: "{#MyAppSource}"; DestDir: "{app}"; DestName: "{#MyAppExeName}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[Code]
function IsRedlineAlreadyInstalled: Boolean;
var
  loc: String;
begin
  Result := RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\A7B3C9E1-4F2D-4A8B-9C0E-REDLINE-GAMING-01_is1',
    'InstallLocation', loc) and (loc <> '');
  if not Result then
    Result := RegQueryStringValue(HKLM, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\A7B3C9E1-4F2D-4A8B-9C0E-REDLINE-GAMING-01_is1',
      'InstallLocation', loc) and (loc <> '');
end;

procedure InitializeWizard;
begin
  if IsRedlineAlreadyInstalled then
  begin
    WizardForm.WelcomeLabel2.Caption :=
      'Bestehende Redline-Installation erkannt.' + #13#10 +
      'Die alte Version wird ersetzt (gleicher Ordner, alte EXE-Dateien werden entfernt).';
  end
  else
  begin
    WizardForm.WelcomeLabel2.Caption :=
      'Redline wird auf diesem PC installiert.';
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  oldDesk, oldPub: String;
begin
  if CurStep = ssPostInstall then
  begin
    oldDesk := ExpandConstant('{autodesktop}\GamingBooster_Pro.exe');
    if FileExists(oldDesk) then
      DeleteFile(oldDesk);
    oldPub := ExpandConstant('{autopf}\GamingBooster_Pro\GamingBooster_Pro.exe');
    if FileExists(oldPub) then
      DeleteFile(oldPub);
  end;
end;
