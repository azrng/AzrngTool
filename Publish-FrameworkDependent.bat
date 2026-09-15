@echo off
rem Double-click to publish AzrngTools as a framework-dependent single-file exe into dist-fdd.
rem The output is small but the target machine MUST have the .NET 10 desktop runtime installed.
rem Keep this file ASCII-only: cmd.exe parses batch files in the ANSI code page,
rem UTF-8 Chinese comments/echo lines get corrupted and break the script.
chcp 65001 >nul
setlocal
cd /d "%~dp0"

set "PROJECT=AzrngTools\AzrngTools.csproj"
set "DIST_DIR=%~dp0dist"

echo ==============================================
echo   AzrngTools framework-dependent publish
echo   Output: %DIST_DIR%
echo ==============================================

if exist "%DIST_DIR%" (
    echo Cleaning old output...
    rd /s /q "%DIST_DIR%"
)

rem --self-contained false overrides the Release default in csproj.
rem Host framework-dependent single-file publish always keeps
rem runtimeconfig.json beside the exe; native libraries are still
rem embedded and self-extract at first launch (csproj setting).
dotnet publish "%PROJECT%" ^
    -c Release ^
    -r win-x64 ^
    --self-contained false ^
    -p:PublishSingleFile=true ^
    -o "%DIST_DIR%" ^
    --ignore-failed-sources
set "EXIT_CODE=%ERRORLEVEL%"

rem DebugType=None in csproj already skips pdb; this is just a safety net
if exist "%DIST_DIR%\*.pdb" del /q "%DIST_DIR%\*.pdb"

if not "%EXIT_CODE%"=="0" (
    echo.
    echo [FAIL] Publish failed, exit code: %EXIT_CODE%
) else (
    echo.
    echo [OK] Published to %DIST_DIR%
)

pause
exit /b %EXIT_CODE%
