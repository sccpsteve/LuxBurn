@echo off
setlocal EnableExtensions

set "APPNAME=LuxBurn"
set "SOURCE=%~dp0App"

if defined LOCALAPPDATA (
    set "TARGET=%LOCALAPPDATA%\LuxBurn"
) else (
    set "TARGET=%USERPROFILE%\Local Settings\Application Data\LuxBurn"
)

if not exist "%TARGET%" mkdir "%TARGET%"
if errorlevel 1 (
    echo Could not create:
    echo %TARGET%
    pause
    exit /b 1
)

xcopy "%SOURCE%\*" "%TARGET%\" /E /I /Y >nul
if errorlevel 1 (
    echo Could not copy LuxBurn files.
    pause
    exit /b 1
)

copy "%~dp0uninstall.cmd" "%TARGET%\Uninstall LuxBurn.cmd" >nul

set "PROGRAMS=%APPDATA%\Microsoft\Windows\Start Menu\Programs\LuxBurn"
if not exist "%PROGRAMS%" mkdir "%PROGRAMS%" >nul 2>nul

call :Shortcut "%PROGRAMS%\LuxBurn.lnk" "%TARGET%\LuxBurn.exe" "%TARGET%" "LuxBurn"
call :Shortcut "%PROGRAMS%\Uninstall LuxBurn.lnk" "%TARGET%\Uninstall LuxBurn.cmd" "%TARGET%" "Uninstall LuxBurn"

if exist "%USERPROFILE%\Desktop" (
    call :Shortcut "%USERPROFILE%\Desktop\LuxBurn.lnk" "%TARGET%\LuxBurn.exe" "%TARGET%" "LuxBurn"
)

echo LuxBurn was installed to:
echo %TARGET%
echo.
echo Start LuxBurn from the Start Menu, Desktop, or:
echo %TARGET%\LuxBurn.exe
pause
exit /b 0

:Shortcut
set "SHORTCUT_VBS=%TEMP%\luxburn-shortcut-%RANDOM%.vbs"
> "%SHORTCUT_VBS%" echo Set shell = CreateObject("WScript.Shell")
>> "%SHORTCUT_VBS%" echo Set link = shell.CreateShortcut(WScript.Arguments(0))
>> "%SHORTCUT_VBS%" echo link.TargetPath = WScript.Arguments(1)
>> "%SHORTCUT_VBS%" echo link.WorkingDirectory = WScript.Arguments(2)
>> "%SHORTCUT_VBS%" echo link.Description = WScript.Arguments(3)
>> "%SHORTCUT_VBS%" echo link.IconLocation = WScript.Arguments(1) ^& ",0"
>> "%SHORTCUT_VBS%" echo link.Save
cscript //nologo "%SHORTCUT_VBS%" "%~1" "%~2" "%~3" "%~4" >nul 2>nul
del "%SHORTCUT_VBS%" >nul 2>nul
exit /b 0
