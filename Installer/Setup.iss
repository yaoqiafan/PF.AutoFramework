#pragma codepage 65001
; =============================================================================
; 通用 Inno Setup 安装脚本模板
;
; 正常情况下本文件无需修改。
; 所有项目相关配置由 Build-Installer.ps1 生成到 _build\ 目录中：
;   _build\_config.iss      — #define 宏（AppName/AppId/AppVersion/…）
;   _build\_svc_files.iss   — [Files] 节的服务文件条目
;   _build\_svc_uninstall.iss — [UninstallRun] 节的服务卸载命令
;   _build\_svc_code.iss    — [Code] 节的服务管理 Pascal 过程
;
; 构建方式：运行 build-installer.bat
; =============================================================================

; 载入自动生成的配置（所有 #define 均在此文件中定义）
#include "_build\_config.iss"

; =============================================================================
[Setup]
; AppId 使用双花括号转义：{{ → {  最终结果为 {GUID}
AppId={{{#AppId}}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} v{#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppPublisherURL}
VersionInfoVersion={#VersionInfo4}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName} Installer v{#AppVersion}
VersionInfoCopyright=Copyright (C) 2025 {#AppPublisher}

DefaultDirName={autopf}\{#AppPublisher}\{#AppName}
DefaultGroupName={#AppPublisher}\{#AppName}
DisableProgramGroupPage=yes

; 自动按系统区域设置选择语言，命令行可用 /LANG=english 覆盖
ShowLanguageDialog=no
LanguageDetectionMethod=locale

OutputDir=Output
OutputBaseFilename={#OutputBaseName}
SetupIconFile={#SetupIconPath}
UninstallDisplayIcon={#SetupIconPath}

Compression=lzma2/ultra64
SolidCompression=yes

; 安装向导外观（Inno Setup 6.6+ 支持 dark 主题）
WizardStyle=modern dark
WizardResizable=yes
WizardImageFile=Assets\wizard_banner.png
WizardImageBackColor=$002E1B13
WizardBackImageFile=Assets\wizard_bg.png
WizardBackImageOpacity=220

ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763

Uninstallable=yes
UninstallDisplayName={#AppName} {#AppVersion}

; =============================================================================
[Languages]
; 简体中文排在第一位 = 语言对话框的默认选项
Name: "chs";     MessagesFile: "Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

; =============================================================================
[Files]
; 主程序（publish\Shell\ 下所有文件，含 Modules\ 子目录）
Source: "publish\Shell\*"; \
  DestDir: "{app}"; \
  Flags: ignoreversion recursesubdirs createallsubdirs

; Windows 服务文件（无服务时此文件为空，有服务时每行一个 Source 条目）
#include "_build\_svc_files.iss"

; =============================================================================
[Icons]
Name: "{group}\{#AppName}";       Filename: "{app}\{#AppExeName}"
Name: "{group}\卸载 {#AppName}";   Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"

; =============================================================================
[UninstallRun]
; 服务停止/删除命令（无服务时此文件为空）
#include "_build\_svc_uninstall.iss"

; =============================================================================
[Registry]
Root: HKLM; \
  Subkey: "SOFTWARE\{#AppPublisher}\{#AppName}"; \
  ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"; \
  Flags: uninsdeletekey

Root: HKLM; \
  Subkey: "SOFTWARE\{#AppPublisher}\{#AppName}"; \
  ValueType: string; ValueName: "Version"; ValueData: "{#AppVersion}"

; =============================================================================
[Code]

{ ─── .NET 运行时检测 ──────────────────────────────────────────────────────── }

function IsDotNetInstalled(): Boolean;
var
  i: Integer;
  KeyPath: String;
  SubKeys: TArrayOfString;
  FindRec: TFindRec;
  VerPrefix: String;
begin
  Result   := False;
  VerPrefix := IntToStr({#DotNetMajorVer}) + '.';

  { 方法一：注册表（由 .NET 独立运行时安装程序写入） }
  KeyPath := 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App';
  if RegGetSubkeyNames(HKLM, KeyPath, SubKeys) then
    for i := 0 to GetArrayLength(SubKeys) - 1 do
      if Pos(VerPrefix, SubKeys[i]) = 1 then
      begin
        Result := True;
        Exit;
      end;

  { 方法二：文件系统（覆盖 SDK 安装场景） }
  if FindFirst(ExpandConstant('{pf}') + '\dotnet\shared\Microsoft.WindowsDesktop.App\' +
               IntToStr({#DotNetMajorVer}) + '.*', FindRec) then
  begin
    FindClose(FindRec);
    Result := True;
  end;
end;

function InitializeSetup(): Boolean;
var
  MsgResult: Integer;
  Msg, VerStr: String;
begin
  Result := True;
  VerStr := IntToStr({#DotNetMajorVer});

  if not IsDotNetInstalled() then
  begin
    if ActiveLanguage = 'chs' then
      Msg := '未检测到 .NET ' + VerStr + ' Desktop Runtime。' + #13#10#13#10 +
             '本程序需要 .NET ' + VerStr + ' Desktop Runtime，请访问以下地址下载安装后重试：' + #13#10 +
             'https://dotnet.microsoft.com/download/dotnet/' + VerStr + #13#10#13#10 +
             '若已确认安装，点击「是」继续；否则点击「否」退出。'
    else
      Msg := '.NET ' + VerStr + ' Desktop Runtime was not detected.' + #13#10#13#10 +
             'This application requires .NET ' + VerStr + ' Desktop Runtime:' + #13#10 +
             'https://dotnet.microsoft.com/download/dotnet/' + VerStr + #13#10#13#10 +
             'Click Yes to continue anyway, or No to exit.';

    MsgResult := MsgBox(Msg, mbConfirmation, MB_YESNO);
    Result := (MsgResult = IDYES);
  end;
end;

{ ─── 服务管理过程（由 _svc_code.iss 生成，StopAllServices / InstallAllServices）── }
{ 无服务时生成空实现；有服务时生成完整的停止和安装逻辑                               }

#include "_build\_svc_code.iss"

{ ─── 安装步骤钩子 ──────────────────────────────────────────────────────────── }

procedure CurStepChanged(CurStep: TSetupStep);
begin
  case CurStep of
    ssInstall:     StopAllServices();     { 覆盖安装前先停服务 }
    ssPostInstall: InstallAllServices();  { 文件落地后注册并启动服务 }
  end;
end;
