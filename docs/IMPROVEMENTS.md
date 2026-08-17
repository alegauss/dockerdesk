# Improvements

## Block A — The Windows engine (Docker without Docker Desktop)

### §DD129 Every way the tray ends

DD128 hangs the stop off `TrayApplication.Quit`, which is the menu item and the `--quit`
signal. Those are not the only ways this process ends. A logoff, a shutdown and an End
task in Task Manager tear it down without any of its handlers running, and the detached
`--run` it launched has no parent to notice — so the virtual machine survives the
session that asked for it, which is the same held memory DD128 removes from one exit
only.

Windows announces two of those three. `SystemEvents.SessionEnding` fires on logoff and
shutdown, and what the stop needs there is a spawn rather than a wait: launching
`--stop` is a `Process.Start` and returns long before the distribution is down, which is
what makes it safe inside a window Windows may end at any moment.

A kill gets no notice and this task does not pretend otherwise. What it buys is that the
ordinary end of a working day — signing out, shutting the machine down — leaves nothing
running, and the honest limit is that an engine orphaned by a crash is still there at
the next launch, where the tray already reports it correctly and the stop item already
works.

### §DD137 DD137

The engine host is launched detached and hidden, which is right — a console window the
user did not ask for is not an improvement. The cost is that everything it says goes
nowhere. When it stops, the line naming what it saw is written to a window that was
never readable and is gone by the time anybody asks.

That was the expensive part of the failure DD134 repairs. The daemon's own log survives
inside the distribution and was decisive; the host's account of why it walked away was
not recoverable at all, and the difference between "the host decided the engine was
dead" and "something killed the host" had to be argued from Hyper-V events and a
sixty-second gap rather than read.

So the host keeps a log of its own beside the install, next to the provisioning log that
is already there. What goes in it is small: what it did, what it saw when it stopped,
and each restart it attempted with the reason. Not a trace of every poll — a file that
grows without bound is its own defect, and a quiet engine should write nothing.

### §DD141 The error that knows the answer

An agent driving this install hits a stopped engine as a raw connection failure: "failed
to connect to the docker API at npipe:////./pipe/docker_engine … check if the daemon is
running". That message is docker's own, written for a world where the daemon could be
anyone's. Here it is not: FreeWilly ships the docker.exe on PATH and knows the engine is
its own, so the one thing the reader needs — freewilly do engine start — is known where
the error is printed and left out of it.

Observed three times in one working session, driving compose builds for an unrelated
project. Each time it read as a broken Docker install rather than a stopped service, and
recovery meant going to read the CLI help. The `read ps` verb already answers this well,
reporting "engine stopped, nothing is answering the pipe" — the gap is that nothing
points at it from where the failure surfaces, which is the docker command already run.

The smallest form is the shim recognising the connection error for its own pipe and
appending one line naming the verb. It need not start anything: an agent told what to
run will run it, and starting a daemon as a side effect of an unrelated command is a
bigger decision than this warrants.

Related to DD137, which keeps evidence of why the host stopped. This is the other half:
what the reader of the failure does next.

## Block B — The daemon client (talk to the engine)

## Block C — The window (claude-tray's elements)

## Block D — Container operations (what a user came to do)

## Block E — Images, volumes and networks

## Block F — Installer and distribution (free, Apache 2.0)

### §DD130 The preflight runs before the install, not after it

The order is the defect. `CurStepChanged` runs the preflight at `ssPostInstall`, which
is after every file has been written, the PATH entry made and the Run value set — so a
machine without WSL2 receives a complete installation of a tool whose one job it cannot
do, plus a message box and a text file explaining that. The engine download is correctly
skipped, and that is the only thing the late check still buys.

So the read moves in front of the copy. `[Files]` is one entry by DD14, so
`ExtractTemporaryFile` puts that same executable in `{tmp}` cheaply, and a wizard page
inserted before `wpReady` runs `--preflight --json` there. The verdict decides whether
Next is available at all; nothing about the judgement changes, because it is the same
code answering — the refusal to write a second opinion in Pascal stands, and this is
what makes it affordable to keep.

Two behaviours have to survive the move. A silent install must not grow a modal it never
had: it stops with a distinct exit code and the report on disk, which is what an
unattended deployment can read. And the report keeps being written to `{app}` when there
is an `{app}` to write it to, because somebody is going to want it open while they
change a setting — when Setup stops before creating that directory, `{tmp}` is gone with
Setup and the page itself has to carry what the file would have said.

### §DD131 The page a blocked install lands on

What a blocked machine gets today is a message box naming `wsl.exe --install
--no-distribution` and a path to a text file. That is exactly right for a reader who
already knows what WSL2 is, and it is the whole of the experience for a reader who does
not — the term is never expanded, the command is in a box that cannot be copied from,
and there is no way to find out whether the fix worked without running Setup again.

The page says four things, in this order: what WSL2 is in one sentence and why a Linux
container engine on Windows cannot exist without it; the numbered steps, each one action
long; the command itself, selectable, with a button that puts it on the clipboard; and a
link to Microsoft's own instructions at learn.microsoft.com/en-us/windows/wsl/install,
which is the page Docker Desktop links for the same reason. `TNewLinkLabel` opens it in
the browser rather than printing a URL nobody can click.

The button that matters most is Check again. It re-runs the read in place and releases
Next the moment the row turns green, so the loop between fixing and finding out is one
click rather than one reinstall. Next stays disabled while the row is red — the point of
DD130 is that there is nothing past this page worth doing — and Cancel is left as the
honest way out.

### §DD132 Setup turns the feature on

Docker Desktop's installer does this and its logs name the step: `EnableFeaturesAction`,
"Required features: VirtualMachinePlatform, Microsoft-Windows-Subsystem-Linux". Its Go
side carries the other half — `wslexec.IsNotInstalled`, `CheckWslUpdate`,
`RebootRequired`, and the sentence "Install it by running: wsl.exe --install
--no-distribution", which is the command this project's preflight already prints. The
difference between the two products here is not knowledge. It is that Docker Desktop
runs elevated from its first dialog, so turning a Windows feature on costs it nothing
extra.

This installer is `PrivilegesRequired=lowest` on purpose, and that decision is not being
reversed for this. The elevation is bought per step instead: `ShellExec` with the
`runas` verb on `wsl.exe`, so there is no UAC prompt to install the application and
exactly one to turn the feature on, raised only after the user presses the button that
asks for it. A refused prompt, or an account that cannot elevate at all, is not an error
— it lands back on DD131's page with the steps still there.

Two details are worth taking from the same source. `--no-distribution` does not exist on
older WSL builds, and Docker Desktop probes for it (`wslexec.FlagSupported`) rather than
assuming; without that probe the generous version of this feature installs Ubuntu on
somebody's machine uninvited. And the feature needs a reboot before it is usable, so the
run ends by asking for one and arranging for the install to be picked up on the other
side rather than leaving a Setup that has to be remembered.

## Block G — The agent surface (an agent operates this, and pays in tokens)

## Block H — The public surface (the site a reader and an agent both read)
