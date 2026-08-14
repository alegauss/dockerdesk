# Roadmap (active backlog)

## Priority

## Block A — The Windows engine (Docker without Docker Desktop)

- 📋 **DD68** (deps: —) **The console-width guard asserts on every rendered line, including the evidence lines DD52 must not wrap** — Only a short fixture path keeps the two rules from colliding, and the repair a red line-length assertion argues for is the split path DD52 was reverted over. → §DD68
- 📋 **DD73** (deps: DD63 ✅) **docker compose is not a command on a clean install, and the do compose verb shells into exactly that** — PlaceCli extracts only docker.exe and nothing lands in a plugins directory, so DD63 and every compose file a user already has fail on a machine that never had Docker Desktop. → §DD73
- 📋 **DD74** (deps: DD73) **No buildx is placed, so a Dockerfile with a cache mount or a heredoc cannot build on a clean install** — Without the plugin docker build falls back to the classic builder at best, and BuildKit syntax a modern Dockerfile assumes fails on a line the error blames on the file. → §DD74
- 📋 **DD75** (deps: —) **A bind mount spelled the Windows way is sent to a Linux daemon that resolves only its own paths** — Docker Desktop rewrites a drive path into a host mount inside its VM and nothing here does, so a compose file with a relative volume arrives as a source the daemon never chose. → §DD75
- 💭 **DD76** (deps: —) **docker inside a user WSL2 distribution reaches nothing, the toggle Docker Desktop calls WSL integration** — The socket lives in the owned distribution and its only way out is a pipe a Linux client cannot dial, so a developer whose shell is Ubuntu has no engine at all. → §DD76

## Block B — The daemon client (talk to the engine)

## Block C — The window (claude-tray's elements)

- 💭 **DD67** (deps: —) **No popup this product draws has ever been photographed, and the one path that could is not reachable** — DD61 made the screen copy refuse the shell, which is right, and left the script with nothing it can find: a menu exists only while open, and nothing opens one. → §DD67
- 📋 **DD69** (deps: —) **The window carries none of the ocean the mark swims in, and its lowest strip is margin and then the frame** — The site closes its hero into water and opens its footer out of it, so a window with no trace of that reads as a different product from the one the mark introduces. → §DD69
- 📋 **DD70** (deps: DD69) **A list that changed under a refresh and an engine that is still starting both arrive with no transition** — Motion is what tells a reader that a thing changed rather than that they misread it, and this window redraws rows and holds a pending engine with none at all. → §DD70

## Block D — Container operations (what a user came to do)

## Block E — Images, volumes and networks

## Block F — Installer and distribution (free, Apache 2.0)

## Block G — The agent surface (an agent operates this, and pays in tokens)

- 📋 **DD77** (deps: —) **Nothing says a machine still carries objects labelled the way the build before the rename wrote them** — So the dual read has no end: the legacy key cannot be dropped without evidence nobody can gather, and every read pays for a generation that may already be gone. → §DD77
- 📋 **DD78** (deps: —) **The shaped token figure is banded because two of its inputs are read from the machine and not a fixture** — A 15% band is wide enough to hide a regression a build could ship, and the request count recorded beside it is exact for want of the same seam. → §DD78
- 📋 **DD79** (deps: —) **SessionLabel.For reads as the one place a label is stamped and nothing in the product stamps through it** — ComposeUp writes the key straight into YAML, so the helper is reached only by a test and the next change to a label has two places to find rather than one. → §DD79

## Block H — The public surface (the site a reader and an agent both read)

- 📋 **DD59** (deps: the GitHub repository rename) **The site is served from a base path containing the old name, so every published route moves at once** — GitHub Pages derives the base path from the repository name, so renaming the repo moves every published URL at once and nothing serves the old ones. → §DD59
- 📋 **DD71** (deps: —) **README and the site still name the product and the distribution as they were before the rename** — One line is not merely stale but wrong: it says the distribution is called dockerdesk, which after DD55 is only true of an install that was adopted. → §DD71

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
