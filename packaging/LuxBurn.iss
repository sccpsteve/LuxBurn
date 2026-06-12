#define AppName "LuxBurn"
#ifndef AppVersion
  #define AppVersion "2.1.8"
#endif
#define AppPublisher "sccpsteve"
#define LegacySourceDir "..\LuxBurn\bin\ReleaseLegacy"
#define ModernSourceDir "..\LuxBurn\bin\ReleaseModern"
#define DotNet35Redist "..\build\redist\dotnetfx35.exe"
#define DotNet40Redist "..\build\redist\dotNetFx40_Full_x86_x64.exe"

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
UninstallDisplayIcon={app}\Modern\LuxBurn.exe
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
Source: "{#DotNet40Redist}"; Flags: dontcopy
Source: "{#LegacySourceDir}\*"; DestDir: "{app}\Legacy"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#ModernSourceDir}\*"; DestDir: "{app}\Modern"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\LuxBurn"; Filename: "{app}\Modern\LuxBurn.exe"; WorkingDir: "{app}\Modern"; Check: IsModernWindows
Name: "{group}\LuxBurn"; Filename: "{app}\Legacy\LuxBurn.exe"; WorkingDir: "{app}\Legacy"; Check: IsLegacyWindows
Name: "{commondesktop}\LuxBurn"; Filename: "{app}\Modern\LuxBurn.exe"; WorkingDir: "{app}\Modern"; Tasks: desktopicon; Check: IsModernWindows
Name: "{commondesktop}\LuxBurn"; Filename: "{app}\Legacy\LuxBurn.exe"; WorkingDir: "{app}\Legacy"; Tasks: desktopicon; Check: IsLegacyWindows

[Run]
Filename: "{app}\Modern\LuxBurn.exe"; Description: "{cm:LaunchProgram,LuxBurn}"; Flags: nowait postinstall skipifsilent; Check: CanLaunchModernLuxBurn
Filename: "{app}\Legacy\LuxBurn.exe"; Description: "{cm:LaunchProgram,LuxBurn}"; Flags: nowait postinstall skipifsilent; Check: CanLaunchLegacyLuxBurn

[Code]
var
  DotNetInstallAttempted: Boolean;
  DotNetInstallExitCode: Integer;
  DotNetRequiresRestart: Boolean;

function IsModernWindows(): Boolean;
var
  Version: TWindowsVersion;
begin
  GetWindowsVersionEx(Version);
  Result := (Version.Major > 6) or ((Version.Major = 6) and (Version.Minor >= 2));
end;

function IsLegacyWindows(): Boolean;
begin
  Result := not IsModernWindows();
end;

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

function DotNet40InstallFlag(RootKey: Integer): Boolean;
var
  InstallValue: Cardinal;
  ReleaseValue: Cardinal;
begin
  Result := RegQueryDWordValue(RootKey, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Install', InstallValue) and (InstallValue = 1);
  if not Result then
    Result := RegQueryDWordValue(RootKey, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', ReleaseValue);
end;

function IsDotNet40Installed(): Boolean;
begin
  Result := DotNet40InstallFlag(HKLM) or DotNet40InstallFlag(HKLM32);
  if (not Result) and IsWin64 then
    Result := DotNet40InstallFlag(HKLM64);
end;

function CanLaunchModernLuxBurn(): Boolean;
begin
  Result := IsModernWindows() and IsDotNet40Installed() and (not DotNetRequiresRestart);
end;

function CanLaunchLegacyLuxBurn(): Boolean;
begin
  Result := IsLegacyWindows() and IsDotNet35Installed() and (not DotNetRequiresRestart);
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
  Result := LogDir + '\dotnet-install.log';
end;

function SelectedRuntimeName(): String;
begin
  if IsModernWindows() then
    Result := 'Microsoft .NET Framework 4'
  else
    Result := 'Microsoft .NET Framework 3.5 SP1';
end;

function DotNetFailureMessage(ExitCode: Integer): String;
begin
  if ExitCode = -1073741819 then
  begin
    Result :=
      SelectedRuntimeName() + ' crashed while installing.' + #13#10 + #13#10 +
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
      SelectedRuntimeName() + ' did not install successfully. Setup cannot continue until it is installed.' + #13#10 + #13#10 +
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

  DotNetInstallAttempted := True;

  if IsModernWindows() then
  begin
    if IsDotNet40Installed() then
      Exit;
    ExtractTemporaryFile('dotNetFx40_Full_x86_x64.exe');
    InstallerPath := ExpandConstant('{tmp}\dotNetFx40_Full_x86_x64.exe');
  end
  else
  begin
    if IsDotNet35Installed() then
      Exit;
    ExtractTemporaryFile('dotnetfx35.exe');
    InstallerPath := ExpandConstant('{tmp}\dotnetfx35.exe');
  end;

  InstallerArgs := '/passive /norestart /log "' + DotNetLogPath() + '"';

  if not Exec(InstallerPath, InstallerArgs, '', SW_SHOW, ewWaitUntilTerminated, DotNetInstallExitCode) then
  begin
    Result := 'Setup could not start the ' + SelectedRuntimeName() + ' installer. Please run LuxBurn Setup again.';
    Exit;
  end;

  if (((IsModernWindows()) and (not IsDotNet40Installed())) or ((IsLegacyWindows()) and (not IsDotNet35Installed()))) and (DotNetInstallExitCode = -1073741819) then
  begin
    MsgBox(
      SelectedRuntimeName() + ' crashed during the automatic install step.' + #13#10 + #13#10 +
      'LuxBurn Setup will now open the Microsoft installer directly. Complete that installer, then return here.',
      mbInformation,
      MB_OK);

    if not Exec(InstallerPath, '/norestart /log "' + DotNetLogPath() + '"', '', SW_SHOW, ewWaitUntilTerminated, DotNetInstallExitCode) then
    begin
      Result := 'Setup could not start the ' + SelectedRuntimeName() + ' installer. Please run LuxBurn Setup again.';
      Exit;
    end;
  end;

  if (DotNetInstallExitCode = 3010) or (DotNetInstallExitCode = 1641) then
  begin
    DotNetRequiresRestart := True;
    NeedsRestart := True;
  end;

  if ((IsModernWindows()) and IsDotNet40Installed()) or ((IsLegacyWindows()) and IsDotNet35Installed()) then
    Exit;

  if DotNetRequiresRestart then
    Exit;

  if IsDotNetExitCodeSuccessful(DotNetInstallExitCode) then
    Result := SelectedRuntimeName() + ' did not finish registering on this computer. Restart Windows, then run LuxBurn Setup again.'
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
    MsgBox(SelectedRuntimeName() + ' was installed and Windows must be restarted before LuxBurn can run. Setup will finish now; please restart Windows before opening LuxBurn.', mbInformation, MB_OK);
end;
