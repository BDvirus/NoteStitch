#ifndef PublishDir
  #error PublishDir must point to the dotnet publish directory
#endif

#ifndef MyAppVersion
  #error MyAppVersion must be supplied by the build
#endif

[Setup]
AppId=BDvirus.NoteStitch
AppName=NoteStitch
AppVersion={#MyAppVersion}
AppPublisher=BDvirus
AppPublisherURL=https://github.com/BDvirus/NoteStitch
AppSupportURL=https://github.com/BDvirus/NoteStitch/issues
DefaultDirName={localappdata}\Programs\NoteStitch
DefaultGroupName=NoteStitch
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\artifacts\installer
OutputBaseFilename=NoteStitch-Setup
SetupIconFile=..\NoteStitch\Assets\icon.ico
UninstallDisplayIcon={app}\NoteStitch.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
CloseApplicationsFilter=NoteStitch.exe
RestartApplications=no

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{userprograms}\NoteStitch"; Filename: "{app}\NoteStitch.exe"; WorkingDir: "{app}"; IconFilename: "{app}\NoteStitch.exe"
