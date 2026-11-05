#define MyAppName "עמדת טעינה HopOn"
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
    RuntimeInstaller := ExpandConstant('{tmp}\\MicrosoftEdgeWebView2RuntimeInstallerX64.exe');
    ExtractTemporaryFile('MicrosoftEdgeWebView2RuntimeInstallerX64.exe');

    if not Exec(RuntimeInstaller, '/silent /install', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    begin
      MsgBox('התקנת רכיב WebView2 נכשלה. ההתקנה תיעצר כדי שלא תישאר תוכנה שאינה יכולה לפעול.', mbCriticalError, MB_OK);
      Abort;
    end;

    if (ResultCode <> 0) and (ResultCode <> 3010) then
    begin
      MsgBox('התקנת רכיב WebView2 נכשלה (קוד ' + IntToStr(ResultCode) + '). ההתקנה תיעצר.', mbCriticalError, MB_OK);
      Abort;
    end;
  end;
end;
