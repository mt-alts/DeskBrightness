; DeskBrightness Inno Setup Script
; Requires Inno Setup 6+ (https://jrsoftware.org/isinfo.php)

[Setup]
AppId=DeskBrightness
AppName=DeskBrightness
AppVersion=1.0.0
AppPublisher=Metin Altıkardeş
AppPublisherURL=https://github.com/mt-alts/DeskBrightness
AppSupportURL=https://github.com/mt-alts/DeskBrightness
AppUpdatesURL=https://github.com/mt-alts/DeskBrightness
DefaultDirName={autopf64}\DeskBrightness
DefaultGroupName=DeskBrightness
OutputDir=.
OutputBaseFilename=DeskBrightness.Setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
DisableProgramGroupPage=yes
CloseApplications=force
SetupIconFile=..\src\DeskBrightness\DeskBrightness.ico
UninstallDisplayIcon={app}\DeskBrightness.exe
UninstallDisplayName=DeskBrightness

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional icons:"; Flags: checkedonce; Check: ShouldShowTask('desktopicon')

[Files]
Source: "..\src\DeskBrightness\bin\Release\net10.0-windows10.0.26100.0\DeskBrightness.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\src\DeskBrightness\bin\Release\net10.0-windows10.0.26100.0\DeskBrightness.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\src\DeskBrightness\bin\Release\net10.0-windows10.0.26100.0\DeskBrightness.deps.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\src\DeskBrightness\bin\Release\net10.0-windows10.0.26100.0\DeskBrightness.runtimeconfig.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\src\DeskBrightness\bin\Release\net10.0-windows10.0.26100.0\DeskBrightness.ico"; DestDir: "{app}"; Flags: ignoreversion

; Project DLLs
Source: "..\src\DeskBrightness\bin\Release\net10.0-windows10.0.26100.0\DeskBrightness.Adb.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\src\DeskBrightness\bin\Release\net10.0-windows10.0.26100.0\DeskBrightness.Core.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\src\DeskBrightness\bin\Release\net10.0-windows10.0.26100.0\DeskBrightness.Win.dll"; DestDir: "{app}"; Flags: ignoreversion

; Third-party DLLs
Source: "..\src\DeskBrightness\bin\Release\net10.0-windows10.0.26100.0\AdvancedSharpAdbClient.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\src\DeskBrightness\bin\Release\net10.0-windows10.0.26100.0\Microsoft.Extensions.DependencyInjection.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\src\DeskBrightness\bin\Release\net10.0-windows10.0.26100.0\Microsoft.Extensions.DependencyInjection.Abstractions.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\src\DeskBrightness\bin\Release\net10.0-windows10.0.26100.0\System.Management.dll"; DestDir: "{app}"; Flags: ignoreversion

; WinRT support
Source: "..\src\DeskBrightness\bin\Release\net10.0-windows10.0.26100.0\Microsoft.Windows.SDK.NET.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\src\DeskBrightness\bin\Release\net10.0-windows10.0.26100.0\WinRT.Runtime.dll"; DestDir: "{app}"; Flags: ignoreversion

; ADB binaries
Source: "..\src\DeskBrightness\bin\Release\net10.0-windows10.0.26100.0\adb.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\src\DeskBrightness\bin\Release\net10.0-windows10.0.26100.0\AdbWinApi.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\src\DeskBrightness\bin\Release\net10.0-windows10.0.26100.0\AdbWinUsbApi.dll"; DestDir: "{app}"; Flags: ignoreversion

; Native runtimes
Source: "..\src\DeskBrightness\bin\Release\net10.0-windows10.0.26100.0\runtimes\*"; DestDir: "{app}\runtimes"; Flags: ignoreversion recursesubdirs createallsubdirs

; Resources
Source: "..\src\DeskBrightness\bin\Release\net10.0-windows10.0.26100.0\Resources\licenses.json"; DestDir: "{app}\Resources"; Flags: ignoreversion

; Headless mobile JAR
Source: "..\src\DeskBrightness\bin\Release\net10.0-windows10.0.26100.0\deskbrightness-headless.jar"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\DeskBrightness"; Filename: "{app}\DeskBrightness.exe"
Name: "{autodesktop}\DeskBrightness"; Filename: "{app}\DeskBrightness.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\DeskBrightness"; ValueType: string; ValueName: "Language"; ValueData: "{language}"; Flags: createvalueifdoesntexist

[Run]
Filename: "{app}\DeskBrightness.exe"; Parameters: "--lang {language}"; Description: "Launch DeskBrightness"; Flags: postinstall nowait

[UninstallRun]
Filename: "taskkill"; Parameters: "/F /IM DeskBrightness.exe"; Flags: runhidden

[Code]
var
  IsUpdateMode: Boolean;

function CmdLineParamExists(const Value: string): Boolean;
var
  I: Integer;
begin
  Result := False;
  for I := 1 to ParamCount do
    if CompareText(ParamStr(I), Value) = 0 then
    begin
      Result := True;
      Exit;
    end;
end;

procedure SaveTaskState(const TaskName: string; State: Integer);
begin
  RegWriteDWordValue(HKCU, 'Software\DeskBrightness\Tasks', TaskName, LongWord(State));
end;

function LoadTaskState(const TaskName: string; Default: Integer): Integer;
var
  Value: LongWord;
begin
  if RegQueryDWordValue(HKCU, 'Software\DeskBrightness\Tasks', TaskName, Value) then
    Result := Integer(Value)
  else
    Result := Default;
end;

function ShouldShowTask(const TaskName: string): Boolean;
begin
  if IsUpdateMode then
    Result := LoadTaskState(TaskName, 1) <> 0
  else
    Result := True;
end;

function InitializeSetup: Boolean;
begin
  IsUpdateMode := CmdLineParamExists('--update');
  Result := True;
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := IsUpdateMode and ((PageID = wpSelectDir) or (PageID = wpSelectProgramGroup) or (PageID = wpSelectTasks));
end;

function UpdateReadyMemo(Space, NewLine, MemoUserInfoInfo, MemoDirInfo, MemoTypeInfo, MemoComponentsInfo, MemoGroupInfo, MemoTasksInfo: String): String;
var
  S: String;
begin
  S := '';
  if IsUpdateMode then
    S := S + 'Update mode: existing installation will be updated.' + NewLine + NewLine;
  S := S + MemoDirInfo + NewLine + NewLine;
  S := S + MemoGroupInfo;
  if not IsUpdateMode then
    S := S + NewLine + NewLine + MemoTasksInfo;
  Result := S;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
  begin
    if IsUpdateMode then
      Log('Running in update mode');

    if IsTaskSelected('desktopicon') then
      SaveTaskState('desktopicon', 1)
    else
      SaveTaskState('desktopicon', 0);
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ResultCode: Integer;
begin
  if CurUninstallStep = usUninstall then
    Exec('taskkill', '/F /IM DeskBrightness.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;
