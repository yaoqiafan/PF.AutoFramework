@echo off
:: 1. 环境初始化：解决中文乱码与路径跳转问题 [cite: 1]
chcp 65001 >nul
pushd "%~dp0"
color 0A

:: ==========================================
:: 核心配置区
:: ==========================================
:: 路径定义
set "NUPKG_DIR=%~dp0nupkg"

echo ========================================================
echo    PF.AutoFramework 自动化本地编译与打包工具
echo ========================================================
echo.

:: ==========================================
:: 阶段 1：清理旧工作区
:: ==========================================
echo [1/2] 清理旧缓存与编译文件...
if exist "%NUPKG_DIR%" rd /s /q "%NUPKG_DIR%"
mkdir "%NUPKG_DIR%"
:: 清理整个解决方案
dotnet clean -c Release >nul

:: ==========================================
:: 阶段 2：编译与打包
:: ==========================================
echo.
echo [2/2] 正在按依赖顺序编译并打包到本地目录... 
:: 1. 编译 (Release 模式)
dotnet build -c Release
if %ERRORLEVEL% NEQ 0 goto :ERROR_EXIT

:: 2. 打包所有项目到指定目录
dotnet pack -c Release --no-build -o "%NUPKG_DIR%"

:: 校验包是否存在
if not exist "%NUPKG_DIR%\*.nupkg" goto :ERROR_EXIT
echo [OK] 打包完成，本地文件已生成到 nupkg 目录。

echo.
color 0B [cite: 5]
echo ========================================================
echo    任务圆满完成！
echo    PF.AutoFramework 的组件已成功编译并打包至本地。
echo ========================================================
popd
pause
exit /b 0

:ERROR_EXIT
color 0C [cite: 6]
echo.
echo [致命错误] 流程由于代码报错或环境问题已中断，请检查上方日志。 [cite: 6]
popd
pause
exit /b 1