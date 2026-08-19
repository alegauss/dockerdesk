@echo off
REM ==========================================================================
REM update-release.cmd - raises FreeWilly's release number and builds the
REM release from it in one step.
REM   1) Updates <Version> in Directory.Build.props - the one place that holds
REM      it (DD14); the installer and release.yml both read it back off the
REM      built .exe rather than stating it a second time.
REM   2) Calls build-installer.cmd (publish + Inno Setup), then checks that the
REM      .exe really carries the number that was asked for. That is the same
REM      check release.yml makes against the tag, made here where it is cheap.
REM   3) [optional] With "upload" as the 2nd argument: commits the bump, pushes
REM      it, then creates and pushes the tag vX.Y.Z. The tag is what fires
REM      .github\workflows\release.yml, which rebuilds, sums the installer and
REM      leaves a DRAFT release. Nothing becomes public here - a person presses
REM      Publish after the installer has been run on a machine with nested
REM      virtualization (by hand, or through scripts\vm.ps1).
REM
REM Usage (from any folder):  build\update-release 0.2.0
REM                           build\update-release 0.2.0 upload
REM ==========================================================================
setlocal

REM This script lives in build\; the commit, the tag and the installer are all about the folder
REM above. run-commit.cmd stages relative to the current directory, so this cd is load-bearing.
cd /d "%~dp0.."

if "%~1"=="" (
    echo *** ERROR: name the new version. ***
    echo Usage: build\update-release 0.2.0 [upload]
    exit /b 1
)

REM --- 1) Raise the version ------------------------------------------------
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0update-release.ps1" -Version "%~1"
if errorlevel 1 (
    echo.
    echo *** ERROR: the version was not updated. ***
    exit /b 1
)

REM --- 2) Build the release: publish + installer ---------------------------
call "%~dp0build-installer.cmd"
if errorlevel 1 (
    echo.
    echo *** ERROR: the installer was not produced. ***
    exit /b 1
)

REM The number that actually travelled into the .exe. A tag naming a version the build did not
REM produce ships an installer whose file name and whose Add/Remove Programs entry disagree, and
REM release.yml refuses it - after the tag is public. Refuse it here instead.
powershell -NoProfile -ExecutionPolicy Bypass -Command "$e='src\FreeWilly.Tray\bin\Release\net10.0-windows\win-x64\publish\FreeWilly.exe'; $v=(Get-Item $e).VersionInfo.ProductVersion.Trim(); if ($v -ne '%~1') { Write-Host ('*** the published .exe says ' + $v + ', not %~1 ***'); exit 1 }"
if errorlevel 1 (
    echo.
    echo *** ERROR: the built .exe does not carry v%~1. ***
    exit /b 1
)

REM --- 3) Upload (optional): commit + push + tag ---------------------------
if /i not "%~2"=="upload" goto :end

echo.
echo === Publishing the tag v%~1 ===
echo.

REM Commits the bump. -m is passed explicitly: without it the message is inferred from the diff,
REM and a one-line version change infers badly.
call run-commit.cmd -m "chore: release v%~1"
if errorlevel 1 (
    echo *** ERROR: the version bump was not committed. ***
    exit /b 1
)

REM Pushes the commit on the current branch, before the tag - a tag whose commit the remote has
REM never seen is a release CI cannot check out.
git push
if errorlevel 1 (
    echo *** ERROR: git push failed. ***
    exit /b 1
)

git tag "v%~1"
if errorlevel 1 (
    echo *** ERROR: the tag v%~1 was not created - it may already exist. ***
    exit /b 1
)
git push origin "v%~1"
if errorlevel 1 (
    echo *** ERROR: the tag v%~1 was not pushed. ***
    exit /b 1
)

echo.
echo === Tag v%~1 pushed ===
echo release.yml is now building the draft release. Nothing is downloadable
echo until somebody runs the installer on a machine that can import the WSL2
echo distribution, and then presses Publish:
echo   gh release view "v%~1" --web
echo.

:end
endlocal
