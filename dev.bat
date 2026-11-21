@echo off
setlocal
cd /d "%~dp0"
title Internet Speed Meter - Live Dev Mode

echo ==========================================================
echo   Internet Speed Meter - Live Dev Mode
echo ==========================================================
echo.
echo Closing any previously running instances...
taskkill /F /IM InternetSpeedMeter.exe 2>nul

:: Local paths to prevent NuGet permission blocks
set "DOTNET_CLI_HOME=%~dp0.dotnet"
set "APPDATA=%~dp0.appdata"

echo.
echo Starting live watcher...
echo ----------------------------------------------------------
echo  1. Keep this console window open in the background.
echo  2. Edit MainWindow.xaml in your editor.
echo  3. Press Ctrl + S to see your widget reload automatically!
echo ----------------------------------------------------------
echo.

dotnet watch --no-hot-reload run

if %ERRORLEVEL% neq 0 (
    echo.
    echo Watcher stopped. Press any key to exit...
    pause >nul
)
