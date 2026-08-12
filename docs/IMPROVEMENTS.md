# Improvements

## Block A — The Windows engine (Docker without Docker Desktop)

### §DD16 The rival row asks where a vendor installs, not what owns the docker command

The rival row shipped looking for `%ProgramFiles%\Docker\Docker\Docker Desktop.exe`, a
`%LOCALAPPDATA%\Programs\Rancher Desktop` executable, and an open
`\\.\pipe\docker_engine`. Docker Desktop now installs per user, into
`%LOCALAPPDATA%\Programs\DockerDesktop`, and its engine is only listening while the app
is running. So a machine with Docker Desktop installed and stopped answers no to all
three.

Measured on the development machine: `docker.exe` resolves to
`C:\Users\alexa\AppData\Local\Programs\DockerDesktop\resources\bin\docker.exe`, `wsl -l
-v` lists a registered `docker-desktop` distribution, and both `C:\Program Files\Docker`
and `%LOCALAPPDATA%\Docker` exist — while the preflight prints `[ok] Container engine —
nothing else owns the docker command or the docker_engine pipe` and exits 0.

That is the one row that must never be wrongly green. Its remedy is "uninstall it
first", and the reason is that two engines competing for one pipe leave the user with
neither — so a false green does not merely omit a warning, it clears an install to walk
into the exact collision the check was added to prevent.

What the design already said is the fix: the fact is whether something **owns the
`docker` command**, and the shipped probe never asked. Resolving `docker` the way the
shell resolves it answers it in one read, independent of where any vendor chose to
install this year, and a registered `docker-desktop` distribution is the second signal
that survives the app being shut down. The three existing checks stay — a path and an
open pipe are still evidence, and evidence carried into the report is what lets a user
argue with it.

### §DD18 The WSL2 row asks the one question that hangs on a machine with no WSL

`wsl.exe` ships in System32 on a Windows 11 that never had WSL, so its presence was
never the answer, and the shipped probe knows that: it runs `wsl --version` for the
kernel version. What that command does on such a machine could not be known until a
clean one existed. It does not answer and it does not fail — it hangs, and the probe's
own fifteen-second timeout ends it.

Measured on a fresh Windows 11 guest, build 26200, through `scripts/vm.ps1 -Action
preflight`:

    [?   ]  WSL2   C:\WINDOWS\system32\wsl.exe did not finish within 15 seconds
             -> Run `wsl --update` in an administrator terminal.

Two defects in one row. The verdict is `Unknown`, which is honest and blocks — that part
of the design held. But the report costs fifteen seconds of silence to say nothing, on
the single most common machine this installer will meet, and the remedy it offers is
`wsl --update`, which updates a WSL that is not installed.

The same guest answers `wsl --status` immediately: exit code 50, and the sentence *"O
Subsistema do Windows para Linux não está instalado. Você pode instalar executando
'wsl.exe --install'"* — the right verdict and the right remedy, in milliseconds, from
the tool itself. `--status` is also the older command, so a machine too old for
`--version` answers it too.

The fix is to ask the cheap question first and reach for `--version` only once something
has said WSL is there. A timeout then becomes what it should always have been: the
report admitting it could not read a fact, rather than the normal path for a bare
machine.

### §DD19 HypervisorPresent answers I am virtualized, not I can host a hypervisor

The virtualization row reads `Win32_ComputerSystem.HypervisorPresent` first and treats
true as proof. That ordering was added for a real reason and it is not the mistake: a
running Hyper-V claims the firmware bit, so Windows reports
`VirtualizationFirmwareEnabled` as false on a machine that is plainly virtualizing, and
reading the bit first sends the user into a BIOS to enable something already on.

The mistake is what true means. `HypervisorPresent` is true of every machine running
*under* a hypervisor, which is every virtual machine there is. So the row answers "I am
virtualized" to a question that asked "can I host one", and those come apart exactly
where it matters.

Measured on the Windows 11 guest, build 26200, two commands apart and nothing changed
between them:

    [ok  ]  Hardware virtualization  enabled — a hypervisor is already running
    This machine can host a container engine.        exit 0

    [FAIL]  ImportDistribution  importing dockerdesk exited -1 saying: O WSL2 não pode ser
            iniciado porque a virtualização não está habilitada nesta máquina.

That guest is a VMware VM with `Virtualize Intel VT-x/EPT` switched off, so nested
virtualization is not exposed to it and WSL2 cannot start. The preflight cleared the
install anyway, and the install failed halfway — which is the exact sequence the whole
check exists to prevent, and worse than DD16, because here the report is confidently
green rather than merely incomplete.

Both facts are needed and neither is sufficient: whether a hypervisor is running, and
whether this machine is itself a guest. A row that cannot tell them apart should say
`Unknown`, not `Pass`.

### §DD20 The CLI follows its active context, not the pipe this engine serves

The engine is reached through `\\.\pipe\docker_engine`, which is what the CLI's
`default` context names and what every tutorial assumes. But the CLI does not read that
context unless it is the active one, and the active one is a per-user setting in
`~/.docker/config.json` that any Docker distribution may have written. Docker Desktop
writes one.

Measured on the development machine, with this project's engine answering and no
`DOCKER_HOST` set:

    docker version
      failed to connect to the docker API at npipe:////./pipe/dockerDesktopLinuxEngine

    docker context ls
      default           npipe:////./pipe/docker_engine
      desktop-linux *   npipe:////./pipe/dockerDesktopLinuxEngine

    docker --context default version
      client 29.7.2 / server 29.7.2 / api 1.55 / os linux/amd64

So the engine was running, serving the right pipe, and the user's own `docker` went
somewhere else and reported the daemon as absent. The tool looks broken and nothing is
wrong with it.

This is not DD16. That one is about the preflight failing to *notice* a rival before
installing. This is about what happens after a clean install on a machine that once had
one: the leftover context outlives the uninstall, because it is configuration in the
user's profile rather than anything the rival's installer removes.

Two candidate answers, not equivalent. Registering a context of this project's own and
making it active is what Docker Desktop does, and it takes the setting over from the
user. Reading the active context and saying so — "your docker points at X, this engine
is at `\\.\pipe\docker_engine`" — leaves the choice with them. The second suits a tool
whose argument is that it takes nothing over, and picking between them is the task
rather than the implementation.

## Block B — The daemon client (talk to the engine)

## Block C — The window (claude-tray's elements)

### §DD21 A new tray icon does not get the visible row

The tray's whole argument is that answering "is Docker up?" costs a glance. Windows 11
does not grant that to a new icon: unless a user has promoted it, a freshly registered
notification icon goes into the overflow behind the chevron, where a glance costs a
click first.

Observed with `NotifyIcon.Visible` true and the process alive: a capture of the
notification area showed the icons already promoted on this machine and not this one.
What it does not settle is which of two things happened — the icon went to the overflow,
the documented default, or it never registered. This is one observation, not a
diagnosis.

Both possibilities are worth the same first step: confirm where the icon actually went,
on a machine that has never seen this application. The test guest is exactly that
machine and has never had a tray icon promoted, which the development machine cannot
say.

If it is the overflow, there is a decision behind it and not a fix. Promoting itself is
what Docker Desktop does, and it is also what every user resents when six installers
each decide their icon deserves the visible row. Either leave it in the overflow and say
so in the installer, or promote once at first run and never again. What is not an option
is a section that promises a glance while a new user gets a chevron.

### §DD22 A window is verified by rendering it, not by copying the screen

Windows here are verified by copying the pixels inside their rectangle off the screen.
That reads whatever is actually there, which is not the window: shipping DD7 it twice
photographed something else — an editor holding the guest's credentials, and a messaging
app holding a medical appointment. Both reached a transcript, which deleting the file
afterwards does not undo.

claude-tray already solved this and its script says why in its own docstring: a screen
copy is the fallback, and the preferred path is rendering the window off-screen, where
there is nothing else in the frame to catch. Its `--capture-*` verbs use
`RenderTargetBitmap` over the page's own content; the screen copy is kept only for
popups, which a render cannot reach.

That script is worth reading rather than reinventing. It carries four assertions earned
from real wrong captures, and one of them is exactly what was missing here: **no foreign
window in front of it overlaps the rectangle about to be copied**. Its history records
that nine sampled points could not answer that, the number of points covering a window
being the number of pixels in it, so it asks about the region instead.

So: a `--capture-window <path>` verb on the tray that renders off-screen and photographs
nothing else, and the overlap-checked screen copy kept for what a render cannot see.
Until then, verifying a window on a machine with anything else open is a privacy
incident waiting for its second turn, and it has already had one.

## Block D — Container operations (what a user came to do)

### §DD9 Logs: a followed stream, de-framed and capped

A container that exited two seconds after starting has one useful artefact, and it is
the log. Without it the GUI can report the failure and nothing about its cause, which
sends the user to a terminal — and a tool people leave to do the actual diagnosis is a
launcher, not a desktop.

`GET /containers/{id}/logs?follow=1` is the same streaming shape the event reader
already handles, with one wrinkle: a container started without a TTY multiplexes stdout
and stderr into a framed stream, eight bytes of header per chunk. Rendered without
de-framing, every line carries visible control bytes, and that is the bug this task
exists to not ship.

The view is a window per container, follow on by default, with a copy-all that puts the
whole buffer on the clipboard — because the next thing a developer does with a stack
trace is paste it somewhere.

The buffer is capped and drops from the front. A chatty container emits megabytes a
minute, and an unbounded collection in a window someone left open overnight is the leak
that gets this tool called heavy — the exact reputation it was built to escape.

### §DD10 A shell in a container: launch the terminal the user already has

Reading a log answers what happened; a shell answers why. Inspecting a config file the
image baked in, running a client against the database in the container, checking what
the process actually sees for an environment variable — none of that is reachable from a
list and all of it is one command away.

The cheap implementation is the right one: launch Windows Terminal — falling back to the
console host where it is absent — running `docker exec -it <id> <shell>`. The
alternative is a terminal emulator inside the window, which means attaching to the exec
stream, handling resize, and interpreting ANSI escape sequences. That is a large amount
of code to reimplement a program the user already has, and it competes for effort with
the engine work that is this project's actual reason to exist.

Which shell is a guess with a fallback: `bash`, then `sh`. A distroless or scratch image
has neither, and the answer there is to say so rather than to open a window that
immediately closes.

The container must be running. On a stopped one the action is offered but disabled, with
the reason on the tooltip, because a disabled control that explains itself is the
difference between a limitation and a bug.

## Block E — Images, volumes and networks

### §DD11 Images: sorted by size, with dangling and in-use named

Images are where the disk goes. A year of pulling base images leaves tens of gigabytes
in layers, most of it dangling — untagged intermediates no container references — and on
a laptop with a 512 GB SSD that is a real problem a GUI is genuinely better at solving
than a command line, because the fix is a judgement over a list.

So the list is sorted by size, states the total, and marks what is dangling and what a
container still uses. Those two facts are what make the decision, and the CLI's answer
to "which of these can I delete" is three commands and a mental join.

Removal is per image, and prune is the bulk door: dangling only by default, with the
reclaimable total named before the click and reported after it. `prune -a` is
deliberately not offered as one button — it deletes every image no *running* container
uses, which on a developer's machine is most of them, and the second half of that
sentence is not on the command's own warning.

An image in use cannot be removed, and the engine says which container holds it. That
answer is passed through, not paraphrased, since it names the row the user has to deal
with first.

### §DD12 Volumes: the irreversible list

A volume is the one thing here that is not recreatable. An image re-pulls, a container
rebuilds, and a deleted volume is a database that is gone — so this list has a different
job from the image list: not reclaiming space, but making the irreversible act legible.

Three columns carry it: the volume's name, its size on disk, and which containers mount
it. The last one is why volumes get their own task rather than a tab on the images work.
The engine's volume list does not report holders, so it is a join over the container
list, and the `docker-compose` naming convention — `<project>_<volume>` — is what tells
a user that the orphan they are looking at belonged to a project they deleted months
ago.

Anonymous volumes are the common orphan and the reason prune is offered at all: every
`docker run -v /data` without a name leaves one behind, and nothing ever collects them.

Deletion of a volume nothing mounts is confirmed once, naming it. Deletion of one still
mounted is refused by the engine, and that refusal is correct — the answer is to deal
with the container, which the dialog names.

## Block F — Installer and distribution (free, Apache 2.0)

### §DD13 Apache-2.0, stated where the choice is made

The licence is the feature. Docker Desktop is free only below a headcount and revenue
threshold, and the reason anyone tries an alternative is that their employer crossed it.
A visitor who cannot establish the terms in the first ten seconds assumes the same trap,
so Apache-2.0 is stated in the README's first paragraph, in the repository's licence
field, and in the window's About — not only in a `LICENSE` file nobody opens.

Apache-2.0 rather than MIT for the patent grant, which is what makes a corporate legal
review straightforward — and a corporate legal review is precisely the room this tool
has to survive to be adopted at all.

`NOTICE` is the other half and the one that gets forgotten. This ships or downloads
upstream Moby and CLI binaries, each with its own licence, and a distribution that omits
their attribution is not compliant however permissive its own terms are. The file lists
every bundled component, its licence, and where it came from.

The claim is scoped honestly: this tool is Apache-2.0, and the engine it installs
carries Moby's own terms. Overstating that would be the same category of surprise the
project exists to remove.

### §DD14 One exe, an Inno Setup installer, and an uninstall that asks

One `.exe`, self-contained, and an Inno Setup installer around it — the arrangement the
tray app this borrows from already ships, so the build script, the version chain from
the `.csproj` and the signing story are patterns to copy rather than decisions to make.

Per-user by default, into `%LOCALAPPDATA%`, with no administrator prompt for the
application itself. This matters more here than for an ordinary tray app: the audience
is developers on managed corporate laptops, and a UAC prompt at install time is where a
large share of them stop. The engine's WSL2 feature may still need one, which is why the
preflight report says so before anything is downloaded rather than a dialog appearing
halfway through.

Uninstall removes what was installed and asks about what was created: the owned WSL2
distro holds every image and volume the user has, and deleting gigabytes of their data
without a question is unforgivable in a tool whose pitch is that it respects the
machine.

No bundled updater in this task. An installer that starts a background service to check
for versions is the weight this project is an answer to; the release feed can be read by
the window later, and that is a separate decision to argue separately.

### §DD15 CI: a second machine, and a release with checksums

A Windows desktop application built on one developer's machine has one build
environment, and every bug that environment hides is found by the first person who
downloads a release. CI is the second machine, and on a project whose whole promise is
"install this and Docker works", a broken release is the only defect that matters.

Two jobs. A check on every push: build, run the test suite, and run the roadkeep lint
that already governs the files in `docs/` — the gate this repository was wired with,
which is worth nothing until something red stops a merge. Then a release job on a tag:
publish the single-file executable, compile the installer, and attach both to the GitHub
release with their SHA-256 sums, because a download whose integrity cannot be checked is
a download a corporate proxy is right to block.

The Windows runner is not optional. Cross-compiling a WPF application is not the thing
under test — what is under test is that the produced `.exe` starts on a clean Windows
image, which is exactly the failure a local build cannot see.

What CI cannot do is verify the engine install: a hosted runner has no nested
virtualization, so that path stays a manual check against the release candidate, and
saying so here is better than a green tick that means less than it appears to.
