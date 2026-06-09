@echo off
setlocal

cd /d "%~dp0"

set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
set "OUTDIR=%CD%\OpenBurningSuite.Xp\bin\Release"
set "OUTEXE=%OUTDIR%\LuxBurn.exe"

if not exist "%CSC%" (
    echo LuxBurn requires the .NET Framework 4.x compiler.
    echo Install .NET Framework 4.0 or newer, then run this command again.
    exit /b 1
)

if not exist "%OUTDIR%" mkdir "%OUTDIR%"

echo Building LuxBurn...
"%CSC%" /nologo /target:winexe /optimize+ /out:"%OUTEXE%" /win32icon:"%CD%\OpenBurningSuite\icon.ico" /win32manifest:"%CD%\OpenBurningSuite.Xp\app.manifest" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:Microsoft.CSharp.dll "%CD%\OpenBurningSuite.Xp\Program.cs" "%CD%\OpenBurningSuite.Xp\MainForm.cs" "%CD%\OpenBurningSuite.Xp\Services\ChecksumService.cs" "%CD%\OpenBurningSuite.Xp\Services\LegacyBurningService.cs" "%CD%\OpenBurningSuite.Xp\Properties\AssemblyInfo.cs"

if errorlevel 1 (
    echo Build failed.
    exit /b 1
)

if exist "%CD%\OpenBurningSuite.Xp\Assets" (
    xcopy "%CD%\OpenBurningSuite.Xp\Assets" "%OUTDIR%\Assets\" /E /I /Y >nul
)

if exist "%CD%\OpenBurningSuite.Xp\Tools" (
    xcopy "%CD%\OpenBurningSuite.Xp\Tools" "%OUTDIR%\Tools\" /E /I /Y >nul
)

echo Built: %OUTEXE%
exit /b 0
