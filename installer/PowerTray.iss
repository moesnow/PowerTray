; PowerTray 安装包脚本（Inno Setup 6）
; 构建方式：先运行 build.ps1（生成 dist\publish），再执行本脚本。
; 当前用户安装（免管理员），安装到 %LocalAppData%\Programs\PowerTray。

#define MyAppName "PowerTray"
; 版本号可由命令行覆盖：ISCC /DMyAppVersion=x.y.z（CI 用）
#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif
#define MyAppPublisher "PowerTray"
#define MyAppExeName "PowerTray.exe"

[Setup]
AppId={{8B3E2C61-4D5A-4F0B-9C2E-7F61A3D0C4A2}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
VersionInfoVersion={#MyAppVersion}
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir=..\dist
OutputBaseFilename=PowerTray-Setup-{#MyAppVersion}
SetupIconFile=..\src\PowerTray\Assets\app.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}
CloseApplications=yes
RestartApplications=no

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加选项:"; Flags: unchecked
Name: "startup"; Description: "开机自动启动 {#MyAppName}"; GroupDescription: "附加选项:"; Flags: checkedonce

[Files]
Source: "..\dist\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autoprograms}\{#MyAppName} 设置"; Filename: "{app}\{#MyAppExeName}"; Parameters: "--settings"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "启动 {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/F /IM {#MyAppExeName}"; Flags: runhidden; RunOnceId: "StopPowerTray"

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
