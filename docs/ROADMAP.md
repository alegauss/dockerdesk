# Roadmap (active backlog)

## Priority

## Block A — The Windows engine (Docker without Docker Desktop)

- 📋 **DD96** (deps: —) **A bind source only compose up can respell, so every other route to the daemon still mounts an empty directory** — read doctor already reads the mounts of a running container and reports them, and it is the one place that could name a source the distribution cannot reach. → §DD96

## Block B — The daemon client (talk to the engine)

## Block C — The window (claude-tray's elements)

- 💭 **DD67** (deps: —) **No popup this product draws has ever been photographed, and the one path that could is not reachable** — DD61 made the screen copy refuse the shell, which is right, and left the script with nothing it can find: a menu exists only while open, and nothing opens one. → §DD67
- 📋 **DD85** (deps: —) **The tray icon is three abstract rings, so the one surface always on screen carries none of the product mark** — The mark is the only thing a user recognises at a glance, and a ring says nothing about which product is running while three of them sit in one overflow. → §DD85

## Block D — Container operations (what a user came to do)

## Block E — Images, volumes and networks

## Block F — Installer and distribution (free, Apache 2.0)

- 📋 **DD97** (deps: —) **Two features write one Run value: the installer's start-with-Windows and the engine's autostart, with different commands** — Whichever ran last wins, so turning the engine autostart on stops the tray starting, and turning it off silently undoes a box the user ticked in the installer. → §DD97

## Block G — The agent surface (an agent operates this, and pays in tokens)

## Block H — The public surface (the site a reader and an agent both read)

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
- **A model, prompts or API keys** FreeWilly is the substrate an external agent drives,
  never a place intelligence lives: the caller already has a model, and hosting one
  would end the free, offline, no-account tool this is.
- **A second Docker CLI** The agent surface answers the joins the Engine API cannot
  make; what docker already answers well is not re-wrapped, so there is no build, no
  push and no registry credentials here.
- **Renumbering the DD task prefix** The rename stops at the product. Every id appears
  in a dependency, a section anchor, a shipped ledger entry and a pushed commit message,
  so a two-letter prefix change rewrites all of it to say exactly the same thing.
