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
DockerDesk.exe --help          every verb
```

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
built `.exe` rather than repeating it. The app icon is committed; `build\icon.mjs`
regenerates it from `docs/logo.svg` and is not part of the build. See [CONTRIBUTING.md](CONTRIBUTING.md) for how the
roadmap, changelog and rationale under `docs/` are written — they are governed by a tool
and a hand edit is refused.
