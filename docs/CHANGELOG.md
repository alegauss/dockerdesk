# Shipped Ledger

## Block A — The Windows engine (Docker without Docker Desktop)

- ✅ **DD1** **A Windows user cannot tell why Docker will not run here: WSL2 missing, virtualization off, or a rival engine** — `dockerdesk-preflight` reports the Windows build, virtualization, the WSL2 kernel and any rival engine one row each with the action that fixes it, and exits 1 while a blocking row is not green.
- ✅ **DD2** **There is no unattended way to put a container engine on Windows without installing Docker Desktop** — `dockerdesk-engine --provision` puts upstream Moby 29.7.2 in an owned WSL2 distro and docker.exe where an installer can add it to PATH.
- ✅ **DD17** **No clean Windows is reachable from here, so a red preflight row and a real install have never been executed** — `vm.ps1` runs the product preflight inside a Windows 11 guest through vmrun and reads back what it said.
- ✅ **DD3** **Nothing starts or stops the engine, and a UI that reports running before the socket answers is lying** — `dockerdesk-engine --run` starts the distro and daemon, serves \.\pipe\docker_engine, and reports Running only once the engine answers.
- ✅ **DD16** **The preflight reports no rival engine on a machine where Docker Desktop is installed per-user and `docker` is on PATH** — The row now asks what owns the docker command, resolving it off PATH the way a shell does and reading the registered WSL distributions, and it names every signal it found.
- ✅ **DD18** **On a Windows 11 that never had WSL, the preflight spends 15 seconds saying it could not read the WSL2 row** — The row asks `wsl --status` first, so a bare machine is named in milliseconds with the remedy that applies, and `--version` is only reached once something says WSL is there.
- ✅ **DD19** **Inside a VM the preflight calls virtualization enabled and clears an install that WSL2 then refuses to start** — The row reads whether this machine is itself a guest, and abstains inside one instead of reading HypervisorPresent as proof it can host a hypervisor.
- ✅ **DD20** **A leftover docker context sends the CLI to another pipe, so docker reports no daemon while this engine is answering** — A preflight row reads the active context and names both endpoints, so a CLI pointing elsewhere is visible, and it changes no setting of the user's.

## Block B — The daemon client (talk to the engine)

- ✅ **DD4** **Nothing in this project can ask the engine anything: no client for the Docker API over the Windows named pipe** — DockerApi speaks the Engine API over the named pipe with no NuGet dependency: ping, version, containers, and a stream for endpoints that never end.
- ✅ **DD5** **A container started in a terminal never appears in the window, and the list is only as fresh as the last refresh** — EngineEvents reads /events as the daemon writes it and re-opens the stream after every break, so nothing here polls.

## Block C — The window (claude-tray's elements)

- ✅ **DD6** **Answering is Docker up? costs opening a window, and starting the engine costs a command line** — A tray icon carries the engine state as a shape, and its menu starts the engine in a process that outlives the tray or stops it.
- ✅ **DD7** **There is no window: a user cannot see which containers exist, their state, or the ports they publish** — A WPF window lists containers with their ports as links, refreshed by the event stream, and says something designed when it is empty.
- ✅ **DD21** **The tray icon was not in the visible notification area while the tray was running, so the glance costs a click** — The icon registers and Windows files it into the overflow, so the install says where it went and how to keep it in sight, and nothing here promotes itself.
- ✅ **DD22** **Verifying a window copies the screen, so a capture twice photographed private content that was in front of it** — A --capture-window verb renders the window off-screen so nothing else can be in the frame, and the screen copy kept for popups refuses when anything overlaps it.

## Block D — Container operations (what a user came to do)

- ✅ **DD8** **The list is read-only: a container cannot be started, stopped, restarted or removed from it** — Start, stop, restart and remove on every row: the click lands in a pending state, the event stream is what ends it, and a refusal shows the daemon's own sentence where the button is.
- ✅ **DD9** **A container that exits immediately shows a state and nothing about the cause, so the user leaves for a terminal** — A window per container: frame headers stripped, stderr told from stdout, follow on by default, copy-all to the clipboard, and a buffer capped at 5,000 lines that drops from the front.
- ✅ **DD10** **There is no way into a running container: no shell, so anything the log does not say is unreachable** — The terminal the user already has, running docker exec: the image is asked which shell it has first, so one with neither says so on the row instead of opening a window that closes.

## Block E — Images, volumes and networks

- ✅ **DD11** **Tens of gigabytes of layers accumulate and nothing says which images are dangling or still in use** — Images sorted by size, each row saying whether a container holds it or it is dangling, with per-image removal and a dangling-only prune that names the space before the click and after it.
- ✅ **DD12** **Volumes are invisible: a user cannot see which exist, what they cost on disk, or which containers mount them** — Volumes with their sizes and what mounts them, the compose project read off the name, and a deletion that names all of it first because a volume is the one thing here that does not come back.

## Block F — Installer and distribution (free, Apache 2.0)

- ✅ **DD13** **Nothing states the terms: a visitor cannot tell this is free at any headcount, and no NOTICE covers the bundled engine** — Apache-2.0 stated in the README's first paragraph, in LICENSE and in the window's About, with a NOTICE generated from the install manifest so a new download cannot ship unattributed.
- ✅ **DD14** **There is nothing to hand a user: no executable, no installer, and no uninstall that respects their data** — One DockerDesk.exe with every verb behind an argument, an Inno Setup installer that is per-user with no administrator prompt, and an uninstall that asks before deleting the distribution.
- ✅ **DD15** **Every release is built on one developer's machine, so the first download finds what that machine hid** — Two Windows workflows: a check that builds, tests and starts the published .exe on every push, and a tag that drafts a release carrying SHA-256 sums for a person to publish.

## Block G — The agent surface (an agent operates this, and pays in tokens)

- ✅ **DD23** **Nothing measures what a Docker task costs an agent, so a cheaper surface is an unfalsifiable claim** — A benchmark measures the canonical task at 11711 estimated tokens over six calls, and agent-budget.json is the ceiling it reads, so a response that grew fails a build.
- ✅ **DD24** **Reading a container and deleting a volume are one allowlist decision, so every read costs an approval** — read and do are separate argv namespaces so one allowlist line grants every read, and a test drives each read verb and requires every request it made to be a GET.
- ✅ **DD25** **Learning what this machine is running costs five commands, and it repeats in full every session** — One read context answers the whole machine at 102 estimated tokens against 5718 for the three list reads it replaces, and an OOM row closes the canonical question in the first call.
- ✅ **DD26** **Why a container is not answering is a join across five commands, and inspect is read for four fields** — One read doctor joins the five commands and returns a verdict and a remedy per row over the preflight's own model, including the port fact only Windows can answer.
- ✅ **DD27** **A container log is read unbounded, so a restart loop is paid for eight times in identical traces** — read logs takes a cursor, a level, a dedup that turned 634 estimated tokens into 95, a ceiling it never cuts in silence, and an --out that turns the read into a Grep.
- ✅ **DD28** **Port is already allocated does not say what holds the port, and the answer is not in Docker at all** — A refusal carries the Windows fact that explains it: read ports names the pid holding a port, and cannot connect became three causes with three remedies.
- ✅ **DD29** **What an agent created is indistinguishable from what the user created, so cleanup is prune or nothing** — A confirm token computed over the printed list scopes cleanup to one session's label, so a stale plan refuses instead of deleting what arrived in between.

## Block H — The public surface (the site a reader and an agent both read)

- ✅ **DD40** **The site is one hand-written page, dark-only, with every claim typed into the markup that displays it** — The landing page renders from a Vite React Tailwind workspace: copy in one content module the sections iterate, and the theme follows the OS.
- ✅ **DD41** **A static host needs a file per route, and nothing checks that a route has a page or a page a title** — Each route prerenders to its own file with a replace-or-throw head, and the route map and metadata table assert against each other in both directions at import time.
- ✅ **DD42** **A Claude-first product answers an agent only in hydrated markup, in the first thing anybody reads** — Every route emits a Markdown twin converted from the same render, a manifest lists the routes and their twin sizes, and llms.txt moves into the build with the twin convention.
- ✅ **DD43** **The status rows are typed into the page, and five of them are already wrong about what has shipped** — The status page and the landing summary are generated from roadkeep export --json on every build, so a shipped task moves its own row and no progress figure is typed.
- ✅ **DD44** **The hero asserts what the tool is, so what it costs an agent per call is nowhere on the page** — The hero autoplays the five-call agent session with its per-call token targets, and the transcript scrolls its own list rather than the window.
- ✅ **DD45** **Nothing on the site says who the operator is, so the positioning is still a GUI with no licence fee** — A section per design law in the order an agent meets them, and the two-actor split, replace the GUI-with-no-licence-fee positioning the pre-DD23 page shipped under.
- ✅ **DD46** **Nobody configuring an agent can find the read and do split, or the one allowlist line that pays for it** — A page for the agent's operator: the read/do split, the one allowlist line Bash(dockerdesk read:*), the DD32 plugin, and the refusals, marked as the designed Block-G surface.
- ✅ **DD47** **A visitor weighing this against Docker Desktop, Rancher, Podman or a plain WSL2 daemon infers it from prose** — A comparison matrix grouped by the law each row comes from, with a column per rival for what it genuinely wins, and a machine-readable table twin.
- ✅ **DD48** **Everything is one scroll, so a pillar gets a paragraph and there is no page to link at** — Five depth pages from one record each, whose route, title and description are read off the same record, so a pillar cannot ship half-declared or untitled.
- ✅ **DD49** **The og image points at an svg, so every share of this link renders a card with no image at all** — A 1200x630 og.png rasterised from public/og.svg on every build, with the mark and the tagline, and og:image points at it on every route.
- ✅ **DD50** **The site is whatever was last committed to docs, so a broken page is public with no gate between** — A workflow_dispatch-only Pages job whose single gate is npm run build (typecheck, the prerender route and head checks), publishing site/dist to the same URL.
- ✅ **DD51** **A claim on the site that has gone false is invisible until somebody reads the page against the product** — node --test asserts the site's own claims: the generated roadmap figures, the route pair and heads, the twin per route with the CTA dropped, the 1200x630 card, and no scrollIntoView.
- ✅ **DD62** **The page for the agent's operator names only the interruption, so the surface reads as a saving in keystrokes** — Eight rows pair what plain docker costs an agent with the verb that replaces it, and every cost, ceiling and shipped badge is generated from agent-budget.json and the verb registry.
