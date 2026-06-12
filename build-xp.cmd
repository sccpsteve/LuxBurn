@echo off
setlocal

cd /d "%~dp0"

set "CSC=%WINDIR%\Microsoft.NET\Framework\v3.5\csc.exe"
set "OUTDIR=%CD%\LuxBurn\bin\Release"
set "OUTEXE=%OUTDIR%\LuxBurn.exe"

if not exist "%CSC%" (
    echo LuxBurn requires the .NET Framework 3.5 compiler.
    echo Install .NET Framework 3.5 SP1 or newer, then run this command again.
    exit /b 1
)

if exist "%OUTDIR%" rmdir /s /q "%OUTDIR%"
mkdir "%OUTDIR%"

echo Building LuxBurn...
"%CSC%" /nologo /target:winexe /optimize+ /out:"%OUTEXE%" /win32icon:"%CD%\LuxBurn\Assets\Brand\LBWindowLogo.ico" /win32manifest:"%CD%\LuxBurn\app.manifest" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll "%CD%\LuxBurn\Program.cs" "%CD%\LuxBurn\MainForm.cs" "%CD%\LuxBurn\Compatibility.cs" "%CD%\LuxBurn\Services\ChecksumService.cs" "%CD%\LuxBurn\Services\LegacyBurningService.cs" "%CD%\LuxBurn\Properties\AssemblyInfo.cs"

if errorlevel 1 (
    echo Build failed.
    exit /b 1
)

if exist "%CD%\LuxBurn\App.config" (
    copy /Y "%CD%\LuxBurn\App.config" "%OUTDIR%\LuxBurn.exe.config" >nul
)

if exist "%CD%\LuxBurn\Assets" (
    xcopy "%CD%\LuxBurn\Assets" "%OUTDIR%\Assets\" /E /I /Y >nul
)

if exist "%CD%\LuxBurn\Tools" (
    xcopy "%CD%\LuxBurn\Tools" "%OUTDIR%\Tools\" /E /I /Y >nul
)

if exist "%CD%\LuxBurn\THIRD-PARTY-NOTICES.txt" (
    copy /Y "%CD%\LuxBurn\THIRD-PARTY-NOTICES.txt" "%OUTDIR%\" >nul
)

echo Built: %OUTEXE%
exit /b 0
