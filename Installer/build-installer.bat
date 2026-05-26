@echo off
chcp 65001 >nul
setlocal enableextensions
pushd "%~dp0"
color 0A

:: =============================================================================
::  通用安装包构建入口
::
::  使用说明：
::    1. 修改下方"配置区"中的变量（每行都有中文注释）
::    2. 直接双击运行本文件，或在 CMD 中执行
::    3. 所有其他脚本（Build-Installer.ps1 / Setup.iss）无需改动
::
::  输出位置：Installer\Output\{AppName}_Setup_{Version}.exe
:: =============================================================================


:: ╔═══════════════════════════════════════════════════════════════════════════╗
:: ║                      ★ 配置区（按项目实际情况修改）★                        ║
:: ╚═══════════════════════════════════════════════════════════════════════════╝

:: ─── 应用基本信息 ─────────────────────────────────────────────────────────────

:: 安装程序中显示的软件名称（出现在标题栏、开始菜单、"添加或删除程序"列表）
set APP_NAME=PF AutoFramework

:: 发布者/公司名，用于开始菜单分组路径和注册表路径
set APP_PUBLISHER=PowerFocus

:: 产品主页地址（显示在"添加或删除程序"的详情中，可留空）
set APP_PUBLISHER_URL=https://gitee.com/juli-intelligence/PF.AutoFramework

:: 应用唯一标识 GUID ★ 不同项目必须不同，否则安装时会互相覆盖！
:: 生成命令（在 PowerShell 中执行）：[guid]::NewGuid()
:: 格式：xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx（不含花括号）
set APP_ID=A3F2C1D8-7B4E-4F9A-B2E6-8D3C5F1A9E7B

:: 运行本软件所需的 .NET 大版本号（正整数，如 8 代表 .NET 8）
:: 安装时若检测不到对应版本，会提示用户下载
set APP_DOTNET_VERSION=8

:: 安装包图标路径（.ico 文件），相对于本 Installer 目录
set APP_ICON=..\PF.Application.Shell\-pfico.ico


:: ─── 主程序（WPF 桌面应用）───────────────────────────────────────────────────

:: 主程序可执行文件名（含 .exe），安装后将在桌面和开始菜单创建快捷方式
set MAIN_EXE=PF.Application.Shell.exe

:: 主程序 .csproj 路径，相对于本 Installer 目录
set MAIN_CSPROJ=..\PF.Application.Shell\PF.Application.Shell.csproj


:: ─── 版本号 ───────────────────────────────────────────────────────────────────

:: 版本来源：
::   auto   = 自动从 MSBuild props 文件中的 <Version> 标签读取（推荐）
::   manual = 使用下方 VERSION_VALUE 手动指定
set VERSION_SOURCE=auto

:: VERSION_SOURCE=auto 时：包含 <Version>x.x.x</Version> 标签的 props 文件路径
set VERSION_PROPS_FILE=..\Directory.Build.props

:: VERSION_SOURCE=manual 时：手动指定的版本号（格式：主版本.次版本.修订号）
set VERSION_VALUE=1.0.0


:: ─── 构建参数 ─────────────────────────────────────────────────────────────────

:: 目标运行时标识符（RID）
::   win-x64   = 64 位 Windows（最常用）
::   win-x86   = 32 位 Windows
::   win-arm64 = ARM64 Windows
set BUILD_RID=win-x64

:: 是否自包含发布
::   false = 目标机需预装 .NET（推荐，安装包体积小约 150MB）
::   true  = 自包含，无需目标机预装 .NET，但安装包体积增大约 150MB
set BUILD_SELF_CONTAINED=false


:: ─── Windows 服务配置 ─────────────────────────────────────────────────────────
:: 填写本项目需要安装的 Windows 服务数量
::   0 = 无服务，跳过所有服务相关步骤
::   N = 有 N 个服务，则需要在下方填写 SERVICE1_* 到 SERVICEN_* 的配置
set SERVICE_COUNT=1

:: ── 服务 1 ───────────────────────────────────────────────────────────────────
:: 服务项目 .csproj 路径，相对于本 Installer 目录
set SERVICE1_CSPROJ=..\PF.SecsGem.Service\PF.SecsGem.Service.csproj

:: 服务可执行文件名（含 .exe）
set SERVICE1_EXE=PF.SecsGem.Service.exe

:: 安装后存放到 {应用安装目录}\ 下的子目录名（只含字母数字下划线，不含空格）
set SERVICE1_SUBDIR=SecsGemService

:: sc.exe 注册时使用的服务名（不能含空格，字母/数字/下划线，全局唯一）
set SERVICE1_NAME=SecsGemService

:: 服务管理器（services.msc）中显示的名称（可含空格和中文）
set SERVICE1_DISPLAY_NAME=PF SECS/GEM 通信服务

:: 服务描述文字（不能含半角双引号 "）
set SERVICE1_DESCRIPTION=PF.AutoFramework SECS/GEM 协议转发代理服务

:: 是否单文件发布（服务推荐 true，便于部署）
::   true  = 发布为单个可执行文件（推荐）
::   false = 发布为散文件
set SERVICE1_SINGLE_FILE=true

:: ── 如需添加更多服务，将上方 7 行复制到此处，编号改为 2、3…，并将 SERVICE_COUNT 改为对应数字 ──


:: ╔═══════════════════════════════════════════════════════════════════════════╗
:: ║                         ▼ 以下内容请勿修改 ▼                               ║
:: ╚═══════════════════════════════════════════════════════════════════════════╝

:: ─── 校验区 ───────────────────────────────────────────────────────────────────
echo.
echo [校验] 正在检查配置项...

:: 必填字符串
if "%APP_NAME%"==""      ( echo [错误] APP_NAME 不能为空 & goto :CFG_ERROR )
if "%APP_PUBLISHER%"=="" ( echo [错误] APP_PUBLISHER 不能为空 & goto :CFG_ERROR )
if "%APP_ID%"==""        ( echo [错误] APP_ID 不能为空 & goto :CFG_ERROR )
if "%MAIN_EXE%"==""      ( echo [错误] MAIN_EXE 不能为空 & goto :CFG_ERROR )
if "%BUILD_RID%"==""     ( echo [错误] BUILD_RID 不能为空 & goto :CFG_ERROR )

:: GUID 格式校验（交给 PowerShell .NET 类型解析）
powershell -NoProfile -Command "[System.Guid]::Parse('%APP_ID%') | Out-Null" 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo [错误] APP_ID 格式不正确
    echo        正确格式：xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx（不含花括号）
    echo        当前值  ：%APP_ID%
    echo        生成命令：在 PowerShell 中执行 [guid]::NewGuid()
    goto :CFG_ERROR
)

:: APP_DOTNET_VERSION 必须是正整数
set /A _DV=%APP_DOTNET_VERSION% 2>nul
if %_DV% LEQ 0 (
    echo [错误] APP_DOTNET_VERSION 必须是正整数（当前：%APP_DOTNET_VERSION%）
    goto :CFG_ERROR
)

:: APP_ICON 文件必须存在
if not exist "%APP_ICON%" (
    echo [错误] 找不到图标文件：%APP_ICON%
    goto :CFG_ERROR
)

:: MAIN_CSPROJ 文件必须存在
if not exist "%MAIN_CSPROJ%" (
    echo [错误] 找不到主程序项目文件：%MAIN_CSPROJ%
    goto :CFG_ERROR
)

:: VERSION_SOURCE 枚举校验
if /I "%VERSION_SOURCE%"=="auto"   goto :VS_OK
if /I "%VERSION_SOURCE%"=="manual" goto :VS_OK
echo [错误] VERSION_SOURCE 必须是 auto 或 manual（当前：%VERSION_SOURCE%）
goto :CFG_ERROR
:VS_OK

:: VERSION_SOURCE=auto 时校验 props 文件是否存在
if /I "%VERSION_SOURCE%"=="auto" (
    if not exist "%VERSION_PROPS_FILE%" (
        echo [错误] 找不到版本 props 文件：%VERSION_PROPS_FILE%
        goto :CFG_ERROR
    )
)

:: VERSION_SOURCE=manual 时校验 VERSION_VALUE 非空
if /I "%VERSION_SOURCE%"=="manual" (
    if "%VERSION_VALUE%"=="" (
        echo [错误] VERSION_SOURCE=manual 时 VERSION_VALUE 不能为空
        goto :CFG_ERROR
    )
)

:: BUILD_SELF_CONTAINED 枚举校验
if /I "%BUILD_SELF_CONTAINED%"=="true"  goto :SC_OK
if /I "%BUILD_SELF_CONTAINED%"=="false" goto :SC_OK
echo [错误] BUILD_SELF_CONTAINED 必须是 true 或 false（当前：%BUILD_SELF_CONTAINED%）
goto :CFG_ERROR
:SC_OK

:: SERVICE_COUNT 校验（非负整数）
if "%SERVICE_COUNT%"=="" set SERVICE_COUNT=0
set /A _SC=%SERVICE_COUNT% 2>nul
if %_SC% LSS 0 (
    echo [错误] SERVICE_COUNT 不能为负数（当前：%SERVICE_COUNT%）
    goto :CFG_ERROR
)

:: 逐个校验每个服务的配置项
if %_SC% GTR 0 (
    for /L %%i in (1,1,%_SC%) do (
        call :CHECK_SERVICE %%i
        if errorlevel 1 goto :CFG_ERROR
    )
)

echo [校验] 所有配置项通过 √
goto :VALIDATION_DONE

:: ── 服务配置校验子程序 ──────────────────────────────────────────────────────
:CHECK_SERVICE
set _IDX=%1
call set _CSPROJ=%%SERVICE%_IDX%_CSPROJ%%
call set _EXE=%%SERVICE%_IDX%_EXE%%
call set _SUBDIR=%%SERVICE%_IDX%_SUBDIR%%
call set _NAME=%%SERVICE%_IDX%_NAME%%
call set _DNAME=%%SERVICE%_IDX%_DISPLAY_NAME%%
call set _DESC=%%SERVICE%_IDX%_DESCRIPTION%%
call set _SF=%%SERVICE%_IDX%_SINGLE_FILE%%

if "%_CSPROJ%"=="" ( echo [错误] SERVICE%_IDX%_CSPROJ 不能为空 & exit /b 1 )
if not exist "%_CSPROJ%" ( echo [错误] 找不到服务 %_IDX% 项目文件：%_CSPROJ% & exit /b 1 )
if "%_EXE%"==""    ( echo [错误] SERVICE%_IDX%_EXE 不能为空 & exit /b 1 )
if "%_SUBDIR%"=="" ( echo [错误] SERVICE%_IDX%_SUBDIR 不能为空 & exit /b 1 )
if "%_NAME%"==""   ( echo [错误] SERVICE%_IDX%_NAME 不能为空 & exit /b 1 )
if "%_DNAME%"==""  ( echo [错误] SERVICE%_IDX%_DISPLAY_NAME 不能为空 & exit /b 1 )
if "%_DESC%"==""   ( echo [错误] SERVICE%_IDX%_DESCRIPTION 不能为空 & exit /b 1 )

:: 服务名不能含空格
set "_CHK=%_NAME: =%"
if not "%_CHK%"=="%_NAME%" (
    echo [错误] SERVICE%_IDX%_NAME 不能含空格（当前：%_NAME%）
    exit /b 1
)

:: SINGLE_FILE 枚举校验
if /I "%_SF%"=="true"  exit /b 0
if /I "%_SF%"=="false" exit /b 0
echo [错误] SERVICE%_IDX%_SINGLE_FILE 必须是 true 或 false（当前：%_SF%）
exit /b 1

:: ── 配置错误退出 ─────────────────────────────────────────────────────────────
:CFG_ERROR
echo.
echo 请修改 build-installer.bat 顶部的配置区后重试。
color 0C
popd
pause
exit /b 1


:VALIDATION_DONE
:: ─── 执行区 ───────────────────────────────────────────────────────────────────
echo.
echo ================================================================
echo   调用 Build-Installer.ps1 执行构建...
echo ================================================================
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "Build-Installer.ps1"
set _PS_EXIT=%ERRORLEVEL%

if %_PS_EXIT% NEQ 0 (
    echo.
    color 0C
    echo ================================================================
    echo   [失败] 构建中止，退出码 %_PS_EXIT%。请查看上方日志。
    echo ================================================================
    popd
    pause
    exit /b 1
)

color 0B
echo.
echo ================================================================
echo   构建完成！安装包已生成到 Output\ 目录。
echo ================================================================
echo.
explorer "Output"
popd
pause
exit /b 0
