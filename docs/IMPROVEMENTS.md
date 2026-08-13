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

### §DD34 One meaning, one declaration, and every window reduced to what is its own

There is no `App.xaml`, no `ResourceDictionary` and no `Brand`. Every style this app has
is declared inside the window that first needed it, which is why `MainWindow.xaml` and
`LogWindow.xaml` each carry their own `BooleanToVisibilityConverter`, their own
`BasedOn` button style with the same comment explaining the same Fluent trap, and the
same `ThemeMode`, `FontFamily` and font-stack spelled out twice.

The colour is the sharper case. `#E5484D` means one thing — *this is the engine's
refusal, or stderr* — and it is written four times: three in `MainWindow.xaml` and once
in `LogWindow.xaml`. None of the four is pinned by a test, so all four can move
independently, and the failure is quiet: a failure line under a container row and a
stderr line in its log window in two reds, saying two things where the point was one.
The engine's own three states have a second, separate source in `StateIcon.ColourFor`,
which is GDI+, and `ShowEngine` converts it to a WPF brush by hand at one call site.

claude-tray met this exact problem and its answer is `Brand.cs`: the value, as bytes,
declared once, with each edge converting — GDI+ for the tray icon, a frozen `Brush` for
WPF, a hex string for anything that wants text. Its docstring says why a value and not a
brush, and the reasoning transfers unchanged.

So: one `Theme.xaml` merged at application scope for what is markup, one `Palette` for
what is a value, and every window reduced to what is actually its own.

### §DD35 The shell owns the chrome and a list owns its page

`MainWindow.xaml` is 447 lines and `MainWindow.xaml.cs` is 586, and between them they
own the engine banner, three lists, three header rows, three empty states, two prune
confirmations, the log windows and the terminal launch. The three lists are not variants
of one thing in the file; they are three hand-written copies of the same stanza — a
header `Grid`, a `ListView` with a duplicated column set, an `Empty` panel, a
`Refresh…Async`, a `Redress…`. DD12 and networks each add a fourth and a fifth copy.

claude-tray splits this the other way. `MainWindow` there is 104 lines of XAML and 129
of code-behind, and owns *only* the chrome: a nav strip of `RadioButton`s in one group,
and a `DestinationHost` grid. Each destination is a `UserControl` with its own header,
navigation and footer, built the first time it is navigated to and then kept alive
collapsed, so a scan, a chart's history and a half-edited setting survive switching
away. The heavier ones are split again by concern across partial classes.

The parts of that worth taking are the ones that are structure and not taste: the shell
owning the chrome, one page per list, pages built lazily and kept alive. The tab strip
is a smaller question — WPF's default `TabItem` headers are the least-designed pixels in
this window, and the accent-underline strip is what the sibling app already looks like.

The test that pins each header's columns to its rows moves with the page it belongs to.

### §DD36 A row carries its state, not a wall of captions

A container row today is five plain `TextBlock`s and up to six `Button`s with word
captions — Logs, Shell, Start, Stop, Restart, Remove — in a fixed 320px column. At forty
containers that is two hundred captions, and the eye has nothing to skip past. Nothing
highlights on hover, so there is no feedback that a row is a row; a click anywhere but a
button does nothing at all.

State is the column actually scanned and the one drawn with the least: `running` and
`exited` in the same tertiary grey as the status beside them, which restates it in the
daemon's words. claude-tray's rows put that kind of fact in a tinted chip — a rounded
`Border`, a translucent tint that works on both surfaces, and a tooltip carrying the
evidence behind the claim, because a chip is an assertion. `RowStyle` resolves those
brushes once per render, not per row.

The actions are pressed once a session. Hiding them behind hover or a context menu — the
pattern `SourceRowTemplate` already uses — costs a discovery problem, so the answer is
not all-or-nothing: keep the one or two verbs a row is actually opened for visible, and
move the rest. Logs stays visible in every state, for the reason DD9 gives.

What must not be lost: the pending word, the engine's own refusal under the row, and
Shell disabled-with-a-tooltip rather than hidden. Those are answers, and a redesign that
drops them is a worse row that looks better.

### §DD37 A heading that sorts, and a box that narrows

Every heading in this window is a dead `TextBlock`. NAME, IMAGE, STATE, REPOSITORY, SIZE
— none does anything when clicked, and the order a list arrives in is the order it is
read in. Images are sorted by size, which DD11 chose and is right as a default;
containers are in whatever order the daemon returned them, which is creation order and
answers nothing.

There is no filter anywhere. A developer's machine carries thirty to sixty images and a
dozen containers, and the only way to reach one is the scrollbar. The window is rarely
opened to survey the machine; it is opened with one container in mind, and that is the
case it serves worst.

claude-tray templates a sorting heading as a `Button` whose template is a `TextBlock`:
it looks like a label, behaves like a control, and its own comment gives the reason — a
heading that reorders the list on click and offers no affordance is a feature only its
author finds. Hover brightens it, and the sorted column carries a glyph.

So: every heading sorts, each list keeps its default, and one filter box per list
narrows by the fields already on the row — a name, an image, a repository, a port.
Filtering is over the rows in hand, never a second call to the daemon, and it survives a
refresh from the event stream — the part easy to get wrong. An empty result is a third
empty state, saying what was typed.

### §DD38 A window is drawn from a fixture, not from whatever is running

Nothing here can be looked at without a running daemon behind it. Every window takes a
`DockerApi` and asks it for the rows it draws, so seeing the images tab means having
images, seeing a failure line means causing a failure, and seeing a pending row means
catching a stop mid-flight. The three empty states, designed prose, are the hardest to
reach on purpose.

That has two costs. A change to the window is reviewed by describing it, and a
screenshot is whatever the machine happened to be running that afternoon — which is also
somebody's container names in a public README. And it is the reason DD22 exists at all:
a capture verb needs something to capture, and today that something is the live screen.

claude-tray solves it with fixtures and flags. `ContextFixture`, `AccountFixture`,
`EnvironmentFixture` and `SampleRoot` build a known machine; `--settings`, `--stats`,
`--context --window` and the capture flags render one page from it; `PageWindow` is a
bare host that exists so a preview is the page and not the shell around it. The captures
are deterministic, which is what makes them reviewable.

The equivalent here is a fake `DockerApi` — a small set of containers, images and
volumes chosen to cover running, exited, published and exposed ports, dangling and
in-use images, an anonymous volume, a pending row and a refusal — plus a flag that opens
any page against it. The fixture is also the fastest route to the empty states, which
are otherwise reached by deleting things.

### §DD39 The window remembers where it was and what was being read

`WindowStartupLocation="CenterScreen"`, `Width="1040" Height="560"`, and nothing
remembered. A tool opened several times a day lands in the middle of the primary monitor
every time, at a size chosen for a laptop, on a desk that has two screens and a place
this window belongs. The log window is `CenterOwner`, which is right for a child, and it
too forgets whatever it was resized to.

The tab is the sharper loss. Somebody clearing disk space works in Images; somebody
debugging works in Containers. The tab is a piece of state the user set on purpose, and
it is discarded the moment the window closes — including when it closes because the
engine was restarted.

None of this needs a settings file of its own. Window placement, the last destination,
and later the sort and filter DD37 adds are a handful of values, and there is already an
`ArtefactStore` and an `EnginePaths` that know where this application's data lives.

Two things it must get right, because both are how this feature is usually got wrong. A
saved rectangle is restored only if it still lands on a monitor that exists — a window
remembered onto a laptop's docked second screen is a window that opens off-screen the
next morning. And a maximised window is remembered as maximised plus its restore bounds,
not as a rectangle the size of the screen.

## Block D — Container operations (what a user came to do)

## Block E — Images, volumes and networks

## Block F — Installer and distribution (free, Apache 2.0)

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

### §DD32 Shipping the surface includes shipping how it is found

A surface nobody discovers is one nobody uses, and the discovery cost is otherwise paid
once per session forever: an agent meeting a machine reaches for `docker` because that
is what it knows is there.

So the install ships how it is found. Three artefacts, none of them large. A skill
carrying the verb list and the one rule that matters — reach for `dockerdesk read`
before `docker` — since a skill is loaded on demand and costs nothing on the turns it is
not needed. An allowlist entry proposed at install time, `Bash(dockerdesk read:*)`,
which is the line that makes DD24 pay: the split is worth nothing until a settings file
expresses it. And a `read context --as brief` that writes a project's own file from the
live machine, so what a session starts knowing is generated rather than hand-maintained
and rotting.

The install proposes and never writes: a tool that edits a user's agent configuration
without asking has broken the rule that nothing here surprises the human, and the
allowlist is exactly the file where that would be least forgivable.

What this must not become is a second place where the surface is described. The skill
names verbs and defers; every sentence explaining what a verb does lives in `--help`,
which is one copy and is the one a caller already has. Two descriptions of one surface
drift, and the one loaded every session is the one that drifts unnoticed.

## Block G — The agent surface (an agent operates this, and pays in tokens)

### §DD23 The measurement is the first deliverable, not a footnote

The constitution this block implements is `docs/specs/DD23-agent-first-dockerdesk.md`,
and every figure in its accounting table is an estimate: 30–60k tokens and 15–30 calls
for a canonical diagnosis, against a target of 2–5k and five. An estimate is what a
design is argued from. It is not what a build can refuse.

So this lands first, and it lands as two artefacts. A benchmark drives the canonical
task — bring a stack up, find why one service is not answering — twice: once through
`docker` the way an agent reaches it today, once through whatever surface exists, and it
reports the ratio in calls and in estimated tokens. A budget file holds a ceiling per
response shape, read by that test, so a build that made the surface more expensive fails
instead of mentioning it.

Raising a ceiling stays allowed. It is a deliberate, reviewable act, and the commit that
raises one says what the tokens bought. What the file prevents is the raise nobody
argued for.

Two things it must not become. It is not a performance suite: wall-clock is a different
question with a different answer, and mixing them means neither number is trusted. And
it must not measure only a well-formed script — a benchmark over calls that are all
correct cannot see the argument an agent gets wrong, and an unknown flag accepted in
silence is the expensive case: a refusal costs one round trip, while a silently dropped
argument costs a wrong outcome nobody notices.

### §DD24 Read and write, separated where a permission rule can see them

The head this needs is a console binary beside `dockerdesk-preflight`, which already
establishes the shape: `--json`, `--help`, and an exit code that means something. What
is new is the split.

`docker ps` and `docker rm -f -v` are one string to an allowlist. A user either grants
the whole verb namespace — which permits deleting a volume — or approves every call by
hand. `dockerdesk read …` beside `dockerdesk do …` makes the rule expressible in one
line, and what that buys is not keystrokes: most of the calls in a diagnosis mutate
nothing, and each of them currently costs the most expensive unit there is, which is a
human round trip.

`read` is a promise and not a naming convention. A verb under it that writes is a
defect, and the guard belongs in a test rather than in review.

Two constraints from the constitution land here rather than later, because retrofitting
either is a rewrite. Addresses are names — a compose service as
`svc:<project>/<service>`, a container as its name — since a 64-hex id changes on
recreate and then has to be threaded across calls by hand. And every response shape
registers a ceiling with the budget DD23 gates, so the limit exists before the first
payload does.

The surface stays a shape over the Engine API. No `build`, no `push`, no registry
credentials: what `docker` already answers well is not re-wrapped.

### §DD25 One call that replaces the session's first five

The first thing any session does is ask what this machine is doing, and today that is
`ps -a`, `compose ps`, `version`, `system df` and a read of the compose file — repeated
three to five times as the state moves, because a table carries no cursor.

One command answers it, in a line format rather than JSON, because entity JSON spends
most of its bytes on punctuation, repeated keys and authoring metadata nothing reads:

```
engine  running  wsl:dockerdesk  api=v1.43  ctx=default(ok)
api     up 4m    healthy   svc:shop/api    :8080→8080 listening
worker  exited 137  ×3/2m   svc:shop/worker  OOM  limit=512m
disk    images 14G (4.2G dangling)  volumes 2.1G (1 unused)
cursor  c:4f21a0
```

Four properties make it work and none of them are cosmetic. **Deterministic order**, so
it caches and a diff means something. **Name addressing**, per DD24. **A hard ceiling
with an explicit truncation cursor**, never a silent cut — a payload that quietly drops
a row is worse than one that refuses. And **state stated rather than probed**, so the
caller never spends a call discovering whether a capability is there.

Note what the sample already answered: `OOM limit=512m` closes the canonical task's
question without a second call. That is the whole argument for this command, and DD23 is
what turns it from an argument into a number. `--json` stays for callers that parse
rather than read.

### §DD26 The diagnostic join, over the verdict model the preflight already has

Asking why a container is not answering costs `ps -a`, `logs`, `inspect`, `port` and
`network inspect`, and the join across them is done in the caller's head. The expensive
one is `inspect`: three to six hundred lines of JSON, paid in full, read for
`State.ExitCode`, `State.OOMKilled`, `HostConfig.PortBindings` and `Mounts`.

One command does the join and returns the conclusion: state and exit code, whether the
kernel killed it and against which limit, restart count over a window, health, the
declared ports beside whether the host port is actually listening, the mounts beside
whether each resolves, and the last lines that went to stderr rather than the whole log.

It is not a new framework. The preflight already carries exactly this vocabulary — a
row, a verdict, and a remedy — assembled by `PreflightInspection` and rendered for a
person or as JSON by `DockerDesk.Preflight`, with an exit code that means something.
This is that model pointed at a container instead of at a machine, which is reuse of a
concept the repository has already paid for.

The verdict is the deliverable, not the field dump. A command that returns forty facts
and no conclusion has moved the join rather than closed it, and the caller pays for the
thirty-six it did not need. Where there is no conclusion to draw, saying so is also a
conclusion, and it costs less than the fields would have.

### §DD27 Logs get a cursor, a dedup and a ceiling, and then become a file

Logs are the largest token sink in this domain and the one with no analogue anywhere
else: a container that restarts eight times writes the same stack trace eight times, and
`--tail` is the only instrument, so the caller either truncates blind or pays for all of
it.

Four arguments close it. `--since <cursor>` reads the delta, using the cursor DD25 hands
out. `--level` filters. `--dedup` collapses an identical repeat to a count — `× 47` is
the answer, and forty-seven copies of it is the same answer at forty-seven times the
price. `--budget <n>` truncates **with a cursor and never in silence**, since a payload
that quietly drops the end reads exactly like a log that ended.

The fifth argument is the one that matters most and is the least obvious. `--out <path>`
writes the log to disk instead of returning it. An agent's cheapest and most reliable
tools are `Grep` and `Read` over a file: against a stream it pays for every line, and
against a file it pays for the lines that match. A ten-megabyte log becomes affordable
rather than merely truncated, and the ceiling stops being a guess about which end held
the answer.

That inversion is a law and not a trick. Where a payload is unbounded and the question
is narrow, the file is the interface and the stream is the fallback.

### §DD28 Every refusal carries the Windows fact that explains it

`port is already allocated` is the refusal an agent cannot act on. The daemon knows a
bind failed; it does not know what holds the socket, and no Docker command anywhere can
tell it. A Windows process can, and this one is already running.

So every refusal on this surface carries the fix, what is allowed, the nearest match and
a minimal correct example — and, where Windows knows something the daemon does not, the
fact that explains it:

```json
{ "type": "…/errors/port-allocated", "status": 409,
  "heldBy": { "pid": 14032, "image": "node.exe", "path": "d:\\Git\\other-project" },
  "fix": "Stop process 14032, or change the host port in docker-compose.yaml:12" }
```

`heldBy` is the argument for this product having an agent surface at all. A JSON
re-wrapping of what `docker` already says adds nothing, since `--format json` exists.
The joins the Engine API cannot make are the whole of the value, and they are available
here only because this is a Windows process rather than a client.

The same shape covers the two rows already on the backlog: a rival engine answering the
pipe (DD16) and a stale context sending the CLI elsewhere (DD20) both currently surface
as `cannot connect to the Docker daemon`, which is one sentence for three unrelated
causes with three unrelated remedies. An error that costs a round trip to interpret is a
defect, and one that names the wrong cause is worse than none.

### §DD29 A label is the audit trail, and a scoped reclaim is the undo

An agent that starts three containers and a volume to reproduce a defect has no way to
take them back. `prune` is scoped to the machine, cannot distinguish what this session
made from what the user made last week, and is therefore the one command nobody
delegates — so the leftovers stay, and the next session inherits a machine with a
history it did not write.

Docker already carries the mechanism: every object takes labels. Everything created
through `dockerdesk do` is stamped `dockerdesk.session=<id>`, and `do reclaim --session`
removes exactly that set and nothing else. Scoped by label, cleanup is an undo, and an
undo is safe enough to be routine in a way that a whole-machine sweep never becomes.

The same label answers the other half, which is what the human sees. `read changes` can
say what this session created without inferring it from timestamps, and a reclaim can
print what it is about to remove before it removes it. A destructive call takes a
confirm token computed over that list: right is the token and the list, wrong is a
refusal naming what would go now, so a plan that went stale between the two calls
refuses rather than deleting something that arrived in between.

Volumes stay the exception the tool is loudest about. A container comes back and a
volume does not.

### §DD30 Cheap textual proof, because the agent cannot look

The daemon reporting `running` and the service actually answering are different facts,
and the gap between them is where an agent stops being able to make progress. A
container can be up with its port bound and answer nothing: the process died inside it,
the app bound to `127.0.0.1` rather than `0.0.0.0`, the health check has never gone
green, the bind mount resolved to an empty directory because a Windows path did not
survive the hop into WSL.

None of that is visible from the Engine API, and all of it is currently closed by a
human looking at a browser and reporting back — which is the most expensive cycle in the
system and the reason two of the three in the canonical task exist at all.

So the surface returns cheap textual proof instead: the host port accepts a connection
*from Windows*, an optional request returns a status, the health check's current state
and its last output, each mount resolved with the file count on the far side. Pass or
fail with a reason, and an exit code.

The same command is the readiness primitive, which removes the other recurring failure:
waiting is currently a sleep loop the caller writes, and a `--wait --timeout` that
returns when the condition holds — or fails saying which part did not — replaces polling
with one call that costs one answer.

### §DD31 A cursor over the stream the tray is already reading

Everything else in these two blocks makes one session cheaper. This is the only one that
makes the *next* session cheaper, which over a week is the larger number.

`read changes --since <cursor>` returns what moved: `worker restarted ×3, exited 137`
and nothing else. A follow-up session syncs in one small call rather than re-deriving
the machine from DD25's pack, and the pack's own cursor is what it is given.

Architecturally this is nearly free here, which is the reason it is worth doing rather
than deferring. The tray is already a long-running process holding `/events` open — that
is what DD7's container list is fed by — so a change feed is a cursor over a stream that
is already running, plus a bounded ring behind it. The comparable feature in an
agent-native CMS required building a server to hold the audit trail; here the server is
the icon the user already started.

Two constraints. The ring is bounded, so a cursor older than it must be answered with
`too old, re-read the context` rather than with a silent partial — the failure mode of a
delta that quietly skips is worse than no delta, because nothing downstream can detect
it. And the feed reports what the *user* did too: a container the human stopped from the
tray is a change, and a feed that only reports the agent's own writes is a memory of its
intentions rather than of the machine.

### §DD33 MCP is a second head, and it is not free

The constitution inverts the usual order and lands every capability on the CLI first, so
this is the task that records the condition under which that decision is revisited
rather than the task that builds the thing.

The measurement is borrowed and it is specific. In Viglet Shio, whose ten design laws
this repository's constitution adapts, the MCP tool list is re-sent on **every turn of
every session before any work happens**, measured at roughly 2 400 tokens across eleven
tools, with a recorded moment of one token of headroom. Its own review concluded that
for an agent which has a terminal, a CLI verb costs nothing per turn while a tool schema
is a permanent tax — and that the tax is worth paying only for a client with no shell,
which could otherwise reach nothing.

Nobody operates Docker on Windows from a client with no shell. So the fixed cost
currently buys nothing here, and it would be worse than in that case because the natural
tool count on this surface is higher.

What would change the answer is evidence of such a caller, and then this lands as a
**second head over the same methods** — never a parallel implementation, which is how a
surface acquires two sets of semantics. Capped at six tools, with the schema total held
by the same budget file DD23 gates, and a raise argued in the commit that makes it.

## Block H — The public surface (the site a reader and an agent both read)

### §DD49 The social card, and the marks around it

The og:image points at logo.svg. No major platform rasterises SVG for a card and none
scales a 32-pixel mark to 1200 by 630, so every share of this link renders an empty
rectangle with a domain under it - on the one surface where the project has no second
chance to introduce itself. The fix is small and generated: an SVG carrying the mark,
the name and the one-line claim, rasterised on every build, so the card cannot drift
from the site the way a hand-exported image does. The favicon and the brand marks belong
in the same task because they are the same asset set and the same question - what this
looks like at 16 pixels, at 32, and at full width - and the answer is the tray's own
vocabulary rather than a second mark, since the app already draws a filled disc, a
broken ring and a plain ring for engine state and those shapes are the project's visual
signature.

### §DD50 The publish job, gated by the build

The site is whatever was last committed under docs/, served by Pages from a branch
folder. There is no gate: a broken page is public the moment it merges, and a build that
never runs in CI is a build that works on one machine. The job is Shio's - install,
typecheck, build, prerender, assemble, deploy - and its gates are the build's own rather
than repeated steps, because a gate that runs twice is one that can be satisfied by the
copy nobody kept current. The typecheck fails on a type error and the prerender throws
on a drifted template or a half-declared route. Dispatch only, on Shio's reasoning: a
deploy that fires on every push to a long-lived branch is one nobody can hold still
while reviewing it, and the site is the artefact where a defect is immediately public.
Two facts about the move: the URL does not change, so the base path stays; and Pages
must be pointed at an Actions artefact instead of the docs folder, a repository setting
the job is inert without, while the old page keeps serving until it is.

### §DD51 The site's own claims, asserted

The one defect class a site cannot notice about itself is a claim that has gone false,
and S1 exists to prevent it. Three of this block's claims fail invisibly, so each is
asserted beside the script that owns it, in the manner of Shio's site-claims suite. That
the progress figures are read from roadkeep and never typed, asserted on the generator,
because the alternative failure is a page confidently quoting last month. That the route
map and the metadata table are one list, asserted in both directions, because a route in
only one of them is a page nobody can reach or a title nobody sees. That every route
emits its twin, asserted on the prerender where the pair is written, because a test that
only counted routes would pass while the twins stopped being produced. Plus the source
lint for S7: nothing outside the hash navigation may call scrollIntoView. These are
properties of the repository rather than of a rendered DOM, so node --test over the
sources is the whole harness and no browser is involved.
