# Roadmap (active backlog)

## Priority

## Block A — The Windows engine (Docker without Docker Desktop)

- 📋 **DD128** (deps: DD3 ✅) **Quitting the tray leaves the engine and its WSL virtual machine running, so the memory stays held** — Quit runs the same stop the menu item does, so the one way out of the tray also gives back the memory the machine was lending it. → §DD128
- 📋 **DD129** (deps: DD128) **A logoff, a shutdown or an End task never reaches the quit path, so the engine outlives the session** — The stop is hung off the session ending as well, so the exits a user does not think of as quitting leave nothing running. → §DD129
- 💭 **DD137** (deps: —) **Why the engine host stopped is printed to a hidden console, so a daemon that vanished leaves no evidence** — The host keeps a small log beside the install, so what it saw and every restart it attempted outlive the window nobody was reading. → §DD137

## Block B — The daemon client (talk to the engine)

## Block C — The window (claude-tray's elements)

## Block D — Container operations (what a user came to do)

## Block E — Images, volumes and networks

## Block F — Installer and distribution (free, Apache 2.0)

- 📋 **DD130** (deps: —) **Setup writes every file and only then reads the machine, so a laptop without WSL2 gets an install it cannot use** — The preflight runs on a wizard page before the first file is copied, so an install that cannot work stops where nothing has been changed yet. → §DD130
- 📋 **DD131** (deps: DD130) **A machine without WSL2 gets a message box naming a command, which assumes the reader knows what WSL2 is** — The blocked install lands on a page that names the feature in plain words, numbers the steps, links Microsoft's own instructions and re-checks in place. → §DD131
- 📋 **DD132** (deps: DD131) **Turning the missing feature on is left to the user, so an install that could finish itself ends in a terminal** — Setup turns WSL2 on itself behind a single elevation prompt, asks for the reboot the feature needs and picks the install up on the other side. → §DD132

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
