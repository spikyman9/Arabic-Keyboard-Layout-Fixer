; Inno Setup script for Arabic Keyboard Fixer
#define MyAppName "مُحوّل الكيبورد (Arabic Keyboard Fixer)"
#define MyAppVersion "1.1.0"
#define MyAppPublisher "Mohamed Rashad"
#define MyAppExeName "ArabicKeyboardFixer.exe"

[Setup]
AppId={{8B8497C1-19B8-4DA4-A152-ED280A3A05F4}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
VersionInfoVersion={#MyAppVersion}
DefaultDirName={autopf}\Arabic Keyboard Fixer
DefaultGroupName=مُحوّل الكيبورد
DisableProgramGroupPage=yes
; Per-user install: no admin rights needed, and the startup entry lives in HKCU where the app manages it.
PrivilegesRequired=lowest
OutputDir=.
OutputBaseFilename=ArabicKeyboardFixer_v{#MyAppVersion}_Setup
SetupIconFile=app_icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "ar"; MessagesFile: "compiler:Languages\Arabic.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
ar.AutoStartDesc=تشغيل البرنامج تلقائياً مع بدء تشغيل ويندوز
ar.AdditionalDesc=إعدادات إضافية:
ar.NeedDotNet=البرنامج محتاج .NET Framework 4.5 أو أحدث، وهو مش متثبت على الجهاز ده.%n%nثبته من موقع مايكروسوفت وبعدين شغل التثبيت تاني.
en.AutoStartDesc=Start automatically with Windows
en.AdditionalDesc=Additional settings:
en.NeedDotNet=This program needs .NET Framework 4.5 or later, which is not installed.%n%nPlease install it from Microsoft and run setup again.

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "autostart"; Description: "{cm:AutoStartDesc}"; GroupDescription: "{cm:AdditionalDesc}"

[Files]
Source: "{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "app_icon.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\مُحوّل الكيبورد (Alt + Q)"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,مُحوّل الكيبورد}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\مُحوّل الكيبورد (Alt + Q)"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "ArabicKeyboardFixer"; ValueData: """{app}\{#MyAppExeName}"" --autostart"; Tasks: autostart

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,مُحوّل الكيبورد}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Stop the tray app so its files can be removed, then remove the startup entry even if it was enabled later from the app menu.
Filename: "{sys}\taskkill.exe"; Parameters: "/f /im {#MyAppExeName}"; Flags: runhidden; RunOnceId: "KillApp"
Filename: "{sys}\reg.exe"; Parameters: "delete ""HKCU\Software\Microsoft\Windows\CurrentVersion\Run"" /v ArabicKeyboardFixer /f"; Flags: runhidden; RunOnceId: "DelRun"
Filename: "{sys}\reg.exe"; Parameters: "delete ""HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run"" /v ArabicKeyboardFixer /f"; Flags: runhidden; RunOnceId: "DelApproved"

[UninstallDelete]
Type: filesandordirs; Name: "{userappdata}\ArabicKeyboardFixer"

[Code]
function InitializeSetup(): Boolean;
begin
  Result := IsDotNetInstalled(net45, 0);
  if not Result then
    MsgBox(CustomMessage('NeedDotNet'), mbCriticalError, MB_OK);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  { Close a running copy so its exe can be replaced during an upgrade. }
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/f /im {#MyAppExeName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := '';
end;
