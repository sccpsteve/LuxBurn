#define AppName "LuxBurn"
#ifndef AppVersion
  #define AppVersion "2.1.2"
#endif
#define AppPublisher "sccpsteve"
#define SourceDir "..\LuxBurn\bin\Release"
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
Source: "{#DotNet40Redist}"; Flags: dontcopy
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

function DotNet40InstallFlag(RootKey: Integer): Boolean;
var
  InstallValue: Cardinal;
begin
  Result := RegQueryDWordValue(RootKey, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Install', InstallValue) and (InstallValue = 1);
end;

function IsDotNet40Installed(): Boolean;
begin
  Result := DotNet40InstallFlag(HKLM) or DotNet40InstallFlag(HKLM32);
  if (not Result) and IsWin64 then
    Result := DotNet40InstallFlag(HKLM64);
end;

function NeedsDotNet40(): Boolean;
begin
  Result := not IsDotNet40Installed();
end;

function CanLaunchLuxBurn(): Boolean;
begin
  Result := IsDotNet40Installed() and (not DotNetRequiresRestart);
end;

function IsDotNetExitCodeSuccessful(ExitCode: Integer): Boolean;
begin
  Result := (ExitCode = 0) or (ExitCode = 3010) or (ExitCode = 1641);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  InstallerPath: String;
begin
  Result := '';
  DotNetInstallAttempted := False;
  DotNetInstallExitCode := 0;
  DotNetRequiresRestart := False;

  if IsDotNet40Installed() then
    Exit;

  DotNetInstallAttempted := True;
  ExtractTemporaryFile('dotNetFx40_Full_x86_x64.exe');
  InstallerPath := ExpandConstant('{tmp}\dotNetFx40_Full_x86_x64.exe');

  if not Exec(InstallerPath, '/passive /norestart', '', SW_SHOW, ewWaitUntilTerminated, DotNetInstallExitCode) then
  begin
    Result := 'Setup could not start the Microsoft .NET Framework 4 installer. Please run LuxBurn Setup again.';
    Exit;
  end;

  if (DotNetInstallExitCode = 3010) or (DotNetInstallExitCode = 1641) then
  begin
    DotNetRequiresRestart := True;
    NeedsRestart := True;
  end;

  if IsDotNet40Installed() then
    Exit;

  if DotNetRequiresRestart then
    Exit;

  if IsDotNetExitCodeSuccessful(DotNetInstallExitCode) then
    Result := 'Microsoft .NET Framework 4 did not finish registering on this computer. Restart Windows, then run LuxBurn Setup again.'
  else
    Result := 'Microsoft .NET Framework 4 did not install successfully. Setup cannot continue until it is installed. Exit code: ' + IntToStr(DotNetInstallExitCode);
end;

function NeedRestart(): Boolean;
begin
  Result := DotNetRequiresRestart;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssPostInstall) and DotNetRequiresRestart then
    MsgBox('Microsoft .NET Framework 4 was installed and Windows must be restarted before LuxBurn can run. Setup will finish now; please restart Windows before opening LuxBurn.', mbInformation, MB_OK);
end;
