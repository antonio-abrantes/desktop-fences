#ifndef MyVersion
  #define MyVersion "0.6.0"
#endif
#ifndef MyArch
  #define MyArch "win-x64"
#endif

#define MyAppName "DesktopFences"
#define MyPublisher "Antônio Abrantes"
#define MyAppExeName "DesktopFences.exe"
#define MySourceDir "..\publish\" + MyArch

[Setup]
AppId={{7D3AF201-08A3-4E2E-8F28-79376F632A51}
AppName={#MyAppName}
AppVersion={#MyVersion}
AppVerName={#MyAppName} {#MyVersion}
AppPublisher={#MyPublisher}
AppPublisherURL=https://github.com/antonio-abrantes/desktop-fences
AppSupportURL=https://github.com/antonio-abrantes/desktop-fences/issues
AppUpdatesURL=https://github.com/antonio-abrantes/desktop-fences/releases
DefaultDirName={localappdata}\Programs\DesktopFences
DefaultGroupName=DesktopFences
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
OutputDir=..\out
OutputBaseFilename=DesktopFences-{#MyVersion}-{#MyArch}-setup
SetupIconFile=..\src\DesktopFences.App\Assets\app.ico
UninstallDisplayIcon={app}\DesktopFences.exe
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
CloseApplications=no
RestartApplications=no
UsePreviousAppDir=yes
UsePreviousLanguage=no
ShowLanguageDialog=yes
LanguageDetectionMethod=none
MinVersion=10.0.17763
#if MyArch == "win-arm64"
ArchitecturesAllowed=arm64
ArchitecturesInstallIn64BitMode=arm64
#else
ArchitecturesAllowed=x64compatible and not arm64
ArchitecturesInstallIn64BitMode=x64compatible
#endif

[Languages]
Name: "portuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
portuguese.DataPageTitle=Configuração existente
portuguese.DataPageDescription=Escolha como esta instalação deve tratar as configurações já encontradas.
portuguese.DataPageSubCaption=Se houver itens dentro das fences, eles serão devolvidos ao Desktop com segurança antes da atualização.
portuguese.KeepData=Usar as configurações existentes (recomendado)
portuguese.ResetData=Começar com configurações novas (o estado anterior será arquivado)
portuguese.MaintenanceFailed=A manutenção segura não foi concluída. A instalação foi cancelada e os dados existentes foram preservados. Se uma versão antiga ou portable estiver aberta, feche-a pela bandeja e tente novamente.
portuguese.FinalizeFailed=O programa foi instalado, mas não foi possível finalizar o idioma e o caminho de inicialização.
portuguese.UninstallQuestion=Como deseja desinstalar?%n%nSIM: remover o programa e manter configurações.%nNÃO: devolver os itens ao Desktop e remover tudo.%nCANCELAR: não desinstalar.
portuguese.UninstallFailed=Não foi possível devolver todos os itens ao Desktop com segurança. A desinstalação foi cancelada; programa, Recovery e dados foram preservados.
portuguese.DowngradeBlocked=Há uma versão mais nova do DesktopFences instalada. O downgrade foi bloqueado para proteger os dados.
portuguese.DesktopIcon=Criar um atalho na Área de Trabalho
portuguese.LaunchApp=Abrir o DesktopFences
english.DataPageTitle=Existing configuration
english.DataPageDescription=Choose how this installation should handle the configuration already found.
english.DataPageSubCaption=If fences contain items, they will be safely returned to the Desktop before the update.
english.KeepData=Use existing configuration (recommended)
english.ResetData=Start with a new configuration (the previous state will be archived)
english.MaintenanceFailed=Safe maintenance did not complete. Installation was cancelled and existing data was preserved. If an older or portable version is open, close it from the tray and try again.
english.FinalizeFailed=The program was installed, but language and startup path finalization failed.
english.UninstallQuestion=How do you want to uninstall?%n%nYES: remove the program and keep settings.%nNO: return items to the Desktop and remove everything.%nCANCEL: do not uninstall.
english.UninstallFailed=Not every item could be returned safely to the Desktop. Uninstall was cancelled; the program, Recovery, and data were preserved.
english.DowngradeBlocked=A newer DesktopFences version is installed. Downgrade was blocked to protect your data.
english.DesktopIcon=Create a Desktop shortcut
english.LaunchApp=Open DesktopFences

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#MySourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\DesktopFences"; Filename: "{app}\DesktopFences.exe"
Name: "{group}\DesktopFences Recovery"; Filename: "{app}\DesktopFences.Recovery.exe"
Name: "{autodesktop}\DesktopFences"; Filename: "{app}\DesktopFences.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\DesktopFences"; ValueType: string; ValueName: "InstallVersion"; ValueData: "{#MyVersion}"
Root: HKCU; Subkey: "Software\DesktopFences"; ValueType: string; ValueName: "InstallArchitecture"; ValueData: "{#MyArch}"

[Run]
Filename: "{app}\DesktopFences.exe"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent

[Code]
var
  DataPage: TInputOptionWizardPage;
  UninstallMode: String;

function HasExistingData: Boolean;
begin
  Result := FileExists(ExpandConstant('{userappdata}\DesktopFences\layout.json')) or
            FileExists(ExpandConstant('{userappdata}\DesktopFences\layout.json.bak')) or
            DirExists(ExpandConstant('{localappdata}\DesktopFences\Items')) or
            DirExists(ExpandConstant('{localappdata}\DesktopFences\Transactions'));
end;

function AppLanguage: String;
begin
  if ActiveLanguage = 'english' then
    Result := 'en'
  else
    Result := 'pt';
end;

function NextVersionPart(var Version: String): Integer;
var
  P: Integer;
  Part: String;
begin
  P := Pos('.', Version);
  if P = 0 then
  begin
    Part := Version;
    Version := '';
  end
  else
  begin
    Part := Copy(Version, 1, P - 1);
    Delete(Version, 1, P);
  end;
  Result := StrToIntDef(Part, 0);
end;

function CompareVersions(LeftVersion, RightVersion: String): Integer;
var
  I, LeftPart, RightPart: Integer;
begin
  Result := 0;
  for I := 1 to 4 do
  begin
    LeftPart := NextVersionPart(LeftVersion);
    RightPart := NextVersionPart(RightVersion);
    if LeftPart > RightPart then
    begin
      Result := 1;
      Exit;
    end;
    if LeftPart < RightPart then
    begin
      Result := -1;
      Exit;
    end;
  end;
end;

function InitializeSetup: Boolean;
var
  InstalledVersion: String;
begin
  Result := True;
  if RegQueryStringValue(HKCU, 'Software\DesktopFences', 'InstallVersion', InstalledVersion) and
     (CompareVersions(InstalledVersion, '{#MyVersion}') > 0) then
  begin
    MsgBox(ExpandConstant('{cm:DowngradeBlocked}'), mbError, MB_OK);
    Result := False;
  end;
end;

procedure InitializeWizard;
begin
  if HasExistingData then
  begin
    DataPage := CreateInputOptionPage(
      wpSelectDir,
      ExpandConstant('{cm:DataPageTitle}'),
      ExpandConstant('{cm:DataPageDescription}'),
      ExpandConstant('{cm:DataPageSubCaption}'),
      True,
      False);
    DataPage.Add(ExpandConstant('{cm:KeepData}'));
    DataPage.Add(ExpandConstant('{cm:ResetData}'));
    DataPage.SelectedValueIndex := 0;
  end;
end;

function RunMaintenance(const Executable, Mode: String; IncludeLanguage: Boolean): Boolean;
var
  Parameters: String;
  ExitCode: Integer;
begin
  Parameters := '--maintenance=' + Mode;
  if IncludeLanguage then
    Parameters := Parameters + ' --language=' + AppLanguage;
  Result := Exec(Executable, Parameters, '', SW_HIDE, ewWaitUntilTerminated, ExitCode) and
            (ExitCode = 0);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Mode: String;
  Helper: String;
begin
  Result := '';
  if not HasExistingData and not FileExists(ExpandConstant('{app}\DesktopFences.exe')) then
    Exit;

  ExtractTemporaryFile('DesktopFences.exe');
  Helper := ExpandConstant('{tmp}\DesktopFences.exe');
  Mode := 'keep';
  if Assigned(DataPage) and (DataPage.SelectedValueIndex = 1) then
    Mode := 'reset';
  if not RunMaintenance(Helper, Mode, True) then
    Result := ExpandConstant('{cm:MaintenanceFailed}');
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    if not RunMaintenance(ExpandConstant('{app}\DesktopFences.exe'), 'finalize', True) then
      MsgBox(ExpandConstant('{cm:FinalizeFailed}'), mbError, MB_OK);
  end;
end;

function InitializeUninstall: Boolean;
var
  Choice: Integer;
begin
  Choice := MsgBox(ExpandConstant('{cm:UninstallQuestion}'), mbConfirmation, MB_YESNOCANCEL);
  if Choice = IDYES then
    UninstallMode := 'uninstallkeep'
  else if Choice = IDNO then
    UninstallMode := 'remove'
  else
  begin
    Result := False;
    Exit;
  end;
  Result := True;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    if not RunMaintenance(ExpandConstant('{app}\DesktopFences.exe'), UninstallMode, False) then
    begin
      MsgBox(ExpandConstant('{cm:UninstallFailed}'), mbError, MB_OK);
      RaiseException(ExpandConstant('{cm:UninstallFailed}'));
    end;
    if UninstallMode = 'remove' then
      RegDeleteKeyIncludingSubkeys(HKCU, 'Software\DesktopFences');
  end;
end;
