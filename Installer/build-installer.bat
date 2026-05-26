@echo off
setlocal enableextensions
pushd "%~dp0"
color 0A

rem =============================================================================
rem  Universal Installer Builder
rem
rem  Usage:
rem    1. Edit installer.conf (the only file you need to change per project)
rem    2. Double-click this file (or run from CMD)
rem
rem  Output: Installer\Output\{AppName}_Setup_{Version}.exe
rem =============================================================================

echo.
echo Calling Build-Installer.ps1...
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "Build-Installer.ps1"
set _EXIT=%ERRORLEVEL%

if %_EXIT% NEQ 0 (
    echo.
    color 0C
    echo Build failed. Exit code: %_EXIT%
    echo See the log above for details.
    popd
    pause
    exit /b 1
)

color 0B
echo.
echo Build complete.  Installer written to Output\
echo.
explorer "Output"
popd
pause
exit /b 0
