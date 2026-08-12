# Improvements

## Block A — The Windows engine (Docker without Docker Desktop)

### §DD2 The engine: upstream Moby in a WSL2 distro this tool owns

The engine is upstream Moby, unmodified, installed into a WSL2 distribution this tool
owns and names — not into the user's Ubuntu, where an apt upgrade or a `wsl
--unregister` they ran for another reason would take the engine with it. An owned distro
also makes the uninstall exact: one `wsl --unregister`, and the machine is as it was.

Two artefacts arrive: the static `dockerd` and `containerd` binaries inside the distro,
and the `docker` CLI on the Windows side, on PATH, talking to that engine. The CLI
matters more than it looks — every tutorial, Dockerfile and CI script a user already has
types `docker`, and a GUI that cannot be scripted around is a GUI people work around
instead.

The download is verified against a published checksum before anything is unpacked, and
pinned to a version this project states rather than "latest": an engine that silently
moves under a user is a support case nobody can reproduce.

Unattended is the requirement, not a nicety. This runs from an installer where there is
no terminal to answer a prompt in, so every step is non-interactive and every failure
names the step it failed at.

### §DD3 Engine lifecycle: three states, and autostart as a choice

An engine is a background service with three states a user cares about — stopped,
starting, running — and the honest one is the third: WSL2 takes seconds to boot the
distro before `dockerd` even opens its socket. A UI that shows "running" the moment the
start command returns is lying for the length of that gap, and the user's first `docker
ps` fails.

So starting is: boot the distro, launch the daemon, then poll the pipe until it answers
or a timeout names which of the two steps did not finish. Stopping is the reverse and
terminates the distro, because an idle WSL2 VM holds memory a laptop user notices.

Autostart at logon is a *choice*, defaulted off, and that is the point of difference
this project is built around. Docker Desktop starting itself on every boot and holding
several gigabytes is the complaint that sends people looking for an alternative. Here
the engine runs when asked, and the setting to make it automatic is one checkbox the
user ticks themselves.

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

### §DD17 A reachable Windows guest, and the snapshot that makes a destructive test repeatable

Every check in this project has only ever run on one machine, and that machine passes.
The preflight's red rows are reached by injected facts alone: virtualization cannot be
switched off here, WSL cannot be unregistered here, and DD16 exists because a rival
engine was installed on this machine the whole time and the report said otherwise. An
install, an uninstall and an upgrade have never been executed at all.

A Windows 11 guest under VMware Workstation closes all of it, and the reason is the
snapshot rather than the guest: reverting to a clean image is what makes a destructive
test repeatable, and an installer is only tested by being run on a machine that has
never seen it.

Two things have to be established first. A reach: `vmrun.exe` with VMware Tools gives
`runProgramInGuest`, `copyFileToGuest` and `revertToSnapshot` over no network at all,
which is why it beats SSH — and either way the credential lives outside this repository.
And nested virtualization: WSL2 needs `Virtualize Intel VT-x/EPT` on, while the same
switch off is the fault injector the virtualization row has never met.

What this delivers is a checked-in script answering, from this machine, whether the
guest is reachable, whether it can be reverted, and what its preflight says — so the
answer is a command anybody re-runs rather than a session somebody remembers. It is
first in the queue because every later verification depends on it.

## Block B — The daemon client (talk to the engine)

### §DD4 The Engine API client: a named pipe, HttpClient, and no dependencies

The engine speaks HTTP over a Windows named pipe, `\\.\pipe\docker_engine`. That is the
whole transport, and .NET has it in the box: `NamedPipeClientStream` plus a
`SocketsHttpHandler` with a custom `ConnectCallback`, handed to `HttpClient`. Roughly
forty lines, and then every endpoint is a JSON call.

Shelling out to `docker.exe` and parsing its output is the alternative, and it is worse
in a way that shows up immediately: a process launch per refresh, output formats that
change between versions, and no way to consume a streaming endpoint without owning the
child's stdout. The API is versioned and documented; the CLI's text output is neither.

Zero third-party dependencies, deliberately, matching how the tray app this borrows its
UI from is built. It keeps the single self-contained `.exe` small, keeps the license
story clean for an Apache-2.0 release, and means a Docker.DotNet release cadence is
never something this project waits on.

The client is typed only where a field is read. A record per response with the four
properties the list needs beats a generated model of an API surface this tool will use a
tenth of.

### §DD5 The event stream: why nothing here polls

`GET /events` is a long-lived response that emits one JSON object per line as containers
are created, started, killed and removed. Reading it is how a list stays true without a
timer, and it is what makes a container someone started in a terminal appear in the
window immediately — which is the behaviour that decides whether the GUI feels like a
view of the engine or a separate opinion about it.

Polling every two seconds is the alternative and it is wrong twice: it burns a request
per tick on an idle machine, and it still shows stale state for up to two seconds at
exactly the moment the user is watching, right after they clicked something.

The stream is a supervised background task with one job: reconnect. The engine stops,
the WSL2 distro is terminated, a laptop suspends — each breaks the connection, and none
of them is an error the user should see. The reconnect loop backs off, and the UI's
engine indicator is what reports the state the loop is in.

Events are coalesced before they reach the UI. A `docker compose up` of eight services
delivers dozens in under a second, and a redraw per event is a visible flicker.

## Block C — The window (claude-tray's elements)

### §DD6 The tray icon: the engine state at a glance

The tray is where this tool lives most of the time, because the answer to "is Docker
up?" is a glance and not a window. A WinForms `NotifyIcon` with a GDI+ drawn icon
carries the state in the icon itself — stopped, starting, running — and the tooltip
names it in words for the case where the colours are ambiguous at 16 pixels.

The menu is deliberately short: start or stop the engine, open the window, quit.
Everything else belongs in the window, and a context menu that grows into a second UI is
how a tray app stops being glanceable.

Quitting does not stop the engine, and that asymmetry is intentional. A container
running a database another process is using must not die because a user closed a tray
icon; the engine is a service with its own lifecycle, and the only thing that stops it
is the menu item that says so.

The pattern, the icon drawing and the theme come from the tray app this project reuses —
same elements, so a user who has both sees one family and this project spends its effort
on the engine rather than on inventing a second look.

### §DD7 The window: the container list, in claude-tray's elements

One window, one list, and the list is containers — because that is what a user opens
this for. Name, image, state, uptime, ports, and the ports are links: a published `8080`
is the thing they actually want, and making them retype `localhost:8080` is a small
daily tax a GUI exists to remove.

WPF with the built-in .NET Fluent theme, which is how the tray app this borrows from
gets a Windows 11 look with no extra package — the same `ThemeMode` switch, the same
light and dark following the OS, the same self-contained `.exe` story. Reusing that
styling is not only economy: two apps by the same author that look unrelated read as two
unfinished apps.

State arrives from the event stream and never from a refresh button. The list is a view
of the engine, so it is correct without being asked.

Empty is a designed state, not a blank grid. No containers with the engine down says
start the engine; no containers with it up says so plainly. The first screen a new user
sees is usually empty, and a table with headers and nothing under them is where a free
alternative loses them.

## Block D — Container operations (what a user came to do)

### §DD8 Container actions: four verbs, and what surrounds them

Start, stop, restart, remove. Four verbs against the engine's own endpoints, and the
work is almost entirely in what surrounds them.

Each action is optimistic and reversible in the UI: the row goes to a pending state
immediately, and the event stream — not the HTTP response — is what confirms it. A stop
can take the full ten-second grace period before the container is killed, and a row that
sits unchanged for ten seconds reads as a click that did nothing.

Remove asks first, and only for what is not recoverable. A container is cheap to
recreate from an image, so the prompt exists mainly for the running case, where removal
implies a kill. The dialog names the container and says what will be lost, because "Are
you sure?" is a question nobody has the information to answer.

Failures are shown where the action was taken, on the row, in the engine's own words. A
port already in use, a volume still mounted, a container that exited immediately — these
are the ordinary answers, and the engine's message is more useful than any paraphrase
this tool could write.

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
