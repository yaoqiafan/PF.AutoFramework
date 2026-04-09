@echo off
:: 1. 环境初始化：解决中文乱码与路径跳转问题 
chcp 65001 >nul
pushd "%~dp0"
color 0A

:: ==========================================
:: 核心配置区 (已同步至 8081 独立站点) 
:: ==========================================
set SOURCE_NAME=BaGet_8081
:: 务必确保此地址在浏览器中可访问
set SOURCE_URL=http://101.43.39.163:8081/v3/index.json
set API_KEY=Gll1243411723
set NUPKG_DIR=%~dp0nupkg
set CONFIG_FILE=%~dp0nuget.config

echo ========================================================
echo    PF.AutoFramework 远程推送专用工具 (8081 稳定带缓冲版)
echo ========================================================

:: ==========================================
:: 阶段 0：环境自检与配置校准 
:: ==========================================
if not exist "%NUPKG_DIR%\*.nupkg" (
    color 0C
    echo [错误] 未在 %NUPKG_DIR% 发现任何 .nupkg 文件！
    echo 请确认打包文件已存放至 nupkg 文件夹。
    pause
    exit /b 1
)

echo [0/1] 正在生成临时 NuGet 配置文件... 
echo ^<?xml version="1.0" encoding="utf-8"?^> > "%CONFIG_FILE%"
echo ^<configuration^> >> "%CONFIG_FILE%"
echo   ^<packageSources^> >> "%CONFIG_FILE%"
echo     ^<add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" /^> >> "%CONFIG_FILE%"
echo     ^<add key="%SOURCE_NAME%" value="%SOURCE_URL%" allowInsecureConnections="true" /^> >> "%CONFIG_FILE%"
echo   ^</packageSources^> >> "%CONFIG_FILE%"
echo ^</configuration^> >> "%CONFIG_FILE%"
echo [OK] 配置文件已就绪。 

:: ==========================================
:: 阶段 1：远程推送到私服 (核心推送逻辑) 
:: ==========================================
echo.
echo [1/1] 开始推送到私服节点... 
echo 目标地址: %SOURCE_URL%
echo --------------------------------------------------------

:: 【关键补丁】强制 .NET 客户端使用旧版 HTTP 处理器，解决连接不稳定的 EOF 错误 
set DOTNET_SYSTEM_NET_HTTP_USESOCKETSHTTPHANDLER=0

for %%f in ("%NUPKG_DIR%\*.nupkg") do (
    echo [正在上传] %%~nxf 
    
    :: 【修复 1】: 使用 %%~ff 引用绝对路径，彻底消灭“找不到驱动器”报错
    :: 使用 --skip-duplicate 允许跳过已存在的包，支持断点续传
    dotnet nuget push "%%~ff" ^
        -k %API_KEY% ^
        -s %SOURCE_NAME% ^
        --configfile "%CONFIG_FILE%" ^
        --skip-duplicate
        
    :: 【修复 2】: 强制缓冲 1 秒，防止服务器 SQLite 数据库并发死锁报错 500
    echo [缓冲] 给服务器 SQLite 数据库 1 秒钟写入时间...
    timeout /t 1 /nobreak >nul
)

echo.
color 0B
echo ========================================================
echo    任务执行完毕！ 
echo    PF.AutoFramework 的组件已尝试同步至 8081 私服。 
echo ========================================================
popd
pause
exit /b 0

:ERROR_EXIT
color 0C
echo.
echo [致命错误] 流程中断，请检查网络连接或 API Key。 
popd
pause
exit /b 1