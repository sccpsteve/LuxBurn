@echo off
setlocal EnableExtensions

set "APPDIR=%~dp0"
set "PROGRAMS=%APPDATA%\Microsoft\Windows\Start Menu\Programs\LuxBurn"

del "%USERPROFILE%\Desktop\LuxBurn.lnk" >nul 2>nul
rmdir /s /q "%PROGRAMS%" >nul 2>nul

set "CLEANUP=%TEMP%\luxburn-uninstall-%RANDOM%.cmd"
> "%CLEANUP%" echo @echo off
>> "%CLEANUP%" echo ping 127.0.0.1 -n 2 ^>nul
>> "%CLEANUP%" echo rmdir /s /q "%APPDIR%"
>> "%CLEANUP%" echo del "%%~f0" ^>nul 2^>nul

start "" /min "%COMSPEC%" /c "%CLEANUP%"
exit /b 0
