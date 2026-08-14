# Roadmap (active backlog)

## Priority

## Block A — The Windows engine (Docker without Docker Desktop)

## Block B — The daemon client (talk to the engine)

## Block C — The window (claude-tray's elements)

- 💭 **DD67** (deps: —) **No popup this product draws has ever been photographed, and the one path that could is not reachable** — DD61 made the screen copy refuse the shell, which is right, and left the script with nothing it can find: a menu exists only while open, and nothing opens one. → §DD67
- 📋 **DD69** (deps: —) **The window carries none of the ocean the mark swims in, and its lowest strip is margin and then the frame** — The site closes its hero into water and opens its footer out of it, so a window with no trace of that reads as a different product from the one the mark introduces. → §DD69
- 📋 **DD70** (deps: DD69) **A list that changed under a refresh and an engine that is still starting both arrive with no transition** — Motion is what tells a reader that a thing changed rather than that they misread it, and this window redraws rows and holds a pending engine with none at all. → §DD70
- 📋 **DD80** (deps: —) **Launching the executable shows nothing, and the shortcuts the installer writes carry no window verb** — CommandLine reads a bare argv as tray-only, so Explorer, the Start menu and the desktop icon all land in silence, and a user with no feedback clicks again. → §DD80
- 📋 **DD81** (deps: DD80) **A second launch starts a second tray rather than raising the first, so two icons and two event streams run** — Nothing holds a mutex, so each extra click is another process polling the daemon; raising the first window and exiting is the answer, not an error a click cannot show. → §DD81
- 📋 **DD82** (deps: —) **Visible is set before the icon and tooltip exist, so Windows persists an empty tooltip for the tray entry** — The add carries no icon flag and no text, and the overflow flyout is exactly where that empty tooltip is read, so the place the icon lands names nothing. → §DD82
- 📋 **DD83** (deps: DD69) **Nothing in the window names the build, the engine version behind it, or the API version the client speaks** — A version is the first thing a bug report asks for and the only way to tell a stale install from a fresh one, and the console verb answers where a window user never looks. → §DD83
- 📋 **DD85** (deps: —) **The tray icon is three abstract rings, so the one surface always on screen carries none of the product mark** — The mark is the only thing a user recognises at a glance, and a ring says nothing about which product is running while three of them sit in one overflow. → §DD85

## Block D — Container operations (what a user came to do)

## Block E — Images, volumes and networks

## Block F — Installer and distribution (free, Apache 2.0)

## Block G — The agent surface (an agent operates this, and pays in tokens)

- 📋 **DD78** (deps: —) **The shaped token figure is banded because two of its inputs are read from the machine and not a fixture** — A 15% band is wide enough to hide a regression a build could ship, and the request count recorded beside it is exact for want of the same seam. → §DD78
- 📋 **DD79** (deps: —) **SessionLabel.For reads as the one place a label is stamped and nothing in the product stamps through it** — ComposeUp writes the key straight into YAML, so the helper is reached only by a test and the next change to a label has two places to find rather than one. → §DD79

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
