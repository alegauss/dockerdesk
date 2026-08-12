# Roadmap (active backlog)

## Priority

## Block A — The Windows engine (Docker without Docker Desktop)

- 📋 **DD16** (deps: —) **The preflight reports no rival engine on a machine where Docker Desktop is installed per-user and `docker` is on PATH** — A false green on the one row whose remedy is uninstall the rival clears an install to walk into the docker_engine collision that row exists to prevent. → §DD16
- 📋 **DD18** (deps: —) **On a Windows 11 that never had WSL, the preflight spends 15 seconds saying it could not read the WSL2 row** — That is the most common machine this installer meets, wsl --status names the state and the fix in milliseconds, and the remedy offered updates a WSL that is not installed. → §DD18
- 📋 **DD19** (deps: —) **Inside a VM the preflight calls virtualization enabled and clears an install that WSL2 then refuses to start** — HypervisorPresent is true of every guest, so the row reads I am virtualized as I can host one, and the install fails halfway on exactly the machine the check exists to stop. → §DD19
- 📋 **DD20** (deps: —) **A leftover docker context sends the CLI to another pipe, so docker reports no daemon while this engine is answering** — The context outlives a rival uninstall because it lives in the user profile, and the result is a tool that looks broken with nothing wrong with it. → §DD20

## Block B — The daemon client (talk to the engine)

## Block C — The window (claude-tray's elements)

- 📋 **DD21** (deps: —) **The tray icon was not in the visible notification area while the tray was running, so the glance costs a click** — Windows 11 puts a new icon in the overflow by default, and a state indicator behind a chevron is not the thing the tray was built to be. → §DD21
- 📋 **DD22** (deps: —) **Verifying a window copies the screen, so a capture twice photographed private content that was in front of it** — Rendering the window off-screen photographs nothing else, and claude-tray already carries both that verb and an overlap-checked screen copy for what a render cannot reach. → §DD22

## Block D — Container operations (what a user came to do)

- 📋 **DD10** (deps: DD7 ✅) **There is no way into a running container: no shell, so anything the log does not say is unreachable** — Launching Windows Terminal with docker exec costs a process, where a terminal inside the window costs a full ANSI emulator this project has no reason to write. → §DD10

## Block E — Images, volumes and networks

- 📋 **DD11** (deps: DD7 ✅) **Tens of gigabytes of layers accumulate and nothing says which images are dangling or still in use** — Reclaiming disk is a judgement over a list, which is what a GUI is better at than three CLI commands and a mental join. → §DD11
- 📋 **DD12** (deps: DD7 ✅) **Volumes are invisible: a user cannot see which exist, what they cost on disk, or which containers mount them** — A volume is the one thing here that does not come back, so the list's job is making an irreversible deletion legible rather than reclaiming space. → §DD12

## Block F — Installer and distribution (free, Apache 2.0)

- 📋 **DD13** (deps: —) **Nothing states the terms: a visitor cannot tell this is free at any headcount, and no NOTICE covers the bundled engine** — The licence is the reason to try this at all, so Apache-2.0 belongs where the choice is made and upstream attribution is a compliance requirement, not a courtesy. → §DD13
- 📋 **DD14** (deps: DD2 ✅, DD13) **There is nothing to hand a user: no executable, no installer, and no uninstall that respects their data** — A per-user install into LOCALAPPDATA with no admin prompt is what reaches a managed corporate laptop, which is the audience Docker Desktop's terms send here. → §DD14
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
