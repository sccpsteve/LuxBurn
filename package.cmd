@echo off
setlocal EnableExtensions

cd /d "%~dp0"

set "APP_NAME=LuxBurn"
set "APP_VERSION=2.1.8"
set "ROOT=%CD%"
set "RELEASE_LEGACY=%ROOT%\LuxBurn\bin\ReleaseLegacy"
set "RELEASE_MODERN=%ROOT%\LuxBurn\bin\ReleaseModern"
set "DIST=%ROOT%\dist"
set "WORK=%ROOT%\build\package"
set "PORTABLE=%DIST%\%APP_NAME%-v%APP_VERSION%-portable.zip"
set "INSTALLER=%DIST%\%APP_NAME%-v%APP_VERSION%-setup.exe"
set "UPDATE_MANIFEST=%DIST%\%APP_NAME%-update.json"
set "INNO_DIR=%ROOT%\build\tools\InnoSetup5"
set "INNO_SETUP=%ROOT%\build\tools\innosetup-5.6.1-unicode.exe"
set "DOTNET35=%ROOT%\build\redist\dotnetfx35.exe"
set "DOTNET40=%ROOT%\build\redist\dotNetFx40_Full_x86_x64.exe"
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
mkdir "%WORK%\Legacy" || exit /b 1
mkdir "%WORK%\Modern" || exit /b 1
xcopy "%RELEASE_LEGACY%" "%WORK%\Legacy\" /E /I /Y >nul
xcopy "%RELEASE_MODERN%" "%WORK%\Modern\" /E /I /Y >nul
> "%WORK%\Start LuxBurn.cmd" echo @echo off
>> "%WORK%\Start LuxBurn.cmd" echo setlocal EnableExtensions
>> "%WORK%\Start LuxBurn.cmd" echo set "MAJOR="
>> "%WORK%\Start LuxBurn.cmd" echo set "MINOR="
>> "%WORK%\Start LuxBurn.cmd" echo for /f "tokens=4,5 delims=. " %%%%A in ('ver') do (
>> "%WORK%\Start LuxBurn.cmd" echo     set "MAJOR=%%%%A"
>> "%WORK%\Start LuxBurn.cmd" echo     set "MINOR=%%%%B"
>> "%WORK%\Start LuxBurn.cmd" echo )
>> "%WORK%\Start LuxBurn.cmd" echo if not defined MAJOR goto legacy
>> "%WORK%\Start LuxBurn.cmd" echo if %%MAJOR%% GTR 6 goto modern
>> "%WORK%\Start LuxBurn.cmd" echo if %%MAJOR%% EQU 6 if %%MINOR%% GEQ 2 goto modern
>> "%WORK%\Start LuxBurn.cmd" echo goto legacy
>> "%WORK%\Start LuxBurn.cmd" echo :modern
>> "%WORK%\Start LuxBurn.cmd" echo start "" "%%~dp0Modern\LuxBurn.exe"
>> "%WORK%\Start LuxBurn.cmd" echo exit /b
>> "%WORK%\Start LuxBurn.cmd" echo :legacy
>> "%WORK%\Start LuxBurn.cmd" echo start "" "%%~dp0Legacy\LuxBurn.exe"
>> "%WORK%\Start LuxBurn.cmd" echo exit /b
pushd "%WORK%"
"%SEVENZIP%" a -tzip -mx=9 "%PORTABLE%" * >nul
popd
if errorlevel 1 exit /b 1

echo Creating installer package...
if not exist "%DOTNET35%" (
    echo Microsoft .NET Framework 3.5 SP1 standalone installer was not found.
    echo Expected: %DOTNET35%
    exit /b 1
)

if not exist "%DOTNET40%" (
    echo Microsoft .NET Framework 4 full offline installer was not found.
    echo Expected: %DOTNET40%
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
