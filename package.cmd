@echo off
setlocal EnableExtensions

cd /d "%~dp0"

set "APP_NAME=LuxBurn"
set "APP_VERSION=1.4"
set "ROOT=%CD%"
set "RELEASE=%ROOT%\LuxBurn\bin\Release"
set "DIST=%ROOT%\dist"
set "WORK=%ROOT%\build\package"
set "PORTABLE=%DIST%\%APP_NAME%-v%APP_VERSION%-portable.zip"
set "INSTALLER=%DIST%\%APP_NAME%-v%APP_VERSION%-setup.exe"
set "SEVENZIP="
set "SFX="

call "%ROOT%\build-xp.cmd"
if errorlevel 1 exit /b 1

if exist "%DIST%" rmdir /s /q "%DIST%"
if exist "%WORK%" rmdir /s /q "%WORK%"
mkdir "%DIST%" || exit /b 1
mkdir "%WORK%" || exit /b 1

if exist "%ProgramFiles%\7-Zip\7z.exe" set "SEVENZIP=%ProgramFiles%\7-Zip\7z.exe"
if exist "%ProgramFiles(x86)%\7-Zip\7z.exe" set "SEVENZIP=%ProgramFiles(x86)%\7-Zip\7z.exe"
if exist "%ProgramFiles%\7-Zip\7z.sfx" set "SFX=%ProgramFiles%\7-Zip\7z.sfx"
if exist "%ProgramFiles(x86)%\7-Zip\7z.sfx" set "SFX=%ProgramFiles(x86)%\7-Zip\7z.sfx"

if not defined SEVENZIP (
    echo 7-Zip was not found. Install 7-Zip to create release packages.
    exit /b 1
)

echo Creating portable package...
pushd "%RELEASE%"
"%SEVENZIP%" a -tzip -mx=9 "%PORTABLE%" * >nul
popd
if errorlevel 1 exit /b 1

echo Creating installer package...
if not defined SFX (
    echo 7-Zip SFX module was not found. Portable package was created, but installer package was not.
    exit /b 1
)

mkdir "%WORK%\installer\App" || exit /b 1
xcopy "%RELEASE%" "%WORK%\installer\App\" /E /I /Y >nul
copy "%ROOT%\packaging\install.cmd" "%WORK%\installer\install.cmd" >nul
copy "%ROOT%\packaging\uninstall.cmd" "%WORK%\installer\uninstall.cmd" >nul

pushd "%WORK%\installer"
"%SEVENZIP%" a -t7z -mx=9 "%WORK%\payload.7z" * >nul
popd
if errorlevel 1 exit /b 1

copy /b "%SFX%" + "%ROOT%\packaging\sfx-config.txt" + "%WORK%\payload.7z" "%INSTALLER%" >nul
if errorlevel 1 exit /b 1

echo Portable:  %PORTABLE%
echo Installer: %INSTALLER%
exit /b 0
