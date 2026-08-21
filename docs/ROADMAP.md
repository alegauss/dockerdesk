# Roadmap (active backlog)

## Priority

## Block A — The Windows engine (Docker without Docker Desktop)

- 📋 **DD163** (deps: —) **The journal records the engine and nothing around it, so a suspend, a logoff and a killed host read as one silence** — The host and the tray write the events either side of a failure - a resume, a session ending, the stream going quiet - so a gap in the file has a cause beside it. → §DD163
- 📋 **DD164** (deps: —) **The host gives up after five attempts, so an engine that could not come back stays down until somebody clicks Start** — Once the quick attempts are spent the host falls back to a long interval instead of exiting, so a machine that recovers an hour later serves again without a click. → §DD164

## Block B — The daemon client (talk to the engine)

## Block C — The window (claude-tray's elements)

- 📋 **DD165** (deps: DD164) **The engine journal is a file nobody finds, so a user who watched the engine go offline has nothing to read** — An Engine page follows the host journal live beside the container lists, carrying the state, the restarts and Copy all the way a container log already does. → §DD165

## Block D — Container operations (what a user came to do)

## Block E — Images, volumes and networks

## Block F — Installer and distribution (free, Apache 2.0)

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
