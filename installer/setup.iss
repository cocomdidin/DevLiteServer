#define MyAppName "Dev Lite Server"
#define MyAppPublisher "Dev Lite Server Team"
#define MyAppURL "https://github.com/cocomdidin/DevLiteServer"
#define MyAppExeName "DevLiteServer.exe"

#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif

#ifndef SourceDir
  #define SourceDir "..\dist\app"
#endif

[Setup]
AppId={{A3E4887F-B763-4CD2-9F12-70F4D08AEB39}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} v{#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName=C:\DevLiteServer
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\dist\installer
OutputBaseFilename=DevLiteServer-Setup-v{#MyAppVersion}
SetupIconFile=..\assets\app.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; 1. Core application binaries, assets, tools, templates (always updated on install)
Source: "{#SourceDir}\*"; DestDir: "{app}"; Excludes: "config.ini,config.ini.default,sites.json,www\*,ssl\*,bin\mysql\*\data\*,bin\postgresql\*\data\*"; Flags: ignoreversion recursesubdirs createallsubdirs

; 2. Default configuration file (installed ONLY if not existing, never deleted on uninstall)
Source: "{#SourceDir}\config.ini"; DestDir: "{app}"; Flags: onlyifdoesntexist uninsneveruninstall

; 3. Configuration template reference
Source: "{#SourceDir}\config.ini.default"; DestDir: "{app}"; Flags: ignoreversion

; 4. Default www starter files (installed ONLY if not existing, never deleted on uninstall)
Source: "{#SourceDir}\www\*"; DestDir: "{app}\www"; Flags: onlyifdoesntexist recursesubdirs createallsubdirs uninsneveruninstall

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
