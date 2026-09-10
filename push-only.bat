@echo off
chcp 65001 >nul
set "_SELF=%~f0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$m='__PS__';$c=[IO.File]::ReadAllText($env:_SELF);& ([ScriptBlock]::Create($c.Substring($c.LastIndexOf($m)+$m.Length)))"
exit /b %errorlevel%

__PS__
[Console]::OutputEncoding = [Text.Encoding]::UTF8

$API_KEY     = "PowerFocus20240930"
$PUSH_URL    = "https://nuget.powerfocus.com.cn/api/v2/package"
$SCRIPT_DIR  = Split-Path $env:_SELF
$NUPKG_DIR   = Join-Path $SCRIPT_DIR "nupkg"
$SERVER_ROOT = $PUSH_URL -replace '/api/v2/package$', ''
$INDEX_URL   = "$SERVER_ROOT/v3/index.json"

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
#  解析本地包的 Id / Version（读 nupkg 内部 .nuspec，不猜文件名）
# ================================================================
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Get-NupkgIdVersion([string]$path) {
    try {
        $zip = [System.IO.Compression.ZipFile]::OpenRead($path)
        try {
            $entry = $zip.Entries | Where-Object { $_.FullName -match '\.nuspec$' } | Select-Object -First 1
            if (-not $entry) { return $null }
            $sr = New-Object IO.StreamReader($entry.Open())
            try { $text = $sr.ReadToEnd() } finally { $sr.Close() }
            [xml]$xmlDoc = $text
            [pscustomobject]@{ Id = $xmlDoc.package.metadata.id; Version = $xmlDoc.package.metadata.version }
        } finally { $zip.Dispose() }
    } catch { return $null }
}

$pkgInfo = foreach ($pkg in $packages) {
    $info = Get-NupkgIdVersion $pkg.FullName
    [pscustomobject]@{
        Name          = $pkg.Name
        FullName      = $pkg.FullName
        Length        = $pkg.Length
        Id            = $(if ($info) { $info.Id } else { $null })
        Version       = $(if ($info) { $info.Version } else { "" })
        RemoteVersion = $null
        RemoteStatus  = "本地解析失败"
        NeedsPush     = $false
    }
}

# ================================================================
#  查询远程仓库版本（BaGet v3 flat container API）
#  —— 全部走 curl.exe（含 VPN --resolve），不用 Invoke-RestMethod：
#     Windows PowerShell 5.1 的 ServicePointManager 默认协议可能不含
#     TLS1.2，握手容易失败；curl 走系统 schannel，和推送环节保持一致。
#  —— 子进程显式声明 StandardOutputEncoding=UTF8：这里要真正解析 JSON
#     内容（不只是读一个状态码数字），编码不对会出乱码。
# ================================================================
Write-Host "[检测] 正在查询远程仓库版本..." -ForegroundColor DarkGray

$packageBaseUrl = $null
try {
    $idxPsi = New-Object System.Diagnostics.ProcessStartInfo
    $idxPsi.FileName               = "curl.exe"
    $idxPsi.Arguments              = "$resolveArg -s -S --fail --max-time 10 `"$INDEX_URL`""
    $idxPsi.UseShellExecute        = $false
    $idxPsi.RedirectStandardOutput = $true
    $idxPsi.RedirectStandardError  = $true
    $idxPsi.StandardOutputEncoding = [Text.Encoding]::UTF8
    $idxProc = [System.Diagnostics.Process]::Start($idxPsi)
    $idxOut  = $idxProc.StandardOutput.ReadToEnd()
    $idxProc.WaitForExit(10000) | Out-Null
    if ($idxProc.ExitCode -eq 0 -and $idxOut) {
        $idxJson = $idxOut | ConvertFrom-Json
        $res = $idxJson.resources | Where-Object { $_.'@type' -eq 'PackageBaseAddress/3.0.0' } | Select-Object -First 1
        if ($res) { $packageBaseUrl = $res.'@id'.TrimEnd('/') }
    }
} catch { $packageBaseUrl = $null }

$remoteVersions = @{}   # idLower -> string[]（$null 表示查询失败/未知）

if ($packageBaseUrl) {
    $uniqueIds = @($pkgInfo | Where-Object { $_.Id } | Select-Object -ExpandProperty Id -Unique)
    if ($uniqueIds.Count -gt 0) {
        $verJobs = foreach ($id in $uniqueIds) {
            $idLower = $id.ToLowerInvariant()
            $url = "$packageBaseUrl/$idLower/index.json"
            $psi = New-Object System.Diagnostics.ProcessStartInfo
            $psi.FileName               = "curl.exe"
            $psi.Arguments              = "$resolveArg -s -S --max-time 10 `"$url`" -w `"\n%{http_code}`""
            $psi.UseShellExecute        = $false
            $psi.RedirectStandardOutput = $true
            $psi.RedirectStandardError  = $true
            $psi.StandardOutputEncoding = [Text.Encoding]::UTF8
            $proc = [System.Diagnostics.Process]::Start($psi)
            [pscustomobject]@{ IdLower = $idLower; Process = $proc }
        }
        $spinner = '|','/','-','\'; $si = 0
        while ($verJobs | Where-Object { -not $_.Process.HasExited }) {
            $done = ($verJobs | Where-Object { $_.Process.HasExited }).Count
            Write-Host "`r$($spinner[$si % 4])  正在检测远程版本... ($done/$($verJobs.Count))   " -NoNewline
            $si++
            Start-Sleep -Milliseconds 150
        }
        Write-Host "`r  远程版本检测完成。                                          "

        foreach ($j in $verJobs) {
            $out    = $j.Process.StandardOutput.ReadToEnd()
            $lines  = $out.Trim().Split("`n")
            $code   = $lines[-1].Trim()
            if ($code -eq "200") {
                try {
                    $body = ($lines[0..($lines.Count - 2)] -join "`n")
                    $json = $body | ConvertFrom-Json
                    $remoteVersions[$j.IdLower] = @($json.versions)
                } catch { $remoteVersions[$j.IdLower] = $null }
            } elseif ($code -eq "404") {
                $remoteVersions[$j.IdLower] = @()
            } else {
                $remoteVersions[$j.IdLower] = $null
            }
        }
    }
} else {
    Write-Host "[警告] 无法连接远程仓库服务索引，所有包的远程版本标记为未知。" -ForegroundColor Yellow
}

foreach ($p in $pkgInfo) {
    if (-not $p.Id) { continue }   # 本地 nuspec 解析失败，保留默认状态
    if (-not $packageBaseUrl) {
        $p.RemoteStatus = "未知（无法连接仓库）"
        continue
    }
    $idLower = $p.Id.ToLowerInvariant()
    $verList = $remoteVersions[$idLower]
    if ($null -eq $verList) {
        $p.RemoteStatus = "未知（查询失败）"
    } elseif ($verList.Count -eq 0) {
        $p.RemoteStatus  = "远程无此包，需推送"
        $p.NeedsPush     = $true
    } elseif ($verList -contains $p.Version) {
        $p.RemoteVersion = $p.Version
        $p.RemoteStatus  = "已存在，无需推送"
    } else {
        $p.RemoteVersion = $verList[-1]
        $p.RemoteStatus  = "需推送（远程最新 $($verList[-1])）"
        $p.NeedsPush     = $true
    }
}

# --- 打印本地/远程版本对比表 ---
Write-Host ""
Write-Host "本地包 vs 远程仓库版本：" -ForegroundColor Cyan
foreach ($p in $pkgInfo) {
    $color = "DarkGray"
    if ($p.NeedsPush) { $color = "Green" }
    elseif ($p.RemoteStatus -like "未知*" -or $p.RemoteStatus -eq "本地解析失败") { $color = "Yellow" }
    $idDisp = if ($p.Id) { $p.Id } else { $p.Name }
    Write-Host ("  {0,-40} 本地 {1,-10} [{2}]" -f $idDisp, $p.Version, $p.RemoteStatus) -ForegroundColor $color
}
Write-Host ""

$autoList = @($pkgInfo | Where-Object { $_.NeedsPush })

# ================================================================
#  手动勾选交互列表（原有实现，改为函数：接收 $pkgInfo，
#  按 NeedsPush 预选，行尾标注远程状态，返回选中的 pkgInfo 子集）
#  [↑/↓] 移动  [空格] 勾选  [A] 全选  [N] 清空  [回车] 确认  [Esc] 退出
# ================================================================
function Select-PackagesManually($items) {
    Write-Host "请选择要推送的包（共 $($items.Count) 个，已按检测结果预选）：" -ForegroundColor Cyan
    Write-Host "  [↑/↓] 移动   [空格] 勾选   [A] 全选   [N] 清空   [回车] 确认   [Esc] 退出" -ForegroundColor DarkGray
    Write-Host ""

    $selected  = New-Object bool[] $items.Count
    for ($i = 0; $i -lt $items.Count; $i++) { $selected[$i] = $items[$i].NeedsPush }
    $cursorIdx = 0
    $viewTop   = 0

    $maxVisible = [Math]::Max(3, [Console]::WindowHeight - 8)
    if ($maxVisible -gt $items.Count) { $maxVisible = $items.Count }
    $reserve = $maxVisible + 2

    for ($i = 0; $i -lt $reserve; $i++) { Write-Host "" }
    $listStart = [Console]::CursorTop - $reserve
    if ($listStart -lt 0) { $listStart = 0 }

    function Draw-List {
        Set-Cursor 0 $listStart
        $lastVisible = $viewTop + $maxVisible - 1
        for ($row = 0; $row -lt $maxVisible; $row++) {
            $i     = $viewTop + $row
            $pkg   = $items[$i]
            $check = "[ ]"
            if ($selected[$i]) { $check = "[X]" }
            $arrow = " "
            if ($i -eq $cursorIdx) { $arrow = ">" }
            $tail  = ""
            if ($row -eq 0 -and $viewTop -gt 0) {
                $tail = "  ↑ 上面还有 $viewTop 个"
            } elseif ($row -eq $maxVisible - 1 -and $lastVisible -lt $items.Count - 1) {
                $tail = "  ↓ 下面还有 $($items.Count - 1 - $lastVisible) 个"
            }
            $line = "  $arrow $check  $($pkg.Name.PadRight(45))  $(Format-Size $pkg.Length)  [$($pkg.RemoteStatus)]$tail"
            if ($i -eq $cursorIdx)  { Write-Line $line "White" }
            elseif ($selected[$i])  { Write-Line $line "Cyan" }
            else                    { Write-Line $line "DarkGray" }
        }
        $cnt = 0
        foreach ($v in $selected) { if ($v) { $cnt++ } }
        $statusColor = "DarkYellow"
        if ($cnt -gt 0) { $statusColor = "Green" }
        Write-Line ("  已选择: {0} / {1} 个包" -f $cnt, $items.Count) $statusColor
        Write-Line ""
    }

    [Console]::CursorVisible = $false
    Draw-List

    $confirmed = $false
    while (-not $confirmed) {
        $key = [Console]::ReadKey($true)

        switch ($key.Key) {
            "UpArrow"   { if ($cursorIdx -gt 0) { $cursorIdx-- } }
            "DownArrow" { if ($cursorIdx -lt $items.Count - 1) { $cursorIdx++ } }
            "Home"      { $cursorIdx = 0 }
            "End"       { $cursorIdx = $items.Count - 1 }
            "PageUp"    { $cursorIdx = [Math]::Max(0, $cursorIdx - $maxVisible) }
            "PageDown"  { $cursorIdx = [Math]::Min($items.Count - 1, $cursorIdx + $maxVisible) }
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
            if ($cursorIdx -lt $viewTop) { $viewTop = $cursorIdx }
            if ($cursorIdx -gt $viewTop + $maxVisible - 1) { $viewTop = $cursorIdx - $maxVisible + 1 }
            Draw-List
        }
    }

    [Console]::CursorVisible = $true
    Set-Cursor 0 ($listStart + $reserve)
    Write-Host ""

    $result = @()
    for ($i = 0; $i -lt $items.Count; $i++) {
        if ($selected[$i]) { $result += $items[$i] }
    }
    return $result
}

# ================================================================
#  顶层菜单：推送有更新的包 / 推送全部 / 手动勾选
# ================================================================
$toPush = $null
while ($null -eq $toPush) {
    Write-Host "--------------------------------------------------------"
    $col1 = if ($autoList.Count -gt 0) { "White" } else { "DarkGray" }
    Write-Host ("  [1] 推送有更新的包（自动检测出 {0} 个）" -f $autoList.Count) -ForegroundColor $col1
    Write-Host ("  [2] 推送全部（{0} 个）" -f $pkgInfo.Count)
    Write-Host "  [3] 手动勾选（进入交互列表）"
    Write-Host "  [Esc] 退出"
    Write-Host "--------------------------------------------------------"
    $topKey = [Console]::ReadKey($true)

    if ($topKey.Key -eq "Escape") {
        Write-Host ""
        Write-Host "已取消，未推送任何包。" -ForegroundColor Yellow
        Read-Host "按回车键退出"; exit 0
    }

    switch ($topKey.KeyChar) {
        '1' {
            if ($autoList.Count -eq 0) {
                Write-Host "  [!] 没有检测到需要推送的包（远程均已存在，或查询失败请改用手动勾选）。" -ForegroundColor Yellow
                Write-Host ""
                continue
            }
            Write-Host ""
            Write-Host "即将推送以下 $($autoList.Count) 个包：" -ForegroundColor Cyan
            foreach ($p in $autoList) { Write-Host "  + $($p.Name)  [$($p.RemoteStatus)]" -ForegroundColor White }
            Write-Host ""
            Write-Host "按回车确认推送，Esc 返回菜单" -ForegroundColor DarkGray
            $k2 = [Console]::ReadKey($true)
            if ($k2.Key -eq "Enter") { $toPush = $autoList } else { Write-Host "" }
        }
        '2' {
            Write-Host ""
            Write-Host "即将推送全部 $($pkgInfo.Count) 个包：" -ForegroundColor Cyan
            foreach ($p in $pkgInfo) { Write-Host "  + $($p.Name)  [$($p.RemoteStatus)]" -ForegroundColor White }
            Write-Host ""
            Write-Host "按回车确认推送，Esc 返回菜单" -ForegroundColor DarkGray
            $k2 = [Console]::ReadKey($true)
            if ($k2.Key -eq "Enter") { $toPush = @($pkgInfo) } else { Write-Host "" }
        }
        '3' {
            $toPush = @(Select-PackagesManually $pkgInfo)
        }
    }
}

Write-Host ""

# --- 汇总待推送清单 ---
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
