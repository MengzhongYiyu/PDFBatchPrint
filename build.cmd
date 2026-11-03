@echo off
chcp 65001 >nul
cd /d "%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo [!] .NET Framework 4.x compiler not found: %CSC%
  exit /b 1
)
rem a running exe makes csc fail with CS0016 - check first
tasklist /fi "imagename eq PdfBatchPrint.exe" 2>nul | "%WINDIR%\System32\find.exe" /i "PdfBatchPrint.exe" >nul
if not errorlevel 1 (
  echo [!] PdfBatchPrint.exe is running - close it before building
  exit /b 1
)
echo Compiling PdfBatchPrint.exe ...
"%CSC%" -nologo -target:winexe -codepage:65001 -optimize+ -r:System.dll -r:System.Drawing.dll -r:System.Windows.Forms.dll -r:System.Core.dll -r:System.Management.dll -out:PdfBatchPrint.exe "src\PdfBatchPrint.cs"
if errorlevel 1 (
  echo [!] build failed
  exit /b 1
)
echo OK: PdfBatchPrint.exe
