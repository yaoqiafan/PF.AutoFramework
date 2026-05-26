#Requires -Version 5.1
<#
.SYNOPSIS
    通用安装包构建引擎。

.DESCRIPTION
    由 build-installer.bat 调用，所有配置通过环境变量传入，无需直接修改本文件。
    执行流程：
      1. 读取 & 解析版本号
      2. 定位 / 安装 Inno Setup 6
      3. dotnet publish 主程序
      4. dotnet publish 各 Windows 服务（按 SERVICE_COUNT 循环）
      5. 生成 _build\ 目录下的四个 ISS 片段文件
      6. 调用 ISCC 编译安装包
#>

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# $PSScriptRoot 在通过 -File 调用时始终是脚本所在目录
$scriptDir = $PSScriptRoot
if (-not $scriptDir) {
    $scriptDir = Split-Path $MyInvocation.MyCommand.Path -Parent
}

# 工具函数：打印红色错误并退出（退出码 1）
function Fail([string]$msg) {
    Write-Host ""
    Write-Host "[错误] $msg" -ForegroundColor Red
    exit 1
}

# ─────────────────────────────────────────────────────────────────────────────
# 1. 读取配置（环境变量由 build-installer.bat 的 set 命令传入）
# ─────────────────────────────────────────────────────────────────────────────
$appName          = $env:APP_NAME
$appPublisher     = $env:APP_PUBLISHER
$appPublisherUrl  = if ($env:APP_PUBLISHER_URL) { $env:APP_PUBLISHER_URL } else { '' }
$appId            = $env:APP_ID
$appDotNetMajor   = [int]$env:APP_DOTNET_VERSION
$appIconRel       = $env:APP_ICON          # 保持相对路径，直接写入 _config.iss
$mainExe          = $env:MAIN_EXE
$mainCsprojRel    = $env:MAIN_CSPROJ
$versionSource    = ($env:VERSION_SOURCE).ToLower()
$versionPropsFile = Join-Path $scriptDir $env:VERSION_PROPS_FILE
$versionValue     = $env:VERSION_VALUE
$buildRid         = $env:BUILD_RID
$buildSC          = ($env:BUILD_SELF_CONTAINED).ToLower()
$serviceCount     = [int]$env:SERVICE_COUNT

$mainCsproj = Join-Path $scriptDir $mainCsprojRel

# ─────────────────────────────────────────────────────────────────────────────
# 2. 解析版本号
# ─────────────────────────────────────────────────────────────────────────────
if ($versionSource -eq 'auto') {
    if (-not (Test-Path $versionPropsFile)) {
        Fail "找不到 props 文件：$versionPropsFile"
    }
    $raw = Get-Content -Raw $versionPropsFile
    $m   = [regex]::Match($raw, '<Version>([^<]+)</Version>')
    if (-not $m.Success) {
        Fail "在 $(Split-Path $versionPropsFile -Leaf) 中找不到 <Version> 标签"
    }
    $version = $m.Groups[1].Value.Trim()
    Write-Host "[版本] 从 props 文件读取: $version"
}
else {
    $version = $versionValue.Trim()
    if (-not $version) { Fail 'VERSION_SOURCE=manual 时 VERSION_VALUE 不能为空' }
    Write-Host "[版本] 手动指定: $version"
}

# VersionInfoVersion 需要四段数字（如 1.2.3.0）
$vParts = @($version -split '\.') + @('0', '0', '0', '0')
$v4 = "$($vParts[0]).$($vParts[1]).$($vParts[2]).$($vParts[3])"

# ─────────────────────────────────────────────────────────────────────────────
# 3. 定位 / 安装 Inno Setup 6
# ─────────────────────────────────────────────────────────────────────────────
function Find-ISCC {
    $candidates = @(
        'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
        'C:\Program Files\Inno Setup 6\ISCC.exe',
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
    )
    foreach ($c in $candidates) {
        if (Test-Path $c) { return $c }
    }
    $cmd = Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    return $null
}

$iscc = Find-ISCC
if (-not $iscc) {
    Write-Host ""
    Write-Host "[提示] 未找到 Inno Setup 6，尝试安装..."
    $bundled = Join-Path $scriptDir 'innosetup-6.7.1.exe'
    if (Test-Path $bundled) {
        Write-Host "      使用随包安装程序: $bundled"
        Start-Process -FilePath $bundled -ArgumentList '/VERYSILENT', '/NORESTART', '/SUPPRESSMSGBOXES' -Wait
    }
    else {
        Write-Host "      随包文件不存在，尝试 winget..."
        & winget install --id JRSoftware.InnoSetup -e -s winget --silent
    }
    $iscc = Find-ISCC
    if (-not $iscc) {
        Fail 'Inno Setup 安装后仍找不到 ISCC.exe，请重启命令行后重试'
    }
}
Write-Host "[ISCC] $iscc"

# ─────────────────────────────────────────────────────────────────────────────
# 4. 构建摘要
# ─────────────────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "================================================================"
Write-Host "  软件名称 : $appName"
Write-Host "  发布者   : $appPublisher"
Write-Host "  版本号   : $version  (VersionInfo: $v4)"
Write-Host "  主程序   : $mainExe"
Write-Host "  RID      : $buildRid  SelfContained=$buildSC"
Write-Host "  服务数量 : $serviceCount"
Write-Host "================================================================"

# ─────────────────────────────────────────────────────────────────────────────
# 5. 清理旧产物
# ─────────────────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "[1/5] 清理旧产物..."
$publishDir = Join-Path $scriptDir 'publish'
$outputDir  = Join-Path $scriptDir 'Output'
$buildDir   = Join-Path $scriptDir '_build'

foreach ($d in @($publishDir, $outputDir, $buildDir)) {
    if (Test-Path $d) { Remove-Item $d -Recurse -Force -ErrorAction SilentlyContinue }
    New-Item $d -ItemType Directory -Force | Out-Null
}
Write-Host "       publish\  Output\  _build\ 已重置"

# ─────────────────────────────────────────────────────────────────────────────
# 6. Publish 主程序
# ─────────────────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "[2/5] 发布主程序 $(Split-Path $mainCsproj -Leaf)..."
$shellOut = Join-Path $publishDir 'Shell'

$mainArgs = @(
    'publish', $mainCsproj,
    '-c', 'Release',
    '-r', $buildRid,
    '--self-contained', $buildSC,
    '-p:DebugType=none',
    '-p:DebugSymbols=false',
    '-o', $shellOut,
    '--nologo', '-v', 'quiet'
)
& dotnet @mainArgs
if ($LASTEXITCODE -ne 0) { Fail "主程序 publish 失败，退出码 $LASTEXITCODE" }
Write-Host "       → publish\Shell\"

# ─────────────────────────────────────────────────────────────────────────────
# 7. Publish 各 Windows 服务
# ─────────────────────────────────────────────────────────────────────────────
$serviceInfos = [System.Collections.Generic.List[PSCustomObject]]::new()

for ($i = 1; $i -le $serviceCount; $i++) {
    $sCsprojRel = [System.Environment]::GetEnvironmentVariable("SERVICE${i}_CSPROJ")
    $sCsproj    = Join-Path $scriptDir $sCsprojRel
    $sExe       = [System.Environment]::GetEnvironmentVariable("SERVICE${i}_EXE")
    $sSubdir    = [System.Environment]::GetEnvironmentVariable("SERVICE${i}_SUBDIR")
    $sName      = [System.Environment]::GetEnvironmentVariable("SERVICE${i}_NAME")
    $sDName     = [System.Environment]::GetEnvironmentVariable("SERVICE${i}_DISPLAY_NAME")
    $sDesc      = [System.Environment]::GetEnvironmentVariable("SERVICE${i}_DESCRIPTION")
    $sSF        = ([System.Environment]::GetEnvironmentVariable("SERVICE${i}_SINGLE_FILE")).ToLower()

    Write-Host ""
    Write-Host "[3.$i/5] 发布服务 $sName ($(Split-Path $sCsproj -Leaf))..."
    $svcOut = Join-Path $publishDir $sSubdir

    $svcArgs = @(
        'publish', $sCsproj,
        '-c', 'Release',
        '-r', $buildRid,
        '--self-contained', 'false',
        '-p:DebugType=none',
        '-p:DebugSymbols=false',
        '-o', $svcOut,
        '--nologo', '-v', 'quiet'
    )
    if ($sSF -eq 'true') { $svcArgs += '-p:PublishSingleFile=true' }

    & dotnet @svcArgs
    if ($LASTEXITCODE -ne 0) { Fail "服务 $sName publish 失败，退出码 $LASTEXITCODE" }
    Write-Host "       → publish\$sSubdir\"

    $serviceInfos.Add([PSCustomObject]@{
        Exe         = $sExe
        Subdir      = $sSubdir
        Name        = $sName
        DisplayName = $sDName
        Description = $sDesc
    })
}

# ─────────────────────────────────────────────────────────────────────────────
# 8. 生成 Inno Setup 片段文件（写入 _build\ 目录）
# ─────────────────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "[4/5] 生成 Inno Setup 配置片段..."

$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
$dq        = [char]34   # 双引号字符，用于在 Pascal 字符串字面量中嵌入 "

# 去除软件名中文件名非法字符，用作输出文件名前缀
$safeName = $appName -replace '[\\/:*?"<>|]', '' -replace '\s+', '_'

# ── 8a. _config.iss ──────────────────────────────────────────────────────────
#    定义所有 #define，供 Setup.iss 通过 {#Name} 引用
$cfg = [System.Text.StringBuilder]::new()
[void]$cfg.AppendLine('; 自动生成，请勿手动编辑（由 Build-Installer.ps1 生成）')
[void]$cfg.AppendLine("#define AppName         `"$appName`"")
[void]$cfg.AppendLine("#define AppPublisher    `"$appPublisher`"")
[void]$cfg.AppendLine("#define AppPublisherURL `"$appPublisherUrl`"")
[void]$cfg.AppendLine("#define AppId           `"$appId`"")
[void]$cfg.AppendLine("#define AppVersion      `"$version`"")
[void]$cfg.AppendLine("#define VersionInfo4    `"$v4`"")
[void]$cfg.AppendLine("#define AppExeName      `"$mainExe`"")
[void]$cfg.AppendLine("#define DotNetMajorVer  $appDotNetMajor")
[void]$cfg.AppendLine("#define SetupIconPath   `"$appIconRel`"")
[void]$cfg.AppendLine("#define OutputBaseName  `"${safeName}_Setup_$version`"")
[System.IO.File]::WriteAllText((Join-Path $buildDir '_config.iss'), $cfg.ToString(), $utf8NoBom)

# ── 8b. _svc_files.iss ───────────────────────────────────────────────────────
#    [Files] 节的服务文件条目（内嵌到 Setup.iss 的 [Files] 节）
$svcFiles = [System.Text.StringBuilder]::new()
[void]$svcFiles.AppendLine('; 自动生成 — 服务文件列表')
foreach ($svc in $serviceInfos) {
    [void]$svcFiles.AppendLine("Source: `"publish\$($svc.Subdir)\*`"; DestDir: `"{app}\$($svc.Subdir)`"; Flags: ignoreversion recursesubdirs createallsubdirs")
}
[System.IO.File]::WriteAllText((Join-Path $buildDir '_svc_files.iss'), $svcFiles.ToString(), $utf8NoBom)

# ── 8c. _svc_uninstall.iss ───────────────────────────────────────────────────
#    [UninstallRun] 节的服务停止/删除命令（内嵌到 Setup.iss 的 [UninstallRun] 节）
$svcUninst = [System.Text.StringBuilder]::new()
[void]$svcUninst.AppendLine('; 自动生成 — 服务卸载命令')
foreach ($svc in $serviceInfos) {
    [void]$svcUninst.AppendLine("Filename: `"{sys}\sc.exe`"; Parameters: `"stop $($svc.Name)`";   Flags: runhidden; RunOnceId: `"SvcStop_$($svc.Name)`"")
    [void]$svcUninst.AppendLine("Filename: `"{sys}\sc.exe`"; Parameters: `"delete $($svc.Name)`"; Flags: runhidden; RunOnceId: `"SvcDel_$($svc.Name)`"")
}
[System.IO.File]::WriteAllText((Join-Path $buildDir '_svc_uninstall.iss'), $svcUninst.ToString(), $utf8NoBom)

# ── 8d. _svc_code.iss ────────────────────────────────────────────────────────
#    [Code] 节的 Pascal 过程（内嵌到 Setup.iss 的 [Code] 节）
#    无论是否有服务，始终定义 StopAllServices / InstallAllServices 两个过程
#    供 Setup.iss 中的 CurStepChanged 调用
$svcCode = [System.Text.StringBuilder]::new()
[void]$svcCode.AppendLine('; 自动生成 — 服务管理 Pascal 代码')
[void]$svcCode.AppendLine('')

if ($serviceInfos.Count -eq 0) {
    # 无服务：提供空实现，保持 CurStepChanged 可正常调用
    [void]$svcCode.AppendLine('procedure StopAllServices(); begin end;')
    [void]$svcCode.AppendLine('procedure InstallAllServices(); begin end;')
}
else {
    # StopAllServices：安装前停止所有服务
    [void]$svcCode.AppendLine('procedure StopAllServices();')
    [void]$svcCode.AppendLine('var')
    [void]$svcCode.AppendLine('  ResultCode: Integer;')
    [void]$svcCode.AppendLine('begin')
    foreach ($svc in $serviceInfos) {
        [void]$svcCode.AppendLine("  Exec(ExpandConstant('{sys}\sc.exe'), 'stop $($svc.Name)', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);")
    }
    [void]$svcCode.AppendLine('  Sleep(2000);')
    [void]$svcCode.AppendLine('end;')
    [void]$svcCode.AppendLine('')

    # InstallAllServices：安装后注册并启动所有服务
    [void]$svcCode.AppendLine('procedure InstallAllServices();')
    [void]$svcCode.AppendLine('var')
    [void]$svcCode.AppendLine('  ResultCode: Integer;')
    [void]$svcCode.AppendLine('  BinPath: String;')
    [void]$svcCode.AppendLine('begin')
    foreach ($svc in $serviceInfos) {
        [void]$svcCode.AppendLine("  { === $($svc.Name) === }")
        # BinPath 含双引号（供 sc.exe 解析含空格的路径）
        [void]$svcCode.AppendLine("  BinPath := ExpandConstant('${dq}{app}\$($svc.Subdir)\$($svc.Exe)${dq}');")
        # 先删除旧服务注册（升级场景）
        [void]$svcCode.AppendLine("  Exec(ExpandConstant('{sys}\sc.exe'), 'delete $($svc.Name)', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);")
        [void]$svcCode.AppendLine('  Sleep(500);')
        # 注册新服务
        [void]$svcCode.AppendLine("  Exec(ExpandConstant('{sys}\sc.exe'),")
        [void]$svcCode.AppendLine("    'create $($svc.Name) binPath= ' + BinPath + ' start= auto DisplayName= ${dq}$($svc.DisplayName)${dq}',")
        [void]$svcCode.AppendLine("    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);")
        [void]$svcCode.AppendLine('  if ResultCode = 0 then')
        [void]$svcCode.AppendLine('  begin')
        # 写服务描述
        [void]$svcCode.AppendLine("    Exec(ExpandConstant('{sys}\sc.exe'), 'description $($svc.Name) ${dq}$($svc.Description)${dq}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);")
        # 启动服务
        [void]$svcCode.AppendLine("    Exec(ExpandConstant('{sys}\sc.exe'), 'start $($svc.Name)', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);")
        [void]$svcCode.AppendLine('  end;')
    }
    [void]$svcCode.AppendLine('end;')
}

[System.IO.File]::WriteAllText((Join-Path $buildDir '_svc_code.iss'), $svcCode.ToString(), $utf8NoBom)

Write-Host "       → _build\_config.iss"
Write-Host "       → _build\_svc_files.iss"
Write-Host "       → _build\_svc_uninstall.iss"
Write-Host "       → _build\_svc_code.iss"

# ─────────────────────────────────────────────────────────────────────────────
# 9. 编译 Inno Setup 安装包
# ─────────────────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "[5/5] 编译安装包..."
$issFile = Join-Path $scriptDir 'Setup.iss'
& $iscc /Q $issFile
if ($LASTEXITCODE -ne 0) { Fail "Inno Setup 编译失败，退出码 $LASTEXITCODE" }

$outExe = Join-Path $outputDir "${safeName}_Setup_${version}.exe"
Write-Host ""
Write-Host "================================================================"
Write-Host "  构建完成！"
Write-Host "  安装包: $outExe"
Write-Host "================================================================"
exit 0
