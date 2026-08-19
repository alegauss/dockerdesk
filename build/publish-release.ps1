# =============================================================================
# publish-release.ps1 - names the installer after its version, sums it, and
# creates the GitHub release with both files attached (DD161).
#
# This is the last step of `build\update-release.cmd <version> upload`, and it is
# what makes that command end in a release rather than in a pushed tag. The three
# things it does are the three release.yml used to do after rebuilding from a
# clean checkout; doing them here attaches the installer a maintainer actually
# ran, rather than a second build of the same commit.
#
# Neither the rename nor the sums file is decoration:
#
#   - DD152 names the asset FreeWilly-Setup-<x.y.z>.exe, and DD154's release
#     check finds the installer by matching that pattern. An asset called
#     FreeWilly-Setup.exe is one no installed copy can see.
#   - DD15 publishes SHA256SUMS.txt beside it, and DD154 fetches that first and
#     refuses to run an installer it does not vouch for. A release without it is
#     a release every installed copy declines.
#
# Usage:  powershell -File build\publish-release.ps1 -Version 1.0.1
#         powershell -File build\publish-release.ps1 -Version 1.0.1 -Draft
# =============================================================================
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    # Holds the release back for a person to press Publish, which is what
    # release.yml always did. Off by default: the maintainer running this has the
    # installer on the machine they just built it on, and DD161 is the decision
    # that a release becomes real where that is true.
    [switch]$Draft
)
$ErrorActionPreference = 'Stop'

if ($Version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+$') {
    throw "invalid version: '$Version'. Use x.y.z (e.g. 1.0.1)."
}

# This script lives in build\; dist\ is in the folder above.
$root = Split-Path $PSScriptRoot -Parent
$built = Join-Path $root 'dist\FreeWilly-Setup.exe'

if (-not (Test-Path $built)) {
    throw "no installer at $built - run build\build-installer.cmd first"
}

# --- The name carries the version (DD152) ------------------------------------
# The installer's OutputBaseFilename has no version in it, because Inno reads the
# number off the built .exe and never states it. So the versioned name is made
# here, exactly as release.yml made it.
$installer = Join-Path $root "dist\FreeWilly-Setup-$Version.exe"
Move-Item -LiteralPath $built -Destination $installer -Force

# The number that actually travelled into the installer's own version resource.
# update-release.cmd already checked the published .exe; this checks the file
# about to be uploaded, which is the one a person downloads.
$carried = (Get-Item $installer).VersionInfo.ProductVersion
if ($carried) { $carried = $carried.Trim() }
if ($carried -and $carried -ne $Version) {
    throw "the installer says $carried and this release is v$Version"
}

# --- The digest, in the shape sha256sum -c reads (DD15) ----------------------
$sums = Join-Path $root 'dist\SHA256SUMS.txt'
$line = '{0}  {1}' -f (Get-FileHash $installer -Algorithm SHA256).Hash.ToLower(),
                      (Split-Path $installer -Leaf)

# ASCII, and the same call release.yml makes: this file is read by sha256sum on
# other people's machines and by ReleaseSums on their own, and a UTF-8 BOM makes
# the first line fail to parse for the first of those. Byte-identical to what CI
# produced for every release before this one.
$line | Out-File -FilePath $sums -Encoding ascii
Get-Content $sums

# --- The release -------------------------------------------------------------
# --generate-notes reads the commits since the previous tag, so the tag has to be
# pushed before this runs - update-release.cmd does that immediately above.
$arguments = @(
    'release', 'create', "v$Version",
    $installer,
    $sums,
    '--title', "FreeWilly $Version",
    '--generate-notes',
    '--verify-tag'
)
if ($Draft) { $arguments += '--draft' }

Write-Host ''
Write-Host "=== Creating the release v$Version ==="
Write-Host ''

# The call operator, and $LASTEXITCODE checked by hand: gh writes progress to
# stderr, and a native command's stderr does not set $? in Windows PowerShell.
& gh @arguments
if ($LASTEXITCODE -ne 0) {
    throw "gh release create failed for v$Version (exit $LASTEXITCODE)"
}

Write-Host ''
if ($Draft) {
    Write-Host "=== v$Version is drafted, and nothing is downloadable until it is published ==="
    Write-Host "  gh release view `"v$Version`" --web"
} else {
    Write-Host "=== v$Version is published, with the installer and its SHA-256 attached ==="
    Write-Host "  gh release view `"v$Version`" --web"
}
Write-Host ''
