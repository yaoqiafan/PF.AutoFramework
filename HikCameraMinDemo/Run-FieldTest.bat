@echo off
setlocal enabledelayedexpansion
cd /d "%~dp0"
title PF HikNetworkCamera Field Test

REM =====================================================================
REM  Hikvision Network Camera (HikNetworkCamera) - one-click field test
REM
REM  Usage A (double click, will ask):   Run-FieldTest.bat
REM  Usage B (command line):
REM      Run-FieldTest.bat --ip 192.168.1.64 --port 8000 --user admin --pwd xxx [--ch 33]
REM
REM  Steps:
REM    [1/3] connectivity check (ping + port 8000)
REM    [2/3] normal capture test + runtime module snapshot (key evidence)
REM    [3/3] capture-buffer stress test (--buf 1, triggers doubling retry)
REM    then  collects all artifacts into ONE zip - just send that zip back.
REM
REM  Notes:
REM    - Password with special chars (^ & ! ' ") may break cmd parsing;
REM      prefer a temporary alphanumeric password for the test.
REM    - No wrong-password case on purpose: Hikvision locks the account
REM      for ~30 min after repeated failures. Run it manually if needed.
REM =====================================================================

set "CAM_IP="
set "CAM_PORT=8000"
set "CAM_USER=admin"
set "CAM_PWD="
set "CAM_CH=0"

:parse
if "%~1"=="" goto parsed
if /i "%~1"=="--ip"   ( set "CAM_IP=%~2"   & shift & shift & goto parse )
if /i "%~1"=="--port" ( set "CAM_PORT=%~2" & shift & shift & goto parse )
if /i "%~1"=="--user" ( set "CAM_USER=%~2" & shift & shift & goto parse )
if /i "%~1"=="--pwd"  ( set "CAM_PWD=%~2"  & shift & shift & goto parse )
if /i "%~1"=="--ch"   ( set "CAM_CH=%~2"   & shift & shift & goto parse )
echo ignoring unknown arg: %~1
shift
goto parse
:parsed

if "%CAM_IP%"=="" set /p CAM_IP=Camera IP:
if "%CAM_USER%"=="" set /p CAM_USER=Username (default admin):
if "%CAM_USER%"=="" set "CAM_USER=admin"
if "%CAM_PWD%"=="" set /p CAM_PWD=Password:
if "%CAM_CH%"==""  set "CAM_CH=0"

echo.
echo Target: %CAM_IP%:%CAM_PORT%  user: %CAM_USER%  channel: %CAM_CH%
echo (password is never written into any log file)
echo.

for /f %%i in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd_HHmmss"') do set "TS=%%i"
set "OUTDIR=FieldTest_Results_%TS%"
mkdir "%OUTDIR%\images" 2>nul
mkdir "%OUTDIR%\FrameworkSaved" 2>nul

REM ---- environment info + native dll presence check ----
> "%OUTDIR%\env.txt" (
    echo Host: %COMPUTERNAME%  Arch: %PROCESSOR_ARCHITECTURE%
    powershell -NoProfile -Command "Get-CimInstance Win32_OperatingSystem | Select-Object Caption,Version,OSArchitecture | Format-List | Out-String"
    echo ===== required native dlls in run directory =====
    for %%f in (HCNetSDK.dll HCCore.dll libcrypto-3-x64.dll libssl-3-x64.dll) do (
        if exist "%%f" ( echo   [OK] %%f ) else ( echo   [MISSING] %%f )
    )
    echo ===== all dlls in run directory =====
    dir /b /a-d *.dll 2>nul
)

echo [1/3] connectivity check %CAM_IP% ...
> "%OUTDIR%\network.txt" (
    echo ===== ping %CAM_IP% =====
    ping -n 2 -w 2000 %CAM_IP%
    echo.
    echo ===== port %CAM_IP%:%CAM_PORT% =====
    powershell -NoProfile -Command "try { (Test-NetConnection -ComputerName '%CAM_IP%' -Port %CAM_PORT% -WarningAction SilentlyContinue) | Select-Object RemoteAddress,RemotePort,TcpTestSucceeded | Format-List | Out-String } catch { $_.Exception.Message }"
)

echo [2/3] normal capture test (with module snapshot) ...

REM 模块快照：并行轮询的 powershell 抓（进程一出现就枚举，真机跑得快也来得及），
REM 后台窗口最小化，抓到后自行退出，最多等 5 秒。
start "" /min powershell -NoProfile -Command "for ($i = 0; $i -lt 50; $i++) { $proc = Get-Process HikCameraMinDemo -ErrorAction SilentlyContinue; if ($proc) { $proc.Modules | ForEach-Object { $_.FileName } | Sort-Object -Unique | Out-File '%OUTDIR%\modules_loaded.txt'; break }; Start-Sleep -Milliseconds 100 }; if (-not (Test-Path '%OUTDIR%\modules_loaded.txt')) { 'snapshot process missed the run (device too fast or exe failed to start - see log)' | Out-File '%OUTDIR%\modules_loaded.txt' }"

REM demo 前台运行：cmd 重定向 + %errorlevel% 保真（Start-Process -PassThru 的 ExitCode 不可靠）
HikCameraMinDemo.exe --ip %CAM_IP% --port %CAM_PORT% --user %CAM_USER% --pwd %CAM_PWD% --ch %CAM_CH% > "%OUTDIR%\caseA_normal.log" 2>&1
set "RC_A=%errorlevel%"
echo       exit code: %RC_A%  (0=all 5 steps OK  2=connect failed  1=exception)

echo [3/3] capture-buffer stress test (--buf 1) ...
HikCameraMinDemo.exe --ip %CAM_IP% --port %CAM_PORT% --user %CAM_USER% --pwd %CAM_PWD% --ch %CAM_CH% --buf 1 > "%OUTDIR%\caseC_buf1.log" 2>&1
set "RC_C=%errorlevel%"
echo       exit code: %RC_C%

REM ---- collect artifacts ----
copy /y demo_*.jpg "%OUTDIR%\images\" >nul 2>&1
xcopy /e /i /y "D:\PFConfig\PFAutoFrameWork\HikCameraMinDemo\NetworkCamera" "%OUTDIR%\FrameworkSaved" >nul 2>&1

> "%OUTDIR%\summary.txt" (
    echo Timestamp: %TS%
    echo Target: %CAM_IP%:%CAM_PORT%  channel: %CAM_CH%
    echo Case A normal capture    exit code: %RC_A%   ^(0=OK 2=connect fail 1=exception^)
    echo Case C buffer stress     exit code: %RC_C%
    echo.
    echo Artifacts:
    echo   env.txt             machine env + 4 native dlls check + full dll list
    echo   network.txt         ping + port connectivity
    echo   caseA_normal.log    normal capture full log (5 steps, serial/channel/bytes/save path)
    echo   modules_loaded.txt  modules actually loaded at runtime (KEY evidence for dll trimming)
    echo   caseC_buf1.log      1MB buffer stress log (expect "doubling retry" then success)
    echo   images\             jpgs captured by the demo
    echo   FrameworkSaved\     jpgs saved by framework ^(copied from D:\PFConfig\...^)
)

powershell -NoProfile -Command "Compress-Archive -Path '%OUTDIR%' -DestinationPath '%OUTDIR%.zip' -Force"

echo.
echo =============================================================
echo  DONE. Send back this single file:
echo    %CD%\%OUTDIR%.zip
echo =============================================================
if "%RC_A%"=="0" ( echo Result: all 5 capture steps succeeded ) else ( echo Result: NOT fully successful - see logs inside the zip )
echo.
pause
