#define MyAppName "RavKavKiosk"
#define MyAppVersion "1.1.0"
#define MyAppPublisher "RavKavKiosk"
#define MyAppExeName "RavKavKiosk.exe"

[Setup]
AppId={{A6B8A4E2-6C7A-4A73-9F41-0F7D2D6C81A9}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\RavKavKiosk
DefaultGroupName={#MyAppName}
OutputDir=..\installer-output
OutputBaseFilename=RavKavKiosk-Setup
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64
ArchitecturesAllowed=x64
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#MyAppExeName}
DisableProgramGroupPage=yes
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "hebrew"; MessagesFile: "compiler:Languages\Hebrew.isl"

[Tasks]
Name: "desktopicon"; Description: "יצירת קיצור דרך בשולחן העבודה"; GroupDescription: "קיצורי דרך:"; Flags: unchecked

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\webview2\MicrosoftEdgeWebView2RuntimeInstallerX64.exe"; Flags: dontcopy

[Icons]
Name: "{group}\עמדת טעינה HopOn"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\עמדת טעינה HopOn"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
  RuntimeInstaller: String;
begin
  if CurStep = ssPostInstall then
  begin
    RuntimeInstaller := ExpandConstant('{tmp}\MicrosoftEdgeWebView2RuntimeInstallerX64.exe');
    ExtractTemporaryFile('MicrosoftEdgeWebView2RuntimeInstallerX64.exe');
    if not Exec(RuntimeInstaller, '/silent /install', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
      MsgBox('לא ניתן להפעיל את מתקין רכיב הדפדפן WebView2. אפשר להתקין אותו מאוחר יותר מאתר Microsoft.', mbInformation, MB_OK)
    else if (ResultCode <> 0) and (ResultCode <> 3010) then
      MsgBox('התקנת רכיב WebView2 לא הושלמה (קוד ' + IntToStr(ResultCode) + '). יש להפעיל את המתקין שוב או להתקין את WebView2 מאתר Microsoft.', mbInformation, MB_OK);
  end;
end;
