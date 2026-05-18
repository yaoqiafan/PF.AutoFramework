#pragma codepage 65001
; =============================================================================
; PF.AutoFramework - Inno Setup Script
; Working dir: Installer\ (all relative paths based here)
; Input:  Installer\publish\Shell\  and  Installer\publish\SecsGemService\
; Output: Installer\Output\PFAutoFramework_Setup_x.x.x.exe
; =============================================================================

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

#define AppName           "PF AutoFramework"
#define AppPublisher      "JuLi Intelligence"
#define AppExeName        "PF.Application.Shell.exe"
#define ServiceExe        "PF.SecsGem.Service.exe"
#define ServiceName       "SecsGemService"
#define ServiceDisplayName "PF SECS/GEM 通信服务"

[Setup]
AppId={{A3F2C1D8-7B4E-4F9A-B2E6-8D3C5F1A9E7B}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} v{#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=https://gitee.com/juli-intelligence/PF.AutoFramework
VersionInfoVersion={#AppVersion}.0
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName} Installer v{#AppVersion}
VersionInfoCopyright=Copyright (C) 2025 {#AppPublisher}

DefaultDirName={autopf}\{#AppPublisher}\PFAutoFramework
DefaultGroupName={#AppPublisher}\{#AppName}
DisableProgramGroupPage=yes

; Language selection dialog before wizard
ShowLanguageDialog=yes

OutputDir=Output
OutputBaseFilename=PFAutoFramework_Setup_{#AppVersion}
SetupIconFile=..\PF.Application.Shell\-pfico.ico

; Modern style + dark left-panel background (closest to dark theme without custom images)
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
WizardResizable=yes
WizardImageBackColor=$00222222

ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763

Uninstallable=yes
UninstallDisplayName={#AppName} {#AppVersion}
UninstallDisplayIcon={app}\{#AppExeName}

; =============================================================================
; Chinese Simplified first = default selection in language dialog
; =============================================================================
[Languages]
Name: "chs";     MessagesFile: "Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

; =============================================================================
[Code]

function IsDotNet8Installed(): Boolean;
var
  i: Integer;
  KeyPath: String;
  SubKeys: TArrayOfString;
  FindRec: TFindRec;
begin
  Result := False;

  // Method 1: registry (written by the standalone runtime installer)
  KeyPath := 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App';
  if RegGetSubkeyNames(HKLM, KeyPath, SubKeys) then
    for i := 0 to GetArrayLength(SubKeys) - 1 do
      if Pos('8.', SubKeys[i]) = 1 then
      begin
        Result := True;
        Exit;
      end;

  // Method 2: file system (covers SDK installations where registry may differ)
  if FindFirst(ExpandConstant('{pf}') + '\dotnet\shared\Microsoft.WindowsDesktop.App\8.*', FindRec) then
  begin
    FindClose(FindRec);
    Result := True;
  end;
end;

function InitializeSetup(): Boolean;
var
  MsgResult: Integer;
  Msg: String;
begin
  Result := True;
  if not IsDotNet8Installed() then
  begin
    if ActiveLanguage = 'chs' then
      Msg := '未检测到 .NET 8 Desktop Runtime。' + #13#10#13#10 +
             '本程序需要 .NET 8 Desktop Runtime，请访问以下地址下载安装后重试：' + #13#10 +
             'https://dotnet.microsoft.com/download/dotnet/8.0' + #13#10#13#10 +
             '若已确认安装，点击"是"继续；否则点击"否"退出。'
    else
      Msg := '.NET 8 Desktop Runtime was not detected.' + #13#10#13#10 +
             'This application requires .NET 8 Desktop Runtime:' + #13#10 +
             'https://dotnet.microsoft.com/download/dotnet/8.0' + #13#10#13#10 +
             'Click Yes to continue anyway, or No to exit.';
    MsgResult := MsgBox(Msg, mbConfirmation, MB_YESNO);
    Result := (MsgResult = IDYES);
  end;
end;

procedure StopSecsGemService();
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\sc.exe'), 'stop {#ServiceName}',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(2000);
end;

procedure InstallSecsGemService();
var
  ResultCode: Integer;
  BinPath: String;
begin
  BinPath := ExpandConstant('"{app}\SecsGemService\{#ServiceExe}"');

  Exec(ExpandConstant('{sys}\sc.exe'), 'delete {#ServiceName}',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(500);

  Exec(ExpandConstant('{sys}\sc.exe'),
    'create {#ServiceName} binPath= ' + BinPath +
    ' start= auto DisplayName= "{#ServiceDisplayName}"',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

  if ResultCode = 0 then
  begin
    Exec(ExpandConstant('{sys}\sc.exe'),
      'description {#ServiceName} "PF.AutoFramework SECS/GEM 协议转发代理服务"',
      '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec(ExpandConstant('{sys}\sc.exe'), 'start {#ServiceName}',
      '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  case CurStep of
    ssInstall:     StopSecsGemService();
    ssPostInstall: InstallSecsGemService();
  end;
end;

[Files]
Source: "publish\Shell\*"; \
  DestDir: "{app}"; \
  Flags: ignoreversion recursesubdirs createallsubdirs

Source: "publish\SecsGemService\*"; \
  DestDir: "{app}\SecsGemService"; \
  Flags: ignoreversion recursesubdirs createallsubdirs

Source: "..\DLL\*"; \
  DestDir: "{app}"; \
  Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}";           Filename: "{app}\{#AppExeName}"
Name: "{group}\卸载 {#AppName}";       Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}";     Filename: "{app}\{#AppExeName}"

[UninstallRun]
Filename: "{sys}\sc.exe"; Parameters: "stop {#ServiceName}";   Flags: runhidden; RunOnceId: "SvcStop"
Filename: "{sys}\sc.exe"; Parameters: "delete {#ServiceName}"; Flags: runhidden; RunOnceId: "SvcDel"

[Registry]
Root: HKLM; \
  Subkey: "SOFTWARE\{#AppPublisher}\PFAutoFramework"; \
  ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"; \
  Flags: uninsdeletekey

Root: HKLM; \
  Subkey: "SOFTWARE\{#AppPublisher}\PFAutoFramework"; \
  ValueType: string; ValueName: "Version"; ValueData: "{#AppVersion}"
