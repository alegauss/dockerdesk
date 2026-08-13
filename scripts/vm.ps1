#Requires -Version 5.1
<#
.SYNOPSIS
  Says whether the Windows test guest is reachable from this machine, and what its preflight says.

.DESCRIPTION
  Every check in this project has otherwise only run on the developer's own machine, and that
  machine passes. The red rows are reached by injected facts alone, and an install has never been
  run at all. This script is the way to a machine where those are real: a Windows guest under
  VMware Workstation, driven through `vmrun`, which needs no guest networking and can be reverted
  to a clean snapshot between destructive runs.

  It answers rather than fixes. Every row is a fact, a verdict and the one action that changes it —
  the same shape as the product's own preflight, on purpose: two reports about one machine that
  read differently are two things to learn.

  `doctor` and `preflight` only read. `revert` is the one action that destroys something, so it
  names the snapshot it will restore and what that discards, and does nothing without -Yes.

.PARAMETER Action
  doctor    every reachability fact, as a report (default)
  preflight build the product preflight, copy it to the guest, run it there, print what it said
  engine    the same for an engine mode; -Command carries it, default --plan
  run       run one command in the guest and print its output
  start     power the guest on and wait for its agent
  revert    put the guest back on a snapshot; -Command names it, -Yes is required
  screenshot capture the guest's screen to a PNG; -Command is the path, default under TEMP

.PARAMETER Command
  For -Action run: the command line to execute in the guest.
  For -Action revert: which snapshot, needed only where the guest has more than one.

.PARAMETER Yes
  Required by -Action revert. Without it the revert is described and refused, because a revert
  throws away everything the guest has done since the snapshot was taken.

.PARAMETER Vmx
  Path to the guest's .vmx. Overrides DOCKERDESK_VMX.

.NOTES
  Secrets are never parameters and never printed. Set them as environment variables:

    DOCKERDESK_VMX              full path to the .vmx
    DOCKERDESK_VM_PASSWORD      the VM *encryption* password (a Windows 11 guest needs a TPM,
                                which means an encrypted VM, which vmrun cannot open without it)
    DOCKERDESK_GUEST_USER       an account inside the guest
    DOCKERDESK_GUEST_PASSWORD   its password

  Or put them as KEY=VALUE lines in a file, searched in this order:

    1. whatever DOCKERDESK_VM_ENV points at
    2. dockerdesk-vm.env in the repository root
    3. d:\tmp\dockerdesk-vm.env

  The repository-root location is covered by the `*.env` line in .gitignore, so `git add -A`
  and `run-commit.cmd` cannot stage it. Two things .gitignore does not do, and they are worth
  knowing: `git add -f` still stages it, and `git clean -xdf` deletes it without asking. So the
  file is convenient rather than safe, and the credential in it should be one that can be
  rotated cheaply.
#>
[CmdletBinding()]
param(
    [ValidateSet('doctor', 'preflight', 'run', 'start', 'engine', 'revert', 'screenshot')]
    [string] $Action = 'doctor',

    [string] $Command,

    [switch] $Yes,

    [string] $Vmx
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:RepoRoot = Split-Path -Parent $PSScriptRoot
$script:GuestStage = 'C:\dockerdesk-test'

# Where the settings file is looked for, in order. The repository-root entry is ignored by the
# `*.env` line in .gitignore, which is what keeps `git add -A` from staging a credential.
$script:EnvFileCandidates = @(
    (Join-Path $script:RepoRoot 'dockerdesk-vm.env'),
    'd:\tmp\dockerdesk-vm.env'
)

# ---------------------------------------------------------------------------------------------
# The report
# ---------------------------------------------------------------------------------------------

$script:Rows = New-Object System.Collections.ArrayList

function Add-Row {
    param(
        [Parameter(Mandatory)] [string] $Title,
        [Parameter(Mandatory)] [ValidateSet('ok', 'FAIL', 'warn', '?')] [string] $Verdict,
        [Parameter(Mandatory)] [string] $Detail,
        [string] $Remedy,
        [switch] $NotBlocking
    )
    $null = $script:Rows.Add([pscustomobject]@{
        Title    = $Title
        Verdict  = $Verdict
        Detail   = $Detail
        Remedy   = $Remedy
        Blocking = -not $NotBlocking
    })
}

function Write-Report {
    $width = 0
    foreach ($row in $script:Rows) {
        if ($row.Title.Length -gt $width) { $width = $row.Title.Length }
    }

    Write-Host ''
    Write-Host 'DockerDesk test guest - what this host can reach'
    Write-Host ''
    foreach ($row in $script:Rows) {
        $tag = $row.Verdict.PadRight(4)
        Write-Host ("  [{0}]  {1}  {2}" -f $tag, $row.Title.PadRight($width), $row.Detail)
        if ($row.Remedy -and $row.Verdict -ne 'ok') {
            Write-Host ("{0}-> {1}" -f (' ' * ($width + 11)).Substring(0, 11), $row.Remedy)
        }
    }
    Write-Host ''

    # An unread fact is not a green row: '?' blocks for the same reason it does in the product's
    # own preflight, which is that a report saying "fine" about a question nobody could ask is
    # worse than one saying it could not ask.
    $blockers = @($script:Rows | Where-Object { $_.Blocking -and $_.Verdict -in @('FAIL', '?') })
    if ($blockers.Count -eq 0) {
        Write-Host 'The guest is reachable.'
        return 0
    }

    $noun = if ($blockers.Count -eq 1) { '1 row blocks' } else { "$($blockers.Count) rows block" }
    Write-Host "$noun reaching the guest. Nothing was run there."
    return 1
}

# ---------------------------------------------------------------------------------------------
# Configuration, and the secrets that are never printed
# ---------------------------------------------------------------------------------------------

function Find-EnvFile {
    # An explicit DOCKERDESK_VM_ENV is honoured even when the file is missing, so a typo in it
    # reads as "that file is not there" rather than silently falling through to another one.
    if ($env:DOCKERDESK_VM_ENV) {
        if (Test-Path -LiteralPath $env:DOCKERDESK_VM_ENV) { return $env:DOCKERDESK_VM_ENV }
        return $null
    }
    foreach ($candidate in $script:EnvFileCandidates) {
        if (Test-Path -LiteralPath $candidate) { return $candidate }
    }
    return $null
}

function Import-EnvFile {
    $path = Find-EnvFile
    if (-not $path) { return $null }

    foreach ($line in Get-Content -LiteralPath $path) {
        $trimmed = $line.Trim()
        if (-not $trimmed -or $trimmed.StartsWith('#')) { continue }
        $split = $trimmed.IndexOf('=')
        if ($split -lt 1) { continue }
        $name = $trimmed.Substring(0, $split).Trim()
        $value = $trimmed.Substring($split + 1).Trim()
        # Only ever fills a blank: an environment variable set for this shell wins, so a one-off
        # override does not require editing the file.
        if (-not [Environment]::GetEnvironmentVariable($name)) {
            Set-Item -Path "env:$name" -Value $value
        }
    }
    return $path
}

function Find-VmRun {
    $candidates = @(
        "${env:ProgramFiles(x86)}\VMware\VMware Workstation\vmrun.exe",
        "$env:ProgramFiles\VMware\VMware Workstation\vmrun.exe",
        "$env:ProgramFiles\VMware\VMware VIX\vmrun.exe"
    )
    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path -LiteralPath $candidate)) { return $candidate }
    }
    $onPath = Get-Command 'vmrun.exe' -ErrorAction SilentlyContinue
    if ($onPath) { return $onPath.Source }
    return $null
}

# ---------------------------------------------------------------------------------------------
# vmrun
# ---------------------------------------------------------------------------------------------

function Invoke-VmRun {
    <#
      Runs vmrun and hands back exit code plus output. Authentication flags are assembled here and
      nowhere else, so no call site can leak one into a log line: the caller passes -Guest to ask
      for guest credentials and never sees them.
    #>
    param(
        [Parameter(Mandatory)] [string[]] $Arguments,
        [switch] $Guest
    )

    $argv = New-Object System.Collections.ArrayList
    if ($env:DOCKERDESK_VM_PASSWORD) {
        $null = $argv.Add('-vp'); $null = $argv.Add($env:DOCKERDESK_VM_PASSWORD)
    }
    if ($Guest) {
        $null = $argv.Add('-gu'); $null = $argv.Add($env:DOCKERDESK_GUEST_USER)
        $null = $argv.Add('-gp'); $null = $argv.Add($env:DOCKERDESK_GUEST_PASSWORD)
    }
    $null = $argv.Add('-T'); $null = $argv.Add('ws')
    foreach ($argument in $Arguments) { $null = $argv.Add($argument) }

    $output = & $script:VmRun @argv 2>&1
    return [pscustomobject]@{
        ExitCode = $LASTEXITCODE
        Output   = ($output | Out-String).Trim()
        Ok       = ($LASTEXITCODE -eq 0)
    }
}

function Get-OneLine {
    param([string] $Text, [int] $Limit = 160)
    if (-not $Text) { return '(no output)' }
    $flat = ($Text -split "`r?`n" | Where-Object { $_.Trim() } | ForEach-Object { $_.Trim() }) -join ' '
    if ($flat.Length -le $Limit) { return $flat }
    return $flat.Substring(0, $Limit) + '...'
}

function Read-SnapshotNames {
    <#
      The names out of a `listSnapshots` answer. One place, because the doctor reports them and
      `revert` picks one out of them, and two parsers of the same output are two chances to disagree
      about what the guest has.

      Nested snapshots come back indented and are addressed by `parent/child`; this returns them as
      written, so a name that is ambiguous is one vmrun refuses rather than one this guesses at.

      Wrap the call in @() — PowerShell unrolls an array on return, so a guest with exactly one
      snapshot hands back a bare string, and under StrictMode `.Count` on it is a terminating error.
      Which is the one guest shape this is most often run against.
    #>
    param([string] $Output)
    return @($Output -split "`r?`n" |
        Where-Object { $_.Trim() -and $_ -notmatch '^Total snapshots' } |
        ForEach-Object { $_.Trim() })
}

function Resolve-Guest {
    <#
      The preamble every action that touches the guest needs: the settings file, vmrun, and which
      .vmx. It was written out three times and is about to be four, and the copy in `screenshot`
      had already drifted — it never checked that DOCKERDESK_VMX was set at all, so a missing one
      reached vmrun as a blank argument and came back as a vmrun usage message.
    #>
    Import-EnvFile | Out-Null
    $script:VmRun = Find-VmRun
    if (-not $script:VmRun) { throw 'vmrun.exe was not found on this host' }
    if ($Vmx) { $env:DOCKERDESK_VMX = $Vmx }
    if (-not $env:DOCKERDESK_VMX) { throw 'DOCKERDESK_VMX is not set' }
    if (-not (Test-Path -LiteralPath $env:DOCKERDESK_VMX)) {
        throw "DOCKERDESK_VMX points at $env:DOCKERDESK_VMX, which does not exist"
    }
    return $env:DOCKERDESK_VMX
}

function Test-GuestRunning {
    param([Parameter(Mandatory)] [string] $VmxPath)
    $running = Invoke-VmRun -Arguments @('list')
    return ($running.Output -match [regex]::Escape([IO.Path]::GetFileName($VmxPath)))
}

function Wait-ForTools {
    <#
      Waits for the guest's agent, because "powered on" and "can run a program" are a minute or more
      apart. Ten minutes, not five: measured on this guest, the agent took longer than five to come
      up after the WSL components were installed, and the script gave up on a machine that was fine.
    #>
    param([Parameter(Mandatory)] [string] $VmxPath)

    $deadline = (Get-Date).AddMinutes(10)
    while ((Get-Date) -lt $deadline) {
        $tools = Invoke-VmRun -Arguments @('checkToolsState', $VmxPath)
        $state = Get-OneLine $tools.Output 40
        if ($state -eq 'running') {
            Write-Host 'VMware Tools is running'
            return 0
        }
        Write-Host "waiting for VMware Tools ($state)"
        Start-Sleep -Seconds 10
    }

    Write-Host 'VMware Tools did not come up within 10 minutes'
    return 1
}

# ---------------------------------------------------------------------------------------------
# The checks
# ---------------------------------------------------------------------------------------------

function Test-Reachability {
    $envFile = Import-EnvFile

    # --- vmrun on this host ---------------------------------------------------------------
    $script:VmRun = Find-VmRun
    if (-not $script:VmRun) {
        Add-Row -Title 'vmrun' -Verdict 'FAIL' -Detail 'not found on this host' `
            -Remedy 'Install VMware Workstation, or put vmrun.exe on PATH.'
        return
    }
    $version = Get-OneLine ((& $script:VmRun 2>&1 | Select-Object -First 3) -join ' ')
    Add-Row -Title 'vmrun' -Verdict 'ok' -Detail "$script:VmRun ($version)"

    # --- where the secrets came from ------------------------------------------------------
    if ($envFile) {
        Add-Row -Title 'settings file' -Verdict 'ok' -Detail "read $envFile" -NotBlocking
    }
    else {
        $looked = if ($env:DOCKERDESK_VM_ENV) {
            "DOCKERDESK_VM_ENV points at $env:DOCKERDESK_VM_ENV, which is not there"
        }
        else {
            'none found; environment variables only'
        }
        Add-Row -Title 'settings file' -Verdict 'warn' -NotBlocking -Detail $looked `
            -Remedy "Put KEY=VALUE lines in $($script:EnvFileCandidates[0]) — .gitignore covers it."
    }

    # --- which guest ----------------------------------------------------------------------
    if ($Vmx) { $env:DOCKERDESK_VMX = $Vmx }
    if (-not $env:DOCKERDESK_VMX) {
        $running = Invoke-VmRun -Arguments @('list')
        $guess = @($running.Output -split "`r?`n" | Where-Object { $_ -match '\.vmx$' })
        $hint = if ($guess.Count -gt 0) { " Running now: $($guess[0])" } else { '' }
        Add-Row -Title 'guest .vmx' -Verdict 'FAIL' -Detail 'no path configured' `
            -Remedy "Set DOCKERDESK_VMX to the guest's .vmx.$hint"
        return
    }

    $vmxPath = $env:DOCKERDESK_VMX
    if (-not (Test-Path -LiteralPath $vmxPath)) {
        Add-Row -Title 'guest .vmx' -Verdict 'FAIL' -Detail "$vmxPath does not exist" `
            -Remedy 'Correct DOCKERDESK_VMX. A path that does not exist is a typo, not a stopped VM.'
        return
    }
    Add-Row -Title 'guest .vmx' -Verdict 'ok' -Detail $vmxPath

    # --- can vmrun open it at all ---------------------------------------------------------
    # listSnapshots is the cheapest call that needs the VM opened, so it is what reports an
    # encrypted VM whose password is absent or wrong. A Windows 11 guest needs a TPM, a TPM needs
    # an encrypted VM, and an encrypted VM answers nothing without -vp.
    $snapshots = Invoke-VmRun -Arguments @('listSnapshots', $vmxPath)
    if (-not $snapshots.Ok) {
        $said = Get-OneLine $snapshots.Output
        # Two distinct messages, and they were worth separating: absent is "A password is required
        # for this operation", wrong is "Incorrect password". Matching only the first sent a wrong
        # password to the branch whose remedy says the problem is not a password.
        if ($said -match 'password is required') {
            Add-Row -Title 'VM encryption' -Verdict 'FAIL' `
                -Detail 'the VM is encrypted and no password was supplied' `
                -Remedy 'Set DOCKERDESK_VM_PASSWORD to the VM encryption password (not the guest login).'
        }
        elseif ($said -match 'ncorrect password') {
            Add-Row -Title 'VM encryption' -Verdict 'FAIL' `
                -Detail 'the VM is encrypted and the password supplied was refused' `
                -Remedy 'DOCKERDESK_VM_PASSWORD is not this VM''s encryption password. It is the one VMware asks for when opening the VM, not the guest login.'
        }
        else {
            Add-Row -Title 'VM encryption' -Verdict '?' -Detail $said `
                -Remedy 'vmrun could not open the VM, and not because of a password. Read the line above.'
        }
        return
    }
    Add-Row -Title 'VM encryption' -Verdict 'ok' -Detail 'the VM opens'

    # --- a snapshot to go back to ---------------------------------------------------------
    $names = @(Read-SnapshotNames $snapshots.Output)
    if ($names.Count -gt 0) {
        # No remedy: a green row does not print one, and there is nothing to fix. What to do with
        # these names is `-Action revert`, which the help says and DD53 added.
        Add-Row -Title 'snapshot' -Verdict 'ok' -Detail ("$($names.Count): " + ($names -join ', '))
    }
    else {
        # Warn and not FAIL: the guest is reachable without one. It is the repeatability that is
        # missing, and an installer test that cannot be repeated is one you get to run once.
        Add-Row -Title 'snapshot' -Verdict 'warn' -NotBlocking -Detail 'none taken' `
            -Remedy 'Take one on a clean guest, so a destructive run can be undone.'
    }

    # --- is it running --------------------------------------------------------------------
    $running = Invoke-VmRun -Arguments @('list')
    if ($running.Output -notmatch [regex]::Escape([IO.Path]::GetFileName($vmxPath))) {
        Add-Row -Title 'power state' -Verdict 'FAIL' -Detail 'the guest is not running' `
            -Remedy "Start it: vmrun -T ws start `"$vmxPath`""
        return
    }
    Add-Row -Title 'power state' -Verdict 'ok' -Detail 'running'

    # --- VMware Tools ---------------------------------------------------------------------
    $tools = Invoke-VmRun -Arguments @('checkToolsState', $vmxPath)
    $toolsState = Get-OneLine $tools.Output 40
    if ($toolsState -ne 'running') {
        Add-Row -Title 'VMware Tools' -Verdict 'FAIL' -Detail $toolsState `
            -Remedy 'Install VMware Tools in the guest. Without it vmrun cannot run a program there.'
        return
    }
    Add-Row -Title 'VMware Tools' -Verdict 'ok' -Detail 'running'

    # --- guest credentials ----------------------------------------------------------------
    if (-not $env:DOCKERDESK_GUEST_USER -or -not $env:DOCKERDESK_GUEST_PASSWORD) {
        Add-Row -Title 'guest login' -Verdict 'FAIL' -Detail 'no guest credentials configured' `
            -Remedy 'Set DOCKERDESK_GUEST_USER and DOCKERDESK_GUEST_PASSWORD.'
        return
    }

    $probe = Invoke-GuestCapture 'echo dockerdesk-reachable'
    if (-not $probe.Ran) {
        # The remedy has to match the message. "Check the credentials" is wrong advice for an error
        # about interactive sessions, and wrong advice costs more than none at all.
        $remedy = if ($probe.VmRunSaid -match 'nvalid user|uthentication fail|assword') {
            'DOCKERDESK_GUEST_USER or DOCKERDESK_GUEST_PASSWORD is wrong. It must be a local account with a non-empty password: vmrun cannot authenticate a Microsoft account signed in with Hello or a PIN.'
        }
        elseif ($probe.VmRunSaid -match 'logged in interactively') {
            'The guest needs a desktop session. Log in once at the guest console.'
        }
        else {
            'Read what vmrun said above; it is not reporting a credential problem.'
        }
        Add-Row -Title 'guest login' -Verdict 'FAIL' -Detail $probe.VmRunSaid -Remedy $remedy
        return
    }
    Add-Row -Title 'guest login' -Verdict 'ok' `
        -Detail "$env:DOCKERDESK_GUEST_USER can run a program"

    # A separate row, because it is a separate fact and the one the preflight depends on: vmrun
    # reports a status and never the guest's standard output, so being able to read what a program
    # there said is a capability of its own and not a consequence of being able to start it.
    if ($probe.Output -match 'dockerdesk-reachable') {
        Add-Row -Title 'guest output' -Verdict 'ok' -Detail "read back through $script:GuestStage"
    }
    else {
        Add-Row -Title 'guest output' -Verdict 'FAIL' `
            -Detail "the program ran and its output did not come back: $($probe.CopySaid)" `
            -Remedy "mkdir $script:GuestStage said: $($probe.MkdirSaid)"
    }
}

# ---------------------------------------------------------------------------------------------
# Running things in the guest
# ---------------------------------------------------------------------------------------------

function Assert-Reachable {
    Test-Reachability
    $code = Write-Report
    if ($code -ne 0) { exit $code }
}

function Read-ConsoleText {
    <#
      Decodes a file of console output as UTF-16LE or UTF-8, by what is in it rather than by what
      the program documents. Guest tools disagree: the product's own exe writes UTF-8, and wsl.exe
      writes UTF-16LE. Reading either with the wrong one is not a crash — it is a string with a NUL
      after every character, or an em dash rendered as three bytes of noise, and both read as data.

      The same decision the product makes in ConsoleTool.Decode, for the same reason.
    #>
    param([Parameter(Mandatory)] [string] $Path)

    $bytes = [IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -eq 0) { return '' }
    if ($bytes.Length -ge 2 -and $bytes[0] -eq 0xFF -and $bytes[1] -eq 0xFE) {
        return [Text.Encoding]::Unicode.GetString($bytes, 2, $bytes.Length - 2)
    }

    # No BOM: UTF-16LE ASCII text puts a zero in every odd byte, which valid UTF-8 never does.
    $zeroes = 0
    for ($i = 1; $i -lt $bytes.Length; $i += 2) {
        if ($bytes[$i] -eq 0) { $zeroes++ }
    }
    if ($bytes.Length -ge 2 -and ($zeroes * 4) -gt $bytes.Length) {
        return [Text.Encoding]::Unicode.GetString($bytes)
    }
    return [Text.Encoding]::UTF8.GetString($bytes)
}

function Invoke-GuestCapture {
    <#
      Runs a command in the guest and brings back both its exit code and what it wrote.

      Two things vmrun does that shape this. It never relays the guest's standard output, so the
      output is redirected to a file inside the guest and copied out — without that, reaching the
      guest to read what its preflight reports produces a bare number. And it does not adopt the
      guest program's exit code as its own: it exits non-zero and states the code in its own
      output. So an exit code of any value is proof the program *ran*, which is what the
      reachability question actually asks; only a failure with no code at all is unreachable.

      No -interactive anywhere. That flag needs an active desktop session and fails with "must be
      logged in interactively" on a guest sitting at the lock screen, which is what a test machine
      does between runs and is the state this harness exists to drive.

      The command is delivered as a batch file rather than as arguments to cmd.exe. Passing
      `/c "... > file 2>&1"` puts the quoting through two layers that both rewrite it — PowerShell's
      native-argument rules and vmrun's own command-line assembly — and what arrives is not what was
      written: `cmd /c exit 0` came back reporting exit code 1, and a redirect silently produced no
      file at all. A batch file run with no arguments has nothing left to mangle.
    #>
    param([Parameter(Mandatory)] [string] $CommandLine)

    $guestOut = Join-Path $script:GuestStage 'last-run.txt'
    $guestScript = Join-Path $script:GuestStage 'run.cmd'
    $hostOut = Join-Path $env:TEMP 'dockerdesk-guest-out.txt'
    $hostScript = Join-Path $env:TEMP 'dockerdesk-guest-run.cmd'
    if (Test-Path -LiteralPath $hostOut) { Remove-Item -LiteralPath $hostOut -Force }

    # Kept rather than discarded: this fails harmlessly when the directory already exists, and it
    # fails fatally when the account cannot write there — and the second one is invisible later,
    # showing up only as an output file that "was not found".
    $mkdir = Invoke-VmRun -Guest -Arguments @(
        'createDirectoryInGuest', $env:DOCKERDESK_VMX, $script:GuestStage)

    # Delete the previous run's output *inside the guest* first. Without this, a command that writes
    # nothing leaves the last one's file in place, the copy succeeds, and the caller is handed stale
    # text as this run's answer — which is worse than an error, because it looks like a result.
    $null = Invoke-VmRun -Guest -Arguments @(
        'deleteFileInGuest', $env:DOCKERDESK_VMX, $guestOut)

    # ASCII: the guest's console code page is not this host's, and a batch file is read as bytes.
    $batch = @(
        '@echo off',
        "$CommandLine > `"$guestOut`" 2>&1",
        'exit /b %ERRORLEVEL%'
    )
    Set-Content -LiteralPath $hostScript -Value $batch -Encoding ascii

    $push = Invoke-VmRun -Guest -Arguments @(
        'copyFileFromHostToGuest', $env:DOCKERDESK_VMX, $hostScript, $guestScript)
    if (-not $push.Ok) {
        return [pscustomobject]@{
            Ran = $false; ExitCode = $null; Output = $null
            VmRunSaid = "copying the command into the guest failed: $(Get-OneLine $push.Output)"
            CopySaid = ''; MkdirSaid = Get-OneLine $mkdir.Output; StagedOk = $mkdir.Ok
        }
    }

    $run = Invoke-VmRun -Guest -Arguments @(
        'runProgramInGuest', $env:DOCKERDESK_VMX, $guestScript)

    $guestCode = $null
    if ($run.Output -match 'non-zero exit code:\s*(-?\d+)') { $guestCode = [int]$Matches[1] }
    elseif ($run.Ok) { $guestCode = 0 }

    $text = $null
    $copy = Invoke-VmRun -Guest -Arguments @(
        'copyFileFromGuestToHost', $env:DOCKERDESK_VMX, $guestOut, $hostOut)
    if ($copy.Ok -and (Test-Path -LiteralPath $hostOut)) {
        $text = Read-ConsoleText $hostOut
    }

    return [pscustomobject]@{
        Ran       = ($null -ne $guestCode)
        ExitCode  = $guestCode
        Output    = $text
        VmRunSaid = Get-OneLine $run.Output
        CopySaid  = Get-OneLine $copy.Output
        MkdirSaid = Get-OneLine $mkdir.Output
        StagedOk  = $mkdir.Ok
    }
}

function Invoke-InGuest {
    param([Parameter(Mandatory)] [string] $CommandLine)

    $result = Invoke-GuestCapture $CommandLine
    if (-not $result.Ran) {
        Write-Host "vmrun could not run it: $($result.VmRunSaid)"
        return 1
    }
    if ($result.Output) {
        foreach ($line in ($result.Output -split "`r?`n")) { Write-Host $line }
    }
    else {
        Write-Host "(it ran and no output came back: $($result.CopySaid))"
    }
    return $result.ExitCode
}

function Publish-ToGuest {
    <#
      Publishes one project and copies it into the guest, returning the exe's guest path.

      Self-contained and single-file, because a clean Windows 11 has no .NET runtime: a
      framework-dependent build died in the guest with 0x80008083 and a link to the download page —
      which is also the fact the installer itself has to reckon with. One file, so one copy call.

      Since DD14 there is one project to publish and one .exe to copy, and every verb is a mode of
      it. That the guest's output still comes back rests on one fact worth naming: cmd waits for a
      windowed executable when it is run from a batch file, and does not when it is typed at an
      interactive prompt. Invoke-GuestCapture writes a batch file, which is the half that waits —
      measured on the host, where the same batch shape returned 407 bytes and exit 0 for
      `--preflight` and 1177 bytes and exit 2 for a verb that does not exist.
    #>
    param(
        [Parameter(Mandatory)] [string] $Project,
        [Parameter(Mandatory)] [string] $Exe
    )

    $publish = Join-Path $env:TEMP "dockerdesk-guest-$Project"
    if (Test-Path -LiteralPath $publish) { Remove-Item -LiteralPath $publish -Recurse -Force }

    Write-Host "publishing a self-contained $Project to $publish"
    & dotnet publish (Join-Path $script:RepoRoot "src\$Project") `
        -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:DebugType=none `
        -o $publish --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with $LASTEXITCODE" }

    $null = Invoke-VmRun -Guest -Arguments @(
        'createDirectoryInGuest', $env:DOCKERDESK_VMX, $script:GuestStage)

    $files = @(Get-ChildItem -LiteralPath $publish -File)
    foreach ($file in $files) {
        $copy = Invoke-VmRun -Guest -Arguments @(
            'copyFileFromHostToGuest', $env:DOCKERDESK_VMX,
            $file.FullName, (Join-Path $script:GuestStage $file.Name))
        if (-not $copy.Ok) { throw "copying $($file.Name) failed: $(Get-OneLine $copy.Output)" }
    }
    Write-Host "copied $($files.Count) file(s) to $script:GuestStage"
    Write-Host ''

    return (Join-Path $script:GuestStage $Exe)
}

function Start-Guest {
    <#
      Powers the guest on and waits for its agent, because the next command needs one and a VM that
      is merely "started" cannot run anything for another minute. The doctor's remedy names the raw
      `vmrun start` line, and it is unusable here for one reason: it would put the encryption
      password on a command line.
    #>
    $vmxPath = Resolve-Guest

    if (Test-GuestRunning $vmxPath) {
        Write-Host 'already running'
    }
    else {
        $start = Invoke-VmRun -Arguments @('start', $vmxPath, 'nogui')
        if (-not $start.Ok) { throw "starting the guest failed: $(Get-OneLine $start.Output)" }
        Write-Host 'started'
    }

    return (Wait-ForTools $vmxPath)
}

function Invoke-Revert {
    <#
      Puts the guest back on a snapshot — the one thing this harness documented and did not do
      (DD53).

      The reason it is worth an action rather than a note in a runbook: every red row in the
      product's preflight is specified against a particular machine state, and a state is spent the
      moment something changes it. DD18 was measured on a guest that had never had WSL, `wsl
      --version` hung there, and the row cost fifteen seconds. The fix was verified against the same
      guest — which by then reported `WSL 2.7.11.0`, because earlier work had installed it. The one
      machine the defect existed on no longer had it, and `Snapshot 1` was sitting in the doctor's
      own output the whole time.

      It refuses rather than prompts. This is run by scripts and by agents as often as by a person,
      and a prompt is either answered blind or hangs forever; -Yes is a decision that survives being
      read back in a transcript. What it prints without -Yes is the whole of what would happen.
    #>
    $vmxPath = Resolve-Guest

    $snapshots = Invoke-VmRun -Arguments @('listSnapshots', $vmxPath)
    if (-not $snapshots.Ok) {
        Write-Host "vmrun could not read the snapshots: $(Get-OneLine $snapshots.Output)"
        Write-Host 'Run scripts\vm.ps1 -Action doctor — it says which of the reasons this is.'
        return 1
    }

    $names = @(Read-SnapshotNames $snapshots.Output)
    if ($names.Count -eq 0) {
        Write-Host 'this guest has no snapshots, so there is nothing to go back to'
        Write-Host 'Take one on a clean guest first, from VMware Workstation.'
        return 1
    }

    if ($Command) {
        $target = @($names | Where-Object { $_ -eq $Command })
        if ($target.Count -eq 0) {
            Write-Host "this guest has no snapshot called '$Command'. It has: $($names -join ', ')"
            return 1
        }
        $snapshot = $target[0]
    }
    elseif ($names.Count -eq 1) {
        $snapshot = $names[0]
    }
    else {
        # Never chosen for you. Reverting to the wrong one of several destroys the same work as
        # reverting to the right one, and takes the machine somewhere nobody asked for as well.
        Write-Host "this guest has $($names.Count) snapshots: $($names -join ', ')"
        Write-Host 'Name one with -Command "<snapshot>".'
        return 1
    }

    $powered = if (Test-GuestRunning $vmxPath) { 'running' } else { 'powered off' }

    Write-Host ''
    Write-Host "revert  $([IO.Path]::GetFileName($vmxPath))"
    Write-Host "  to    $snapshot"
    Write-Host "  now   the guest is $powered"
    Write-Host '  cost  everything the guest has done since that snapshot was taken, including any'
    Write-Host '        installed engine, any WSL distribution, and anything staged under'
    Write-Host "        $script:GuestStage"
    Write-Host ''

    if (-not $Yes) {
        Write-Host 'Nothing was changed. Add -Yes to do it.'
        return 2
    }

    $revert = Invoke-VmRun -Arguments @('revertToSnapshot', $vmxPath, $snapshot)
    if (-not $revert.Ok) {
        Write-Host "reverting failed: $(Get-OneLine $revert.Output)"
        return 1
    }
    Write-Host "reverted to $snapshot"

    # A snapshot taken with the guest powered off leaves it powered off, so the next action would
    # meet "the guest is not running" and a remedy naming a raw vmrun line. Saying which of the two
    # happened here is one line and saves that round trip.
    if (Test-GuestRunning $vmxPath) {
        return (Wait-ForTools $vmxPath)
    }

    Write-Host 'The guest is powered off. Start it with: scripts\vm.ps1 -Action start'
    return 0
}

# ---------------------------------------------------------------------------------------------

switch ($Action) {
    'doctor' {
        Test-Reachability
        exit (Write-Report)
    }
    'start' {
        exit (Start-Guest)
    }
    'revert' {
        exit (Invoke-Revert)
    }
    'screenshot' {
        # The one answer no exit code carries. A guest whose agent never comes up is either booting,
        # waiting at a prompt, or showing an error, and those are indistinguishable from here —
        # `checkToolsState` says "installed" for all three. So look at the screen.
        $null = Resolve-Guest
        $shot = if ($Command) { $Command } else { Join-Path $env:TEMP 'dockerdesk-guest.png' }
        if (Test-Path -LiteralPath $shot) { Remove-Item -LiteralPath $shot -Force }

        # -Guest, because captureScreen is a guest operation: without a login vmrun answers
        # "Anonymous guest operations are not allowed on this virtual machine". Which also means it
        # cannot answer at all while the agent is down — the one moment you most want to look.
        $result = Invoke-VmRun -Guest -Arguments @('captureScreen', $env:DOCKERDESK_VMX, $shot)
        if ($result.Ok -and (Test-Path -LiteralPath $shot)) {
            Write-Host $shot
            exit 0
        }
        Write-Host "captureScreen failed: $(Get-OneLine $result.Output)"
        exit 1
    }
    'run' {
        if (-not $Command) { throw '-Action run needs -Command' }
        Assert-Reachable
        exit (Invoke-InGuest $Command)
    }
    'preflight' {
        Assert-Reachable
        $exe = Publish-ToGuest -Project 'DockerDesk.Tray' -Exe 'DockerDesk.exe'
        exit (Invoke-InGuest "$exe --preflight")
    }
    'engine' {
        Assert-Reachable
        $exe = Publish-ToGuest -Project 'DockerDesk.Tray' -Exe 'DockerDesk.exe'
        $mode = if ($Command) { $Command } else { '--plan' }
        Write-Host "running DockerDesk.exe $mode in the guest"
        exit (Invoke-InGuest "$exe $mode")
    }
}
