#Requires -Version 5.1
<#
.SYNOPSIS
    将旧版（无项目隔离）的配置目录迁移到按项目名隔离的新布局。

.DESCRIPTION
    旧布局：D:\PFConfig\PFAutoFrameWork\*.db, user.config, Recipe\ ...
    新布局：D:\PFConfig\PFAutoFrameWork\{项目名}\*.db, user.config, Recipe\ ...

    本脚本在现场升级时手工执行一次。执行前必须先停止主程序和 SecsGemService。

    为什么整体搬而不是按白名单搬：根目录下除框架自带的 4 个数据库和几个配置文件外，
    还混杂着各下游项目自己产生的文件（Email.db / EnvHistory.db / Storage.db /
    WaferComparison.db / SiloTypes.json / Region\ / AxisPoints\ 等）。白名单一定会漏，
    漏掉的文件在新布局下读不到，等同于配置丢失。

.PARAMETER ProjectName
    项目名，即主程序入口程序集名（如 PF.Application.Shell），
    与安装包里 Installer\installer.conf 的 PROJECT_NAME 一致。

.PARAMETER ConfigRoot
    配置根目录，默认 D:\PFConfig\PFAutoFrameWork。

.PARAMETER Force
    把"服务仍在运行"由中止降级为告警。仅在确认该 SecsGemService 与本机台无关
    （例如机器上装过其它无关软件）时使用；正常情况请先停止服务。

.PARAMETER WhatIf
    只打印将要执行的操作，不实际移动文件。

.EXAMPLE
    .\Migrate-Config.ps1 -ProjectName PF.Application.Shell -WhatIf
    .\Migrate-Config.ps1 -ProjectName PF.Application.Shell
#>

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectName,

    [string]$ConfigRoot = 'D:\PFConfig\PFAutoFrameWork',

    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Fail([string]$msg) {
    Write-Host ""
    Write-Host "[ERROR] $msg" -ForegroundColor Red
    exit 1
}

function Warn([string]$msg) {
    Write-Host "[WARN]  $msg" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "================================================================"
Write-Host "  PF.AutoFramework 配置目录迁移"
Write-Host "  根目录  : $ConfigRoot"
Write-Host "  项目名  : $ProjectName"
Write-Host "================================================================"

# ── 1. 基本校验 ──────────────────────────────────────────────────────────────
if ($ProjectName -match '[\\/:*?"<>|]') { Fail "项目名含非法路径字符: '$ProjectName'" }
if (-not (Test-Path $ConfigRoot))       { Fail "配置根目录不存在: $ConfigRoot" }

$targetDir = Join-Path $ConfigRoot $ProjectName

# ── 2. 确认没有进程占用 ──────────────────────────────────────────────────────
Write-Host ""
Write-Host "[1/5] 检查运行中的进程和服务..."

$runningSvc = @(Get-Service -Name 'SecsGemService*' -ErrorAction SilentlyContinue |
                Where-Object { $_.Status -eq 'Running' })
if ($runningSvc.Count -gt 0) {
    $names = ($runningSvc | ForEach-Object { $_.Name }) -join "`n  "
    if ($WhatIfPreference -or $Force) {
        # -WhatIf 是纯预览，不应被守卫挡住；-Force 用于该服务与本机台无关的场合
        Warn "以下服务仍在运行，正式执行前必须停止：`n  $names"
    }
    else {
        Fail "以下服务仍在运行，请先停止：`n  $names"
    }
}

# SQLite 的 -wal 文件非 0 字节说明有未提交的事务日志，通常意味着进程没有正常退出。
# 直接搬走会丢失 wal 中尚未 checkpoint 的数据。
$dirtyWal = Get-ChildItem -Path $ConfigRoot -Filter '*.db-wal' -File -ErrorAction SilentlyContinue |
            Where-Object { $_.Length -gt 0 }
if ($dirtyWal) {
    Warn "以下 -wal 文件非 0 字节，说明可能仍有进程占用数据库或上次未正常退出："
    $dirtyWal | ForEach-Object { Write-Host "        $($_.Name)  ($($_.Length) bytes)" -ForegroundColor Yellow }
    Warn "建议：确认主程序已完全退出后重新运行本脚本。继续迁移仍会连同 -wal/-shm 一起搬走。"
}
Write-Host "      OK"

# ── 3. 备份 ──────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "[2/5] 备份当前配置..."

$stamp     = Get-Date -Format 'yyyyMMddHHmmss'
$backupZip = Join-Path (Split-Path $ConfigRoot -Parent) "PFAutoFrameWork_backup_$stamp.zip"

if ($PSCmdlet.ShouldProcess($backupZip, "创建备份")) {
    Compress-Archive -Path (Join-Path $ConfigRoot '*') -DestinationPath $backupZip -Force
    Write-Host "      -> $backupZip"
}
else {
    Write-Host "      [WhatIf] 将备份到 $backupZip"
}

# ── 4. 决定要搬哪些条目 ──────────────────────────────────────────────────────
Write-Host ""
Write-Host "[3/5] 枚举待迁移条目..."

# 已经存在的项目子目录不参与迁移：一台机器上可能已经迁移过别的项目。
# 判定依据是目录名恰好等于某个已迁移项目——通过 .pf-project 标记文件识别，
# 首次迁移时由本脚本写入。
# 用 foreach 语句而非 ForEach-Object 管道：管道块内的 += 只会写到块的局部作用域，
# 父作用域的 $existingProjects 拿不到结果。
$existingProjects = @()
foreach ($dir in @(Get-ChildItem -Path $ConfigRoot -Directory -ErrorAction SilentlyContinue)) {
    if (Test-Path (Join-Path $dir.FullName '.pf-project')) {
        $existingProjects += $dir.Name
    }
}
if ($existingProjects.Count -gt 0) {
    Write-Host "      已迁移过的项目目录（跳过）: $($existingProjects -join ', ')"
}

$items = Get-ChildItem -Path $ConfigRoot -Force |
         Where-Object { $_.Name -notin $existingProjects -and $_.Name -ne $ProjectName }

if (-not $items -or $items.Count -eq 0) {
    Write-Host "      根目录下没有待迁移的内容，可能已经迁移过了。" -ForegroundColor Green
    exit 0
}

Write-Host "      共 $($items.Count) 项："
$items | ForEach-Object {
    $kind = if ($_.PSIsContainer) { '[目录]' } else { '[文件]' }
    Write-Host "        $kind $($_.Name)"
}

# ── 5. 执行迁移 ──────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "[4/5] 移动到 $targetDir ..."

if ($PSCmdlet.ShouldProcess($targetDir, "创建目标目录并移动 $($items.Count) 项")) {
    if (-not (Test-Path $targetDir)) {
        New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
    }

    foreach ($item in $items) {
        $dest = Join-Path $targetDir $item.Name
        if (Test-Path $dest) {
            Fail "目标已存在，为避免覆盖已中止：$dest`n请人工确认后处理，备份位于 $backupZip"
        }
        Move-Item -LiteralPath $item.FullName -Destination $dest -Force
        Write-Host "      moved  $($item.Name)"
    }

    # 标记该目录是一个已迁移的项目配置目录，供后续其它项目迁移时识别并跳过
    Set-Content -Path (Join-Path $targetDir '.pf-project') `
                -Value "$ProjectName`nmigrated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" `
                -Encoding utf8
}
else {
    Write-Host "      [WhatIf] 将移动 $($items.Count) 项到 $targetDir"
}

# ── 6. 完成 ──────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "[5/5] 完成"
Write-Host ""
Write-Host "================================================================" -ForegroundColor Green
Write-Host "  迁移完成" -ForegroundColor Green
Write-Host "  配置目录: $targetDir" -ForegroundColor Green
Write-Host "  备份文件: $backupZip" -ForegroundColor Green
Write-Host "================================================================" -ForegroundColor Green
Write-Host ""
Write-Host "后续步骤："
Write-Host "  1. 运行新版安装包（会把 ProjectName 写入服务配置并重新注册 SecsGemService）"
Write-Host "  2. 启动主程序，逐页确认：参数 / 硬件 / 通讯 / 配方 / 报警历史 / 生产记录"
Write-Host "  3. 确认 Splash 阶段没有弹出「配置校验」告警"
Write-Host "  4. 在 SECS 面板确认服务状态为「运行中」，且 SECS 配置数据完整"
Write-Host ""
