# Roadmap (active backlog)

## Priority

## Block A — The Windows engine (Docker without Docker Desktop)

- 📋 **DD101** (deps: —) **A bind source that looks like a distribution path and is not there stays unchecked, and it is the expensive failure** — DD96 answered the two cases a string can settle; this one needs asking the distribution, and a source that is legitimately empty must not be called a defect. → §DD101

## Block B — The daemon client (talk to the engine)

## Block C — The window (claude-tray's elements)

- ⏳ **DD99** (deps: —) **The tray icon is always drawn at 16 pixels, so the per-monitor-DPI manifest buys a shell-scaled icon anyway** — app.manifest opts in to PerMonitorV2 and names the blurry square it avoids, and every caller of StateIcon.Icon takes the 16 default, so above 100% the shell scales after all. → §DD99
- 📋 **DD103** (deps: —) **Three tests fail whenever a tray is running, and the failure names an assertion rather than the tray** — They claim the product's real named objects on purpose, so the suite cannot pass on a machine using the product, and the message points at none of that. → §DD103
- 📋 **DD106** (deps: —) **Every container of a compose project is a peer row, so the project it belongs to is invisible** — The label that names the project is already on the list response, so the hierarchy costs no second call, and without it a four-service project is found by reading forty rows. → §DD106

## Block D — Container operations (what a user came to do)

- 📋 **DD107** (deps: DD106) **A project is stopped one service at a time, so its row carries no verb and four clicks do what one should** — The parent row is only worth drawing if acting on it acts on the project, and fanning four verbs across children needs an order and somewhere for a partial failure to go. → §DD107

## Block E — Images, volumes and networks

## Block F — Installer and distribution (free, Apache 2.0)

- 📋 **DD102** (deps: —) **The installer script is compiled only by the release workflow, so a syntax error in it is found by the release** — check.yml never reads it and PackagingTests asserts over the text, so DD97 shipped an Inno construct on evidence that cannot say whether Inno accepts it. → §DD102

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
