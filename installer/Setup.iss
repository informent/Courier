#ifndef AppVersion
  #error AppVersion must be supplied
#endif
#ifndef PayloadDir
  #error PayloadDir must be supplied
#endif
#ifndef OutputDir
  #error OutputDir must be supplied
#endif
[Setup]
AppId={{D529A165-6A75-562D-A5AD-28707519748C}
AppName=Courier
AppVersion={#AppVersion}
AppPublisher=informent
AppPublisherURL=https://github.com/informent/Courier
DefaultDirName={localappdata}\Programs\Courier
DefaultGroupName=Courier
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir={#OutputDir}
OutputBaseFilename=Courier-v{#AppVersion}-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\Courier.exe
CloseApplications=yes
CloseApplicationsFilter=Courier.exe
RestartApplications=no
SetupLogging=yes
[Tasks]
Name: desktopicon; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked
[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{autoprograms}\Courier"; Filename: "{app}\Courier.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\Courier"; Filename: "{app}\Courier.exe"; WorkingDir: "{app}"; Tasks: desktopicon
[Run]
Filename: "{app}\Courier.exe"; Description: "Open Courier"; Flags: nowait postinstall skipifsilent unchecked
