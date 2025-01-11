; Before compiling this script, do a PUBLISH (not just release) build. Use both existing publish profiles to build both x64 and x86.

#define MyAppName "SpikeFinder"
#define MyAppPublisher "Tim Cooke"
#define MyAppURL "https://github.com/timothylcooke/SpikeFinder"
#define MyAppExeName "SpikeFinder.exe"
#define PublishDir "SpikeFinder\bin\Publish\net8.0-windows10.0.17763\"
#define MyAppVersion GetVersionNumbersString(PublishDir + 'win-x86\' + MyAppExeName)

#if MyAppVersion != GetVersionNumbersString(PublishDir + 'win-x64\' + MyAppExeName)
    #error Version numbers do not match. Ensure both x86 and x64 executables are built.
#endif

; To make signing the (setup) SpikeFinder.exe, you must use Inno Setup Compiler, and choose "Tools/Configure Sign Tools" from the file menu.
; Add an option with the name of "signtool" and a value of the following:
; "C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x64\signtool.exe" sign /sha1 abcdef0123456789abcdef0123456789abcdef01 /fd sha256 /tr http://timestamp.digicert.com /td sha256 $f
; Note that you may need to adjust the exact path to signtool and the signing certificate's thumbprint. The certificate should be in the current user's Personal certificate store.

[Setup]
AppId={{8084D2EB-2321-4FC1-88DF-5937997D3A9F}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={commonpf}\SpikeFinder
DisableDirPage=yes
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputBaseFilename=SpikeFinderSetup-{#MyAppVersion}
Compression=lzma/ultra64
SolidCompression=yes
OutputDir=SpikeFinder\bin\Setup
ArchitecturesInstallIn64BitMode=x64compatible
VersionInfoVersion={#MyAppVersion}
; SignTool=signtool

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "SpikeFinder\bin\Publish\net8.0-windows10.0.17763\win-x86\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: not Is64BitInstallMode
Source: "SpikeFinder\bin\Publish\net8.0-windows10.0.17763\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: Is64BitInstallMode

[Dirs]
Name: "{commonappdata}\SpikeFinder"; Flags: uninsneveruninstall; Permissions: users-modify

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Run SpikeFinder now"; Flags: postinstall nowait skipifsilent

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; WorkingDir: "{app}"

[Tasks]
Name: desktopicon; Description: {cm:CreateDesktopIcon}; Flags: unchecked
