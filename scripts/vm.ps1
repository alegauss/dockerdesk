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

  `doctor` and `preflight` only read. Nothing here reverts a snapshot or writes to the guest
  unless you ask for it by name, because a revert discards whatever the guest currently holds.

.PARAMETER Action
  doctor    every reachability fact, as a report (default)
  preflight build the product preflight, copy it to the guest, run it there, print what it said
  run       run one command in the guest and print its output

.PARAMETER Command
  For -Action run: the command line to execute in the guest.

.PARAMETER Vmx
  Path to the guest's .vmx. Overrides DOCKERDESK_VMX.

.NOTES
  Secrets are never parameters and never printed. Set them as environment variables:

    DOCKERDESK_VMX              full path to the .vmx
    DOCKERDESK_VM_PASSWORD      the VM *encryption* password (a Windows 11 guest needs a TPM,
                                which means an encrypted VM, which vmrun cannot open without it)
    DOCKERDESK_GUEST_USER       an account inside the guest
    DOCKERDESK_GUEST_PASSWORD   its password

  Or put them as KEY=VALUE lines in a file outside this repository and point
  DOCKERDESK_VM_ENV at it. The default location searched is d:\tmp\dockerdesk-vm.env.
  Nothing in this repository ever holds a credential.
#>
[CmdletBinding()]
param(
    [ValidateSet('doctor', 'preflight', 'run')]
    [string] $Action = 'doctor',

    [string] $Command,

    [string] $Vmx
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:RepoRoot = Split-Path -Parent $PSScriptRoot
$script:DefaultEnvFile = 'd:\tmp\dockerdesk-vm.env'
$script:GuestStage = 'C:\dockerdesk-test'

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

function Import-EnvFile {
    $path = $env:DOCKERDESK_VM_ENV
    if (-not $path) { $path = $script:DefaultEnvFile }
    if (-not (Test-Path -LiteralPath $path)) { return $null }

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
        Add-Row -Title 'settings file' -Verdict 'warn' -NotBlocking `
            -Detail 'none read; environment variables only' `
            -Remedy "Put KEY=VALUE lines in $script:DefaultEnvFile, or point DOCKERDESK_VM_ENV at a file."
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
    $names = @($snapshots.Output -split "`r?`n" |
        Where-Object { $_.Trim() -and $_ -notmatch '^Total snapshots' } |
        ForEach-Object { $_.Trim() })
    if ($names.Count -gt 0) {
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

    $probe = Invoke-VmRun -Guest -Arguments @(
        'runProgramInGuest', $vmxPath, '-interactive',
        'C:\Windows\System32\cmd.exe', '/c', 'echo dockerdesk')
    if (-not $probe.Ok) {
        Add-Row -Title 'guest login' -Verdict 'FAIL' -Detail (Get-OneLine $probe.Output) `
            -Remedy 'Check DOCKERDESK_GUEST_USER and DOCKERDESK_GUEST_PASSWORD against the guest.'
        return
    }
    Add-Row -Title 'guest login' -Verdict 'ok' -Detail "$env:DOCKERDESK_GUEST_USER can run a program"
}

# ---------------------------------------------------------------------------------------------
# Running things in the guest
# ---------------------------------------------------------------------------------------------

function Assert-Reachable {
    Test-Reachability
    $code = Write-Report
    if ($code -ne 0) { exit $code }
}

function Invoke-InGuest {
    param([Parameter(Mandatory)] [string] $CommandLine)

    $result = Invoke-VmRun -Guest -Arguments @(
        'runProgramInGuest', $env:DOCKERDESK_VMX, '-interactive',
        'C:\Windows\System32\cmd.exe', '/c', $CommandLine)
    Write-Host $result.Output
    return $result.ExitCode
}

function Invoke-GuestPreflight {
    <#
      Publishes the product's preflight, copies it into the guest and runs it there. This is the
      whole reason the reach exists: the report's red rows have never been produced by a machine,
      only by injected facts, and this is the machine that can produce them.
    #>
    $publish = Join-Path $env:TEMP 'dockerdesk-guest-preflight'
    Write-Host "publishing preflight to $publish"
    & dotnet publish (Join-Path $script:RepoRoot 'src\DockerDesk.Preflight') `
        -c Release -o $publish --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with $LASTEXITCODE" }

    $null = Invoke-VmRun -Guest -Arguments @(
        'runProgramInGuest', $env:DOCKERDESK_VMX, '-interactive',
        'C:\Windows\System32\cmd.exe', '/c', "if not exist $script:GuestStage mkdir $script:GuestStage")

    foreach ($file in Get-ChildItem -LiteralPath $publish -File) {
        $copy = Invoke-VmRun -Guest -Arguments @(
            'copyFileFromHostToGuest', $env:DOCKERDESK_VMX,
            $file.FullName, (Join-Path $script:GuestStage $file.Name))
        if (-not $copy.Ok) { throw "copying $($file.Name) failed: $(Get-OneLine $copy.Output)" }
    }
    Write-Host "copied $((Get-ChildItem -LiteralPath $publish -File).Count) file(s) to $script:GuestStage"
    Write-Host ''

    return Invoke-InGuest "$script:GuestStage\dockerdesk-preflight.exe"
}

# ---------------------------------------------------------------------------------------------

switch ($Action) {
    'doctor' {
        Test-Reachability
        exit (Write-Report)
    }
    'run' {
        if (-not $Command) { throw '-Action run needs -Command' }
        Assert-Reachable
        exit (Invoke-InGuest $Command)
    }
    'preflight' {
        Assert-Reachable
        exit (Invoke-GuestPreflight)
    }
}
