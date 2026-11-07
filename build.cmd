@echo off
setlocal
set "LT_COMPILER=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%LT_COMPILER%" (
 echo .NET Framework 4.x compiler not found.
 exit /b 1
)
if not exist "%~dp0release" mkdir "%~dp0release"
"%LT_COMPILER%" /nologo /target:winexe /optimize+ /platform:anycpu /codepage:65001 /win32manifest:"%~dp0app.manifest" /win32icon:"%~dp0app.ico" /resource:"%~dp0LICENSE.txt",LanTime.LICENSE.txt /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.ServiceProcess.dll /reference:System.Runtime.Serialization.dll /out:"%~dp0release\LanTime.exe" "%~dp0src\*.cs"
if errorlevel 1 exit /b 1
copy /y "%~dp0LanTime.exe.config" "%~dp0release\LanTime.exe.config" >nul
echo Built: %~dp0release\LanTime.exe
exit /b 0
