#define MyAppName "עמדת טעינת רב-קו — מערכת חדשה"
#ifndef MyAppVersion
  #define MyAppVersion "2.0.0"
#endif
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
OutputBaseFilename=RavKavKiosk-V2-Setup
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
Source: "..\publish-v2\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\webview2\MicrosoftEdgeWebView2RuntimeInstallerX64.exe"; Flags: dontcopy

[Icons]
Name: "{group}\עמדת טעינה רב-קו — מערכת חדשה"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\עמדת טעינה רב-קו — מערכת חדשה"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Code]
function HasRuntimeVersion(const Version: String): Boolean;
begin
  Result := (Version <> '') and (Version <> '0.0.0.0');
end;

function IsWebView2RuntimeInstalled: Boolean;
var
  RuntimeVersion: String;
begin
  Result := False;
  RuntimeVersion := '';
  if RegQueryStringValue(HKLM64,
       'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}',
       'pv', RuntimeVersion) and HasRuntimeVersion(RuntimeVersion) then
  begin
    Result := True;
    Exit;
  end;
  RuntimeVersion := '';
  if RegQueryStringValue(HKCU,
       'Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}',
       'pv', RuntimeVersion) and HasRuntimeVersion(RuntimeVersion) then
    Result := True;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
  RuntimeInstaller: String;
begin
  if CurStep <> ssPostInstall then Exit;
  if IsWebView2RuntimeInstalled then Exit;
  RuntimeInstaller := ExpandConstant('{tmp}\MicrosoftEdgeWebView2RuntimeInstallerX64.exe');
  ExtractTemporaryFile('MicrosoftEdgeWebView2RuntimeInstallerX64.exe');
  if not Exec(RuntimeInstaller, '/silent /install', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    Abort;
  if (ResultCode <> 0) and (ResultCode <> 3010) then Abort;
end;
