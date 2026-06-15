@echo off
set "_SELF=%~f0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$m='__PS__';$c=[IO.File]::ReadAllText($env:_SELF);& ([ScriptBlock]::Create($c.Substring($c.LastIndexOf($m)+$m.Length)))"
exit /b %errorlevel%

__PS__
$API_KEY    = "PowerFocus20240930"
$PUSH_URL   = "https://nuget.powerfocus.com.cn/api/v2/package"
$SCRIPT_DIR = Split-Path $env:_SELF
$NUPKG_DIR  = Join-Path $SCRIPT_DIR "nupkg"

# --- nuget.config auto-create ---
$nugetConfig = Join-Path $SCRIPT_DIR "nuget.config"
if (-not (Test-Path $nugetConfig)) {
    $xml = @'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
    <add key="PowerFocus_NuGet" value="https://nuget.powerfocus.com.cn/v3/index.json" />
  </packageSources>
  <packageRestore>
    <add key="enabled" value="True" />
    <add key="automatic" value="False" />
  </packageRestore>
</configuration>
'@
    [IO.File]::WriteAllText($nugetConfig, $xml, [Text.Encoding]::UTF8)
    Write-Host "[Config] nuget.config not found - created at: $nugetConfig" -ForegroundColor Yellow
} else {
    Write-Host "[Config] nuget.config OK" -ForegroundColor DarkGray
}

function Format-Size($b) {
    if ($b -ge 1MB) { return "{0:F2} MB  ({1:F0} KB)" -f ($b/1MB),($b/1KB) }
    return "{0:F1} KB" -f ($b/1KB)
}
function Format-Speed($bps) {
    if ($bps -ge 1MB) { return "{0:F2} MB/s  ({1:F0} KB/s)" -f ($bps/1MB),($bps/1KB) }
    return "{0:F0} KB/s" -f ($bps/1KB)
}

Write-Host "========================================================"
Write-Host "  PF.AutoFramework NuGet Push Tool  (PowerFocus)"
Write-Host "========================================================"
Write-Host ""

$vpnOk = [bool](Test-Connection -ComputerName "10.0.0.1" -Count 1 -Quiet -ErrorAction SilentlyContinue)
$resolveArg = if ($vpnOk) { '--resolve "nuget.powerfocus.com.cn:443:10.0.0.1"' } else { "" }
if ($vpnOk) {
    Write-Host "[VPN]    WireGuard connected  - routing via internal tunnel" -ForegroundColor Green
} else {
    Write-Host "[Direct] VPN not connected    - routing via public internet" -ForegroundColor Yellow
}

$packages = @(Get-ChildItem -Path $NUPKG_DIR -Filter "*.nupkg" -ErrorAction SilentlyContinue)
if ($packages.Count -eq 0) {
    Write-Host "[ERROR] No .nupkg files found in: $NUPKG_DIR" -ForegroundColor Red
    Read-Host "Press Enter to exit"; exit 1
}
Write-Host "Target   : $PUSH_URL"
Write-Host "Packages : $($packages.Count) file(s)"
Write-Host "Mode     : $(if ($packages.Count -gt 1) { 'Parallel' } else { 'Single' })"
Write-Host "--------------------------------------------------------"

$totalSize = ($packages | Measure-Object -Property Length -Sum).Sum

$jobs = foreach ($pkg in $packages) {
    $startTime = [datetime]::UtcNow

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName               = "curl.exe"
    $psi.Arguments              = "$resolveArg -X PUT `"$PUSH_URL`" -H `"X-NuGet-ApiKey: $API_KEY`" -F `"package=@$($pkg.FullName)`" -w `"\n%{http_code}`""
    $psi.UseShellExecute        = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError  = $true

    Write-Host "[Start] $($pkg.Name)  [$(Format-Size $pkg.Length)]" -ForegroundColor Cyan
    $proc = [System.Diagnostics.Process]::Start($psi)

    [pscustomobject]@{
        Name      = $pkg.Name
        Length    = $pkg.Length
        Process   = $proc
        StartTime = $startTime
    }
}

$spinner = '|','/','-','\'
$i = 0
while ($jobs | Where-Object { -not $_.Process.HasExited }) {
    $done    = ($jobs | Where-Object { $_.Process.HasExited }).Count
    $running = $jobs.Count - $done
    Write-Host "`r$($spinner[$i % 4])  Uploading... ($done/$($jobs.Count) done, $running in progress)   " -NoNewline
    $i++
    Start-Sleep -Milliseconds 200
}
Write-Host "`r  All uploads finished.                                  "
Write-Host ""

$successCnt = 0
$totalTime  = 0.0

foreach ($j in $jobs) {
    $stdout  = $j.Process.StandardOutput.ReadToEnd()
    $elapsed = ([datetime]::UtcNow - $j.StartTime).TotalSeconds
    $lines   = $stdout.Trim().Split("`n")
    $status  = $lines[-1].Trim()
    $col     = if ($status -match "^2") { "Green" } elseif ($status -eq "409") { "Yellow" } else { "Red" }

    Write-Host "  $($j.Name)" -ForegroundColor Cyan
    Write-Host "    Status : $status" -ForegroundColor $col
    Write-Host "    Size   : $(Format-Size $j.Length)"
    Write-Host "    Speed  : $(Format-Speed ($j.Length / [Math]::Max($elapsed, 0.001)))"
    Write-Host "    Time   : $("{0:F2}" -f $elapsed) s"
    Write-Host ""

    if ($status -match "^2") { $successCnt++ }
    $totalTime = [Math]::Max($totalTime, $elapsed)
}

$wallSpeed = if ($totalTime -gt 0) { $totalSize / $totalTime } else { 0 }
Write-Host "========================================================"
Write-Host "  Summary"
Write-Host ("  Success    : {0} / {1} packages" -f $successCnt, $jobs.Count)
Write-Host "  Total size : $(Format-Size $totalSize)"
Write-Host "  Wall time  : $("{0:F2}" -f $totalTime) s"
Write-Host "  Throughput : $(Format-Speed $wallSpeed)  (all packages combined)"
Write-Host "========================================================"
Read-Host "Press Enter to exit"
