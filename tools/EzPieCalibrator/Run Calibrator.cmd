@echo off
setlocal EnableExtensions

cd /d "%~dp0"

set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
set "EXE=%~dp0EzPieCalibrator.exe"

if not exist "%CSC%" (
    echo .NET Framework 4 compiler was not found.
    pause
    exit /b 1
)

"%CSC%" /nologo /target:winexe /out:"%EXE%" /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll Program.cs
if errorlevel 1 (
    pause
    exit /b 1
)

start "" "%EXE%"
exit /b 0
