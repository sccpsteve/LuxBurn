@echo off
cd /d "%~dp0"

call package.cmd
if errorlevel 1 (
    echo.
    echo Package build failed.
    echo Check the messages above, then press any key to close this window.
    pause >nul
    exit /b 1
)

echo.
echo Packages are ready:
echo dist\LuxBurn-v2.1.7-portable.zip
echo dist\LuxBurn-v2.1.7-setup.exe
echo.
echo Press any key to close this window.
pause >nul
