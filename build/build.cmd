@echo off
REM ==========================================================================
REM build.cmd - publishes FreeWilly as one self-contained .exe (DD14).
REM Output: src\FreeWilly.Tray\bin\Release\net10.0-windows\win-x64\publish\FreeWilly.exe
REM ==========================================================================
setlocal

REM This script lives in build\; the solution is in the folder above.
cd /d "%~dp0.."

REM A running tray holds the .exe the publish is about to overwrite, and the SDK fails inside its own
REM bundler with an UnauthorizedAccessException that names neither the process nor the file. Whoever
REM installed by building runs the tray from exactly that folder, so this is a state rather than a
REM bug. Named here, and nothing is terminated on anybody's behalf.
tasklist /FI "IMAGENAME eq FreeWilly.exe" 2>nul | find /I "FreeWilly.exe" >nul
if not errorlevel 1 (
    echo.
    echo *** FreeWilly.exe is running. Quit it from the tray and run this again. ***
    echo     The publish would overwrite the file it is running from.
    exit /b 1
)

echo.
echo === Publishing FreeWilly (Release, win-x64, self-contained, one file) ===
echo.

dotnet publish src\FreeWilly.Tray -c Release --nologo
set "PUBERR=%errorlevel%"

REM The WPF SDK writes a "<name>_<random>_wpftmp.csproj" beside the project while it compiles the
REM XAML and normally deletes it; an interrupted build leaves one behind. It is in .gitignore, but a
REM stray copy also confuses the next build's globbing.
del /q "%~dp0..\src\FreeWilly.Tray\*_wpftmp.csproj" >nul 2>nul

if not "%PUBERR%"=="0" (
    echo.
    echo *** dotnet publish failed. ***
    exit /b 1
)

echo.
echo === Done ===
echo src\FreeWilly.Tray\bin\Release\net10.0-windows\win-x64\publish\FreeWilly.exe
echo.

endlocal
