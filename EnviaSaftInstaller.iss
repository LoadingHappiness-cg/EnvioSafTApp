; Inno Setup Script - EnviaSaft Installer

[Setup]
AppName=EnviaSaft
AppVersion=1.1.0
DefaultDirName={autopf}\EnviaSaft
DefaultGroupName=EnviaSaft
OutputDir=.\Output
OutputBaseFilename=EnviaSaftSetup_v1.1.0
Compression=lzma
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
SetupIconFile="C:\Deploy\EnviaSaftFinal\icon.ico"

[Languages]
Name: "pt"; MessagesFile: "compiler:Languages\Portuguese.isl"

[Files]
; App executável principal
Source: "C:\Deploy\EnviaSaftFinal\EnvioSaftApp.exe"; DestDir: "{app}"; Flags: ignoreversion
; Dependências (dll, json, etc.)
Source: "C:\Deploy\EnviaSaftFinal\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

; Runtime offline do .NET 8
Source: "C:\Deploy\EnviaSaftFinal\external\windowsdesktop-runtime-8.0.16-win-x64.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall

[Icons]
Name: "{group}\EnviaSaft"; Filename: "{app}\EnvioSaftApp.exe"
Name: "{commondesktop}\EnviaSaft"; Filename: "{app}\EnvioSaftApp.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Criar ícone no ambiente de trabalho"; GroupDescription: "Opções adicionais"

[Run]
; Instalar .NET Runtime apenas se necessário
Filename: "{tmp}\windowsdesktop-runtime-8.0.16-win-x64.exe"; \
    Parameters: "/install /quiet /norestart"; \
    StatusMsg: "A instalar .NET Desktop Runtime 8.0..."; \
    Flags: waituntilterminated runhidden; \
    Check: NeedsDotNetDesktop80

; Executar a app após instalação
Filename: "{app}\EnvioSaftApp.exe"; Description: "Iniciar EnviaSaft"; Flags: nowait postinstall skipifsilent

[Code]
function NeedsDotNetDesktop80(): Boolean;
begin
  Result := not RegKeyExists(HKLM, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft .NET Runtime - 8.0.0 (x64)')
         and not RegKeyExists(HKLM64, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft .NET Runtime - 8.0.0 (x64)');
end;