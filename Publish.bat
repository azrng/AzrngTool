@echo off
rem 双击运行：以 Native AOT 方式发布单文件 exe 到 dist 目录
chcp 65001 >nul
setlocal
cd /d "%~dp0"

set "PROJECT=AzrngTools\AzrngTools.csproj"
set "DIST_DIR=%~dp0dist"

echo ==============================================
echo   AzrngTools AOT 发布
echo   输出目录: %DIST_DIR%
echo ==============================================

if exist "%DIST_DIR%" (
    echo 清理旧的发布产物...
    rd /s /q "%DIST_DIR%"
)

rem PublishAot=true 会在 csproj 中联动开启 PublishTrimmed 并关闭 ReadyToRun
dotnet publish "%PROJECT%" ^
    -c Release ^
    -r win-x64 ^
    -o "%DIST_DIR%" ^
    -p:PublishAot=true ^
    --ignore-failed-sources
set "EXIT_CODE=%ERRORLEVEL%"

rem 清理调试符号文件，发布产物只保留可执行文件与原生依赖
if exist "%DIST_DIR%\*.pdb" del /q "%DIST_DIR%\*.pdb"

if not "%EXIT_CODE%"=="0" (
    echo.
    echo [失败] AOT 发布失败，请检查上方日志。错误码: %EXIT_CODE%
) else (
    echo.
    echo [完成] 发布成功，产物位于 dist 目录。
)

pause
exit /b %EXIT_CODE%
