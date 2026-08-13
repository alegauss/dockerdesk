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

export const navLinks = [
  { href: "#why", label: "Why" },
  { href: "#preflight", label: "Preflight" },
  { href: "#engine", label: "Engine" },
  { href: "#pipe", label: "The pipe" },
  { href: "#tray", label: "Tray" },
  { href: "#window", label: "Window" },
  { href: "#status", label: "Status" },
] as const;

export const footer = {
  links: [
    { href: repoUrl, label: "GitHub" },
    { href: `${repoUrl}/blob/main/docs/ROADMAP.md`, label: "Roadmap" },
    { href: `${repoUrl}/blob/main/docs/CHANGELOG.md`, label: "Changelog" },
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
