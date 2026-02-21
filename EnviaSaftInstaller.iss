; Inno Setup Script - EnviaSaft Installer

#define MyAppName "EnviaSaft"
#define MyAppVersion "2.0.0"
#define MyAppPublisher "Loading Happiness"
#define MyAppExeName "EnvioSafTApp.exe"
#define MyAppPublishDir "dist\\windows\\publish"

[Setup]
AppId={{5F4EBF8B-8F57-4608-9D30-2F5E3CF0A84E}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputDir=dist\windows\installer
OutputBaseFilename=EnviaSaftSetup_v{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
SetupIconFile=Assets\EnviaSaft.ico
UninstallDisplayIcon={app}\Assets\EnviaSaft.ico

[Languages]
Name: "pt"; MessagesFile: "compiler:Languages\Portuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar ícone no ambiente de trabalho"; GroupDescription: "Opções adicionais"

[Files]
Source: "{#MyAppPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Iniciar {#MyAppName}"; Flags: nowait postinstall skipifsilent
