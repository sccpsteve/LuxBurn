#define AppName "LuxBurn"
#ifndef AppVersion
  #define AppVersion "2.1.1"
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
Source: "{#DotNet40Redist}"; DestDir: "{tmp}"; Flags: deleteafterinstall; Check: NeedsDotNet40
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\LuxBurn"; Filename: "{app}\LuxBurn.exe"; WorkingDir: "{app}"
Name: "{commondesktop}\LuxBurn"; Filename: "{app}\LuxBurn.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{tmp}\dotNetFx40_Full_x86_x64.exe"; Parameters: "/passive /norestart"; StatusMsg: "Installing Microsoft .NET Framework 4..."; Flags: waituntilterminated; Check: NeedsDotNet40
Filename: "{app}\LuxBurn.exe"; Description: "{cm:LaunchProgram,LuxBurn}"; Flags: nowait postinstall skipifsilent; Check: CanLaunchLuxBurn

[Code]
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
  Result := IsDotNet40Installed();
end;
