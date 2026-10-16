; Build with Inno Setup 6 or newer:
;   ISCC.exe /DStagingDir="<verified-staging-directory>" SkyrimRP-Setup-1.0.0.iss
; The staging directory must first pass Scripts\Test-InstallerStaging.ps1.

#ifndef StagingDir
  #error StagingDir is required. Pass /DStagingDir="C:\path\to\verified-staging"
#endif

#define AppName "Skyrim RP Encyclopedia"
#define AppVersion "1.0.0"
#define AppExeName "SkyrimRpEncyclopedia.exe"

[Setup]
AppId={{B677E144-184B-4F56-B2BE-7A143268D19C}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Skyrim RP
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=no
DisableDirPage=no
OutputDir={#StagingDir}\InstallerOutput
OutputBaseFilename=SkyrimRP-Setup-1.0.0
SetupIconFile=..\Assets\Branding\SkyrimRpEncyclopediaSetup.ico
UninstallDisplayIcon={app}\{#AppExeName}
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=commandline
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
DisableWelcomePage=no
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "Создать ярлык на рабочем столе"; GroupDescription: "Дополнительные ярлыки:"; Flags: unchecked

[Files]
Source: "{#StagingDir}\App\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#StagingDir}\Data\knowledge.db"; DestDir: "{app}\Data"; Flags: ignoreversion
Source: "{#StagingDir}\Data\rp_rules.db"; DestDir: "{app}\Data"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Запустить {#AppName}"; Flags: nowait postinstall skipifsilent

[Code]
const
  WebView2RuntimeClientKey = 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}';

function HasRegisteredWebView2Runtime(RootKey: Integer): Boolean;
var
  RuntimeVersion: String;
begin
  { Microsoft documents the pv REG_SZ value in these EdgeUpdate client keys as
    the supported Runtime-presence test.  HKLM32 intentionally reads the
    WOW6432Node view on 64-bit Windows, where the machine-wide Runtime lives. }
  Result := RegQueryStringValue(RootKey, WebView2RuntimeClientKey, 'pv', RuntimeVersion) and
            (RuntimeVersion <> '') and
            (RuntimeVersion <> '0.0.0.0');
end;

function HasWebView2Runtime(): Boolean;
begin
  { On 64-bit Windows Microsoft specifies HKLM\SOFTWARE\WOW6432Node and HKCU.
    The Runtime executable is versioned below Application, so checking a fixed
    Application\msedgewebview2.exe path produces false negatives. }
  Result := HasRegisteredWebView2Runtime(HKLM32) or
            HasRegisteredWebView2Runtime(HKCU);
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
  if not HasWebView2Runtime() then
  begin
    MsgBox(
      'Не найден Microsoft Edge WebView2 Evergreen Runtime. ' +
      'Установите официальный WebView2 Evergreen Runtime x64 и повторите запуск установщика. ' +
      'Этот пакет не содержит сторонний загрузчик, поэтому не скачивает компоненты автоматически.',
      mbError, MB_OK);
    Result := False;
  end;
end;

[UninstallDelete]
; Only installed application files are removed by the uninstaller. User databases, settings,
; logs and the WebView2 profile live in %LOCALAPPDATA% and are deliberately not listed here.
Type: filesandordirs; Name: "{app}\Data"
