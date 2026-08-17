# Roadmap (active backlog)

## Priority

## Block A — The Windows engine (Docker without Docker Desktop)

- 💭 **DD137** (deps: —) **Why the engine host stopped is printed to a hidden console, so a daemon that vanished leaves no evidence** — The host keeps a small log beside the install, so what it saw and every restart it attempted outlive the window nobody was reading. → §DD137
- 📋 **DD141** (deps: —) **The docker CLI's failure names the pipe but not the one command that brings the engine back** — FreeWilly ships the shim and owns the daemon behind it, so the error it prints when the engine is down could name the verb that starts it. → §DD141
- 📋 **DD142** (deps: DD133 ✅) **Every docker client fails together for a burst of tens of seconds, then all of them work again untouched** — Inside a burst docker ps, version and compose all fail the same way and retries do not help; outside it they all work, with the engine reporting healthy throughout. → §DD142
- 📋 **DD143** (deps: —) **do compose up reads only docker-compose.yml, so the agent verb brings up a different stack than the project defines** — Compose applies the override file by convention and the verb does not, silently: two services become one, and the line printed names the file it read. → §DD143

## Block B — The daemon client (talk to the engine)

## Block C — The window (claude-tray's elements)

## Block D — Container operations (what a user came to do)

## Block E — Images, volumes and networks

## Block F — Installer and distribution (free, Apache 2.0)

- 📋 **DD145** (deps: —) **A wizard page is checked by reading Pascal, so an overlap is found by somebody running an installer** — A committed harness renders one page from the script's own code and reports every rectangle, so a layout defect fails a test instead. → §DD145
- 📋 **DD146** (deps: —) **A successful install keeps no record of what it was cleared on, and the uninstall deletes a file nothing writes** — The report is written once {app} exists, so an install that went through keeps the reading it was allowed on. → §DD146

## Block G — The agent surface (an agent operates this, and pays in tokens)

- 📋 **DD147** (deps: —) **A budget figure is typed rather than produced, so an exact gate can bind a number nobody measured** — The measurement writes the figures it produced, so a recorded number and the run behind it cannot disagree. → §DD147

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
