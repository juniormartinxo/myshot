; Instalador do MyShot (Inno Setup 6). Gerado por scripts\build.ps1, que passa SourceDir, AppVersion,
; OutputDir e OutputBaseFilename na linha de comando. Instala por usuário, sem administrador.

#ifndef SourceDir
  #define SourceDir "..\artifacts"
#endif
#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts"
#endif
#ifndef OutputBaseFilename
  #define OutputBaseFilename "MyShot-Setup"
#endif

[Setup]
; AppId fixo: instalar uma versão nova substitui a antiga e a desinstalação encontra o app pelo mesmo ID.
AppId={{8DCFAEB5-C491-4213-8740-CD60AFDE764E}
AppName=MyShot
AppVersion={#AppVersion}
AppPublisher=Junior Martins
DefaultDirName={localappdata}\Programs\MyShot
DefaultGroupName=MyShot
DisableProgramGroupPage=yes
UninstallDisplayName=MyShot
UninstallDisplayIcon={app}\MyShot.exe
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
OutputDir={#OutputDir}
OutputBaseFilename={#OutputBaseFilename}
SetupIconFile=..\assets\myshot.ico

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Files]
Source: "{#SourceDir}\MyShot.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\MyShot.exe.config"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\MyShot"; Filename: "{app}\MyShot.exe"; Comment: "MyShot · Captura de tela"
Name: "{group}\Desinstalar MyShot"; Filename: "{uninstallexe}"

[Registry]
; Remove a inicialização com o Windows (HKCU) se o usuário a tiver ativado no app.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "MyShot"; ValueType: none; Flags: uninsdeletevalue

[Run]
Filename: "{app}\MyShot.exe"; Description: "Abrir o MyShot"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; O app roda na bandeja e o Restart Manager nem sempre o encerra: fecha antes de apagar os arquivos.
Filename: "{sys}\taskkill.exe"; Parameters: "/IM MyShot.exe /F"; Flags: runhidden; RunOnceId: "StopMyShot"
