#ifndef MyVersion
  #define MyVersion "0.6.6"
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
portuguese.DataPageSubCaption=Usar as configurações existentes troca o programa sem devolver os ícones ao Desktop. Começar de novo devolve os itens e arquiva o estado anterior.
portuguese.KeepData=Usar as configurações existentes (recomendado)
portuguese.ResetData=Começar com configurações novas (o estado anterior será arquivado)
portuguese.MaintenanceFailed=A manutenção segura não foi concluída. A instalação foi cancelada e os dados existentes foram preservados.
portuguese.MaintenanceInstanceBusy=O DesktopFences está aberto (ícone na bandeja). O setup pede para sair sem mexer nos teus ícones.%n%nFecha-o pela bandeja se o pedido falhar, depois tenta outra vez.%n%nTentar novamente / Cancelar: os dados ficam intactos.
portuguese.MaintenanceCustodyBlocked=A manutenção não foi concluída com segurança (journal ou devolução). Os teus dados estão no sítio; o programa não foi removido.%n%nSIM: abrir Recovery%nNÃO: tentar outra vez%nCANCELAR: cancelar sem apagar nada
portuguese.FinalizeFailed=O programa foi instalado, mas não foi possível finalizar o idioma e o caminho de inicialização.
portuguese.UninstallQuestion=Como deseja desinstalar?%n%nSIM: remover o programa e manter configurações.%nNÃO: devolver os itens ao Desktop e remover tudo.%nCANCELAR: não desinstalar.%n%nSe a devolução falhar, nada é apagado e podes usar o Recovery.
portuguese.UninstallFailed=Não foi possível concluir a desinstalação com segurança. O programa, o Recovery e os dados foram preservados. Não houve erro interno do instalador.
portuguese.UninstallOpenRecovery=Abrir o DesktopFences Recovery agora? (pede confirmação antes de copiar para o Desktop)
portuguese.DowngradeBlocked=Há uma versão mais nova do DesktopFences instalada. O downgrade foi bloqueado para proteger os dados.
portuguese.DesktopIcon=Criar um atalho na Área de Trabalho
portuguese.LaunchApp=Abrir o DesktopFences
portuguese.NewFenceMenu=Nova fence
portuguese.FinishedHeadingHint=Instalação concluída — um passo recomendado
portuguese.RestartHint=Recomendado: reinicie o Windows depois de clicar em Concluir.%n%nNão é obrigatório agora — pode usar o computador na mesma. Se Novo → Fence ainda não aparecer (botão direito no Desktop vazio → Mostrar mais opções → Novo), o item aparece depois desse reinício.
portuguese.FinishedContinue=Pode abrir o DesktopFences já; o reinício só atualiza o menu Novo do Explorer.
english.DataPageTitle=Existing configuration
english.DataPageDescription=Choose how this installation should handle the configuration already found.
english.DataPageSubCaption=Keeping your configuration replaces the program without returning icons to the Desktop. Starting fresh returns items and archives the previous state.
english.KeepData=Use existing configuration (recommended)
english.ResetData=Start with a new configuration (the previous state will be archived)
english.MaintenanceFailed=Safe maintenance did not complete. Installation was cancelled and existing data was preserved.
english.MaintenanceInstanceBusy=DesktopFences is open (tray icon). Setup will ask it to exit without moving your icons.%n%nIf that request fails, close it from the tray, then try again.%n%nRetry / Cancel: your data stays intact.
english.MaintenanceCustodyBlocked=Maintenance could not finish safely (journal or return to Desktop). Your data is still in place; the program was not removed.%n%nYES: open Recovery%nNO: try again%nCANCEL: cancel without deleting anything
english.FinalizeFailed=The program was installed, but language and startup path finalization failed.
english.UninstallQuestion=How do you want to uninstall?%n%nYES: remove the program and keep settings.%nNO: return items to the Desktop and remove everything.%nCANCEL: do not uninstall.%n%nIf returning items fails, nothing is deleted and you can use Recovery.
english.UninstallFailed=Uninstall could not finish safely. The program, Recovery, and data were preserved. This is not an installer internal crash.
english.UninstallOpenRecovery=Open DesktopFences Recovery now? (it asks for confirmation before copying to the Desktop)
english.DowngradeBlocked=A newer DesktopFences version is installed. Downgrade was blocked to protect your data.
english.DesktopIcon=Create a Desktop shortcut
english.LaunchApp=Open DesktopFences
english.NewFenceMenu=New fence
english.FinishedHeadingHint=Setup complete — one recommended step
english.RestartHint=Recommended: restart Windows after you click Finish.%n%nIt is not required now — you can keep using the computer. If New → Fence is not on the desktop menu yet (right-click empty desktop → Show more options → New), it will appear after that restart.
english.FinishedContinue=You can open DesktopFences now; the restart only refreshes Explorer's New menu.

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
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\DesktopFencesNewFence"; ValueType: string; ValueName: ""; ValueData: "{cm:NewFenceMenu}"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\DesktopFencesNewFence"; ValueType: string; ValueName: "MUIVerb"; ValueData: "{cm:NewFenceMenu}"
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\DesktopFencesNewFence"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName},0"
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\DesktopFencesNewFence\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" --create-fence"
Root: HKCU; Subkey: "Software\Classes\DesktopBackground\shell\DesktopFencesNewFence"; ValueType: string; ValueName: ""; ValueData: "{cm:NewFenceMenu}"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\DesktopBackground\shell\DesktopFencesNewFence"; ValueType: string; ValueName: "MUIVerb"; ValueData: "{cm:NewFenceMenu}"
Root: HKCU; Subkey: "Software\Classes\DesktopBackground\shell\DesktopFencesNewFence"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName},0"
Root: HKCU; Subkey: "Software\Classes\DesktopBackground\shell\DesktopFencesNewFence"; ValueType: string; ValueName: "Position"; ValueData: "Bottom"
Root: HKCU; Subkey: "Software\Classes\DesktopBackground\shell\DesktopFencesNewFence\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" --create-fence"
Root: HKCU; Subkey: "Software\Classes\.desktopfence"; ValueType: string; ValueName: ""; ValueData: "DesktopFences.NewFence"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\.desktopfence\ShellNew"; ValueType: string; ValueName: "Command"; ValueData: """{app}\{#MyAppExeName}"" --create-fence"
Root: HKCU; Subkey: "Software\Classes\.desktopfence\ShellNew"; ValueType: string; ValueName: "ItemName"; ValueData: "Fence"
Root: HKCU; Subkey: "Software\Classes\.desktopfence\ShellNew"; ValueType: string; ValueName: "IconPath"; ValueData: "{app}\{#MyAppExeName},0"
Root: HKCU; Subkey: "Software\Classes\DesktopFences.NewFence"; ValueType: string; ValueName: ""; ValueData: "Fence"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\DesktopFences.NewFence\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"

[Run]
Filename: "{app}\DesktopFences.exe"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent

[Code]
var
  DataPage: TInputOptionWizardPage;
  UninstallMode: String;
  RestartBanner: TPanel;
  RestartHint: TNewStaticText;

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

  RestartBanner := TPanel.Create(WizardForm);
  RestartBanner.Parent := WizardForm.FinishedPage;
  RestartBanner.BevelOuter := bvNone;
  RestartBanner.ParentBackground := False;
  RestartBanner.Color := $00C8F4FF;
  RestartBanner.Visible := False;

  RestartHint := TNewStaticText.Create(WizardForm);
  RestartHint.Parent := RestartBanner;
  RestartHint.AutoSize := False;
  RestartHint.WordWrap := True;
  RestartHint.Font.Style := [fsBold];
  RestartHint.Font.Size := WizardForm.FinishedLabel.Font.Size + 1;
  RestartHint.Font.Color := $0014334C;
end;

procedure CurPageChanged(CurPageID: Integer);
var
  ContentLeft: Integer;
  ContentWidth: Integer;
  BannerTop: Integer;
begin
  if CurPageID <> wpFinished then
    Exit;

  WizardForm.FinishedHeadingLabel.Caption := ExpandConstant('{cm:FinishedHeadingHint}');

  ContentLeft := WizardForm.FinishedLabel.Left;
  ContentWidth := WizardForm.FinishedLabel.Width;
  BannerTop := WizardForm.FinishedHeadingLabel.Top + WizardForm.FinishedHeadingLabel.Height + ScaleY(12);

  RestartBanner.Left := ContentLeft;
  RestartBanner.Width := ContentWidth;
  RestartBanner.Top := BannerTop;
  RestartBanner.Height := ScaleY(108);

  RestartHint.Caption := ExpandConstant('{cm:RestartHint}');
  RestartHint.Left := ScaleX(12);
  RestartHint.Top := ScaleY(10);
  RestartHint.Width := RestartBanner.Width - ScaleX(24);
  RestartHint.Height := RestartBanner.Height - ScaleY(20);

  RestartBanner.Visible := True;

  WizardForm.FinishedLabel.Caption := ExpandConstant('{cm:FinishedContinue}');
  WizardForm.FinishedLabel.Top := RestartBanner.Top + RestartBanner.Height + ScaleY(10);
  WizardForm.RunList.Top := WizardForm.FinishedLabel.Top + WizardForm.FinishedLabel.Height + ScaleY(8);
end;

function MaintenanceLogPath: String;
begin
  Result := ExpandConstant('{localappdata}\DesktopFences\maintenance-last.log');
end;

function ReadMaintenanceKind: String;
var
  Lines: TArrayOfString;
  I: Integer;
  Line: String;
begin
  Result := '';
  if not LoadStringsFromFile(MaintenanceLogPath, Lines) then
    Exit;
  for I := 0 to GetArrayLength(Lines) - 1 do
  begin
    Line := Lines[I];
    if Pos('kind=', Line) = 1 then
    begin
      Result := Copy(Line, 6, MaxInt);
      StringChangeEx(Result, #13, '', True);
      StringChangeEx(Result, #10, '', True);
      Exit;
    end;
  end;
end;

function RunMaintenanceEx(const Executable, Mode: String; IncludeLanguage: Boolean; var ExitCode: Integer): Boolean;
var
  Parameters: String;
begin
  Parameters := '--maintenance=' + Mode;
  if IncludeLanguage then
    Parameters := Parameters + ' --language=' + AppLanguage;
  Result := Exec(Executable, Parameters, '', SW_HIDE, ewWaitUntilTerminated, ExitCode) and
            (ExitCode = 0);
end;

function RunMaintenance(const Executable, Mode: String; IncludeLanguage: Boolean): Boolean;
var
  ExitCode: Integer;
begin
  Result := RunMaintenanceEx(Executable, Mode, IncludeLanguage, ExitCode);
end;

function OpenRecoveryTool: Boolean;
var
  Recovery: String;
  Code: Integer;
begin
  Result := False;
  Recovery := ExpandConstant('{app}\DesktopFences.Recovery.exe');
  if not FileExists(Recovery) then
  begin
    try
      ExtractTemporaryFile('DesktopFences.Recovery.exe');
      Recovery := ExpandConstant('{tmp}\DesktopFences.Recovery.exe');
    except
      Recovery := '';
    end;
  end;
  if (Recovery <> '') and FileExists(Recovery) then
  begin
    Exec(Recovery, '', '', SW_SHOWNORMAL, ewWaitUntilTerminated, Code);
    Result := True;
  end;
end;

function IsInstanceBusy(ExitCode: Integer; const Kind: String): Boolean;
begin
  Result := (Kind = 'InstanceBusy') or ((Kind = '') and (ExitCode = 1));
end;

function IsCustodyBlocked(ExitCode: Integer; const Kind: String): Boolean;
begin
  Result := (Kind = 'CustodyBlocked') or (ExitCode = 3);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Mode: String;
  Helper: String;
  ExitCode: Integer;
  Tries: Integer;
  Kind: String;
  Choice: Integer;
begin
  Result := '';
  NeedsRestart := False;
  if not HasExistingData and not FileExists(ExpandConstant('{app}\DesktopFences.exe')) then
    Exit;

  ExtractTemporaryFile('DesktopFences.exe');
  Helper := ExpandConstant('{tmp}\DesktopFences.exe');
  Mode := 'upgradekeep';
  if Assigned(DataPage) and (DataPage.SelectedValueIndex = 1) then
    Mode := 'reset';

  for Tries := 1 to 3 do
  begin
    if RunMaintenanceEx(Helper, Mode, True, ExitCode) then
      Exit;

    Kind := ReadMaintenanceKind;
    if WizardSilent then
    begin
      if IsCustodyBlocked(ExitCode, Kind) then
        Result := ExpandConstant('{cm:MaintenanceCustodyBlocked}')
      else if IsInstanceBusy(ExitCode, Kind) then
        Result := ExpandConstant('{cm:MaintenanceInstanceBusy}')
      else
        Result := ExpandConstant('{cm:MaintenanceFailed}');
      Exit;
    end;

    if IsInstanceBusy(ExitCode, Kind) then
    begin
      Choice := MsgBox(ExpandConstant('{cm:MaintenanceInstanceBusy}'), mbConfirmation, MB_RETRYCANCEL);
      if Choice = IDCANCEL then
      begin
        Result := ExpandConstant('{cm:MaintenanceInstanceBusy}');
        Exit;
      end;
    end
    else if IsCustodyBlocked(ExitCode, Kind) then
    begin
      Choice := MsgBox(ExpandConstant('{cm:MaintenanceCustodyBlocked}'), mbError, MB_YESNOCANCEL);
      if Choice = IDYES then
        OpenRecoveryTool
      else if Choice = IDCANCEL then
      begin
        Result := ExpandConstant('{cm:MaintenanceCustodyBlocked}');
        Exit;
      end;
    end
    else
    begin
      Result := ExpandConstant('{cm:MaintenanceFailed}');
      Exit;
    end;
  end;

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
var
  ExitCode: Integer;
  Tries: Integer;
  Kind: String;
  Choice: Integer;
begin
  if CurUninstallStep <> usUninstall then
    Exit;

  for Tries := 1 to 3 do
  begin
    if RunMaintenanceEx(ExpandConstant('{app}\DesktopFences.exe'), UninstallMode, False, ExitCode) then
    begin
      if UninstallMode = 'remove' then
        RegDeleteKeyIncludingSubkeys(HKCU, 'Software\DesktopFences');
      Exit;
    end;

    Kind := ReadMaintenanceKind;
    MsgBox(ExpandConstant('{cm:UninstallFailed}'), mbError, MB_OK);

    if IsCustodyBlocked(ExitCode, Kind) then
    begin
      Choice := MsgBox(ExpandConstant('{cm:MaintenanceCustodyBlocked}'), mbError, MB_YESNOCANCEL);
      if Choice = IDYES then
        OpenRecoveryTool
      else if Choice = IDCANCEL then
        Abort;
    end
    else if IsInstanceBusy(ExitCode, Kind) then
    begin
      Choice := MsgBox(ExpandConstant('{cm:MaintenanceInstanceBusy}'), mbConfirmation, MB_RETRYCANCEL);
      if Choice = IDCANCEL then
        Abort;
    end
    else
    begin
      if MsgBox(ExpandConstant('{cm:UninstallOpenRecovery}'), mbConfirmation, MB_YESNO) = IDYES then
        OpenRecoveryTool;
      Abort;
    end;
  end;

  Abort;
end;
