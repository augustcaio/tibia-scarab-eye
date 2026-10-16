; Instalador do Tibia Scarab Eye (Inno Setup 6). Gerado por installer\build.ps1, que publica o programa em
; artifacts\publish e chama o compilador com /DAppVersion=<versao>.
#ifndef AppVersion
  #define AppVersion "0.2.0"
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
; So aparece se o OBS ja foi aberto alguma vez (existe a pasta das colecoes de cenas).
Name: "obsscript"; Description: "Adicionar o script à lista de scripts do OBS (Ferramentas > Scripts)"; GroupDescription: "OBS Studio:"; Check: ObsConfigFound

[Files]
Source: "..\artifacts\publish\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion
Source: "..\obs\TibiaScarabEye.lua"; DestDir: "{app}\obs"; Flags: ignoreversion
Source: "obs-script.ps1"; DestDir: "{app}\obs"; Flags: ignoreversion
Source: "..\docs\LEIA-ME.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\Guia de uso"; Filename: "{app}\LEIA-ME.txt"
Name: "{group}\Script do OBS (pasta)"; Filename: "{app}\obs"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[Code]
const
  ObsOpen = 3;

function ObsConfigFound: Boolean;
begin
  Result := DirExists(ExpandConstant('{userappdata}\obs-studio\basic\scenes'));
end;

// 0 ok | 1 erro | 2 OBS sem colecao de cenas | 3 OBS aberto (ele reescreveria o arquivo ao fechar)
function RunObsScript(Action: String): Integer;
var
  Code: Integer;
begin
  if not Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
      '-NoProfile -ExecutionPolicy Bypass -File "' + ExpandConstant('{app}\obs\obs-script.ps1') + '" -Action ' + Action +
      ' -Script "' + ExpandConstant('{app}\obs\TibiaScarabEye.lua') + '"',
      '', SW_HIDE, ewWaitUntilTerminated, Code) then
    Code := 1;
  Result := Code;
end;

// O OBS precisa estar fechado: ele reescreve a colecao de cenas ao sair e desfaria a alteracao.
function RunObsScriptClosed(Action: String; Prompt: String): Integer;
begin
  repeat
    Result := RunObsScript(Action);
    if Result <> ObsOpen then Break;
  until SuppressibleMsgBox(Prompt, mbInformation, MB_RETRYCANCEL, IDCANCEL) = IDCANCEL;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssPostInstall) and WizardIsTaskSelected('obsscript') then
    if RunObsScriptClosed('Register',
        'O OBS está aberto. Feche o OBS e clique em Repetir para adicionar o script a ele, ou em Cancelar para pular.') <> 0 then
      SuppressibleMsgBox('O script não foi adicionado ao OBS. Para adicionar depois: no OBS, Ferramentas > Scripts > + e escolha' + #13#10 +
        ExpandConstant('{app}\obs\TibiaScarabEye.lua'), mbInformation, MB_OK, IDOK);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if (CurUninstallStep = usUninstall) and ObsConfigFound then
    RunObsScriptClosed('Unregister',
      'O OBS está aberto. Feche o OBS e clique em Repetir para remover o script dele, ou em Cancelar para deixá-lo na lista.');
end;
