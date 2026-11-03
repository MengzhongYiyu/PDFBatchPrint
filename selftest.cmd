@echo off
setlocal
chcp 65001 >nul
cd /d "%~dp0"
set "PY=%~dp0python\python.exe"
if exist "%PY%" goto :run
if defined PDFBATCH_PYTHON set "PY=%PDFBATCH_PYTHON%"
if exist "%PY%" goto :run
where python >nul 2>nul
if errorlevel 1 (
  echo [!] no python with pymupdf found. Put one in python\ or set PDFBATCH_PYTHON.
  exit /b 3
)
set "PY=python"
:run
"%PY%" "%~dp0selftest.py" %*
exit /b %ERRORLEVEL%
