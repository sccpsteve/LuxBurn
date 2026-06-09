@echo off
setlocal

cd /d "%~dp0"

call "%~dp0build-xp.cmd"
if errorlevel 1 (
    pause
    exit /b 1
)

echo Starting LuxBurn...
start "" "%~dp0LuxBurn\bin\Release\LuxBurn.exe"
