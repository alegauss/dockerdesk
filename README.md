# DockerDesk

**A free Docker Desktop alternative for Windows, under Apache-2.0 — no headcount
threshold, no revenue threshold, no licence to buy.** Docker Desktop is free only below
its own limits, and the reason to look for an alternative is usually that your employer
crossed one. This is the whole answer to that: [Apache-2.0](LICENSE), including the
patent grant that makes a corporate legal review straightforward.

It installs and drives Docker on Windows: a preflight that says whether this machine can
run it, an owned WSL2 distribution so nothing of yours is touched, a tray icon carrying
the engine's state, and one window for containers, images and volumes.

## What it is

- **A preflight, not a support case.** Every check states a fact, a verdict, and the one
  action that changes it — before anything is installed.
- **An owned distribution.** The engine lives in a WSL2 distribution called `dockerdesk`
  that this tool created. An `apt upgrade` or a `wsl --unregister` you ran for another
  reason cannot take the engine with it, and the uninstall is one command.
- **A tray icon you can read at a glance — once Windows shows it.** Shape carries the
  state and colour only reinforces it, so it survives a taskbar, a colour-blind reader
  and a black-and-white screenshot. Windows 11 files an icon it has not seen before into
  the overflow behind the chevron, and nothing here promotes itself out of there: drag it
  onto the taskbar once and Windows remembers. Until then the state is on hover.
- **One window.** Containers with their ports as links, their logs de-framed and
  followed, and a shell in the terminal you already have. Images sorted by size with
  dangling and in-use named. Volumes with what mounts them, because a volume is the one
  thing here that does not come back.
- **No daemon of its own.** Quitting the tray leaves the engine exactly as it was.

## What it is not

These are binding, not aspirational — see the non-goals in
[docs/ROADMAP.md](docs/ROADMAP.md):

- Feature parity with Docker Desktop
- A fork of the engine
- macOS and Linux
- Telemetry, accounts or a sign-in
- A resident background service
- A model, prompts or API keys
- A second Docker CLI

## Installing

One `.exe`, and an installer around it. Both are per-user: DockerDesk installs into
`%LOCALAPPDATA%\DockerDesk` and **asks for no administrator prompt**, which is what
reaches a managed corporate laptop — the audience Docker Desktop's terms send here. The
engine's WSL2 feature may still need elevation of its own, and the installer runs the
preflight and says so rather than failing halfway through a download.

Windows 10 2004 (build 19041) or later, 64-bit. Nothing else: the executable carries its
own .NET runtime, so a clean machine needs no prerequisite.

Uninstalling removes what was installed and **asks about what was created**. The
`dockerdesk` WSL2 distribution holds every image, container and volume you have, so it is
never deleted without a question, and an unattended uninstall keeps it.

The same executable is every verb — there is no second tool to find:

```
DockerDesk.exe                 the tray icon
DockerDesk.exe --window        the tray, with the window open
DockerDesk.exe --preflight     what this machine can host; --json for a script
DockerDesk.exe --provision     download, verify and install the engine
DockerDesk.exe --run           start the engine and serve the pipe until Ctrl+C
DockerDesk.exe --capture-window <png> [tab]
                               render the window to a PNG off-screen
DockerDesk.exe --help          every verb
```

## The agent surface

DockerDesk's other operator is a coding agent, and the split that matters to one is in argv:

```
dockerdesk read context       the whole machine in one budgeted payload
dockerdesk read doctor <name> why one container is not answering
dockerdesk read ps            every container, one line each — mutates nothing
dockerdesk do   engine start  brings the engine up
```

`read doctor` closes a join that five commands used to leave to the caller, and returns
conclusions rather than fields:

```
  [FAIL]  memory    the kernel killed it for exceeding 512M
           -> Raise it above 512M, or hold less.
  [FAIL]  ports     :8080→8080/tcp nothing listening
           -> Port 8080 is published and nothing on Windows holds it: it is not running,
              or its process never bound.
  [FAIL]  mounts    /app ← C:\Users\dev\shop\api MISSING, /data ← volume:shop_data
```

The rows are the preflight's own — a fact, a verdict and the one action that changes it —
so nothing new has to be learned to read them. The ports row is the one Docker structurally
cannot answer: the daemon knows what was published and only Windows knows whether anything
holds the socket. A mount this tool did not map is reported **unchecked** rather than broken,
because a false "does not resolve" is worse than no answer.

`read context` is the one that replaces a session's first five calls:

```
engine  running  wsl:dockerdesk  api=v1.43  ctx=default(ok)
shop-api-1  exited 137  svc:shop/api  8080->8080/tcp  OOM  ×3  limit=512M
shop-db-1  up 4m (healthy)  svc:shop/db  5432->5432/tcp
disk    images 14G (2G dangling)  volumes 2
cursor  c:231884
```

**102 estimated tokens** for a five-service stack, against 5718 measured for the three
container-list reads a diagnosis makes today. The first row already answers *why is the api
container not responding* — `OOM limit=512M` — with no second call. Order is deterministic so
the payload caches and a diff means something, the ceiling is hard and a truncated payload
**says how many rows went** rather than cutting silently, and the cursor fingerprints the
machine rather than the text. `--json` is there for callers that parse.

`docker ps` and `docker rm -f -v` are the same string to an allowlist, so a rule either
grants the whole verb namespace — which permits deleting a volume — or every call stops to
ask. Separating them makes the rule one line:

```jsonc
// .claude/settings.json
"allow": ["Bash(dockerdesk read:*)"]
```

**`read` is a promise, not a prefix.** A verb under it that writes is a defect, and two
things keep that honest: a read verb is handed a handle with no start, remove or prune on
it, and a test drives every registered read verb and requires every request it made to be
a `GET`. Addresses are names — a container by its name, a compose service as
`svc:<project>/<service>` — because an id changes when a container is recreated.

Every response shape has a ceiling in [`agent-budget.json`](agent-budget.json), and a test
fails a build that made one more expensive. See
[docs/specs/DD23-agent-first-dockerdesk.md](docs/specs/DD23-agent-first-dockerdesk.md).

`--capture-window` renders the window's own content and never photographs the screen, so it
cannot catch anything that happens to be in front of it — and it needs no desktop at all,
which a screen copy does. [`scripts/Capture-Window.ps1`](scripts/Capture-Window.ps1) is the
screen-copy fallback for popups, and it refuses rather than writing when something overlaps
the window.

A windowed program does not hold the prompt, so a typed verb prints *after* the prompt
returns. Redirecting (`DockerDesk.exe --preflight > report.txt`) has neither problem, and
is the form a script or an installer uses anyway.

## Licence and attribution

DockerDesk is [Apache-2.0](LICENSE). Copyright DockerDesk contributors.

The engine it installs is upstream software under its own terms, and those files are not
redistributed here — they are downloaded from their official locations at install time,
against the versions and digests this build pins. [NOTICE](NOTICE) lists every one of
them, its licence and where it came from; the window's **About** says the same thing
where the choice is actually made.

## Building

```
dotnet build
dotnet test
build\build.cmd              one self-contained DockerDesk.exe
build\build-installer.cmd    that, wrapped in dist\DockerDesk-Setup.exe
```

Requires the .NET 10 SDK and Windows; the installer also needs
[Inno Setup 6](https://jrsoftware.org/isdl.php), found machine-wide, per-user or on the
PATH. The version is stated once, in
[Directory.Build.props](Directory.Build.props) — the installer reads it back off the
built `.exe` rather than repeating it. The mark and the app icon are committed, and
neither is part of the build: `build\trace-logo.mjs` traces
[`build/logo-source.png`](build/logo-source.png) into `docs/logo.svg` and the
tray-sized `docs/icon.svg`, and `build\icon.mjs` rasterises those two into the `.ico`
— below 48 pixels from the simplified one. See [CONTRIBUTING.md](CONTRIBUTING.md) for how the
roadmap, changelog and rationale under `docs/` are written — they are governed by a tool
and a hand edit is refused.
