#Requires -Version 5.1
<#
.SYNOPSIS
    Universal installer build engine.

.DESCRIPTION
    Called by build-installer.bat. Configuration is read from installer.conf
    (KEY=VALUE format, # comments supported). Do NOT modify this file per project.

    Steps:
      0. Read installer.conf into process environment variables
      1. Validate configuration
      2. Parse version number
      3. Locate / install Inno Setup 6
      4. dotnet publish main app
      5. dotnet publish each Windows service (looped via SERVICE_COUNT)
      6. Generate four ISS fragment files in _build\
      7. Run ISCC to compile the installer
#>

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptDir = $PSScriptRoot
if (-not $scriptDir) {
    $scriptDir = Split-Path $MyInvocation.MyCommand.Path -Parent
}

function Fail([string]$msg) {
    Write-Host ""
    Write-Host "[ERROR] $msg" -ForegroundColor Red
    exit 1
}

# ─────────────────────────────────────────────────────────────────────────────
# 0. Read installer.conf -> set as process environment variables
# ─────────────────────────────────────────────────────────────────────────────
$configFile = Join-Path $scriptDir 'installer.conf'
if (-not (Test-Path $configFile)) {
    Write-Host ""
    Write-Host "[ERROR] installer.conf not found in: $scriptDir" -ForegroundColor Red
    Write-Host "        Copy installer.conf from another project and edit the CONFIG values." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "[Config] Reading installer.conf..."
$lineNum = 0
foreach ($line in (Get-Content $configFile -Encoding UTF8)) {
    $lineNum++
    $trimmed = $line.Trim()
    if ($trimmed -eq '' -or $trimmed.StartsWith('#')) { continue }   # blank / comment
    $eqIdx = $trimmed.IndexOf('=')
    if ($eqIdx -le 0) {
        Write-Host "         [WARN] Line $lineNum ignored (no '='): $trimmed" -ForegroundColor Yellow
        continue
    }
    $key   = $trimmed.Substring(0, $eqIdx).Trim()
    $value = $trimmed.Substring($eqIdx + 1).Trim()
    [System.Environment]::SetEnvironmentVariable($key, $value, 'Process')
}
Write-Host "[Config] Loaded OK  ($configFile)"

# ─────────────────────────────────────────────────────────────────────────────
# 1. Read configuration from environment variables (populated above)
# ─────────────────────────────────────────────────────────────────────────────
$appName          = $env:APP_NAME
$appPublisher     = $env:APP_PUBLISHER
$appPublisherUrl  = if ($env:APP_PUBLISHER_URL) { $env:APP_PUBLISHER_URL } else { '' }
$appId            = $env:APP_ID
$appDotNetMajorRaw = $env:APP_DOTNET_VERSION
$appIconRel       = $env:APP_ICON          # kept as relative path, written as-is into _config.iss
$mainExe          = $env:MAIN_EXE
$mainCsprojRel    = $env:MAIN_CSPROJ
$versionSource    = if ($env:VERSION_SOURCE) { ($env:VERSION_SOURCE).ToLower() } else { '' }
$versionPropsFileRel = $env:VERSION_PROPS_FILE
$versionValue     = $env:VERSION_VALUE
$buildRid         = $env:BUILD_RID
$buildSC          = if ($env:BUILD_SELF_CONTAINED) { ($env:BUILD_SELF_CONTAINED).ToLower() } else { '' }
$serviceCountRaw  = $env:SERVICE_COUNT

# ─────────────────────────────────────────────────────────────────────────────
# 1b. Validate all configuration (moved here from bat for reliability)
# ─────────────────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "[CHECK] Validating configuration..."

if (-not $appName)    { Fail "APP_NAME is empty" }
if (-not $appPublisher) { Fail "APP_PUBLISHER is empty" }
if (-not $appId)      { Fail "APP_ID is empty" }
if (-not $mainExe)    { Fail "MAIN_EXE is empty" }
if (-not $buildRid)   { Fail "BUILD_RID is empty" }

# GUID format
try   { [System.Guid]::Parse($appId) | Out-Null }
catch { Fail "APP_ID is not a valid GUID: '$appId'`nExpected: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`nGenerate: [guid]::NewGuid() in PowerShell" }

# APP_DOTNET_VERSION must be a positive integer
$appDotNetMajor = 0
if (-not [int]::TryParse($appDotNetMajorRaw, [ref]$appDotNetMajor) -or $appDotNetMajor -le 0) {
    Fail "APP_DOTNET_VERSION must be a positive integer (current: '$appDotNetMajorRaw')"
}

# APP_ICON file must exist
$appIconAbs = Join-Path $scriptDir $appIconRel
if (-not (Test-Path $appIconAbs)) { Fail "Icon file not found: $appIconRel" }

# MAIN_CSPROJ must exist
$mainCsproj = Join-Path $scriptDir $mainCsprojRel
if (-not (Test-Path $mainCsproj)) { Fail "Main project file not found: $mainCsprojRel" }

# VERSION_SOURCE enum
if ($versionSource -notin @('auto','manual')) {
    Fail "VERSION_SOURCE must be 'auto' or 'manual' (current: '$versionSource')"
}
if ($versionSource -eq 'auto') {
    $versionPropsFile = Join-Path $scriptDir $versionPropsFileRel
    if (-not (Test-Path $versionPropsFile)) { Fail "Version props file not found: $versionPropsFileRel" }
} else {
    if (-not $versionValue) { Fail "VERSION_VALUE cannot be empty when VERSION_SOURCE=manual" }
}

# BUILD_SELF_CONTAINED enum
if ($buildSC -notin @('true','false')) {
    Fail "BUILD_SELF_CONTAINED must be 'true' or 'false' (current: '$buildSC')"
}

# SERVICE_COUNT non-negative integer
$serviceCount = 0
if (-not [int]::TryParse($serviceCountRaw, [ref]$serviceCount) -or $serviceCount -lt 0) {
    Fail "SERVICE_COUNT must be a non-negative integer (current: '$serviceCountRaw')"
}

# Validate each service
for ($i = 1; $i -le $serviceCount; $i++) {
    $sCsprojRel = [System.Environment]::GetEnvironmentVariable("SERVICE${i}_CSPROJ")
    $sExe    = [System.Environment]::GetEnvironmentVariable("SERVICE${i}_EXE")
    $sSubdir = [System.Environment]::GetEnvironmentVariable("SERVICE${i}_SUBDIR")
    $sName   = [System.Environment]::GetEnvironmentVariable("SERVICE${i}_NAME")
    $sDName  = [System.Environment]::GetEnvironmentVariable("SERVICE${i}_DISPLAY_NAME")
    $sDesc   = [System.Environment]::GetEnvironmentVariable("SERVICE${i}_DESCRIPTION")
    $sSFRaw  = [System.Environment]::GetEnvironmentVariable("SERVICE${i}_SINGLE_FILE")

    if (-not $sCsprojRel) { Fail "SERVICE${i}_CSPROJ is empty" }
    $sCsproj = Join-Path $scriptDir $sCsprojRel
    if (-not (Test-Path $sCsproj))  { Fail "Service $i project not found: $sCsprojRel" }
    if (-not $sExe)    { Fail "SERVICE${i}_EXE is empty" }
    if (-not $sSubdir) { Fail "SERVICE${i}_SUBDIR is empty" }
    if (-not $sName)   { Fail "SERVICE${i}_NAME is empty" }
    if ($sName -match '\s') { Fail "SERVICE${i}_NAME must not contain spaces (current: '$sName')" }
    if (-not $sDName)  { Fail "SERVICE${i}_DISPLAY_NAME is empty" }
    if (-not $sDesc)   { Fail "SERVICE${i}_DESCRIPTION is empty" }
    $sSF = if ($sSFRaw) { $sSFRaw.ToLower() } else { '' }
    if ($sSF -notin @('true','false')) {
        Fail "SERVICE${i}_SINGLE_FILE must be 'true' or 'false' (current: '$sSFRaw')"
    }
}

Write-Host "[CHECK] All configuration OK"

# ─────────────────────────────────────────────────────────────────────────────
# 2. Resolve version number
# ─────────────────────────────────────────────────────────────────────────────
if ($versionSource -eq 'auto') {
    if (-not (Test-Path $versionPropsFile)) {
        Fail "Props file not found: $versionPropsFile"
    }
    $raw = Get-Content -Raw $versionPropsFile
    $m   = [regex]::Match($raw, '<Version>([^<]+)</Version>')
    if (-not $m.Success) {
        Fail "<Version> tag not found in $(Split-Path $versionPropsFile -Leaf)"
    }
    $version = $m.Groups[1].Value.Trim()
    Write-Host "[Version] Read from props file: $version"
}
else {
    $version = $versionValue.Trim()
    if (-not $version) { Fail 'VERSION_VALUE cannot be empty when VERSION_SOURCE=manual' }
    Write-Host "[Version] Manual: $version"
}

# VersionInfoVersion requires four numeric segments (e.g. 1.2.3.0)
$vParts = @($version -split '\.') + @('0', '0', '0', '0')
$v4 = "$($vParts[0]).$($vParts[1]).$($vParts[2]).$($vParts[3])"

# ─────────────────────────────────────────────────────────────────────────────
# 3. Locate / install Inno Setup 6
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
    Write-Host "[INFO] Inno Setup 6 not found, attempting installation..."
    $bundled = Join-Path $scriptDir 'innosetup-6.7.1.exe'
    if (Test-Path $bundled) {
        Write-Host "       Using bundled installer: $bundled"
        Start-Process -FilePath $bundled -ArgumentList '/VERYSILENT', '/NORESTART', '/SUPPRESSMSGBOXES' -Wait
    }
    else {
        Write-Host "       Bundled installer not found, trying winget..."
        & winget install --id JRSoftware.InnoSetup -e -s winget --silent
    }
    $iscc = Find-ISCC
    if (-not $iscc) {
        Fail 'ISCC.exe still not found after installation. Please restart your terminal and retry.'
    }
}
Write-Host "[ISCC] $iscc"

# ─────────────────────────────────────────────────────────────────────────────
# 4. Clean previous build artifacts (publish\, Output\, _build\)
# ─────────────────────────────────────────────────────────────────────────────
$publishDir = Join-Path $scriptDir 'publish'
$outputDir  = Join-Path $scriptDir 'Output'
$buildDir   = Join-Path $scriptDir '_build'

Write-Host ""
Write-Host "[Clean] Removing previous build artifacts..."
foreach ($d in @($publishDir, $outputDir, $buildDir)) {
    if (Test-Path $d) {
        $label = (Resolve-Path $d -ErrorAction SilentlyContinue)
        if (-not $label) { $label = $d }
        Remove-Item $d -Recurse -Force
        Write-Host "        Deleted  $label"
    }
    New-Item $d -ItemType Directory -Force | Out-Null
}
Write-Host "[Clean] Done"

# ─────────────────────────────────────────────────────────────────────────────
# 5. Build summary
# ─────────────────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "================================================================"
Write-Host "  App name   : $appName"
Write-Host "  Publisher  : $appPublisher"
Write-Host "  Version    : $version  (VersionInfo: $v4)"
Write-Host "  Main exe   : $mainExe"
Write-Host "  RID        : $buildRid  SelfContained=$buildSC"
Write-Host "  Services   : $serviceCount"
Write-Host "================================================================"

# ─────────────────────────────────────────────────────────────────────────────
# 6. Publish main app
# ─────────────────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "[1/4] Publishing main app: $(Split-Path $mainCsproj -Leaf)..."
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
if ($LASTEXITCODE -ne 0) { Fail "Main app publish failed (exit code $LASTEXITCODE)" }
Write-Host "       -> publish\Shell\"

# ─────────────────────────────────────────────────────────────────────────────
# 7. Publish Windows services
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
    Write-Host "[2.$i/4] Publishing service $sName ($(Split-Path $sCsproj -Leaf))..."
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
    if ($LASTEXITCODE -ne 0) { Fail "Service $sName publish failed (exit code $LASTEXITCODE)" }
    Write-Host "       -> publish\$sSubdir\"

    $serviceInfos.Add([PSCustomObject]@{
        Exe         = $sExe
        Subdir      = $sSubdir
        Name        = $sName
        DisplayName = $sDName
        Description = $sDesc
    })
}

# ─────────────────────────────────────────────────────────────────────────────
# 8. Generate Inno Setup fragment files into _build\
# ─────────────────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "[3/4] Generating Inno Setup config fragments..."

$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
$dq        = [char]34   # double-quote character, for embedding " inside Pascal string literals

# Sanitize app name for use in output filename
$safeName = $appName -replace '[\\/:*?"<>|]', '' -replace '\s+', '_'

# ── 8a. _config.iss ──────────────────────────────────────────────────────────
# Defines all preprocessor symbols; Setup.iss references them via {#SymbolName}
$cfg = [System.Text.StringBuilder]::new()
[void]$cfg.AppendLine('; Auto-generated -- do not edit manually (generated by Build-Installer.ps1)')
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
# [Files] section entries for service binaries; included inside Setup.iss [Files]
$svcFiles = [System.Text.StringBuilder]::new()
[void]$svcFiles.AppendLine('; Auto-generated -- service file entries')
foreach ($svc in $serviceInfos) {
    [void]$svcFiles.AppendLine("Source: `"publish\$($svc.Subdir)\*`"; DestDir: `"{app}\$($svc.Subdir)`"; Flags: ignoreversion recursesubdirs createallsubdirs")
}
[System.IO.File]::WriteAllText((Join-Path $buildDir '_svc_files.iss'), $svcFiles.ToString(), $utf8NoBom)

# ── 8c. _svc_uninstall.iss ───────────────────────────────────────────────────
# [UninstallRun] entries to stop and delete services on uninstall
$svcUninst = [System.Text.StringBuilder]::new()
[void]$svcUninst.AppendLine('; Auto-generated -- service uninstall commands')
foreach ($svc in $serviceInfos) {
    [void]$svcUninst.AppendLine("Filename: `"{sys}\sc.exe`"; Parameters: `"stop $($svc.Name)`";   Flags: runhidden; RunOnceId: `"SvcStop_$($svc.Name)`"")
    [void]$svcUninst.AppendLine("Filename: `"{sys}\sc.exe`"; Parameters: `"delete $($svc.Name)`"; Flags: runhidden; RunOnceId: `"SvcDel_$($svc.Name)`"")
}
[System.IO.File]::WriteAllText((Join-Path $buildDir '_svc_uninstall.iss'), $svcUninst.ToString(), $utf8NoBom)

# ── 8d. _svc_code.iss ────────────────────────────────────────────────────────
# Pascal procedures included into Setup.iss [Code] section.
# Always defines StopAllServices() and InstallAllServices() so that
# CurStepChanged in Setup.iss can call them unconditionally.
$svcCode = [System.Text.StringBuilder]::new()
[void]$svcCode.AppendLine('// Auto-generated -- service management Pascal code')
[void]$svcCode.AppendLine('')

if ($serviceInfos.Count -eq 0) {
    # No services: provide empty stubs so CurStepChanged compiles
    [void]$svcCode.AppendLine('procedure StopAllServices(); begin end;')
    [void]$svcCode.AppendLine('procedure InstallAllServices(); begin end;')
}
else {
    # StopAllServices: called before file copy to allow overwriting locked binaries
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

    # InstallAllServices: called after files are in place
    [void]$svcCode.AppendLine('procedure InstallAllServices();')
    [void]$svcCode.AppendLine('var')
    [void]$svcCode.AppendLine('  ResultCode: Integer;')
    [void]$svcCode.AppendLine('  BinPath: String;')
    [void]$svcCode.AppendLine('begin')
    foreach ($svc in $serviceInfos) {
        [void]$svcCode.AppendLine("  { Service: $($svc.Name) }")
        # BinPath includes double-quotes so sc.exe handles paths with spaces
        [void]$svcCode.AppendLine("  BinPath := ExpandConstant('${dq}{app}\$($svc.Subdir)\$($svc.Exe)${dq}');")
        # Delete any previous registration (upgrade scenario)
        [void]$svcCode.AppendLine("  Exec(ExpandConstant('{sys}\sc.exe'), 'delete $($svc.Name)', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);")
        [void]$svcCode.AppendLine('  Sleep(500);')
        # Register service
        [void]$svcCode.AppendLine("  Exec(ExpandConstant('{sys}\sc.exe'),")
        [void]$svcCode.AppendLine("    'create $($svc.Name) binPath= ' + BinPath + ' start= auto DisplayName= ${dq}$($svc.DisplayName)${dq}',")
        [void]$svcCode.AppendLine("    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);")
        [void]$svcCode.AppendLine('  if ResultCode = 0 then')
        [void]$svcCode.AppendLine('  begin')
        # Set description
        [void]$svcCode.AppendLine("    Exec(ExpandConstant('{sys}\sc.exe'), 'description $($svc.Name) ${dq}$($svc.Description)${dq}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);")
        # Start service
        [void]$svcCode.AppendLine("    Exec(ExpandConstant('{sys}\sc.exe'), 'start $($svc.Name)', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);")
        [void]$svcCode.AppendLine('  end;')
    }
    [void]$svcCode.AppendLine('end;')
}

[System.IO.File]::WriteAllText((Join-Path $buildDir '_svc_code.iss'), $svcCode.ToString(), $utf8NoBom)

Write-Host "       -> _build\_config.iss"
Write-Host "       -> _build\_svc_files.iss"
Write-Host "       -> _build\_svc_uninstall.iss"
Write-Host "       -> _build\_svc_code.iss"

# ─────────────────────────────────────────────────────────────────────────────
# 9. Compile installer with ISCC
# ─────────────────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "[4/4] Compiling installer..."
$issFile = Join-Path $scriptDir 'Setup.iss'
& $iscc /Q $issFile
if ($LASTEXITCODE -ne 0) { Fail "Inno Setup compilation failed (exit code $LASTEXITCODE)" }

# ─────────────────────────────────────────────────────────────────────────────
# 6. Clean up _build\ (intermediate fragments, no longer needed)
# ─────────────────────────────────────────────────────────────────────────────
Remove-Item $buildDir -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "[Clean] _build\ removed"

$outExe = Join-Path $outputDir "${safeName}_Setup_${version}.exe"
Write-Host ""
Write-Host "================================================================"
Write-Host "  Build complete!"
Write-Host "  Installer: $outExe"
Write-Host "================================================================"
exit 0
