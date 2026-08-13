# Roadmap (active backlog)

## Priority

## Block A — The Windows engine (Docker without Docker Desktop)

- 💭 **DD52** (deps: —) **The rival row prints its evidence as one 254-character line, and wrapping it on spaces splits a path** — Evidence exists so a user can check it against `where docker`, and a path broken across lines cannot be copied or grepped. → §DD52
- 📋 **DD55** (deps: DD54) **The engine owns a WSL distribution and an app root both named dockerdesk, and a rename orphans them** — Those two names are state on a user machine rather than text in a build: renamed with no migration, every image and volume the old distribution holds becomes unreachable. → §DD55
- 📋 **DD56** (deps: DD55) **The rival probe carries a rule that exists only because dockerdesk contains docker, and freewilly does not** — The rule and its tests go dead the day the distribution is renamed, and a leftover dockerdesk distribution from an older install starts reading as a rival engine. → §DD56

## Block B — The daemon client (talk to the engine)

## Block C — The window (claude-tray's elements)

- 📋 **DD39** (deps: —) **The window opens at one fixed size on the primary screen every time and forgets which list was being read** — A tool opened several times a day on a two-monitor desk is placed by hand every time, and the tab is the one piece of state the user set on purpose. → §DD39
- 📋 **DD61** (deps: —) **A screen copy of the window carries a blurred image of what is behind it, because the Fluent backdrop transmits it** — The overlap check cannot answer for this: the intruder is not in front of the window, it is showing through it, so the copy leaks with every assertion satisfied. → §DD61

## Block D — Container operations (what a user came to do)

## Block E — Images, volumes and networks

## Block F — Installer and distribution (free, Apache 2.0)

- 📋 **DD53** (deps: —) **The test guest cannot be returned to a clean snapshot, so the machine a check was measured on is gone once it drifts** — A row measured on a bare Windows is unverifiable the moment that guest has WSL, and reverting is the one thing the harness documents and does not do. → §DD53
- 📋 **DD54** (deps: —) **Every project, namespace and assembly in the tree spells DockerDesk, so a rename starts at the solution file** — A namespace is the spelling every file in the tree repeats, so renaming it after the machine-facing names leaves two products inside one build for as long as that takes. → §DD54
- 📋 **DD57** (deps: DD54, DD55) **The installer identifies the product by an AppId with the old name inside it, so a rename installs a second one** — Inno Setup identifies a product by AppId alone, so keeping the id upgrades the old entry under a new label and changing it leaves two products installed. → §DD57

## Block G — The agent surface (an agent operates this, and pays in tokens)

- 📋 **DD58** (deps: DD54) **The agent surface is invoked as dockerdesk and quoted that way in allowlist patterns matched literally** — An allowlist pattern is a string a user pasted into their own settings, so this project cannot migrate it, and DD32 has to write the new name rather than the old one. → §DD58
- 📋 **DD63** (deps: DD29 ✅) **No verb on this surface creates anything, so the session stamp has nothing to stamp** — agent-budget.json already reserves a ceiling for do compose up, and DD29's label is reachable only from tests until a do verb creates something. → §DD63
- 📋 **DD64** (deps: —) **The test that gates every cost claim can go red for a reason that is not cost** — AgentBudgetTests went red once in a full run and never again in 23; the only non-deterministic assertion in it is the daemon's recorded request count. → §DD64
- 📋 **DD65** (deps: DD30 ✅) **The budget file still says the agent surface does not exist, so the ratio it was built to prove is unrecorded** — agent-budget.json sets surface.exists false and names read context, doctor, logs and verify as not existing; all four shipped and are now measurable. → §DD65

## Block H — The public surface (the site a reader and an agent both read)

- 📋 **DD59** (deps: the GitHub repository rename) **The site is served from a base path containing the old name, so every published route moves at once** — GitHub Pages derives the base path from the repository name, so renaming the repo moves every published URL at once and nothing serves the old ones. → §DD59
- 📋 **DD60** (deps: —) **Fifteen lines of governed prose name DockerDesk, and the guard denies the edit that would change them** — The guard denies the edit, so each one moves through the verb that owns it, and a ledger naming a product nobody can find is where a stale name actively misleads. → §DD60

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
- **A model, prompts or API keys** DockerDesk is the substrate an external agent drives,
  never a place intelligence lives: the caller already has a model, and hosting one
  would end the free, offline, no-account tool this is.
- **A second Docker CLI** The agent surface answers the joins the Engine API cannot
  make; what docker already answers well is not re-wrapped, so there is no build, no
  push and no registry credentials here.
- **Renumbering the DD task prefix** The rename stops at the product. Every id appears
  in a dependency, a section anchor, a shipped ledger entry and a pushed commit message,
  so a two-letter prefix change rewrites all of it to say exactly the same thing.
