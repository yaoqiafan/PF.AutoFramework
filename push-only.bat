@echo off
chcp 65001 >nul
set "_SELF=%~f0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$m='__PS__';$c=[IO.File]::ReadAllText($env:_SELF);& ([ScriptBlock]::Create($c.Substring($c.LastIndexOf($m)+$m.Length)))"
exit /b %errorlevel%

__PS__
[Console]::OutputEncoding = [Text.Encoding]::UTF8

$API_KEY    = "PowerFocus20240930"
$PUSH_URL   = "https://nuget.powerfocus.com.cn/api/v2/package"
$SCRIPT_DIR = Split-Path $env:_SELF
$NUPKG_DIR  = Join-Path $SCRIPT_DIR "nupkg"

# --- 缺失时自动生成 nuget.config ---
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
    Write-Host "[配置] 已自动生成 nuget.config：$nugetConfig" -ForegroundColor Yellow
} else {
    Write-Host "[配置] nuget.config 已存在" -ForegroundColor DarkGray
}

function Format-Size($b) {
    if ($b -ge 1MB) { return "{0:F2} MB  ({1:F0} KB)" -f ($b/1MB),($b/1KB) }
    return "{0:F1} KB" -f ($b/1KB)
}
function Format-Speed($bps) {
    if ($bps -ge 1MB) { return "{0:F2} MB/s  ({1:F0} KB/s)" -f ($bps/1MB),($bps/1KB) }
    return "{0:F0} KB/s" -f ($bps/1KB)
}

# 安全落笔：目标行钳制在缓冲区范围内，杜绝 SetCursorPosition 越界异常
function Set-Cursor([int]$x, [int]$y) {
    $maxY = [Console]::BufferHeight - 1
    if ($y -gt $maxY) { $y = $maxY }
    if ($y -lt 0)     { $y = 0 }
    [Console]::SetCursorPosition($x, $y)
}

# 整行输出：按显示宽度截断（中文/全角按 2 列计）并右补空格到窗口宽度以内，
# 保证每次调用恰好占一行——不折行、不留残影，重绘区行数才不会错位
function Write-Line([string]$text, [string]$color = "Gray") {
    $max = [Console]::WindowWidth - 1
    $sb  = New-Object Text.StringBuilder
    $w   = 0
    foreach ($ch in $text.ToCharArray()) {
        $cw = 1
        if ([int]$ch -gt 0x2E7F) { $cw = 2 }
        if ($w + $cw -gt $max) { break }
        [void]$sb.Append($ch)
        $w += $cw
    }
    [void]$sb.Append(' ' * ($max - $w))
    Write-Host $sb.ToString() -ForegroundColor $color
}

Write-Host "========================================================"
Write-Host "  PF.AutoFramework NuGet 包推送工具  (PowerFocus)"
Write-Host "========================================================"
Write-Host ""

# --- VPN 检测 ---
$vpnOk = [bool](Test-Connection -ComputerName "10.0.0.1" -Count 1 -Quiet -ErrorAction SilentlyContinue)
$resolveArg = if ($vpnOk) { '--resolve "nuget.powerfocus.com.cn:443:10.0.0.1"' } else { "" }
if ($vpnOk) {
    Write-Host "[VPN]  WireGuard 已连接 —— 通过内网隧道推送" -ForegroundColor Green
} else {
    Write-Host "[直连] 未检测到 VPN —— 通过公网线路推送" -ForegroundColor Yellow
}
Write-Host "目标源 : $PUSH_URL"
Write-Host ""

# --- 扫描待推送的包 ---
$packages = @(Get-ChildItem -Path $NUPKG_DIR -Filter "*.nupkg" -ErrorAction SilentlyContinue | Sort-Object Name)
if ($packages.Count -eq 0) {
    Write-Host "[错误] 目录下没有找到任何 .nupkg 包：$NUPKG_DIR" -ForegroundColor Red
    Write-Host "       请先运行 publish-only.bat 完成编译打包。" -ForegroundColor Red
    Read-Host "按回车键退出"; exit 1
}

# ================================================================
#  交互式多选
#  [↑/↓] 移动  [空格] 勾选  [A] 全选  [N] 清空  [回车] 确认  [Esc] 退出
# ================================================================
Write-Host "请选择要推送的包（共 $($packages.Count) 个）：" -ForegroundColor Cyan
Write-Host "  [↑/↓] 移动   [空格] 勾选   [A] 全选   [N] 清空   [回车] 确认   [Esc] 退出" -ForegroundColor DarkGray
Write-Host ""

$selected  = New-Object bool[] $packages.Count
$cursorIdx = 0
$viewTop   = 0

# 视口最多显示的包行数：窗口装不下全部时列表随光标滚动，绘制区永不超出缓冲区
$maxVisible = [Math]::Max(3, [Console]::WindowHeight - 8)
if ($maxVisible -gt $packages.Count) { $maxVisible = $packages.Count }
$reserve = $maxVisible + 2   # 包列表 + 状态行 + 消息行

# 先写空行把绘制区撑出来，再用实际光标位置反推区顶——
# 撑区过程若触发滚屏，反推出的 listStart 依然准确（旧写法滚屏后就画错行了）
for ($i = 0; $i -lt $reserve; $i++) { Write-Host "" }
$listStart = [Console]::CursorTop - $reserve
if ($listStart -lt 0) { $listStart = 0 }

function Draw-List {
    Set-Cursor 0 $listStart
    $lastVisible = $viewTop + $maxVisible - 1
    for ($row = 0; $row -lt $maxVisible; $row++) {
        $i     = $viewTop + $row
        $pkg   = $packages[$i]
        $check = "[ ]"
        if ($selected[$i]) { $check = "[X]" }
        $arrow = " "
        if ($i -eq $cursorIdx) { $arrow = ">" }
        $tail  = ""
        if ($row -eq 0 -and $viewTop -gt 0) {
            $tail = "  ↑ 上面还有 $viewTop 个"
        } elseif ($row -eq $maxVisible - 1 -and $lastVisible -lt $packages.Count - 1) {
            $tail = "  ↓ 下面还有 $($packages.Count - 1 - $lastVisible) 个"
        }
        $line = "  $arrow $check  $($pkg.Name.PadRight(50))  $(Format-Size $pkg.Length)$tail"
        if ($i -eq $cursorIdx)  { Write-Line $line "White" }
        elseif ($selected[$i])  { Write-Line $line "Cyan" }
        else                    { Write-Line $line "DarkGray" }
    }
    $cnt = 0
    foreach ($v in $selected) { if ($v) { $cnt++ } }
    $statusColor = "DarkYellow"
    if ($cnt -gt 0) { $statusColor = "Green" }
    Write-Line ("  已选择: {0} / {1} 个包" -f $cnt, $packages.Count) $statusColor
    Write-Line ""   # 消息行，平时留空
}

[Console]::CursorVisible = $false
Draw-List

$confirmed = $false
while (-not $confirmed) {
    $key = [Console]::ReadKey($true)

    switch ($key.Key) {
        "UpArrow"   { if ($cursorIdx -gt 0) { $cursorIdx-- } }
        "DownArrow" { if ($cursorIdx -lt $packages.Count - 1) { $cursorIdx++ } }
        "Home"      { $cursorIdx = 0 }
        "End"       { $cursorIdx = $packages.Count - 1 }
        "PageUp"    { $cursorIdx = [Math]::Max(0, $cursorIdx - $maxVisible) }
        "PageDown"  { $cursorIdx = [Math]::Min($packages.Count - 1, $cursorIdx + $maxVisible) }
        "Spacebar"  { $selected[$cursorIdx] = -not $selected[$cursorIdx] }
        "Enter" {
            $selCount = 0
            foreach ($v in $selected) { if ($v) { $selCount++ } }
            if ($selCount -gt 0) {
                $confirmed = $true
            } else {
                Set-Cursor 0 ($listStart + $maxVisible + 1)
                Write-Line "  [!] 请至少勾选一个包再按回车。" "Red"
                Start-Sleep -Milliseconds 800
            }
        }
        "Escape" {
            [Console]::CursorVisible = $true
            Set-Cursor 0 ($listStart + $reserve)
            Write-Host ""
            Write-Host "  已取消，未推送任何包。" -ForegroundColor Yellow
            Read-Host "按回车键退出"; exit 0
        }
    }

    if (-not $confirmed) {
        $ch = [char]::ToUpper($key.KeyChar)
        if ($ch -eq 'A') {
            for ($i = 0; $i -lt $selected.Length; $i++) { $selected[$i] = $true }
        } elseif ($ch -eq 'N') {
            for ($i = 0; $i -lt $selected.Length; $i++) { $selected[$i] = $false }
        }
        # 光标移出视口时滚动列表
        if ($cursorIdx -lt $viewTop) { $viewTop = $cursorIdx }
        if ($cursorIdx -gt $viewTop + $maxVisible - 1) { $viewTop = $cursorIdx - $maxVisible + 1 }
        Draw-List
    }
}

[Console]::CursorVisible = $true
# 光标移到交互区下方，恢复正常滚动输出
Set-Cursor 0 ($listStart + $reserve)
Write-Host ""

# --- 汇总待推送清单 ---
$toPush = @()
for ($i = 0; $i -lt $packages.Count; $i++) {
    if ($selected[$i]) { $toPush += $packages[$i] }
}

Write-Host "--------------------------------------------------------"
Write-Host ("即将推送 {0} 个包：" -f $toPush.Count) -ForegroundColor Cyan
foreach ($pkg in $toPush) {
    Write-Host "  + $($pkg.Name)" -ForegroundColor White
}
$modeText = "单包推送"
if ($toPush.Count -gt 1) { $modeText = "并行推送" }
Write-Host "模式   : $modeText"
Write-Host "--------------------------------------------------------"
Write-Host ""

# --- 并行推送 ---
$totalSize = ($toPush | Measure-Object -Property Length -Sum).Sum

$jobs = foreach ($pkg in $toPush) {
    $startTime = [datetime]::UtcNow
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName               = "curl.exe"
    $psi.Arguments              = "$resolveArg -s -S -X PUT `"$PUSH_URL`" -H `"X-NuGet-ApiKey: $API_KEY`" -F `"package=@$($pkg.FullName)`" -w `"\n%{http_code}`""
    $psi.UseShellExecute        = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError  = $true
    Write-Host "[开始] $($pkg.Name)  [$(Format-Size $pkg.Length)]" -ForegroundColor Cyan
    $proc = [System.Diagnostics.Process]::Start($psi)
    [pscustomobject]@{ Name=$pkg.Name; Length=$pkg.Length; Process=$proc; StartTime=$startTime }
}

$spinner = '|','/','-','\'
$si = 0
while ($jobs | Where-Object { -not $_.Process.HasExited }) {
    $done    = ($jobs | Where-Object {  $_.Process.HasExited }).Count
    $running = $jobs.Count - $done
    Write-Host "`r$($spinner[$si % 4])  正在上传... (已完成 $done/$($jobs.Count)，进行中 $running)   " -NoNewline
    $si++
    Start-Sleep -Milliseconds 200
}
Write-Host "`r  全部上传完成。                                          "
Write-Host ""

$successCnt = 0
$totalTime  = 0.0

foreach ($j in $jobs) {
    $stdout  = $j.Process.StandardOutput.ReadToEnd()
    $elapsed = ([datetime]::UtcNow - $j.StartTime).TotalSeconds
    $lines   = $stdout.Trim().Split("`n")
    $status  = $lines[-1].Trim()
    $col = "Red"
    $statusDesc = "失败"
    if ($status -match "^2") {
        $col = "Green";  $statusDesc = "成功"
    } elseif ($status -eq "409") {
        $col = "Yellow"; $statusDesc = "版本冲突（服务器上已存在同名同版本包）"
    } elseif (-not $status) {
        $statusDesc = "无响应（网络或服务器异常）"
    }
    Write-Host "  $($j.Name)" -ForegroundColor Cyan
    Write-Host "    状态 : $status $statusDesc" -ForegroundColor $col
    Write-Host "    大小 : $(Format-Size $j.Length)"
    Write-Host "    速度 : $(Format-Speed ($j.Length / [Math]::Max($elapsed, 0.001)))"
    Write-Host "    耗时 : $("{0:F2}" -f $elapsed) 秒"
    Write-Host ""
    if ($status -match "^2") { $successCnt++ }
    $totalTime = [Math]::Max($totalTime, $elapsed)
}

$wallSpeed = 0
if ($totalTime -gt 0) { $wallSpeed = $totalSize / $totalTime }
Write-Host "========================================================"
Write-Host "  推送结果汇总"
Write-Host ("  成功     : {0} / {1} 个包" -f $successCnt, $toPush.Count)
Write-Host "  总大小   : $(Format-Size $totalSize)"
Write-Host "  总耗时   : $("{0:F2}" -f $totalTime) 秒"
Write-Host "  平均速度 : $(Format-Speed $wallSpeed)  （所有包合计）"
Write-Host "========================================================"
Read-Host "按回车键退出"
