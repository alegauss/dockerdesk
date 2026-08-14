#Requires -Version 5.1
<#
.SYNOPSIS
  Copies a FreeWilly window off the screen to a PNG - the fallback, for what a render cannot see.

.DESCRIPTION
  `FreeWilly.exe --capture-window <out.png> [tab]` is the preferred way to photograph a window, and
  this script is not it. That verb renders the window's own visual tree off-screen, where there is
  nothing else in the frame; this one copies the pixels that are actually on the screen inside the
  window's rectangle, which is a different and more dangerous thing.

  It exists for the one case a render cannot reach: a popup - a context menu, a balloon tip - is its
  own top-level window and is not in the main window's visual tree, so a RenderTargetBitmap over that
  tree cannot see it.

  A popup also exists only while it is open, and until DD67 nothing opened one: this script launched
  the exe with --window and copied whatever it found, and the only thing it found was the window
  DD61 had just taught it to refuse. So no popup this product draws had ever been photographed. The
  exe opens its own now, behind `--show-menu`, which is why that is the default below - the driving
  stays inside the process that owns the menu, and no Win32 click reaches into another process's UI.

  Measured 2026-08-14, first run: 237x168, and two independent runs byte-identical. Every pixel on
  every edge is the menu's own #808080 border, so nothing behind it is in the file.

  Because it is a screen copy, it verifies what it captured. Shipping DD7, a copy like this twice
  photographed something else: an editor holding the guest's credentials, and a messaging app holding
  a medical appointment. Both reached a transcript, which deleting the file afterwards does not undo.
  Five assertions stand between that and a green run, and the success line names the window and the
  pid so the next wrong capture reports itself:

    1. the window handle belongs to the process this script launched;
    2. no other FreeWilly window is open, unless -IgnoreOtherInstances says to allow it;
    3. the window has no system backdrop, so it is not transmitting what is behind it;
    4. no foreign window in front of it overlaps the rectangle about to be copied;
    5. what came back is not a single flat colour.

  (3) is DD61, and it is the assertion that decides what this script is FOR. Measured 2026-08-13:
  with (4) satisfied and nothing overlapping, the copy still carried a blurred image of the desktop
  behind the window - another application's content legible through the frame - because a Fluent
  window's backdrop is translucent and composites what is behind it by design. Z-order reasoning
  cannot answer for that: the intruder is not in front of the window, it is showing through it. A
  printed warning was the first response and it was not enough - a warning is not a refusal, and the
  file gets written either way.

  The refusal is not "this is the main window", which would be a name rather than a reason. DWM is
  asked directly: DWMWA_SYSTEMBACKDROP_TYPE answers 2, 3 or 4 for a window that has opted into Mica,
  acrylic or the tabbed backdrop, and answers nothing at all for a window that never set it. So the
  refusal is positive evidence of transmission, and a popup - a context menu, a balloon tip, which is
  the one thing a render cannot reach and this script exists for - is not refused by it.

  Measured on this machine: the tray's window answers 2 (DWMSBT_MAINWINDOW). So this script is for
  popups, `--capture-window` is for the window, and (3) is what makes that true rather than advisory.

  (4) is the one that decides the file otherwise, and it is asked about the region rather than about
  sampled points: the number of points that finally covers a window is the number of pixels in it. The
  Z order above the window is enumerated and each frame intersected with the copy rectangle, which
  answers for the whole area in one pass and names the intruder, its pid and the rectangle it covers.
  An overlap FAILS rather than cropping the copy around it - a file quietly trimmed to dodge an
  intruder is a picture of something nobody asked for.

  Being in the foreground is only a proxy for (4), and one this script cannot insist on: Windows
  refuses SetForegroundWindow to a process that does not own focus. So the window is raised and pushed
  topmost as best effort, and then what is in front of it is checked directly.

  (5) is this project's own, measured 2026-08-13 while shipping DD21. A copy of the notification area
  on this machine came back as exactly one distinct colour: the session was there, explorer was there,
  [Environment]::UserInteractive was true, and the display was not rendering anything a copy could
  read. A flat rectangle is not a picture of a window, and without this the script would have written
  it and exited 0.

  What it copies is the window's PAINTED frame, from DWMWA_EXTENDED_FRAME_BOUNDS, and not
  GetWindowRect: that one spans the invisible resize border and the drop-shadow margin, so the copy
  carries a strip of whatever is behind the window down its edges. The run prints how much it trimmed.

  claude-tray carries a fifth assertion this one does not: that the page is not still showing its own
  loading text. It reads that string out of `lang\*.json`, and this project has no such file and no
  asynchronous page that announces itself as loading. Adding a check that matches nothing is the shape
  of defect this whole file exists to stop, so it is named here and left out.

.PARAMETER Exe
  The executable to launch. Defaults to the Release publish output.

.PARAMETER AppArgs
  Arguments passed to the exe. Defaults to --show-menu, which holds the tray's context menu open with
  no icon and no window behind it - the one thing this script can photograph (DD67). Pass
  @('--show-menu', 'running') for the menu a running engine offers.

.PARAMETER Out
  Output PNG path.

.PARAMETER WaitMs
  Milliseconds to wait for the window to appear and draw. Default 8000, and the number is measured:
  a cold start of the single-file self-contained .exe took longer than 2500ms to put its window up on
  this machine, and the run refused with "no FreeWilly window" on an application that was fine. The
  wait is a deadline polled every 200ms, so a warm start still returns as soon as the window exists.

.PARAMETER IgnoreOtherInstances
  Allow the capture to proceed with another FreeWilly window open. Assertion (2) exists because a
  capture once returned a picture of another instance's window, so this is opt-in and named in the
  output when used.

.PARAMETER KeepOpen
  Leave the launched process running. By default it is stopped, so a script run does not leave a tray
  icon behind.
#>
[CmdletBinding()]
param(
    [string] $Exe,
    [string[]] $AppArgs = @('--show-menu'),
    [string] $Out,
    [int] $WaitMs = 8000,
    [switch] $IgnoreOtherInstances,
    [switch] $KeepOpen
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
if (-not $Exe) {
    $Exe = Join-Path $repo 'src\FreeWilly.Tray\bin\Release\net10.0-windows\win-x64\publish\FreeWilly.exe'
}
if (-not $Out) { $Out = Join-Path $repo 'docs\_preview\menu.png' }

if (-not (Test-Path -LiteralPath $Exe)) {
    Write-Host "not found: $Exe"
    Write-Host 'Build it first: build\build.cmd'
    exit 1
}

Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

public static class Win {
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr p);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassNameW(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out RECT v, int size);
    [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")] public static extern int DwmGetInt(IntPtr h, int attr, out int v, int size);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();

    public static string TextOf(IntPtr h) { var sb = new StringBuilder(512); GetWindowTextW(h, sb, sb.Capacity); return sb.ToString(); }
    public static string ClassOf(IntPtr h) { var sb = new StringBuilder(256); GetClassNameW(h, sb, sb.Capacity); return sb.ToString(); }

    /// Top-level visible windows in Z order, front first. EnumWindows already answers in Z order,
    /// which is what makes "everything above this window" a prefix of the list rather than a search.
    public static List<IntPtr> ZOrder() {
        var found = new List<IntPtr>();
        EnumWindows((h, p) => { if (IsWindowVisible(h)) found.Add(h); return true; }, IntPtr.Zero);
        return found;
    }

    /// DWMWA_SYSTEMBACKDROP_TYPE, or -1 where the window never opted into one. DD61: 2, 3 and 4 are
    /// Mica, acrylic and the tabbed backdrop, and every one of them composites what is behind the
    /// window into the pixels a screen copy reads. A window that never set it answers an error rather
    /// than a zero, which is why "unreadable" and "none" are the same answer here and both allow.
    public static int BackdropOf(IntPtr h) {
        int value;
        return DwmGetInt(h, 38 /* DWMWA_SYSTEMBACKDROP_TYPE */, out value, sizeof(int)) == 0 ? value : -1;
    }

    /// The painted frame, which is smaller than GetWindowRect by the resize border and the shadow.
    public static RECT PaintedFrame(IntPtr h) {
        RECT r;
        if (DwmGetWindowAttribute(h, 9 /* DWMWA_EXTENDED_FRAME_BOUNDS */, out r, Marshal.SizeOf(typeof(RECT))) == 0) return r;
        GetWindowRect(h, out r);
        return r;
    }
}
'@

# Before any rectangle is read. Without this the host is DPI-virtualised and the two APIs below answer
# in different coordinate spaces: measured on this machine, GetWindowRect came back virtualised while
# DWMWA_EXTENDED_FRAME_BOUNDS came back in physical pixels, and the run printed a painted frame LARGER
# than the window rectangle it is a subset of - "trimmed -1469px right", which is not a thing.
[void][Win]::SetProcessDPIAware()

# A popup has no title, so the title filter alone excluded the one thing this script is actually for
# (DD61). These are what a menu, a balloon tip and a WinForms popup are drawn as.
$script:PopupClasses = @('#32768', 'tooltips_class32')

function Get-FreeWillyWindows {
    param([int] $OwnerPid = 0)
    $wanted = @()
    foreach ($h in [Win]::ZOrder()) {
        $owner = 0
        [void][Win]::GetWindowThreadProcessId($h, [ref] $owner)
        $proc = Get-Process -Id $owner -ErrorAction SilentlyContinue
        if (-not $proc) { continue }
        if ($proc.ProcessName -ne 'FreeWilly') { continue }

        $class = [Win]::ClassOf($h)
        $title = [Win]::TextOf($h)
        $popup = ($script:PopupClasses -contains $class) -or ($class -like 'WindowsForms10.Window*')
        if (($title -notlike '*FreeWilly*') -and -not $popup) { continue }

        # Z order is front first, so a menu that is open is found before the shell under it — which is
        # what makes "capture the popup" the default whenever there is one.
        $wanted += [pscustomobject]@{
            Handle   = $h
            Pid      = $owner
            Title    = $title
            Class    = $class
            Backdrop = [Win]::BackdropOf($h)
            Mine     = ($OwnerPid -ne 0 -and $owner -eq $OwnerPid)
        }
    }
    return $wanted
}

function Get-BackdropName {
    param([int] $Type)
    switch ($Type) {
        2 { 'Mica (DWMSBT_MAINWINDOW)' }
        3 { 'acrylic (DWMSBT_TRANSIENTWINDOW)' }
        4 { 'tabbed (DWMSBT_TABBEDWINDOW)' }
        default { "type $Type" }
    }
}

# ---------------------------------------------------------------------------------------------
Write-Host "launching $Exe $($AppArgs -join ' ')"
$proc = Start-Process -FilePath $Exe -ArgumentList $AppArgs -PassThru
$deadline = (Get-Date).AddMilliseconds($WaitMs)
$mine = $null
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Milliseconds 200
    $all = Get-FreeWillyWindows -OwnerPid $proc.Id
    $mine = @($all | Where-Object { $_.Mine })
    if ($mine.Count -gt 0) { break }
}

function Stop-Launched {
    if (-not $KeepOpen) {
        try { $proc.Kill() } catch { }
    } else {
        Write-Host "left running: pid $($proc.Id)"
    }
}

# (1) the window belongs to the process this script launched
if (-not $mine -or @($mine).Count -eq 0) {
    Write-Host "no FreeWilly window from pid $($proc.Id) within ${WaitMs}ms. Nothing was written."
    Stop-Launched
    exit 1
}
$target = @($mine)[0]

# (2) no other instance's window
$others = @(Get-FreeWillyWindows -OwnerPid $proc.Id | Where-Object { -not $_.Mine })
if ($others.Count -gt 0) {
    $named = ($others | ForEach-Object { "pid $($_.Pid) '$($_.Title)'" }) -join '; '
    if (-not $IgnoreOtherInstances) {
        Write-Host "another FreeWilly window is open ($named). Nothing was written."
        Write-Host 'A capture once returned a picture of another instance. Pass -IgnoreOtherInstances to allow it.'
        Stop-Launched
        exit 1
    }
    Write-Host "-IgnoreOtherInstances: proceeding with $named also open"
}

# (3) DD61: the window is not transmitting what is behind it. No flag turns this off — a switch that
# waives a privacy refusal is a refusal that means nothing, and the safe way to photograph this
# window already exists and renders off-screen where there is nothing else in the frame.
if ($target.Backdrop -ge 2) {
    Write-Host ("'{0}' ({1}) has a {2} system backdrop. Nothing was written." -f `
        $target.Title, $target.Class, (Get-BackdropName $target.Backdrop))
    Write-Host 'A translucent backdrop composites what is behind the window, so the pixels inside its'
    Write-Host 'rectangle are partly somebody else''s content. No overlap check can see that: the'
    Write-Host 'intruder is not in front of the window, it is showing through it.'
    Write-Host "Use: FreeWilly.exe --capture-window <out.png> [tab] - it renders the window's own"
    Write-Host 'visual tree off-screen, where nothing else can be in the frame.'
    Write-Host 'This script is for a popup, which a render cannot reach and which has no backdrop.'
    Write-Host 'Open one: this script''s own default, -AppArgs @(''--show-menu'').'
    Stop-Launched
    exit 1
}

# Best effort only: Windows refuses SetForegroundWindow to a process that does not own focus.
[void][Win]::SetForegroundWindow($target.Handle)
[void][Win]::SetWindowPos($target.Handle, [IntPtr] (-1), 0, 0, 0, 0, 0x0001 -bor 0x0002)  # HWND_TOPMOST, NOSIZE|NOMOVE
Start-Sleep -Milliseconds 400

$outer = New-Object Win+RECT
[void][Win]::GetWindowRect($target.Handle, [ref] $outer)
$frame = [Win]::PaintedFrame($target.Handle)
$x = $frame.Left; $y = $frame.Top
$w = $frame.Right - $frame.Left; $h = $frame.Bottom - $frame.Top
Write-Host ("window pid {0} '{1}' painted frame {2}x{3} at ({4},{5}); trimmed {6}px left, {7}px right, {8}px bottom off GetWindowRect" -f `
    $target.Pid, $target.Title, $w, $h, $x, $y, ($frame.Left - $outer.Left), ($outer.Right - $frame.Right), ($outer.Bottom - $frame.Bottom))

if ($w -le 0 -or $h -le 0) {
    Write-Host "the window measured ${w}x${h}. Nothing was written."
    Stop-Launched
    exit 1
}

# (4) nothing in front of it overlaps the rectangle about to be copied
$z = [Win]::ZOrder()
$above = @()
foreach ($hwnd in $z) {
    if ($hwnd -eq $target.Handle) { break }
    $above += $hwnd
}

$copy = New-Object System.Drawing.Rectangle $x, $y, $w, $h
$intruders = @()
foreach ($hwnd in $above) {
    $owner = 0
    [void][Win]::GetWindowThreadProcessId($hwnd, [ref] $owner)
    if ($owner -eq $target.Pid) { continue }          # our own popups are the thing being captured
    $r = [Win]::PaintedFrame($hwnd)
    $rect = New-Object System.Drawing.Rectangle $r.Left, $r.Top, ($r.Right - $r.Left), ($r.Bottom - $r.Top)
    if ($rect.Width -le 0 -or $rect.Height -le 0) { continue }
    $hit = [System.Drawing.Rectangle]::Intersect($copy, $rect)
    if ($hit.IsEmpty) { continue }
    # A window covering the whole screen behind everything (the desktop, a wallpaper host) is not an
    # intruder; one with no title and no class worth naming usually is not either. Named anyway, so
    # the refusal can be argued with rather than guessed at.
    $p = Get-Process -Id $owner -ErrorAction SilentlyContinue
    $intruders += [pscustomobject]@{
        Process = if ($p) { $p.ProcessName } else { "pid $owner" }
        Pid     = $owner
        Class   = [Win]::ClassOf($hwnd)
        Title   = [Win]::TextOf($hwnd)
        Covers  = "$($hit.Width)x$($hit.Height) at ($($hit.X),$($hit.Y))"
    }
}
if ($intruders.Count -gt 0) {
    Write-Host "$($intruders.Count) window(s) in front of it overlap the copy rectangle. Nothing was written."
    $intruders | ForEach-Object { Write-Host ("  {0} (pid {1}, {2}) '{3}' covers {4}" -f $_.Process, $_.Pid, $_.Class, $_.Title, $_.Covers) }
    Write-Host 'Cropping around an intruder would be a picture of something nobody asked for.'
    Write-Host "Prefer: FreeWilly.exe --capture-window <out.png> - it renders off-screen and cannot photograph anything else."
    Stop-Launched
    exit 1
}

# The copy itself
$bmp = New-Object System.Drawing.Bitmap $w, $h
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($x, $y, 0, 0, $bmp.Size)
$g.Dispose()

# (5) what came back is not a single flat colour
$seen = New-Object 'System.Collections.Generic.HashSet[int]'
for ($py = 0; $py -lt $h; $py += 4) {
    for ($px = 0; $px -lt $w; $px += 4) {
        [void]$seen.Add($bmp.GetPixel($px, $py).ToArgb())
        if ($seen.Count -gt 8) { break }
    }
    if ($seen.Count -gt 8) { break }
}
if ($seen.Count -le 1) {
    $bmp.Dispose()
    Write-Host "the copy came back as one flat colour, so the screen is not rendering anything a copy can read."
    Write-Host 'Nothing was written. This is a locked or non-rendering session, not a window defect.'
    Write-Host "Use: FreeWilly.exe --capture-window <out.png> - a render needs no desktop at all."
    Stop-Launched
    exit 1
}

$dir = Split-Path -Parent $Out
if ($dir -and -not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Host ("captured {0}x{1} of pid {2} '{3}' -> {4}" -f $w, $h, $target.Pid, $target.Title, $Out)
Stop-Launched
exit 0
