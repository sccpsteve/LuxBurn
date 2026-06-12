@echo off
setlocal

cd /d "%~dp0"

set "CSC35=%WINDIR%\Microsoft.NET\Framework\v3.5\csc.exe"
set "CSC40=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
set "BASEOUT=%CD%\LuxBurn\bin"
set "LEGACYDIR=%BASEOUT%\ReleaseLegacy"
set "MODERNDIR=%BASEOUT%\ReleaseModern"

if not exist "%CSC35%" (
    echo LuxBurn legacy build requires the .NET Framework 3.5 compiler.
    echo Install .NET Framework 3.5 SP1, then run this command again.
    exit /b 1
)

if not exist "%CSC40%" (
    echo LuxBurn modern build requires the .NET Framework 4 compiler.
    echo Install .NET Framework 4 or newer, then run this command again.
    exit /b 1
)

if exist "%LEGACYDIR%" rmdir /s /q "%LEGACYDIR%"
if exist "%MODERNDIR%" rmdir /s /q "%MODERNDIR%"
mkdir "%LEGACYDIR%" || exit /b 1
mkdir "%MODERNDIR%" || exit /b 1

echo Building LuxBurn legacy build...
"%CSC35%" /nologo /target:winexe /optimize+ /out:"%LEGACYDIR%\LuxBurn.exe" /win32icon:"%CD%\LuxBurn\Assets\Brand\LBWindowLogo.ico" /win32manifest:"%CD%\LuxBurn\app.manifest" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll "%CD%\LuxBurn\Program.cs" "%CD%\LuxBurn\MainForm.cs" "%CD%\LuxBurn\Compatibility.cs" "%CD%\LuxBurn\Services\ChecksumService.cs" "%CD%\LuxBurn\Services\LegacyBurningService.cs" "%CD%\LuxBurn\Properties\AssemblyInfo.cs"

if errorlevel 1 (
    echo Legacy build failed.
    exit /b 1
)

> "%LEGACYDIR%\LuxBurn.exe.config" echo ^<?xml version="1.0" encoding="utf-8" ?^>
>> "%LEGACYDIR%\LuxBurn.exe.config" echo ^<configuration^>
>> "%LEGACYDIR%\LuxBurn.exe.config" echo   ^<startup^>
>> "%LEGACYDIR%\LuxBurn.exe.config" echo     ^<supportedRuntime version="v2.0.50727" /^>
>> "%LEGACYDIR%\LuxBurn.exe.config" echo   ^</startup^>
>> "%LEGACYDIR%\LuxBurn.exe.config" echo ^</configuration^>

echo Building LuxBurn modern build...
"%CSC40%" /nologo /target:winexe /optimize+ /out:"%MODERNDIR%\LuxBurn.exe" /win32icon:"%CD%\LuxBurn\Assets\Brand\LBWindowLogo.ico" /win32manifest:"%CD%\LuxBurn\app.manifest" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll "%CD%\LuxBurn\Program.cs" "%CD%\LuxBurn\MainForm.cs" "%CD%\LuxBurn\Compatibility.cs" "%CD%\LuxBurn\Services\ChecksumService.cs" "%CD%\LuxBurn\Services\LegacyBurningService.cs" "%CD%\LuxBurn\Properties\AssemblyInfo.cs"

if errorlevel 1 (
    echo Modern build failed.
    exit /b 1
)

> "%MODERNDIR%\LuxBurn.exe.config" echo ^<?xml version="1.0" encoding="utf-8" ?^>
>> "%MODERNDIR%\LuxBurn.exe.config" echo ^<configuration^>
>> "%MODERNDIR%\LuxBurn.exe.config" echo   ^<startup useLegacyV2RuntimeActivationPolicy="true"^>
>> "%MODERNDIR%\LuxBurn.exe.config" echo     ^<supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.0" /^>
>> "%MODERNDIR%\LuxBurn.exe.config" echo   ^</startup^>
>> "%MODERNDIR%\LuxBurn.exe.config" echo ^</configuration^>

if exist "%CD%\LuxBurn\Assets" (
    xcopy "%CD%\LuxBurn\Assets" "%LEGACYDIR%\Assets\" /E /I /Y >nul
    xcopy "%CD%\LuxBurn\Assets" "%MODERNDIR%\Assets\" /E /I /Y >nul
)

if exist "%CD%\LuxBurn\Tools" (
    xcopy "%CD%\LuxBurn\Tools" "%LEGACYDIR%\Tools\" /E /I /Y >nul
    xcopy "%CD%\LuxBurn\Tools" "%MODERNDIR%\Tools\" /E /I /Y >nul
)

if exist "%CD%\LuxBurn\THIRD-PARTY-NOTICES.txt" (
    copy /Y "%CD%\LuxBurn\THIRD-PARTY-NOTICES.txt" "%LEGACYDIR%\" >nul
    copy /Y "%CD%\LuxBurn\THIRD-PARTY-NOTICES.txt" "%MODERNDIR%\" >nul
)

echo Built: %LEGACYDIR%\LuxBurn.exe
echo Built: %MODERNDIR%\LuxBurn.exe
exit /b 0
