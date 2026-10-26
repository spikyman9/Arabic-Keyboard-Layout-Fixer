; Script generated for Arabic Keyboard Layout Fixer
#define MyAppName "مُحوّل الكيبورد (Arabic Keyboard Fixer)"
#define MyAppVersion "5.0.0"
#define MyAppPublisher "Arabic Keyboard Fixer"
#define MyAppExeName "ArabicKeyboardFixer.exe"

[Setup]
AppId={{8B8497C1-19B8-4DA4-A152-ED280A3A05F4}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\Arabic Keyboard Fixer
DefaultGroupName=مُحوّل الكيبورد
AllowNoIcons=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog commandline
OutputDir=.
OutputBaseFilename=ArabicKeyboardFixer_v5_Setup
SetupIconFile=app_icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "ar"; MessagesFile: "compiler:Languages\Arabic.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
ar.AutoStartDesc=تشغيل البرنامج تلقائياً مع بدء تشغيل الويندوز
ar.AdditionalDesc=إعدادات إضافية:
en.AutoStartDesc=Start automatically with Windows
en.AdditionalDesc=Additional settings:

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "autostart"; Description: "{cm:AutoStartDesc}"; GroupDescription: "{cm:AdditionalDesc}"

[Files]
Source: "{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "app_icon.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "README.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\مُحوّل الكيبورد (Alt + Q)"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,مُحوّل الكيبورد}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\مُحوّل الكيبورد (Alt + Q)"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKA; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "ArabicKeyboardFixer"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,مُحوّل الكيبورد}"; Flags: nowait postinstall skipifsilent
