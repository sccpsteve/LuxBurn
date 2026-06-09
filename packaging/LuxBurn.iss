#define AppName "LuxBurn"
#ifndef AppVersion
  #define AppVersion "1.5.5"
#endif
#define AppPublisher "sccpsteve"
#define SourceDir "..\LuxBurn\bin\Release"

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
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\LuxBurn"; Filename: "{app}\LuxBurn.exe"; WorkingDir: "{app}"
Name: "{commondesktop}\LuxBurn"; Filename: "{app}\LuxBurn.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\LuxBurn.exe"; Description: "{cm:LaunchProgram,LuxBurn}"; Flags: nowait postinstall skipifsilent
