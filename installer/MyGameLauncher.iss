; Instalador do MyGameLauncher (Inno Setup 6 — https://jrsoftware.org/isinfo.php)
; Para criar o instalador: duplo clique em "criar-instalador.bat" na pasta do projeto.
; (Esse ficheiro prepara a app em "publish-instalador" e depois compila este script.)

#define MyAppName "MyGameLauncher"
#define MyAppVersion "1.1.0"
#define MyAppPublisher "diog0_h"
#define MyAppExeName "MyGameLauncher.exe"
#define SourceDir "..\publish-instalador"

[Setup]
; Não mudar o AppId: é o que permite ao Windows reconhecer atualizações da mesma app.
AppId={{65BE22AB-9A75-4F7F-ABFD-9E1101F648F6}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppCopyright=© 2026 {#MyAppPublisher}
VersionInfoCompany={#MyAppPublisher}
VersionInfoVersion={#MyAppVersion}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename={#MyAppName}-Setup-{#MyAppVersion}
SetupIconFile=..\Assets\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
; Instala sem pedir administrador (só para o utilizador atual); quem quiser pode escolher "para todos".
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
; Fecha a app se estiver aberta durante uma atualização.
CloseApplications=yes
RestartApplications=no
ShowLanguageDialog=auto

[Languages]
; O instalador usa a língua do Windows; se não for nenhuma destas, usa a primeira (inglês).
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "portuguese"; MessagesFile: "compiler:Languages\Portuguese.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "french"; MessagesFile: "compiler:Languages\French.isl"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"

[CustomMessages]
portuguese.DeleteData=Apagar também os teus dados do MyGameLauncher (favoritos, coleções, tempo de jogo registado, definições e imagens guardadas)?
english.DeleteData=Also delete your MyGameLauncher data (favorites, collections, recorded playtime, settings and cached images)?
spanish.DeleteData=¿Borrar también tus datos de MyGameLauncher (favoritos, colecciones, tiempo de juego registrado, ajustes e imágenes guardadas)?
french.DeleteData=Supprimer aussi tes données MyGameLauncher (favoris, collections, temps de jeu enregistré, paramètres et images en cache) ?
german.DeleteData=Auch deine MyGameLauncher-Daten löschen (Favoriten, Sammlungen, erfasste Spielzeit, Einstellungen und gespeicherte Bilder)?

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\THIRD-PARTY-NOTICES.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[Code]
// Ao desinstalar, pergunta se também deve apagar os dados guardados em %LocalAppData%\GameLauncher.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    DataDir := ExpandConstant('{localappdata}\GameLauncher');
    if DirExists(DataDir) then
      if SuppressibleMsgBox(CustomMessage('DeleteData'), mbConfirmation, MB_YESNO or MB_DEFBUTTON2, IDNO) = IDYES then
        DelTree(DataDir, True, True, True);
  end;
end;
