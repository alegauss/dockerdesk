// The copy lives here and nowhere else (S3). Every section component imports a value
// from this module and only renders it — so a claim is an array element a reviewer can
// check against the product, not a string welded into the markup that displays it. The
// composition (which section, in which order, and the illustrative SVGs) lives in the
// JSX; this file is the words.
//
// Fragments carrying inline code or emphasis are modelled as a small tagged run list
// (`Rich`) rather than raw HTML, so a section renders them without dangerouslySetInnerHTML
// and the twin generator in DD42 has a structure to convert rather than markup to parse.

export type Run =
  | string
  | { code: string }
  | { b: string }
  | { i: string };

export type Rich = Run[];

/* ------------------------------------------------------------------ meta + chrome */

export const meta = {
  title: "DockerDesk — Docker on Windows, without Docker Desktop",
  description:
    "A free Windows desktop app that installs upstream Moby into a WSL2 distribution it owns, serves the docker_engine pipe your existing tools already look for, and shows your containers in one window. Per-user install, no admin, nothing resident.",
  og: {
    title: "DockerDesk",
    description:
      "Upstream Moby in a WSL2 distro it owns, the docker_engine pipe your tools already look for, and one window of containers. No licence fee, nothing resident.",
    url: "https://alegauss.github.io/dockerdesk/",
  },
} as const;

export const repoUrl = "https://github.com/alegauss/dockerdesk";
export const parentUrl = "https://alegauss.github.io/";

// Section anchors (#x) act on the landing page; the page links are base-absolute so they
// resolve the same from every route. The brand and footer link home the same way.
export const navLinks = [
  { href: "#operator", label: "Agent" },
  { href: "#pipe", label: "The pipe" },
  { href: "#window", label: "Window" },
  { href: "/dockerdesk/claude-code/", label: "Claude Code" },
  { href: "/dockerdesk/compare/", label: "Compare" },
  { href: "/dockerdesk/status/", label: "Status" },
] as const;

export const footer = {
  links: [
    { href: "/dockerdesk/claude-code/", label: "Claude Code" },
    { href: "/dockerdesk/compare/", label: "Compare" },
    { href: "/dockerdesk/status/", label: "Status" },
    { href: repoUrl, label: "GitHub" },
    { href: `${repoUrl}/blob/main/docs/ROADMAP.md`, label: "Roadmap" },
    { href: `${repoUrl}/blob/main/CONTRIBUTING.md`, label: "Contributing" },
  ],
  // The trademark disclaimer is not up for revision (§1). DD13 shipped LICENSE and NOTICE,
  // so the claim is now the shape — a link to LICENSE — rather than the string "not written
  // yet", which is exactly the S1 corollary that survived Shio's 195-file licence change.
  disclaimer:
    "Unofficial / community project — not affiliated with, endorsed by, or sponsored by Docker, Inc. “Docker” and “Docker Desktop” are trademarks of Docker, Inc.; this tool installs the Apache-2.0 licensed Moby engine and Docker CLI as published upstream, unmodified, and pins each artefact to a version and a digest. Apache-2.0 is this project's stated licence, in LICENSE, with a NOTICE covering the bundled upstream binaries. © 2026 Alexandre Oliveira.",
} as const;

/* ------------------------------------------------------------------ hero */

export const hero = {
  badge: "In development · No release yet · Windows 10 / 11",
  titleLead: "Docker on Windows,",
  titleAccent: "without Docker Desktop.",
  sub: [
    "DockerDesk puts ",
    { b: "upstream Moby" },
    " into a WSL2 distribution it owns, serves the ",
    { code: "docker_engine" },
    " pipe every tool you already have looks for, and shows your containers in one window. Per-user install, no administrator prompt, and nothing left running that you did not ask for.",
  ] as Rich,
  meta: [
    "🔓 Free at any headcount",
    "🚫 No telemetry, no account",
    "💤 Not a background service",
  ],
  pills: [
    [{ b: ".NET 10" }, " · WinForms tray + WPF window"] as Rich,
    ["Upstream ", { b: "Moby 29.7.2" }, ", pinned by digest"] as Rich,
    ["One owned ", { b: "WSL2" }, " distro"] as Rich,
    ["Zero NuGet ", { b: "Engine API client" }] as Rich,
  ],
};

/* ------------------------------------------------------------------ hero session */
// The five-call session from DD23 §3.1, and the context pack from §4.2. The commands and
// their flags are the real agent surface; the values are illustrative demo data. Every
// cost is a target DD23 must prove or falsify — an acceptance criterion, never a
// measurement, because the benchmark that would measure it has not been written (§4 of the
// site constitution). Rendered as an autoplaying transcript that scrolls its own list (S7).

export const heroSession = {
  eyebrow: "What it costs an agent",
  question:
    "bring this project's stack up and tell me why the api container is not responding",
  steps: [
    {
      cmd: "dockerdesk read context",
      cost: "~150 tok",
      // the context pack (§4.2): one budgeted payload in a terse line format, not JSON
      pack: [
        "engine  running  wsl:dockerdesk  api=v1.43  pipe=docker_engine  ctx=default(ok)",
        "web     up 6m       healthy   svc:shop/web   :8080→80   listening",
        "db      up 6m       healthy   svc:shop/db    :5432→5432 listening",
        "api     exited 137  ×3/2m     svc:shop/api   OOM  limit=512m",
        "disk    images 14G (4.2G dangling)  volumes 2.1G (1 unused)",
        "compose ./docker-compose.yaml → shop  3 svc, 3 present",
        "cursor  c:4f21a0",
      ],
    },
    {
      cmd: "dockerdesk read doctor api",
      cost: "~200 tok",
      out: "api  OOM-killed ×3  ·  mem_limit 512m too low  →  raise it in compose",
    },
    {
      cmd: "dockerdesk read logs api --dedup --budget 1500 --out .dockerdesk/logs/api.log",
      cost: "~300 tok",
      out: "1 unique line ×3 (deduped from 41), written to file — then Grep, paying for matches only",
    },
    {
      cmd: "dockerdesk do compose up --wait",
      cost: "~100 tok",
      out: "shop  3/3 ready  ·  api healthy in 4.2s  (this one still asks — it writes)",
    },
    {
      cmd: "dockerdesk read verify svc:shop/api",
      cost: "~80 tok",
      out: ":8080 answers from Windows  ·  200 OK  ·  PASS",
    },
  ],
  today: "Docker today: 15–30 calls · 30–60k tokens · 1–3 interruptions",
  target: "This session: ≈5 calls · ~2–5k tokens · 0 interruptions",
  note: [
    "A scripted session — the costs are ",
    { b: "targets DD23 must prove or falsify" },
    ", acceptance criteria and not measurements. Steps 1, 2, 3 and 5 are reads, so one allowlist line — ",
    { code: "Bash(dockerdesk read:*)" },
    " — removes every prompt on the inspection path while step 4 still asks.",
  ] as Rich,
};

/* ------------------------------------------------------------------ operator: two actors + ten laws */
// DD23's constitution, restated for the site (§DD45). This is the description the project
// ships under and the reason its surface is shaped as it is — the largest thing the
// pre-DD23 page was out of date about. Each law names the defect it prevents, which is
// what makes it checkable rather than a value statement.

export const operator = {
  eyebrow: "Who operates this",
  heading: "The operator is an agent. You install and approve.",
  intro: [
    "DockerDesk is a Docker installation whose primary operator is a coding agent. The agent runs, inspects and diagnoses; you install, approve and intervene. Every decision on the agent surface is judged in tokens rather than clicks — which is why the site opens on a session and not a screenshot.",
  ] as Rich,
  actors: [
    {
      who: "Agent",
      sub: "Claude Code",
      iface: "the dockerdesk CLI, over an ordinary shell",
      job: "Run, inspect, diagnose, clean up after itself",
    },
    {
      who: "You",
      sub: "at the keyboard",
      iface: "the installer, the tray, the container and log windows",
      job: "Install, approve, intervene, uninstall",
    },
  ],
  actorsNote: [
    "The desktop path is not sacrificed — it is what the installer and the tray (",
    { code: "DD14" },
    ", ",
    { code: "DD15" },
    ", Block C) are for, and the agent surface does not start until they ship.",
  ] as Rich,
  lawsEyebrow: "The design laws",
  lawsHeading: "Ten laws, in the order an agent meets them",
  lawsIntro: [
    "Binding, in the same sense as the product's. A feature that breaks one is wrong even if it was asked for. Each names the defect it prevents.",
  ] as Rich,
  laws: [
    { id: "P1", title: "The shell is the surface", body: "Every agent-facing capability is a dockerdesk CLI verb first. An MCP tool is a second head over the same method, or it does not exist." },
    { id: "P2", title: "One call replaces a session", body: "Learning what the machine is running is a product feature, not a docs problem. Needing six commands to learn the engine's state is a defect in DockerDesk." },
    { id: "P3", title: "Tokens are a measured budget", body: "Every response has a size ceiling and the canonical task has a measured cost. “It got cheaper” has to be a number, and a regression fails the build." },
    { id: "P4", title: "A file beats a stream", body: "An unbounded log read is the largest token sink here. Write it to disk and let the agent Grep it — it pays for the lines that match, not for the whole log." },
    { id: "P5", title: "Names, not ids", body: "A 64-hex id changes on every recreate. The address is the name — svc:<project>/<service>, or the container name — so ids stop being currency threaded across calls." },
    { id: "P6", title: "Errors are instructions", body: "Every refusal carries what was wrong, what is allowed, the nearest match, a correct example — and the Windows fact that explains it. An error that costs a round trip to read is a defect." },
    { id: "P7", title: "Never surprise you", body: "Read and write are split at the argv level so an allowlist can tell them apart. Destructive calls take a confirm token, and everything the agent creates is labelled with its session." },
    { id: "P8", title: "The agent cannot see", body: "Give it cheap textual proof that what it started works — the port listens from Windows, the mount resolved, the service answered — or every mistake costs a trip back to you." },
    { id: "P9", title: "Session N+1 is cheaper than N", body: "A cursor and a change feed, so a follow-up session reads the delta rather than re-deriving the whole machine from nothing." },
    { id: "P10", title: "Compose, don't fork", body: "The surface is a shape over the Engine API and facts Windows already knows. It is not a second Docker CLI and never grows a build, a push or a compose up of its own." },
  ],
};

/* ------------------------------------------------------------------ why */

export const why = {
  eyebrow: "Why it exists",
  heading: "The engine is free. The desktop app is what costs.",
  intro: [
    "Moby and the ",
    { code: "docker" },
    " CLI are Apache-2.0 and always were. What a company pays for, and what a managed laptop cannot install, is the wrapper around them. DockerDesk is a different wrapper: it installs the same engine, gets out of the way, and stops when you tell it to.",
  ] as Rich,
  cards: [
    {
      icon: "🪪",
      title: "No licence to count seats against",
      body: [
        "Free at any headcount, for any use, with no seat maths and no renewal date. That is the whole reason to try this instead of the thing everyone already has.",
      ] as Rich,
    },
    {
      icon: "🔌",
      title: "Nothing resident",
      body: [
        "No Windows service, no scheduled task, and autostart is ",
        { b: "off" },
        " until you turn it on. The engine runs for exactly as long as something is running it — an engine that holds gigabytes from every boot is the complaint this project starts from.",
      ] as Rich,
    },
    {
      icon: "🧍",
      title: "Per-user, no admin prompt",
      body: [
        "Everything lands under ",
        { code: "%LOCALAPPDATA%\\DockerDesk" },
        ": the verified downloads, the distribution's disk, and the ",
        { code: "docker.exe" },
        " that goes on your PATH. Which is what reaches a corporate laptop you are not an administrator on.",
      ] as Rich,
    },
    {
      icon: "📦",
      title: "Upstream, not a fork",
      body: [
        "Alpine's minirootfs, Docker's own static Linux binaries, Docker's own Windows CLI — each pinned to a version ",
        { b: "and a SHA-256 this project states itself" },
        ". Nothing is patched, so nothing here can be blamed for how the engine behaves.",
      ] as Rich,
    },
    {
      icon: "🧪",
      title: "It checks before it copies",
      body: [
        "A preflight reads the Windows build, virtualization, the WSL2 kernel and any rival engine, and refuses the install while a blocking row is not green. ",
        { b: "Nothing is written to disk while the answer is no." },
      ] as Rich,
    },
    {
      icon: "🗑️",
      title: "An uninstall that is one command",
      body: [
        "The distribution is called ",
        { code: "dockerdesk" },
        " and it is this tool's, never yours. Your own WSL distros are untouched, and removing the engine cannot take anything of yours with it.",
      ] as Rich,
    },
  ],
};

/* ------------------------------------------------------------------ preflight */

export const preflight = {
  eyebrow: "Before anything is installed",
  heading: "Why Docker will not run here — in four rows",
  intro: [
    "“It does not work on my machine” has four common causes on Windows, and they have four different remedies. ",
    { code: "dockerdesk-preflight" },
    " names the one you have, prints the command that fixes it, changes nothing, and exits ",
    { code: "1" },
    " so an installer can stop rather than fail halfway.",
  ] as Rich,
  terminalTitle: "dockerdesk-preflight",
  checks: [
    ["Windows build", " — 19041 or later, because below it no amount of configuration gets a WSL2 kernel"],
    [
      "Hardware virtualization",
      " — and it reads the hypervisor first: Windows reports the firmware bit as off once something has claimed it, so the naive order sends you into a BIOS to enable what is already on",
    ],
    [
      "WSL2",
      " — missing wsl.exe, a half-installed feature with no kernel behind it, and “new distros default to WSL1” are three different states with three different lines",
    ],
    [
      "Container engine",
      " — anything else that owns the docker command or the docker_engine pipe, because two engines competing for one pipe leaves neither working",
    ],
    [
      "Every row carries its remedy",
      " — the command that fixes it, wrapped and marked once, because repeating the arrow per line reads as several actions where there is one",
    ],
    ["Or as JSON", " — --json gives an installer the same report, verdicts and remedies included"],
  ] as [string, string][],
  note: [
    "The same check runs inside the installer, and the same report is what ",
    { b: "a clean Windows 11 virtual machine" },
    " is driven through on the way to a release — because a red row nobody has ever executed is not a check.",
  ] as Rich,
};

/* ------------------------------------------------------------------ engine */

export const engine = {
  eyebrow: "Provisioning",
  heading: "Seven steps, and it stops at the one that broke",
  intro: [
    "Provisioning runs from an installer, where there is no terminal to answer a prompt in. So every step is unattended, every step is named, and the run stops at the first failure — a report listing six failures where there was one is a report nobody can act on.",
  ] as Rich,
  steps: [
    {
      n: "1",
      title: "Acquire, and verify",
      body: [
        "Three of the seven steps, one per artefact: the Alpine root filesystem, the static Linux engine, and the Windows CLI zip — each downloaded and checked against a ",
        { b: "digest recorded in this repository" },
        ", not one served by the same host as the file. Already verified on disk means no second download.",
      ] as Rich,
    },
    {
      n: "2",
      title: "Inspect, before touching WSL",
      body: [
        "The engine tarball's member list is read locally: do the entries share one top directory, and are ",
        { code: "dockerd" },
        ", ",
        { code: "containerd" },
        ", ",
        { code: "runc" },
        " and the rest actually in there? A bad archive is caught before a distribution exists.",
      ] as Rich,
    },
    {
      n: "3",
      title: "Import an owned distro",
      body: [
        { code: "wsl --import dockerdesk … --version 2" },
        ". A fixed name that is this tool's, so an ",
        { code: "apt upgrade" },
        " or a ",
        { code: "wsl --unregister" },
        " you ran for your own reasons cannot take the engine with it.",
      ] as Rich,
    },
    {
      n: "4",
      title: "Install the engine inside it",
      body: [
        "One non-interactive ",
        { code: "sh" },
        " script under ",
        { code: "set -e" },
        ", so it stops where it broke: ",
        { code: "iptables" },
        " and ",
        { code: "socat" },
        " from apk, the binaries unpacked into ",
        { code: "/usr/local/bin" },
        ", and ",
        { code: "systemd=false" },
        " — nothing here is a service.",
      ] as Rich,
    },
    {
      n: "5",
      title: "Place the Windows CLI",
      body: [
        { code: "docker.exe" },
        " is extracted to ",
        { code: "…\\DockerDesk\\bin" },
        ". Putting that directory on your PATH is the installer's job, and this step's only job is being the path it points at.",
      ] as Rich,
    },
    {
      n: "?",
      ask: true,
      title: "Or just look first",
      body: [
        { code: "--plan" },
        " prints every pinned version, digest and path and reaches nothing at all. ",
        { code: "--acquire" },
        " downloads and verifies, and stops before WSL2 is touched — both change nothing outside this tool's own directory.",
      ] as Rich,
    },
  ],
  helpTitle: "dockerdesk-engine --help",
  help: `dockerdesk-engine — put upstream Moby into a WSL2 distribution this tool owns.

  --plan        the pinned versions, digests and paths; reaches nothing
  --acquire     download and verify every artefact, and stop
  --provision   acquire, import the distribution, install the engine, place docker.exe

  --run         start the engine and serve \\\\.\\pipe\\docker_engine until Ctrl+C
  --stop        stop the engine and terminate the distribution
  --status      what the engine is doing, by asking it
  --api         version and containers, read through the Engine API
  --watch       print /events as they happen, until Ctrl+C
  --autostart   on | off | status  - off unless you turn it on

  --help        this`,
  helpNote:
    "Exit code 0 means the mode finished; 1 names the step it stopped at. For --status, 1 means the engine is not answering.",
};

/* ------------------------------------------------------------------ pipe */

export const pipe = {
  eyebrow: "The transport",
  headingRuns: [
    "Your ",
    { code: "docker" },
    " commands do not need to know",
  ] as Rich,
  intro: [
    "A Linux ",
    { code: "dockerd" },
    " cannot create a Windows named pipe — that is a Win32 object. So something on the Windows side has to, or every shell and every script you already have needs a ",
    { code: "DOCKER_HOST" },
    ". DockerDesk is that something.",
  ] as Rich,
  cards: [
    {
      icon: "🔒",
      title: "The ACL is the reason",
      body: [
        "The pipe is created for ",
        { b: "your account and nobody else" },
        ". A forwarded port cannot say that, and full access to the Engine API is full access to the machine — so the hop runs over ",
        { code: "wsl.exe" },
        "'s stdio instead.",
      ] as Rich,
    },
    {
      icon: "🧩",
      title: "Your existing tools, unchanged",
      body: [
        "It is the same pipe name, so the CLI, Compose, Testcontainers, an IDE plugin and whatever your CI script does locally all find it without a setting. Nothing has to be told about DockerDesk.",
      ] as Rich,
    },
    {
      icon: "🧵",
      title: "An API client with no dependencies",
      body: [
        "The app talks HTTP over the pipe directly — a named-pipe stream handed to .NET's own HTTP handler, pinned to Engine API ",
        { code: "v1.43" },
        ". No NuGet package, and no shelling out to ",
        { code: "docker.exe" },
        " once per refresh.",
      ] as Rich,
    },
  ],
};

/* ------------------------------------------------------------------ tray */

export const tray = {
  eyebrow: "In the tray",
  heading: "“Is Docker up?” should be a glance",
  intro: [
    "The icon carries the engine state as a ",
    { b: "shape" },
    ", and colour only reinforces it. At sixteen pixels a hue is a hint: two of these are seen at a glance, by people who may not separate red from green, against a taskbar that is light on one machine and dark on the next. These three are still distinguishable in a screenshot printed in black and white.",
  ] as Rich,
  states: [
    {
      kind: "run" as const,
      title: "Running",
      body: [
        "A filled disc. And it means the engine ",
        { b: "answered" },
        " — the state comes from the event stream's own connection, not from remembering that a start was clicked.",
      ] as Rich,
    },
    {
      kind: "start" as const,
      title: "Starting",
      body: [
        "A ring with a bite out of it: the same outline as stopped and unmistakably not it, which is what tells “on its way up” from “not running” without relying on hue.",
      ] as Rich,
    },
    {
      kind: "stop" as const,
      title: "Stopped",
      body: [
        "A plain ring. ",
        { code: "Start engine" },
        " is one click away in the menu, and it launches the engine in a process the tray does not own.",
      ] as Rich,
    },
  ],
  splitEyebrow: "Deliberately short",
  splitHeading: "Four items, because a tray menu that grows is a second app",
  splitList: [
    [{ b: "Start engine" }, " / ", { b: "Stop engine" }, " — each enabled only when it would do something"] as Rich,
    [
      { b: "Open window" },
      " — the container list, and the one already open is brought forward rather than duplicated",
    ] as Rich,
    [
      { b: "Quit" },
      " — and the engine keeps running. The asymmetry is the point: a database another process is using does not die because somebody closed an icon",
    ] as Rich,
    [{ b: "The only thing that stops the engine" }, " is the menu item that says so"] as Rich,
  ],
};

/* ------------------------------------------------------------------ window */

export const windowSection = {
  eyebrow: "The window",
  heading: "One window, and it is the list of containers",
  intro: [
    "Because that is what the tool is opened for. Name, image, state, how long it has been up, and the ports — and the ports are links, because a published ",
    { code: "8080" },
    " is the thing you actually wanted and retyping ",
    { code: "localhost:8080" },
    " is a small daily tax a GUI exists to remove.",
  ] as Rich,
  caption: [
    { b: "Dark, because Windows is." },
    " The window is WPF on the built-in Fluent theme with ",
    { code: "ThemeMode=\"System\"" },
    " — light and dark follow the OS, with no extra package.",
  ] as Rich,
  detailsEyebrow: "The details",
  detailsHeading: "Correct without being asked, and empty on purpose",
  detailsList: [
    [
      { b: "No refresh button." },
      " The list is a view of the engine: it reads ",
      { code: "/events" },
      " as the daemon writes it, and only the events that change a container list cost a read — the rest would be a poll in disguise",
    ] as Rich,
    [
      { b: "Started in a terminal, shown here." },
      " A ",
      { code: "docker run" },
      " in any shell appears without you touching the window",
    ] as Rich,
    [
      { b: "The stream re-opens itself" },
      " after every break, so stopping and starting the engine does not leave a window quietly lying",
    ] as Rich,
    [
      { b: "Empty is a designed state" },
      ", and the two reasons a list is empty read differently — only one of them is something you can act on, and that one offers the button",
    ] as Rich,
    [
      { b: "Duplicates folded." },
      " A port published on both address families comes back twice from the API and would otherwise be two identical cells",
    ] as Rich,
    [
      { b: "UDP is text." },
      " Only TCP gets a link, because ",
      { code: "http://localhost:x" },
      " is not where a published UDP port is",
    ] as Rich,
  ],
};

/* ------------------------------------------------------------------ not resident */

export const notResident = {
  icon: "💤",
  heading: "It is not running right now",
  body: [
    [
      "There is no service and no scheduled task. The engine runs for exactly as long as something is holding it — quit that, and Windows is back to where it was. Autostart exists, it is a single per-user registry value, it is ",
      { b: "off unless you turn it on" },
      ", and turning it off ",
      { b: "removes" },
      " the value rather than setting it to zero.",
    ] as Rich,
    [
      "Nothing is uploaded, there is no account, and there is nothing to log into. The only network traffic this project makes is downloading the three pinned artefacts, from ",
      { code: "dl-cdn.alpinelinux.org" },
      " and ",
      { code: "download.docker.com" },
      ", during a provision you asked for.",
    ] as Rich,
  ],
};

/* ------------------------------------------------------------------ non-goals */

export const nonGoals = {
  eyebrow: "Scope",
  heading: "What it is not",
  intro: [
    "Five things this project has decided against, written down where they can be pointed at. A tool with no stated non-goals is a tool that will eventually be asked for all of them.",
  ] as Rich,
  items: [
    {
      title: "Feature parity with Docker Desktop",
      body: "Kubernetes, extensions, a dashboard for everything. The list a user actually opens the app for is short, and stopping there is what keeps this small enough to trust.",
    },
    {
      title: "A fork of the engine",
      body: "Upstream Moby, unpatched, pinned by digest. If the engine misbehaves, that is between you and upstream, and this project has not touched it.",
    },
    {
      title: "macOS and Linux",
      body: "Linux already has the engine, and the entire mechanism here is WSL2 and a Windows named pipe. Portability would mean a different tool wearing the same name.",
    },
    {
      title: "Telemetry, accounts or a sign-in",
      body: "Nothing is measured, nothing is sent, and there is nothing to register. There is no build of this with an opt-out.",
    },
    {
      title: "A resident background service",
      body: "The complaint that sends people looking for an alternative is an engine holding gigabytes from every boot. Not being that is the product, not a preference.",
    },
  ],
};

/* ------------------------------------------------------------------ status */
// The intro copy stays here; every figure and every row is generated from
// `roadkeep export --json` and read through src/lib/roadmap.ts (S2, DD43). The landing
// summary and the /status page are the two readers, and neither types a task.

export const status = {
  eyebrow: "Where it actually is",
  heading: "Honest status: there is nothing to download yet",
  intro: [
    "The engine and the container window work end to end — provision, start, stop, the pipe, the API, the event stream, the tray, the list. What does not exist yet is the thing you would install: no executable, no installer, no release. Building from source is the only way in today.",
  ] as Rich,
  roadmapUrl: `${repoUrl}/blob/main/docs/ROADMAP.md`,
  improvementsUrl: `${repoUrl}/blob/main/docs/IMPROVEMENTS.md`,
};

/* ------------------------------------------------------------------ /claude-code page */
// §DD46 — the page for the agent's operator. The read/do split is the highest-leverage
// decision in DD23 and it is invisible to the person who would benefit from it. The whole
// surface is designed (Block G) and the plugin is DD32 (Block F); the page says so, and
// links its status, so a reader is never told a shipped thing that is not.

export const claudeCode = {
  meta: {
    title: "DockerDesk for Claude Code — the read/do split",
    description:
      "The agent surface: one allowlist line splits reads from writes, so the 90% of agent Docker work that mutates nothing stops asking. Designed under Block G.",
    ogTitle: "DockerDesk for Claude Code",
    ogDescription:
      "The read/do split, the one allowlist line, and the plugin that makes the surface discoverable — the designed agent surface.",
  },
  eyebrow: "For the agent's operator",
  heading: "Configure it once, and the reads stop asking",
  intro: [
    "Listing containers and deleting a volume are one string to an allowlist, so a user either grants every ",
    { code: "docker" },
    " call — which permits deleting a volume — or approves each one. Splitting the verbs in argv makes it one line of settings, and what that buys is not keystrokes: it is the removal of an interruption from the 90% of agent Docker work that mutates nothing.",
  ] as Rich,
  status: [
    "This is the designed surface, not a shipped one — the CLI is ",
    { b: "Block G" },
    " and the plugin is ",
    { b: "DD32" },
    ", both open. Follow them on the status page; nothing here is downloadable yet.",
  ] as Rich,
  allowlistHeading: "The one line that pays for it",
  allowlistLead: "One entry in .claude/settings.json:",
  allowlistLine: "Bash(dockerdesk read:*)",
  allowlistNote: [
    { code: "read" },
    " is a promise, not a naming convention: a verb under it that writes is a defect. So this single grant removes every prompt on the inspection path, while every ",
    { code: "do" },
    " still asks.",
  ] as Rich,
  readHeading: "read — the inspection path (no prompt)",
  read: [
    { v: "context", d: "the whole machine in one budgeted, terse payload — engine, services, ports, disk, cursor" },
    { v: "doctor <name>", d: "the diagnostic join: the verdict and the remedy for one container" },
    { v: "logs <name>", d: "deduped and budgeted, written to a file the agent Greps — paying for matches, not the whole log" },
    { v: "ps", d: "the container list, addressed by name, with a cursor" },
    { v: "ports", d: "what is published, and whether it actually listens from Windows" },
    { v: "disk", d: "images and volumes — what is dangling, what is unused" },
    { v: "changes --since <cur>", d: "the delta since last session, so N+1 is cheaper than N" },
    { v: "verify <target>", d: "cheap textual proof a service answers — the agent cannot see" },
    { v: "path <name>", d: "resolve a name to its id and paths" },
  ],
  doHeading: "do — the mutating path (still asks)",
  do: [
    { v: "start · stop · restart", d: "lifecycle, by name" },
    { v: "rm", d: "remove, behind a confirm token" },
    { v: "compose", d: "shells out to the compose you already have, with --wait" },
    { v: "engine", d: "start or stop the engine itself" },
    { v: "reclaim", d: "an undo scoped to this session's own labels" },
    { v: "prune", d: "the machine-wide cleanup — still explicit, never implicit" },
  ],
  pluginHeading: "The plugin that makes it discoverable",
  pluginBody: [
    "A surface nobody discovers is one nobody uses, and the moment it is discoverable is the moment the installer runs. So the Claude Code plugin — the skill, the allowlist entry, and a project brief generated from the live machine — is ",
    { code: "DD32" },
    ", filed under the installer because that is what it is.",
  ] as Rich,
  refusesHeading: "What it deliberately refuses",
  refusesLead: [
    "DockerDesk is the substrate; the intelligence is the caller's. It is a CLI over the Engine API and facts Windows already knows — not a second Docker CLI (P10).",
  ] as Rich,
  refuses: [
    { t: "No model", b: "It calls no LLM." },
    { t: "No prompts", b: "It stores none." },
    { t: "No API keys", b: "There is no secret to hold." },
    { t: "No build", b: "That stays docker's." },
    { t: "No push", b: "And so does that." },
    { t: "No registry auth", b: "do compose shells out to yours." },
  ],
};

/* ------------------------------------------------------------------ /compare page */
// §DD47 — a visitor arrives having already decided against something. The honest question
// is narrow: what does this do that the alternative does not. A matrix of green ticks is
// not believed, so every rival gets a column for what it is genuinely better at, and the
// rows are grouped by the law each comes from so the matrix argues rather than tallies.

export const compare = {
  meta: {
    title: "DockerDesk vs Docker Desktop, Rancher, Podman, plain WSL2",
    description:
      "What DockerDesk does that Docker Desktop, Rancher Desktop, Podman Desktop or a hand-rolled WSL2 daemon does not — and what each of those is genuinely better at.",
    ogTitle: "DockerDesk — the honest comparison",
    ogDescription:
      "Checkable rows grouped by the law each comes from, and a column for what every alternative wins.",
  },
  eyebrow: "Against the alternatives",
  heading: "What this does that the others do not — and where they win",
  intro: [
    "You arrive having already decided against something. A matrix that wins every row is one nobody believes, so this one is grouped by the law each row comes from, and every alternative keeps the column it genuinely wins.",
  ] as Rich,
  columns: ["DockerDesk", "Docker Desktop", "Rancher Desktop", "Podman Desktop", "WSL2 daemon"],
  legend: [
    { sym: "✓", label: "yes" },
    { sym: "~", label: "partial" },
    { sym: "✗", label: "no" },
  ],
  groups: [
    {
      law: "Cost & access",
      rows: [
        { cap: "Free at any headcount", cells: ["✓", "✗", "✓", "✓", "✓"] },
        { cap: "Per-user install, no admin prompt", cells: ["✓", "✗", "✗", "~", "✗"] },
      ],
    },
    {
      law: "Footprint — nothing resident",
      rows: [
        { cap: "No service or VM held from every boot", cells: ["✓", "✗", "✗", "~", "~"] },
      ],
    },
    {
      law: "Compatibility",
      rows: [
        { cap: "Standard docker_engine pipe, no DOCKER_HOST", cells: ["✓", "✓", "✓", "✗", "✗"] },
        { cap: "Cross-platform (macOS, Linux)", cells: ["✗", "✓", "✓", "✓", "✗"] },
      ],
    },
    {
      law: "For an agent",
      rows: [
        { cap: "Read/do split at the argv level", cells: ["✓†", "✗", "✗", "✗", "✗"] },
      ],
    },
    {
      law: "Breadth — where a rival wins",
      rows: [
        { cap: "Kubernetes built in", cells: ["✗", "✓", "✓", "~", "✗"] },
        { cap: "Extensions / build cloud", cells: ["✗", "✓", "✗", "✗", "✗"] },
        { cap: "Rootless, daemonless", cells: ["✗", "✗", "✗", "✓", "✗"] },
        { cap: "Commercial support", cells: ["✗", "✓", "~", "~", "✗"] },
      ],
    },
  ],
  tableNote: "† designed under Block G — not shipped yet.",
  winsHeading: "What each alternative is genuinely better at",
  wins: [
    { name: "Docker Desktop", body: "Kubernetes, an extensions marketplace, a build cloud, and somebody to call. Pick it when you need those, or want a vendor on the hook." },
    { name: "Rancher Desktop", body: "Cross-platform and ships Kubernetes. Pick it on macOS or Linux, or for a Kubernetes dev loop." },
    { name: "Podman Desktop", body: "Daemonless and rootless. Pick it when a rootless, no-daemon model is the requirement." },
    { name: "Plain WSL2 daemon", body: "Free of this project entirely. Pick it if you would rather wire dockerd into WSL2 and manage the pipe yourself." },
  ],
  winsFooter: [
    "What is left is the axis where this one wins: a per-user install with no admin prompt, nothing resident, the standard pipe so no tool needs telling, and an agent surface that splits reads from writes.",
  ] as Rich,
};

/* ------------------------------------------------------------------ build */

export const build = {
  eyebrow: "Try it today",
  heading: "Four commands, from a clone",
  intro: [
    "You need the ",
    { b: ".NET 10 SDK" },
    " and Windows 10 build 19041 or later. The first two commands change nothing on your machine — the preflight only reads, and ",
    { code: "--plan" },
    " does not even download.",
  ] as Rich,
  steps: [
    {
      n: "1",
      title: "Check the machine",
      body: ["Run the preflight. If a row is red, its remedy is the next thing to do — and nothing has been copied to disk."] as Rich,
    },
    {
      n: "2",
      title: "Install the engine",
      body: [
        { code: "--provision" },
        " downloads and verifies, imports the ",
        { code: "dockerdesk" },
        " distribution, and puts ",
        { code: "docker.exe" },
        " under ",
        { code: "%LOCALAPPDATA%\\DockerDesk\\bin" },
        ".",
      ] as Rich,
    },
    {
      n: "3",
      title: "Run the tray",
      body: [
        "Start the tray and use ",
        { b: "Start engine" },
        ", or hold the engine in a terminal yourself with ",
        { code: "--run" },
        " until Ctrl+C.",
      ] as Rich,
    },
  ],
  commands: [
    { id: "clone", label: "Copy the clone command", text: "git clone https://github.com/alegauss/dockerdesk && cd dockerdesk" },
    { id: "preflight", label: "Copy the preflight command", text: "dotnet run --project src/DockerDesk.Preflight" },
    { id: "provision", label: "Copy the provision command", text: "dotnet run --project src/DockerDesk.Engine -- --provision" },
    { id: "tray", label: "Copy the tray command", text: "dotnet run --project src/DockerDesk.Tray" },
  ],
  planNote: [
    "Prefer to look before you download anything? ",
    { code: "-- --plan" },
    " prints every pinned version, digest and path and reaches nothing.",
  ] as Rich,
};
