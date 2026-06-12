#define AppName "LuxBurn"
#ifndef AppVersion
  #define AppVersion "2.1.8"
#endif
#define AppPublisher "sccpsteve"
#define SourceDir "..\LuxBurn\bin\Release"
#define DotNet35Redist "..\build\redist\dotnetfx35.exe"

[Setup]
AppId={{6B9103D3-7F75-40B8-89F1-220D6142E752}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=https://github.com/sccpsteve/LuxBurn
AppSupportURL=https://github.com/sccpsteve/LuxBurn/issues
AppUpdatesURL=https://github.com/sccpsteve/LuxBurn/releases/tag/latest
DefaultDirName={pf}\LuxBurn
DefaultGroupName=LuxBurn
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=LuxBurn-v{#AppVersion}-setup
SetupIconFile=..\LuxBurn\Assets\Brand\LBWindowLogo.ico
UninstallDisplayIcon={app}\LuxBurn.exe
Compression=lzma/ultra
SolidCompression=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x86 x64
WizardImageStretch=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#DotNet35Redist}"; Flags: dontcopy
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\LuxBurn"; Filename: "{app}\LuxBurn.exe"; WorkingDir: "{app}"
Name: "{commondesktop}\LuxBurn"; Filename: "{app}\LuxBurn.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\LuxBurn.exe"; Description: "{cm:LaunchProgram,LuxBurn}"; Flags: nowait postinstall skipifsilent; Check: CanLaunchLuxBurn

[Code]
var
  DotNetInstallAttempted: Boolean;
  DotNetInstallExitCode: Integer;
  DotNetRequiresRestart: Boolean;

function DotNet35InstallFlag(RootKey: Integer): Boolean;
var
  InstallValue: Cardinal;
  ServicePackValue: Cardinal;
begin
  Result := RegQueryDWordValue(RootKey, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v3.5', 'Install', InstallValue) and (InstallValue = 1);
  if Result and RegQueryDWordValue(RootKey, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v3.5', 'SP', ServicePackValue) then
    Result := ServicePackValue >= 1;
end;

function IsDotNet35Installed(): Boolean;
begin
  Result := DotNet35InstallFlag(HKLM) or DotNet35InstallFlag(HKLM32);
  if (not Result) and IsWin64 then
    Result := DotNet35InstallFlag(HKLM64);
end;

function NeedsDotNet35(): Boolean;
begin
  Result := not IsDotNet35Installed();
end;

function CanLaunchLuxBurn(): Boolean;
begin
  Result := IsDotNet35Installed() and (not DotNetRequiresRestart);
end;

function IsDotNetExitCodeSuccessful(ExitCode: Integer): Boolean;
begin
  Result := (ExitCode = 0) or (ExitCode = 3010) or (ExitCode = 1641);
end;

function WindowsVersionDescription(): String;
var
  Version: TWindowsVersion;
begin
  GetWindowsVersionEx(Version);
  Result := IntToStr(Version.Major) + '.' + IntToStr(Version.Minor) + ', Service Pack ' + IntToStr(Version.ServicePackMajor);
end;

function IsUnsupportedWindowsXp(): Boolean;
var
  Version: TWindowsVersion;
begin
  GetWindowsVersionEx(Version);
  Result := (Version.Major = 5) and (Version.Minor = 1) and (Version.ServicePackMajor < 3);
end;

function WindowsInstallerDescription(): String;
var
  VersionString: String;
begin
  if GetVersionNumbersString(ExpandConstant('{sys}\msi.dll'), VersionString) then
    Result := VersionString
  else
    Result := 'unknown';
end;

function DotNetLogPath(): String;
var
  LogDir: String;
begin
  LogDir := ExpandConstant('{commonappdata}\LuxBurn\Logs');
  ForceDirectories(LogDir);
  Result := LogDir + '\dotnet35-install.log';
end;

function DotNetFailureMessage(ExitCode: Integer): String;
begin
  if ExitCode = -1073741819 then
  begin
    Result :=
      'Microsoft .NET Framework 3.5 SP1 crashed while installing.' + #13#10 + #13#10 +
      'Windows reported 0xC0000005, which is an access violation inside the Microsoft installer, not inside LuxBurn.' + #13#10 + #13#10 +
      'Try these repairs, then run LuxBurn Setup again:' + #13#10 +
      '1. Make sure Windows XP Service Pack 3 is installed.' + #13#10 +
      '2. Restart Windows.' + #13#10 +
      '3. If it still fails, repair or reinstall Windows Installer, then run this setup again.' + #13#10 + #13#10 +
      'Detected Windows: ' + WindowsVersionDescription() + #13#10 +
      'Windows Installer file version: ' + WindowsInstallerDescription() + #13#10 +
      'Installer log: ' + DotNetLogPath();
  end
  else
  begin
    Result :=
      'Microsoft .NET Framework 3.5 SP1 did not install successfully. Setup cannot continue until it is installed.' + #13#10 + #13#10 +
      'Exit code: ' + IntToStr(ExitCode) + #13#10 +
      'Detected Windows: ' + WindowsVersionDescription() + #13#10 +
      'Windows Installer file version: ' + WindowsInstallerDescription() + #13#10 +
      'Installer log: ' + DotNetLogPath();
  end;
end;

function InitializeSetup(): Boolean;
begin
  Result := True;

  if IsUnsupportedWindowsXp() then
  begin
    MsgBox(
      'LuxBurn requires Windows XP Service Pack 3 or newer.' + #13#10 + #13#10 +
      'This computer appears to be running Windows ' + WindowsVersionDescription() + '.' + #13#10 +
      'Install Windows XP Service Pack 3, restart Windows, then run LuxBurn Setup again.',
      mbCriticalError,
      MB_OK);
    Result := False;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  InstallerPath: String;
  InstallerArgs: String;
begin
  Result := '';
  DotNetInstallAttempted := False;
  DotNetInstallExitCode := 0;
  DotNetRequiresRestart := False;

  if IsDotNet35Installed() then
    Exit;

  DotNetInstallAttempted := True;
  ExtractTemporaryFile('dotnetfx35.exe');
  InstallerPath := ExpandConstant('{tmp}\dotnetfx35.exe');
  InstallerArgs := '/passive /norestart /log "' + DotNetLogPath() + '"';

  if not Exec(InstallerPath, InstallerArgs, '', SW_SHOW, ewWaitUntilTerminated, DotNetInstallExitCode) then
  begin
    Result := 'Setup could not start the Microsoft .NET Framework 3.5 SP1 installer. Please run LuxBurn Setup again.';
    Exit;
  end;

  if (not IsDotNet35Installed()) and (DotNetInstallExitCode = -1073741819) then
  begin
    MsgBox(
      'Microsoft .NET Framework 3.5 SP1 crashed during the automatic install step.' + #13#10 + #13#10 +
      'LuxBurn Setup will now open the Microsoft installer directly. Complete that installer, then return here.',
      mbInformation,
      MB_OK);

    if not Exec(InstallerPath, '/norestart /log "' + DotNetLogPath() + '"', '', SW_SHOW, ewWaitUntilTerminated, DotNetInstallExitCode) then
    begin
      Result := 'Setup could not start the Microsoft .NET Framework 3.5 SP1 installer. Please run LuxBurn Setup again.';
      Exit;
    end;
  end;

  if (DotNetInstallExitCode = 3010) or (DotNetInstallExitCode = 1641) then
  begin
    DotNetRequiresRestart := True;
    NeedsRestart := True;
  end;

  if IsDotNet35Installed() then
    Exit;

  if DotNetRequiresRestart then
    Exit;

  if IsDotNetExitCodeSuccessful(DotNetInstallExitCode) then
    Result := 'Microsoft .NET Framework 3.5 SP1 did not finish registering on this computer. Restart Windows, then run LuxBurn Setup again.'
  else
    Result := DotNetFailureMessage(DotNetInstallExitCode);
end;

function NeedRestart(): Boolean;
begin
  Result := DotNetRequiresRestart;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssPostInstall) and DotNetRequiresRestart then
    MsgBox('Microsoft .NET Framework 3.5 SP1 was installed and Windows must be restarted before LuxBurn can run. Setup will finish now; please restart Windows before opening LuxBurn.', mbInformation, MB_OK);
end;
