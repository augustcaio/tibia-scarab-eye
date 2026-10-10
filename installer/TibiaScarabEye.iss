; Instalador do Tibia Scarab Eye (Inno Setup 6). Gerado por installer\build.ps1, que publica o programa em
; artifacts\publish e chama o compilador com /DAppVersion=<versao>.
#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif
#define AppName "Tibia Scarab Eye"
#define AppExe "TibiaScarabEye.exe"
#define AppUrl "https://github.com/augustcaio/tibia-scarab-eye"

[Setup]
AppId={{B2E1D4A6-7C3F-4A1E-9D52-3F8A6C0B7E11}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=augustcaio
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
OutputDir=Output
OutputBaseFilename=TibiaScarabEye-Setup-{#AppVersion}
SetupIconFile=..\src\TibiaScarabEye\Assets\app.ico
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
; Instala so para o usuario por padrao (sem pedir administrador); o assistente deixa escolher para todos.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\artifacts\publish\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion
Source: "..\obs\TibiaScarabEye.lua"; DestDir: "{app}\obs"; Flags: ignoreversion
Source: "..\docs\LEIA-ME.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\Guia de uso"; Filename: "{app}\LEIA-ME.txt"
Name: "{group}\Script do OBS (pasta)"; Filename: "{app}\obs"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
