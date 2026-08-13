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
- 📋 **DD34** (deps: —) **The two windows redeclare their chrome and one red is spelled four times, so a meaning has four definitions** — A style that lives in whichever window needed it first drifts silently, and claude-tray declares the colours whose value is not a free choice once, converting at each edge. → §DD34
- 📋 **DD35** (deps: DD34) **Three lists, the engine, the logs and the shell share one window class, so a fourth list grows the same two files** — claude-tray's shell owns only the chrome and each destination its own header and footer; here every list repeats the header, empty-state and refresh stanza in full. → §DD35
- 📋 **DD36** (deps: DD34) **A row says its state in plain text and carries six always-visible captions, so a list of forty reads as a form** — State is the one column scanned down, and the actions are pressed once a session; a tinted chip and a hover-revealed action set are what claude-tray's rows already do. → §DD36
- 📋 **DD37** (deps: DD35) **No heading sorts and no list filters, so finding one container among forty is done with the scrollbar** — The window is opened with one container in mind; a heading that reorders on click and a box that narrows are what turn a long list into an answer rather than a scroll. → §DD37
- 📋 **DD38** (deps: —) **No window can be drawn without a running daemon holding the containers the picture is meant to show** — claude-tray renders every page from a fixture behind a flag, which is what makes a screenshot reviewable and deterministic, and what gives DD22's capture something to photograph. → §DD38
- 📋 **DD39** (deps: —) **The window opens at one fixed size on the primary screen every time and forgets which list was being read** — A tool opened several times a day on a two-monitor desk is placed by hand every time, and the tab is the one piece of state the user set on purpose. → §DD39

## Block D — Container operations (what a user came to do)

## Block E — Images, volumes and networks

## Block F — Installer and distribution (free, Apache 2.0)

- 📋 **DD14** (deps: DD2 ✅, DD13 ✅) **There is nothing to hand a user: no executable, no installer, and no uninstall that respects their data** — A per-user install into LOCALAPPDATA with no admin prompt is what reaches a managed corporate laptop, which is the audience Docker Desktop's terms send here. → §DD14
- 📋 **DD15** (deps: DD14) **Every release is built on one developer's machine, so the first download finds what that machine hid** — A broken install is the only defect that matters in a tool promising Docker works after it runs, and the roadkeep gate is worth nothing until red stops a merge. → §DD15
- 📋 **DD32** (deps: DD24, DD14) **An agent meeting this machine has no way to know the surface exists, so it reaches for docker** — A capability nobody discovers is one nobody uses, and the allowlist entry that makes the read split pay is a settings file the install never touches. → §DD32

## Block G — The agent surface (an agent operates this, and pays in tokens)

- 📋 **DD23** (deps: DD14, DD15) **Nothing measures what a Docker task costs an agent, so a cheaper surface is an unfalsifiable claim** — A cost that is argued rather than measured drifts quietly and in somebody else's environment, so the measurement is the first deliverable rather than a footnote. → §DD23
- 📋 **DD24** (deps: DD23) **Reading a container and deleting a volume are one allowlist decision, so every read costs an approval** — The docker CLI mixes reads and writes in one verb namespace, so no rule permits inspection without permitting deletion, and the read path pays a human round trip. → §DD24
- 📋 **DD25** (deps: DD24) **Learning what this machine is running costs five commands, and it repeats in full every session** — Discovery is answered by a truncating human table with no cursor, so an agent pays for the whole machine each time and reads four fields out of six hundred lines. → §DD25
- 📋 **DD26** (deps: DD24) **Why a container is not answering is a join across five commands, and inspect is read for four fields** — One inspect is three to six hundred lines of JSON paid in full, and the join that turns those fields into a conclusion has no command at all. → §DD26
- 📋 **DD27** (deps: DD25) **A container log is read unbounded, so a restart loop is paid for eight times in identical traces** — Logs are the largest token sink here and the read has no cursor, no level, no dedup and no ceiling, so the cost is the size of the file rather than of the answer. → §DD27
- 📋 **DD28** (deps: DD24, DD16, DD20) **Port is already allocated does not say what holds the port, and the answer is not in Docker at all** — The daemon knows a bind failed and a Windows process knows which PID owns the socket, so the one refusal an agent cannot act on is the one this app can complete. → §DD28
- 📋 **DD29** (deps: DD24) **What an agent created is indistinguishable from what the user created, so cleanup is prune or nothing** — Prune is scoped to the whole machine and is the one command nobody delegates, so leftovers stay rather than risk a volume the session did not create. → §DD29
- 📋 **DD30** (deps: DD26) **Nothing proves a service is reachable: a running container with a bound port can answer nothing** — An agent cannot see, so the gap between the daemon reporting running and the port answering from Windows is closed by a human looking, which is the costliest cycle. → §DD30
- 📋 **DD31** (deps: DD25, DD7 ✅) **Every session re-derives the whole machine, because nothing states what moved since the last one** — The tray already holds the event stream open, so a delta is a cursor over a running stream and the only mechanism that makes a second session cheaper than the first. → §DD31
- 💭 **DD33** (deps: DD24) **A client with no shell cannot reach this surface at all, the CLI being the only head there is** — A tool schema is re-sent every turn of every session, so a second head is worth its fixed cost only if a shell-less caller exists, which no evidence yet says it does. → §DD33

## Block H — The public surface (the site a reader and an agent both read)

- 📋 **DD46** (deps: DD40 ✅) **Nobody configuring an agent can find the read and do split, or the one allowlist line that pays for it** — A page for the agent's operator: the allowlist entry, the calls a session opens with, the plugin that makes the surface discoverable, and what it deliberately refuses. → §DD46
- 📋 **DD47** (deps: DD40 ✅) **A visitor weighing this against Docker Desktop, Rancher, Podman or a plain WSL2 daemon infers it from prose** — Checkable rows grouped by the law each comes from, and a column per alternative for what it is genuinely better at, because a matrix that wins every row is not believed. → §DD47
- 📋 **DD48** (deps: DD41 ✅) **Everything is one scroll, so a pillar gets a paragraph and there is no page to link at** — One page per pillar from one record each, with the route, the title and the description read off the same record, so a new pillar cannot ship half-declared or untitled. → §DD48
- 📋 **DD49** (deps: DD40 ✅) **The og image points at an svg, so every share of this link renders a card with no image at all** — A 1200 by 630 card rasterised from an svg on every build, with the favicon and the marks beside it, so a shared link does not introduce the project as an empty rectangle. → §DD49
- 📋 **DD50** (deps: DD41 ✅) **The site is whatever was last committed to docs, so a broken page is public with no gate between** — A dispatch-only job whose gates are the build's own, typecheck then the prerender's route and head checks, publishing to the same URL the project already has. → §DD50
- 📋 **DD51** (deps: DD41 ✅, DD42 ✅, DD43 ✅) **A claim on the site that has gone false is invisible until somebody reads the page against the product** — node --test beside the scripts that own them: the generated figures, the route pair, the twin per route, and the rule that only the reader scrolls the window. → §DD51

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
