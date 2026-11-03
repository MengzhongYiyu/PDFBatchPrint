@echo off
setlocal
chcp 65001 >nul
cd /d "%~dp0"
rem Rebuild portable package: dist\PDF批量打印工具\ and dist\PDF批量打印工具_便携版_v1.4.zip
rem Python source: prefer the bundled .\python (dev runtime of this project),
rem else the DSH runtime of the current user.

set "SRC=%~dp0python"
if exist "%SRC%\python.exe" goto :src_ok
for /d %%D in ("%USERPROFILE%\.dsh\dsh-runtimes\*") do (
  if exist "%%~fD\dependencies\python\python.exe" set "SRC=%%~fD\dependencies\python"
)
if not exist "%SRC%\python.exe" (
  echo [!] python runtime with PyMuPDF not found.
  echo     Put a python\ next to this script, or set SRC=... at the top of this file.
  exit /b 1
)
:src_ok
set "DST=%~dp0dist\PDF批量打印工具"
set "OUTZIP=%~dp0dist\PDF批量打印工具_便携版_v1.4.zip"

if not exist "%~dp0PdfBatchPrint.exe" (
  echo [!] run build.cmd first
  exit /b 1
)
echo SRC = %SRC%

if exist "%DST%" rmdir /s /q "%DST%"
mkdir "%DST%\python\Lib\site-packages"

copy /y "%~dp0PdfBatchPrint.exe" "%DST%\" >nul
copy /y "%~dp0render_pdf.py" "%DST%\" >nul
copy /y "%~dp0README.md" "%DST%\" >nul
if exist "%~dp0使用说明.txt" copy /y "%~dp0使用说明.txt" "%DST%\" >nul

for %%f in (python.exe python3.dll python312.dll vcruntime140.dll vcruntime140_1.dll LICENSE.txt) do copy /y "%SRC%\%%f" "%DST%\python\" >nul
robocopy "%SRC%\DLLs" "%DST%\python\DLLs" /E /NFL /NDL /NJH /NJS /NP >nul
robocopy "%SRC%\Lib" "%DST%\python\Lib" /E /XF *.pyc /XD "%SRC%\Lib\site-packages" "%SRC%\Lib\test" "%SRC%\Lib\idlelib" "%SRC%\Lib\tkinter" "%SRC%\Lib\lib2to3" "%SRC%\Lib\ensurepip" "%SRC%\Lib\turtledemo" /NFL /NDL /NJH /NJS /NP >nul
robocopy "%SRC%\Lib\site-packages\pymupdf" "%DST%\python\Lib\site-packages\pymupdf" /E /XF *.onnx *.pdb /NFL /NDL /NJH /NJS /NP >nul
if exist "%SRC%\Lib\site-packages\fitz" robocopy "%SRC%\Lib\site-packages\fitz" "%DST%\python\Lib\site-packages\fitz" /E /XF *.pyc /NFL /NDL /NJH /NJS /NP >nul
robocopy "%SRC%\Lib\site-packages\PIL" "%DST%\python\Lib\site-packages\PIL" /E /XF *.pyc /NFL /NDL /NJH /NJS /NP >nul

"%DST%\python\python.exe" -c "import pymupdf,sys;print('bundled python ok', sys.version.split()[0], pymupdf.__version__)"
if errorlevel 1 (
  echo [!] bundled python self-test failed ^(pymupdf^)
  exit /b 1
)
"%DST%\python\python.exe" -c "import PIL;print('bundled pillow ok', PIL.__version__)"
if errorlevel 1 (
  echo [!] bundled python self-test failed ^(PIL^)
  exit /b 1
)

if exist "%OUTZIP%" del /q "%OUTZIP%"
powershell -NoProfile -Command "Compress-Archive -LiteralPath '%DST%' -DestinationPath '%OUTZIP%' -CompressionLevel Optimal -Force"
if errorlevel 1 (
  echo [!] zip failed
  exit /b 1
)
echo OK: %OUTZIP%
endlocal
