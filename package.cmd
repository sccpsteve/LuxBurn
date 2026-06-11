@echo off
setlocal EnableExtensions

cd /d "%~dp0"

set "APP_NAME=LuxBurn"
set "APP_VERSION=2.1.4"
set "ROOT=%CD%"
set "RELEASE=%ROOT%\LuxBurn\bin\Release"
set "DIST=%ROOT%\dist"
set "WORK=%ROOT%\build\package"
set "PORTABLE=%DIST%\%APP_NAME%-v%APP_VERSION%-portable.zip"
set "INSTALLER=%DIST%\%APP_NAME%-v%APP_VERSION%-setup.exe"
set "UPDATE_MANIFEST=%DIST%\%APP_NAME%-update.json"
set "INNO_DIR=%ROOT%\build\tools\InnoSetup5"
set "INNO_SETUP=%ROOT%\build\tools\innosetup-5.6.1-unicode.exe"
set "DOTNET40_URL=http://download.microsoft.com/download/5/6/4/5641DA81-E6FA-4550-9F80-A1D862D9CFAA/dotNetFx40_Full_x86.exe"
set "DOTNET40=%ROOT%\build\redist\dotNetFx40_Full_x86.exe"
set "ISCC="
set "SEVENZIP="

call "%ROOT%\build-xp.cmd"
if errorlevel 1 exit /b 1

if exist "%DIST%" rmdir /s /q "%DIST%"
if exist "%WORK%" rmdir /s /q "%WORK%"
mkdir "%DIST%" || exit /b 1
mkdir "%WORK%" || exit /b 1

if exist "%ProgramFiles%\7-Zip\7z.exe" set "SEVENZIP=%ProgramFiles%\7-Zip\7z.exe"
if exist "%ProgramFiles(x86)%\7-Zip\7z.exe" set "SEVENZIP=%ProgramFiles(x86)%\7-Zip\7z.exe"
if exist "%ProgramFiles%\Inno Setup 5\ISCC.exe" set "ISCC=%ProgramFiles%\Inno Setup 5\ISCC.exe"
if exist "%ProgramFiles(x86)%\Inno Setup 5\ISCC.exe" set "ISCC=%ProgramFiles(x86)%\Inno Setup 5\ISCC.exe"
if exist "%INNO_DIR%\ISCC.exe" set "ISCC=%INNO_DIR%\ISCC.exe"

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
if not exist "%DOTNET40%" (
    echo Downloading Microsoft .NET Framework 4 standalone installer...
    if not exist "%ROOT%\build\redist" mkdir "%ROOT%\build\redist"
    powershell -NoProfile -ExecutionPolicy Bypass -Command "$ErrorActionPreference='Stop'; $ProgressPreference='SilentlyContinue'; Invoke-WebRequest -Uri '%DOTNET40_URL%' -OutFile '%DOTNET40%'"
    if errorlevel 1 exit /b 1
)

if not exist "%DOTNET40%" (
    echo Microsoft .NET Framework 4 standalone installer was not found.
    exit /b 1
)

if not defined ISCC (
    echo Inno Setup 5.6.1 compiler was not found. Downloading it now...
    if not exist "%ROOT%\build\tools" mkdir "%ROOT%\build\tools"
    powershell -NoProfile -ExecutionPolicy Bypass -Command "$ErrorActionPreference='Stop'; $ProgressPreference='SilentlyContinue'; Invoke-WebRequest -Uri 'https://files.jrsoftware.org/is/5/innosetup-5.6.1-unicode.exe' -OutFile '%INNO_SETUP%'"
    if errorlevel 1 exit /b 1

    "%INNO_SETUP%" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /DIR="%INNO_DIR%"
    if errorlevel 1 exit /b 1
    if exist "%INNO_DIR%\ISCC.exe" set "ISCC=%INNO_DIR%\ISCC.exe"
)

if not defined ISCC (
    echo Inno Setup compiler was not found after installation.
    exit /b 1
)

"%ISCC%" /DAppVersion="%APP_VERSION%" "%ROOT%\packaging\LuxBurn.iss"
if errorlevel 1 exit /b 1

echo Creating update manifest...
for /f "usebackq delims=" %%I in (`powershell -NoProfile -Command "[DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')"`) do set "MANIFEST_GENERATED=%%I"
for /f "usebackq delims=" %%I in (`powershell -NoProfile -Command "(Get-FileHash -LiteralPath '%INSTALLER%' -Algorithm SHA256).Hash.ToLowerInvariant()"`) do set "INSTALLER_SHA256=%%I"
for /f "usebackq delims=" %%I in (`powershell -NoProfile -Command "(Get-FileHash -LiteralPath '%PORTABLE%' -Algorithm SHA256).Hash.ToLowerInvariant()"`) do set "PORTABLE_SHA256=%%I"
> "%UPDATE_MANIFEST%" echo {
>> "%UPDATE_MANIFEST%" echo   "latestVersion": "%APP_VERSION%",
>> "%UPDATE_MANIFEST%" echo   "generatedAtUtc": "%MANIFEST_GENERATED%",
>> "%UPDATE_MANIFEST%" echo   "installerUrl": "https://github.com/sccpsteve/LuxBurn/releases/download/latest/%APP_NAME%-v%APP_VERSION%-setup.exe",
>> "%UPDATE_MANIFEST%" echo   "installerSha256": "%INSTALLER_SHA256%",
>> "%UPDATE_MANIFEST%" echo   "portableUrl": "https://github.com/sccpsteve/LuxBurn/releases/download/latest/%APP_NAME%-v%APP_VERSION%-portable.zip",
>> "%UPDATE_MANIFEST%" echo   "portableSha256": "%PORTABLE_SHA256%",
>> "%UPDATE_MANIFEST%" echo   "releasePageUrl": "https://github.com/sccpsteve/LuxBurn/releases/tag/latest"
>> "%UPDATE_MANIFEST%" echo }

echo Portable:  %PORTABLE%
echo Installer: %INSTALLER%
echo Update manifest: %UPDATE_MANIFEST%
exit /b 0
