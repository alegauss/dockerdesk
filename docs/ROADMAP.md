# Roadmap (active backlog)

## Priority

- DD17

## Block A — The Windows engine (Docker without Docker Desktop)

- 🛠 **DD2** (deps: DD1 ✅) **There is no unattended way to put a container engine on Windows without installing Docker Desktop** — The engine is the product: until upstream Moby lands in an owned WSL2 distro with the docker CLI on PATH, there is nothing for a GUI to drive. → §DD2
- 📋 **DD3** (deps: DD2) **Nothing starts or stops the engine, and a UI that reports running before the socket answers is lying** — WSL2 needs seconds to boot the distro before dockerd opens its pipe, so the state a user acts on has to be the pipe answering and never the start command returning. → §DD3
- 📋 **DD16** (deps: —) **The preflight reports no rival engine on a machine where Docker Desktop is installed per-user and `docker` is on PATH** — A false green on the one row whose remedy is uninstall the rival clears an install to walk into the docker_engine collision that row exists to prevent. → §DD16
- 📋 **DD17** (deps: —) **No clean Windows is reachable from here, so a red preflight row and a real install have never been executed** — The two defects that matter, a false green and a broken install, only appear on a machine this one cannot be turned into, and a snapshot is what makes them repeatable. → §DD17

## Block B — The daemon client (talk to the engine)

- 📋 **DD4** (deps: —) **Nothing in this project can ask the engine anything: no client for the Docker API over the Windows named pipe** — Every list, action and status in the UI is one Engine API call, and shelling out to docker.exe pays a process per refresh and parses output that changes between versions. → §DD4
- 📋 **DD5** (deps: DD4) **A container started in a terminal never appears in the window, and the list is only as fresh as the last refresh** — The engine publishes every state change on /events, so a UI that reads it is a view of the engine rather than a periodic guess at it. → §DD5

## Block C — The window (claude-tray's elements)

- 📋 **DD6** (deps: DD3, DD4) **Answering is Docker up? costs opening a window, and starting the engine costs a command line** — The tray is where this tool lives between tasks: the icon carries the engine state at a glance and its menu holds the two verbs that change it. → §DD6
- 📋 **DD7** (deps: DD4, DD5, DD6) **There is no window: a user cannot see which containers exist, their state, or the ports they publish** — This is the screen the tool is opened for, and the tray app's WPF Fluent theme gets that list a Windows 11 look with no extra dependency. → §DD7

## Block D — Container operations (what a user came to do)

- 📋 **DD8** (deps: DD7) **The list is read-only: a container cannot be started, stopped, restarted or removed from it** — This is what a user came for, and the work is in the pending state and the confirmation around the call, not in the four endpoints. → §DD8
- 📋 **DD9** (deps: DD7) **A container that exits immediately shows a state and nothing about the cause, so the user leaves for a terminal** — The log is the one artefact a failed container leaves, and its stream is framed per chunk unless a TTY was allocated. → §DD9
- 📋 **DD10** (deps: DD7) **There is no way into a running container: no shell, so anything the log does not say is unreachable** — Launching Windows Terminal with docker exec costs a process, where a terminal inside the window costs a full ANSI emulator this project has no reason to write. → §DD10

## Block E — Images, volumes and networks

- 📋 **DD11** (deps: DD7) **Tens of gigabytes of layers accumulate and nothing says which images are dangling or still in use** — Reclaiming disk is a judgement over a list, which is what a GUI is better at than three CLI commands and a mental join. → §DD11
- 📋 **DD12** (deps: DD7) **Volumes are invisible: a user cannot see which exist, what they cost on disk, or which containers mount them** — A volume is the one thing here that does not come back, so the list's job is making an irreversible deletion legible rather than reclaiming space. → §DD12

## Block F — Installer and distribution (free, Apache 2.0)

- 📋 **DD13** (deps: —) **Nothing states the terms: a visitor cannot tell this is free at any headcount, and no NOTICE covers the bundled engine** — The licence is the reason to try this at all, so Apache-2.0 belongs where the choice is made and upstream attribution is a compliance requirement, not a courtesy. → §DD13
- 📋 **DD14** (deps: DD2, DD13) **There is nothing to hand a user: no executable, no installer, and no uninstall that respects their data** — A per-user install into LOCALAPPDATA with no admin prompt is what reaches a managed corporate laptop, which is the audience Docker Desktop's terms send here. → §DD14
- 📋 **DD15** (deps: DD14) **Every release is built on one developer's machine, so the first download finds what that machine hid** — A broken install is the only defect that matters in a tool promising Docker works after it runs, and the roadkeep gate is worth nothing until red stops a merge. → §DD15

## Non-goals

- **Feature parity with Docker Desktop** Kubernetes, the extensions marketplace and Dev
  Environments are most of that product and none of them is why anyone leaves it; the
  scope here is install, see, start, stop.
- **A fork of the engine** This drives upstream Moby unmodified. A fork would make every
  Docker answer on the internet subtly wrong for this tool's users, which is a worse tax
  than any licence.
- **macOS and Linux** The problem being solved is Windows-specific: Docker Desktop's
  terms plus WSL2 plumbing. Linux needs no GUI to install an engine, and macOS already
  has free alternatives.
- **Telemetry, accounts or a sign-in** Nothing here phones home and there is nothing to
  log into. A tool adopted to escape a licence check must not ship a different reason to
  be blocked by a corporate proxy.
- **A resident background service** The complaint this project answers is a desktop app
  holding gigabytes at every boot. Both the app and the engine run when asked, and
  autostart stays a setting the user turns on.
