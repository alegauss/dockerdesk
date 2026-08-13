@echo off
REM ==========================================================================
REM build-installer.cmd - publishes the .exe, then wraps it in the Inno Setup
REM installer (DD14). Output: dist\FreeWilly-Setup.exe
REM ==========================================================================
setlocal

REM This script lives in build\; everything it produces (dist\) goes in the folder above.
cd /d "%~dp0.."

REM --- 1) Publish ---------------------------------------------------------
call "%~dp0build.cmd"
if errorlevel 1 (
    echo *** Build failed; no installer was produced. ***
    exit /b 1
)

REM --- 2) Find the Inno Setup compiler -----------------------------------
REM Machine-wide and per-user installations, then the PATH. The per-user location is checked because
REM this project's own audience cannot necessarily install machine-wide either.
set "ISCC="
set "P1=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
set "P2=%ProgramFiles%\Inno Setup 6\ISCC.exe"
set "P3=%LocalAppData%\Programs\Inno Setup 6\ISCC.exe"
if exist "%P1%" set "ISCC=%P1%"
if not defined ISCC if exist "%P2%" set "ISCC=%P2%"
if not defined ISCC if exist "%P3%" set "ISCC=%P3%"
if not defined ISCC for /f "delims=" %%I in ('where ISCC.exe 2^>nul') do if not defined ISCC set "ISCC=%%I"

if not defined ISCC (
    echo.
    echo *** ISCC.exe was not found. ***
    echo Install Inno Setup 6: https://jrsoftware.org/isdl.php
    exit /b 1
)

REM --- 3) Compile the installer ------------------------------------------
echo.
echo === Building the installer with Inno Setup ===
echo.

"%ISCC%" "%~dp0installer.iss"
if errorlevel 1 (
    echo.
    echo *** The installer failed to compile. ***
    exit /b 1
)

echo.
echo === Done ===
echo dist\FreeWilly-Setup.exe
echo.

endlocal
