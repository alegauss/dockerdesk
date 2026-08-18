# DD145 — measuring a wizard page instead of reading it.
#
# Four tasks have built a page in Pascal (DD121's uninstall form, DD123's tasks page, DD131's
# blocked-install page, DD132's button on it) and every one was checked by reading the script. The
# failures that misses are the ones it has already produced: a caption assigned before its width
# wrapped at column zero; a page that rendered correctly above a screenful of blank space; and a
# Copy button standing nine pixels below the box it belongs to, because an edit sizes itself to its
# font and a button does not. Each was found by running an installer, which is the most expensive
# place to find anything.
#
# A page's geometry is readable from inside Setup, and that is the whole idea here. This takes the
# block between the page-probe markers in build\installer.iss, wraps it in a Setup that shows the
# page under a named state, reports every control's rectangle and closes itself — no clicks, no
# screenshot, nobody watching. Then it checks what a reader cannot: that nothing overlaps, that
# nothing is off the surface, and that the page says what the state asked for.
#
# The [Code] block is the input either way, so this compiles the same text the installer ships and
# cannot drift from it. What it does NOT prove is that the installer as a whole compiles — that is
# the separate step DD102 put in check.yml, and neither replaces the other.
#
#   scripts\page-probe.ps1              # every state
#   scripts\page-probe.ps1 -Keep        # leave the generated .iss and dumps behind to look at

[CmdletBinding()]
param(
    # Where to build. Defaults to a temporary directory that is removed on the way out.
    [string] $WorkDirectory,

    # Keep the generated script and the dumps. For a failure worth looking at by hand.
    [switch] $Keep
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$installer = Join-Path $root 'build\installer.iss'

# The same three places build\build-installer.cmd looks, in the same order. A fourth opinion about
# where Inno Setup lives is how two of them come to disagree.
function Find-Iscc {
    @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:LocalAppData\Programs\Inno Setup 6\ISCC.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1
}

$iscc = Find-Iscc
if (-not $iscc) {
    throw "ISCC.exe was not found. This harness compiles a real Setup; install Inno Setup 6."
}

if (-not $WorkDirectory) {
    $WorkDirectory = Join-Path ([IO.Path]::GetTempPath()) "freewilly-page-probe-$([guid]::NewGuid().ToString('N'))"
}
New-Item -ItemType Directory -Force -Path $WorkDirectory | Out-Null

# ---------------------------------------------------------------------------------------------
# The page, taken out of the installer between its markers
# ---------------------------------------------------------------------------------------------

$lines = Get-Content $installer
$open = ($lines | Select-String -SimpleMatch '// >>> page-probe' | Select-Object -First 1).LineNumber
$close = ($lines | Select-String -SimpleMatch '// <<< page-probe' | Select-Object -First 1).LineNumber
if (-not $open -or -not $close -or $close -le $open) {
    throw "build\installer.iss no longer carries a page-probe block between its markers"
}

# The page anchors itself after the tasks page, which this Setup does not have. wpWelcome is the
# only substitution made to the shipped text, and it is one token.
$page = (($lines[$open..($close - 2)]) -join "`n") -replace '(?m)^\s*TasksPage\.ID,\s*$', '    wpWelcome,'

# ---------------------------------------------------------------------------------------------
# The Setup around it
# ---------------------------------------------------------------------------------------------
#
# The variables the page reads are declared here rather than sliced, because they live in the
# installer's own var block among things this Setup has no use for. A variable added there and used
# by the page arrives here as a compile error, which is loud — the failure mode to avoid is a
# harness that silently stops covering something.

$head = @'
[Setup]
AppName=PageProbe
AppVersion=1.0
DefaultDirName={tmp}\PageProbe
PrivilegesRequired=lowest
OutputDir=.
OutputBaseFilename=page-probe
Uninstallable=no
CreateAppDir=no
WizardStyle=modern
DisableWelcomePage=yes

[Code]
var
  PreflightPage: TWizardPage;
  PreflightHeading, PreflightFooter: TNewStaticText;
  PreflightMemo: TNewMemo;
  PreflightCommandBox: TNewEdit;
  PreflightCopy, PreflightAgain, PreflightTurnOn: TNewButton;
  PreflightLink: TNewLinkLabel;
  PreflightAsked, PreflightClear, PreflightWsl2: Boolean;
  PreflightRefused, PreflightFeatureOn, PreflightRestartWanted: Boolean;
  PreflightSaid, PreflightCommand, PreflightReport: string;

// The page never calls this; the buttons do, and this Setup presses nothing.
function Preflight: Boolean;
begin
  Result := PreflightClear;
end;

'@

$tail = @'

function YesNo(V: Boolean): string;
begin
  if V then Result := 'yes' else Result := 'no';
end;

procedure Note(var Report: TArrayOfString; const Name: string; C: TControl);
var
  N: Integer;
begin
  N := GetArrayLength(Report);
  SetArrayLength(Report, N + 1);
  Report[N] := Format('%s|%d|%d|%d|%d|%s', [Name, C.Left, C.Top, C.Width, C.Height, YesNo(C.Visible)]);
end;

procedure Dump;
var
  Report: TArrayOfString;
  N: Integer;
begin
  SetArrayLength(Report, 1);
  Report[0] := Format('surface|0|0|%d|%d|yes', [PreflightPage.SurfaceWidth, PreflightPage.SurfaceHeight]);

  Note(Report, 'heading', PreflightHeading);
  Note(Report, 'memo', PreflightMemo);
  Note(Report, 'commandbox', PreflightCommandBox);
  Note(Report, 'copy', PreflightCopy);
  Note(Report, 'link', PreflightLink);
  Note(Report, 'turnon', PreflightTurnOn);
  Note(Report, 'again', PreflightAgain);
  Note(Report, 'footer', PreflightFooter);

  N := GetArrayLength(Report);
  SetArrayLength(Report, N + 3);
  Report[N] := 'next-enabled=' + YesNo(WizardForm.NextButton.Enabled);
  Report[N + 1] := 'heading-says=' + PreflightHeading.Caption;
  Report[N + 2] := 'turnon-says=' + PreflightTurnOn.Caption;

  SaveStringsToUTF8File(ExpandConstant('{param:out}'), Report, False);
end;

procedure InitializeWizard;
begin
  PreflightSaid := ExpandConstant('{param:said|a row  some detail}');
  PreflightCommand := ExpandConstant('{param:command}');
  PreflightWsl2 := ExpandConstant('{param:wsl2|no}') = 'yes';
  PreflightRefused := ExpandConstant('{param:refused|no}') = 'yes';
  PreflightFeatureOn := ExpandConstant('{param:on|no}') = 'yes';
  PreflightRestartWanted := ExpandConstant('{param:pending|no}') = 'yes';
  PreflightClear := ExpandConstant('{param:clear|no}') = 'yes';
  PreflightReport := 'C:\Users\someone\AppData\Local\Temp\preflight.txt';
  PreflightAsked := True;

  BuildPreflightPage;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID <> PreflightPage.ID then
  begin
    WizardForm.NextButton.Enabled := True;
    Exit;
  end;

  ShowTheVerdict;
  Dump;
  WizardForm.Close;
end;
'@

$script = Join-Path $WorkDirectory 'page-probe.iss'
Set-Content -Path $script -Value ($head + $page + $tail) -Encoding utf8

$compile = & $iscc $script 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Output ($compile -join "`n")
    throw "ISCC refused the page probe (exit $LASTEXITCODE)"
}

$probe = Join-Path $WorkDirectory 'page-probe.exe'

# ---------------------------------------------------------------------------------------------
# The states worth rendering
# ---------------------------------------------------------------------------------------------
#
# One per shape the page can take, because the defects this catches are per-state: a control hidden
# in one state and not repositioned in another is exactly the kind of thing reading cannot see.

$states = @(
    @{
        Name = 'wsl2-blocked'
        Args = @('/wsl2=yes', '/command=wsl.exe --install --no-distribution')
        Heading = 'Windows needs one feature turned on first'
        TurnOn = 'Turn it on for me'
        Next = 'no'
        Shown = @('heading', 'memo', 'commandbox', 'copy', 'link', 'turnon', 'again', 'footer')
    },
    @{
        Name = 'wsl2-refused'
        Args = @('/wsl2=yes', '/refused=yes', '/command=wsl.exe --install --no-distribution')
        Heading = 'Windows needs one feature turned on first'
        TurnOn = 'Turn it on for me'
        Next = 'no'
        Shown = @('heading', 'memo', 'commandbox', 'copy', 'link', 'turnon', 'again', 'footer')
    },
    @{
        Name = 'feature-on'
        Args = @('/wsl2=yes', '/on=yes', '/pending=yes', '/command=wsl.exe --install --no-distribution')
        Heading = 'Windows needs to restart to finish turning it on'
        TurnOn = 'Restart now'
        Next = 'no'
        Shown = @('heading', 'memo', 'commandbox', 'copy', 'link', 'turnon', 'again', 'footer')
    },
    @{
        Name = 'other-blocker'
        Args = @('/wsl2=no')
        Heading = 'This machine cannot host the container engine yet'
        Next = 'no'
        # No command and not the WSL2 row, so the box, the Copy button, the link and the button that
        # elevates all have to be gone rather than merely empty.
        Hidden = @('commandbox', 'copy', 'link', 'turnon')
        Shown = @('heading', 'memo', 'again', 'footer')
    },
    @{
        Name = 'cleared'
        Args = @('/clear=yes', '/wsl2=yes', '/command=wsl.exe --install --no-distribution')
        Heading = 'Nothing blocks an install any more'
        # Reachable only through Check again, and the whole point of it is that Next comes back.
        Next = 'yes'
        Shown = @('heading', 'memo', 'again')
    }
)

$problems = New-Object System.Collections.Generic.List[string]

function Add-Problem([string] $state, [string] $text) {
    $problems.Add("${state}: $text")
}

foreach ($state in $states) {
    $out = Join-Path $WorkDirectory "$($state.Name).txt"
    $run = Start-Process -FilePath $probe -ArgumentList (@("/out=$out") + $state.Args) -PassThru
    if (-not $run.WaitForExit(30000)) { $run.Kill() }

    # Setup keeps running after WizardForm.Close on some paths; the dump is what is being waited
    # for, and it is written before the close.
    Get-Process -Name 'page-probe', 'page-probe.tmp' -ErrorAction SilentlyContinue |
        Stop-Process -Force -ErrorAction SilentlyContinue

    if (-not (Test-Path $out)) {
        Add-Problem $state.Name 'the page never rendered, so nothing could be measured'
        continue
    }

    $boxes = @{}
    $facts = @{}
    foreach ($line in Get-Content $out -Encoding utf8) {
        if ($line -match '^(?<k>[a-z-]+)=(?<v>.*)$') {
            $facts[$Matches.k] = $Matches.v
            continue
        }

        $parts = $line -split '\|'
        if ($parts.Count -eq 6) {
            $boxes[$parts[0]] = [pscustomobject]@{
                Left = [int]$parts[1]; Top = [int]$parts[2]
                Width = [int]$parts[3]; Height = [int]$parts[4]
                Shown = $parts[5] -eq 'yes'
            }
        }
    }

    $surface = $boxes['surface']

    # --- what the page says ------------------------------------------------------------------
    if ($state.Heading -and $facts['heading-says'] -ne $state.Heading) {
        Add-Problem $state.Name "the heading says '$($facts['heading-says'])', not '$($state.Heading)'"
    }
    if ($state.TurnOn -and $facts['turnon-says'] -ne $state.TurnOn) {
        Add-Problem $state.Name "the button says '$($facts['turnon-says'])', not '$($state.TurnOn)'"
    }
    if ($state.Next -and $facts['next-enabled'] -ne $state.Next) {
        Add-Problem $state.Name "Next is enabled=$($facts['next-enabled']), expected $($state.Next)"
    }

    # --- which controls are there ------------------------------------------------------------
    foreach ($name in $state.Shown) {
        if (-not $boxes[$name].Shown) { Add-Problem $state.Name "$name is hidden and should be shown" }
    }
    foreach ($name in $state.Hidden) {
        if ($boxes[$name].Shown) { Add-Problem $state.Name "$name is shown and should be hidden" }
    }

    # --- and where they are ------------------------------------------------------------------
    $visible = @($boxes.Keys | Where-Object { $_ -ne 'surface' -and $boxes[$_].Shown } | Sort-Object)

    foreach ($name in $visible) {
        $b = $boxes[$name]
        if ($b.Height -le 0 -or $b.Width -le 0) {
            Add-Problem $state.Name "$name measured $($b.Width)x$($b.Height), which is a control nobody can read"
        }
        if ($b.Top -lt 0 -or $b.Left -lt 0) {
            Add-Problem $state.Name "$name starts at ($($b.Left),$($b.Top)), off the top or left of the page"
        }
        if (($b.Top + $b.Height) -gt $surface.Height) {
            Add-Problem $state.Name ("$name ends at $($b.Top + $b.Height), past the surface's $($surface.Height)")
        }
        if (($b.Left + $b.Width) -gt $surface.Width) {
            Add-Problem $state.Name ("$name ends at $($b.Left + $b.Width), past the surface's $($surface.Width)")
        }
    }

    for ($i = 0; $i -lt $visible.Count; $i++) {
        for ($j = $i + 1; $j -lt $visible.Count; $j++) {
            $a = $boxes[$visible[$i]]
            $b = $boxes[$visible[$j]]
            $overlaps = ($a.Left -lt ($b.Left + $b.Width)) -and (($a.Left + $a.Width) -gt $b.Left) -and
                        ($a.Top -lt ($b.Top + $b.Height)) -and (($a.Top + $a.Height) -gt $b.Top)
            if ($overlaps) {
                Add-Problem $state.Name "$($visible[$i]) and $($visible[$j]) overlap"
            }
        }
    }

    Write-Output "$($state.Name): $($visible.Count) control(s) on a $($surface.Width)x$($surface.Height) surface"
}

if (-not $Keep) {
    Remove-Item -Recurse -Force $WorkDirectory -ErrorAction SilentlyContinue
} else {
    Write-Output "kept: $WorkDirectory"
}

if ($problems.Count -gt 0) {
    Write-Output ''
    foreach ($problem in $problems) { Write-Output "  $problem" }
    throw "$($problems.Count) layout problem(s) on the preflight page"
}

Write-Output "every state renders, and nothing overlaps"
