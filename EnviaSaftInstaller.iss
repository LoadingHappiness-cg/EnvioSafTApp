; Inno Setup Script - EnviaSaft Installer

[Setup]
AppName=EnviaSaft
AppVersion=1.0.0
DefaultDirName={autopf}\EnviaSaft
DefaultGroupName=EnviaSaft
OutputDir=.\Output
OutputBaseFilename=EnviaSaftSetup
Compression=lzma
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
SetupIconFile="C:\Deploy\EnviaSaftFinal\icon.ico"

[Languages]
Name: "pt"; MessagesFile: "compiler:Languages\Portuguese.isl"

[Files]
; App files
Source: "C:\Deploy\EnviaSaftFinal\EnvioSaftApp.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\Deploy\EnviaSaftFinal\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

; .NET Runtime installer (offline)
Source: "C:\Deploy\EnviaSaftFinal\external\windowsdesktop-runtime-8.0.16-win-x64.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall

[Icons]
Name: "{group}\EnviaSaft"; Filename: "{app}\EnvioSftApp.exe"
Name: "{commondesktop}\EnviaSaft"; Filename: "{app}\EnvioSaftApp.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Criar ícone no ambiente de trabalho"; GroupDescription: "Opções adicionais"

[Run]
; Instala o .NET Desktop Runtime apenas se necessário
Filename: "{tmp}\windowsdesktop-runtime-8.0.16-win-x64.exe"; \
    Parameters: "/install /quiet /norestart"; \
    StatusMsg: "A instalar .NET Desktop Runtime 8.0..."; \
    Flags: waituntilterminated runhidden; \
    Check: NeedsDotNetDesktop80

; Iniciar a aplicação após instalar
Filename: "{app}\EnviaSaftApp.exe"; Description: "Iniciar EnviaSaft"; Flags: nowait postinstall skipifsilent

[Code]
function NeedsDotNetDesktop80(): Boolean;
var
  key: string;
begin
  key := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft .NET Runtime - 8.0.0 (x64)';
  Result := not RegKeyExists(HKLM, key);
end;