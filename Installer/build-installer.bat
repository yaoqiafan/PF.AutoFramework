@echo off
chcp 65001 >nul
pushd "%~dp0"
color 0A

:: =============================================================================
::  PF.AutoFramework One-Click Installer Builder
::  Working directory: Installer\  (all paths relative to here)
::
::  Layout:
::    ..\                       -> solution root
::    publish\Shell\            -> dotnet publish output for Shell (temp, gitignored)
::    publish\SecsGemService\   -> dotnet publish output for Service (temp, gitignored)
::    Output\                   -> final installer .exe
:: =============================================================================

:: -- 1. Read version from Directory.Build.props
::    findstr with <> in pattern is mis-parsed by CMD as redirection, so use PowerShell regex via temp file
powershell -NoProfile -Command "[regex]::Match((Get-Content -Raw '..\Directory.Build.props'), '<Version>([^<]+)</Version>').Groups[1].Value" > "%TEMP%\_pfver.txt" 2>nul
set /p VERSION=<"%TEMP%\_pfver.txt"
del "%TEMP%\_pfver.txt" >nul 2>nul

if "%VERSION%"=="" (
    echo [WARN] Cannot read version, using default 1.0.0
    set VERSION=1.0.0
)

:: -- 2. Locate Inno Setup compiler (auto-install via winget if missing)
call :FIND_ISCC
if not "%ISCC%"=="" goto :ISCC_FOUND

echo.
echo [WARN] Inno Setup 6 not found. Installing via winget...
winget install --id JRSoftware.InnoSetup -e -s winget -i
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] winget install failed. Please install manually:
    echo         https://jrsoftware.org/isdl.php
    goto :ERROR_EXIT
)

call :FIND_ISCC
if "%ISCC%"=="" (
    echo [ERROR] Inno Setup 6 still not found. Please restart CMD and retry.
    goto :ERROR_EXIT
)
echo [OK] Inno Setup 6 installed successfully.

:ISCC_FOUND

echo.
echo ================================================================
echo   PF.AutoFramework Installer Build  v%VERSION%
echo   Output: %~dp0Output\
echo ================================================================

:: -- 3. Clean previous artifacts
echo.
echo [1/4] Cleaning old publish directories...
if exist "publish"  rd /s /q "publish"
if exist "Output"   rd /s /q "Output"
mkdir "publish\Shell"
mkdir "publish\SecsGemService"
mkdir "Output"

:: -- 4. Publish main app (Shell)
echo.
echo [2/4] Publishing PF.Application.Shell...
dotnet publish "..\PF.Application.Shell\PF.Application.Shell.csproj" ^
    -c Release ^
    -r win-x64 ^
    --self-contained false ^
    -p:DebugType=none ^
    -p:DebugSymbols=false ^
    -o "publish\Shell" ^
    --nologo -v quiet
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Shell publish failed!
    goto :ERROR_EXIT
)
echo [OK] -^> publish\Shell\

:: -- 5. Publish SECS/GEM service
echo.
echo [3/4] Publishing PF.SecsGem.Service...
dotnet publish "..\PF.SecsGem.Service\PF.SecsGem.Service.csproj" ^
    -c Release ^
    -r win-x64 ^
    --self-contained false ^
    -p:PublishSingleFile=true ^
    -p:DebugType=none ^
    -p:DebugSymbols=false ^
    -o "publish\SecsGemService" ^
    --nologo -v quiet
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] SecsGem service publish failed!
    goto :ERROR_EXIT
)
echo [OK] -^> publish\SecsGemService\

:: -- 6. Compile Inno Setup installer
echo.
echo [4/4] Compiling installer...
"%ISCC%" /DAppVersion=%VERSION% /Q "Setup.iss"
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Inno Setup compilation failed!
    goto :ERROR_EXIT
)

:: -- Done
echo.
color 0B
echo ================================================================
echo   Build complete!
echo.
echo   Installer: %~dp0Output\PFAutoFramework_Setup_%VERSION%.exe
echo.
echo   Copy the Output\ folder to any target PC to install.
echo ================================================================
echo.

explorer "Output"

popd
pause
exit /b 0

:FIND_ISCC
set "ISCC="
if exist "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" set "ISCC=C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if exist "C:\Program Files\Inno Setup 6\ISCC.exe"       set "ISCC=C:\Program Files\Inno Setup 6\ISCC.exe"
if exist "%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" set "ISCC=%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe"
if "%ISCC%"=="" for /f "tokens=*" %%p in ('where ISCC.exe 2^>nul') do if "%ISCC%"=="" set "ISCC=%%p"
exit /b 0

:ERROR_EXIT
color 0C
echo.
echo [FAILED] Build aborted. Check the log above.
popd
pause
exit /b 1
