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

REM The WPF SDK writes a "<name>_<random>_wpftmp.csproj" beside the project while it compiles the
REM XAML and normally deletes it; an interrupted build leaves one behind. It is in .gitignore.
REM
REM BEFORE the publish, and that is DD109. This delete used to run after it, to tidy up - so the one
REM script that owns the cleanup failed on exactly the run the cleanup exists for, and worked the
REM second time, which is what made it read as flaky rather than as a rule.
del /q "%~dp0..\src\FreeWilly.Tray\*_wpftmp.csproj" >nul 2>nul

REM The project file and not the folder (DD109): MSBuild answers a folder holding two projects with
REM MSB1050 before it has evaluated anything, so the delete above is the belt and this is the braces
REM - a command that has already chosen its project cannot raise it at all.
dotnet publish src\FreeWilly.Tray\FreeWilly.Tray.csproj -c Release --nologo
set "PUBERR=%errorlevel%"

REM And again after, so a build interrupted from here leaves nothing behind for the next one.
del /q "%~dp0..\src\FreeWilly.Tray\*_wpftmp.csproj" >nul 2>nul

if not "%PUBERR%"=="0" (
    echo.
    echo *** dotnet publish failed. ***
    exit /b 1
)

REM DD141's docker forwarder. Published separately because it is a different subsystem: a command a
REM shell waits for has to be console, and the tray's .exe is a windowed application. Named here
REM rather than left to the installer, so a build produces everything the installer needs.
dotnet publish src\FreeWilly.Shim\FreeWilly.Shim.csproj -c Release --nologo
if errorlevel 1 (
    echo.
    echo *** dotnet publish failed for the docker forwarder. ***
    exit /b 1
)

echo.
echo === Done ===
echo src\FreeWilly.Tray\bin\Release\net10.0-windows\win-x64\publish\FreeWilly.exe
echo.

endlocal
