#define MyAppName "עמדת טעינה רב-קו"
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

[Dirs]
; User can manually place branding images here after installation.
Name: "{app}\Assets"; Permissions: users-modify

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\webview2\MicrosoftEdgeWebView2RuntimeInstallerX64.exe"; Flags: dontcopy

[Icons]
Name: "{group}\עמדת טעינה HopOn"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\עמדת טעינה HopOn"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Code]
function HasValidRuntimeVersion(const Version: String): Boolean;
begin
  Result := (Version <> '') and (Version <> '0.0.0.0');
end;

function IsWebView2RuntimeInstalled: Boolean;
var
  RuntimeVersion: String;
begin
  Result := False;
  RuntimeVersion := '';

  // Microsoft documents this key for 64-bit Windows machine-wide installs.
  if RegQueryStringValue(HKLM64,
       'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}',
       'pv', RuntimeVersion) and HasValidRuntimeVersion(RuntimeVersion) then
  begin
    Result := True;
    Exit;
  end;

  RuntimeVersion := '';
  if RegQueryStringValue(HKCU,
       'Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}',
       'pv', RuntimeVersion) and HasValidRuntimeVersion(RuntimeVersion) then
    Result := True;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
  RuntimeInstaller: String;
begin
  if CurStep <> ssPostInstall then
    Exit;

  if IsWebView2RuntimeInstalled then
  begin
    Log('WebView2 Runtime already exists. Skipping runtime installation.');
    Exit;
  end;

  Log('WebView2 Runtime is missing. Starting bundled offline installer.');
  RuntimeInstaller := ExpandConstant('{tmp}\MicrosoftEdgeWebView2RuntimeInstallerX64.exe');
  ExtractTemporaryFile('MicrosoftEdgeWebView2RuntimeInstallerX64.exe');

  if not Exec(RuntimeInstaller, '/silent /install', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    Log('Failed to launch WebView2 Runtime installer.');
    MsgBox('התקנת רכיב WebView2 נכשלה. ההתקנה תיעצר כדי שלא תישאר תוכנה שאינה יכולה לפעול.', mbCriticalError, MB_OK);
    Abort;
  end;

  Log('WebView2 Runtime installer exit code: ' + IntToStr(ResultCode));
  if (ResultCode <> 0) and (ResultCode <> 3010) then
  begin
    MsgBox('התקנת רכיב WebView2 נכשלה (קוד ' + IntToStr(ResultCode) + '). ההתקנה תיעצר.', mbCriticalError, MB_OK);
    Abort;
  end;
end;
