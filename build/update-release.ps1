# =============================================================================
# update-release.ps1 - raises the release number, in the one place that holds it.
#
# Updates:
#   - Directory.Build.props -> <Version>
#
# That is the whole list, and DD14 is why: the version travels from there into the
# published .exe, build\installer.iss reads it back off the file with
# GetStringFileInfo, and release.yml compares the tag against what the build
# produced. Nothing else states the number, so nothing else has to be edited.
#
# Usage:  build\update-release.cmd 0.2.0
#         powershell -File build\update-release.ps1 -Version 0.2.0
# =============================================================================
param(
    [Parameter(Mandatory = $true)]
    [string]$Version
)
$ErrorActionPreference = 'Stop'

# This script lives in build\; Directory.Build.props is in the folder above.
$root = Split-Path $PSScriptRoot -Parent

# --- The x.y.z shape ---------------------------------------------------------
# release.yml strips a leading "v" off the tag and compares the rest to the .exe,
# so anything this does not accept would fail there instead - after the tag is
# public, which is the expensive place to find out.
if ($Version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+$') {
    throw "invalid version: '$Version'. Use x.y.z (e.g. 0.2.0)."
}

$path = Join-Path $root 'Directory.Build.props'

# Directory.Build.props is written with a UTF-8 BOM, and its comments carry em
# dashes. Read and write it as UTF-8 explicitly - Windows PowerShell 5.1 would
# otherwise decode with the ANSI codepage on the way in - and put the BOM back
# exactly as it was found, so the bump is a one-line diff and not a whole file.
$hadBom = $false
$prefix = [System.IO.File]::ReadAllBytes($path) | Select-Object -First 3
if ($prefix.Count -eq 3 -and $prefix[0] -eq 0xEF -and $prefix[1] -eq 0xBB -and $prefix[2] -eq 0xBF) {
    $hadBom = $true
}
$text = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)

if ($text -notmatch '<Version>\s*([0-9]+\.[0-9]+\.[0-9]+)\s*</Version>') {
    throw "no <Version> found in Directory.Build.props"
}
$old = $Matches[1]
if ($old -eq $Version) {
    Write-Host "Directory.Build.props: already $Version, nothing to change"
    return
}

$text = $text -replace '<Version>\s*[0-9]+\.[0-9]+\.[0-9]+\s*</Version>', "<Version>$Version</Version>"
[System.IO.File]::WriteAllText($path, $text, (New-Object System.Text.UTF8Encoding($hadBom)))

Write-Host "Directory.Build.props: $old -> $Version"
