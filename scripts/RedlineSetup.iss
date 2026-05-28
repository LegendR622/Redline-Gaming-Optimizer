; Redline Gaming Optimizer – Windows Installer (Inno Setup 6)
; Build: "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" scripts\RedlineSetup.iss

#define MyAppName "Redline Gaming Optimizer"
#define MyAppVersion "9.15"
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
VersionInfoVersion=9.11.0.0
VersionInfoProductVersion={#MyAppVersion}

[Languages]
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#MyAppSource}"; DestDir: "{app}"; DestName: "{#MyAppExeName}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent
