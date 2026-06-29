#define AppVersion "1.0.3"

[Setup]
AppName=HyoPDF
AppVersion={#AppVersion}
AppPublisher=HyoT
AppPublisherURL=https://furss123.github.io/hyot-software-center/ko/
AppSupportURL=https://github.com/furss123/HyoPDF/issues
AppUpdatesURL=https://github.com/furss123/HyoPDF/releases
DefaultDirName={autopf}\HyoT\HyoPDF
DefaultGroupName=HyoT\HyoPDF
AllowNoIcons=yes
OutputDir=..\..\artifacts
OutputBaseFilename=HyoPDF-{#AppVersion}-Setup
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
WizardSizePercent=120

SetupIconFile=..\..\assets\installer\setup-icon.ico
WizardImageFile=..\..\assets\installer\wizard-image.bmp
WizardSmallImageFile=..\..\assets\installer\wizard-banner.bmp

WizardImageStretch=no
WizardImageBackColor=$0A0A0A

PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
MinVersion=10.0.17763
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\HyoPDF.exe
UninstallDisplayName=HyoPDF

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
korean.WelcomeLabel1=HyoPDF 설치를 시작합니다
korean.WelcomeLabel2=HyoT가 만든 빠르고 가벼운 Windows PDF 뷰어 및 편집기입니다.%n%n설치를 계속하려면 다음을 클릭하세요.
korean.FinishedLabel=HyoPDF 설치가 완료되었습니다.%n%n지금 바로 HyoPDF를 실행할 수 있습니다.
korean.TaskDesktopIcon=바탕화면에 아이콘 만들기
korean.TaskFileAssoc=PDF 파일을 HyoPDF로 열기 (기본 앱으로 설정)
korean.TaskGroup=추가 옵션:
korean.LaunchProgram=HyoPDF 지금 실행

english.WelcomeLabel1=Welcome to HyoPDF Setup
english.WelcomeLabel2=A fast and lightweight PDF viewer and editor by HyoT.%n%nClick Next to continue.
english.FinishedLabel=HyoPDF has been successfully installed.%n%nYou can now launch HyoPDF.
english.TaskDesktopIcon=Create a desktop icon
english.TaskFileAssoc=Open PDF files with HyoPDF (set as default)
english.TaskGroup=Additional options:
english.LaunchProgram=Launch HyoPDF now

[Tasks]
Name: "desktopicon"; \
  Description: "{cm:TaskDesktopIcon}"; \
  GroupDescription: "{cm:TaskGroup}"
Name: "fileassoc"; \
  Description: "{cm:TaskFileAssoc}"; \
  GroupDescription: "{cm:TaskGroup}"; \
  Flags: unchecked

[Files]
Source: "..\..\artifacts\publish\win-x64\*"; \
  DestDir: "{app}"; \
  Flags: ignoreversion recursesubdirs createallsubdirs

Source: "..\..\assets\icons\app.ico"; \
  DestDir: "{app}"; \
  Flags: ignoreversion

[Icons]
Name: "{group}\HyoPDF"; \
  Filename: "{app}\HyoPDF.exe"; \
  IconFilename: "{app}\app.ico"
Name: "{group}\{cm:UninstallProgram,HyoPDF}"; \
  Filename: "{uninstallexe}"
Name: "{commondesktop}\HyoPDF"; \
  Filename: "{app}\HyoPDF.exe"; \
  IconFilename: "{app}\app.ico"; \
  Tasks: desktopicon

[Registry]
Root: HKCU; \
  Subkey: "Software\Classes\.pdf\OpenWithProgids"; \
  ValueType: string; ValueName: "HyoPDF.pdf"; \
  ValueData: ""; \
  Tasks: fileassoc

Root: HKCU; \
  Subkey: "Software\Classes\HyoPDF.pdf"; \
  ValueType: string; ValueName: ""; \
  ValueData: "PDF 문서 (HyoPDF)"; \
  Tasks: fileassoc

Root: HKCU; \
  Subkey: "Software\Classes\HyoPDF.pdf\DefaultIcon"; \
  ValueType: string; ValueName: ""; \
  ValueData: "{app}\app.ico"; \
  Tasks: fileassoc

Root: HKCU; \
  Subkey: "Software\Classes\HyoPDF.pdf\shell\open\command"; \
  ValueType: string; ValueName: ""; \
  ValueData: """{app}\HyoPDF.exe"" ""%1"""; \
  Tasks: fileassoc

[Run]
Filename: "{app}\HyoPDF.exe"; \
  Description: "{cm:LaunchProgram}"; \
  Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
end;
