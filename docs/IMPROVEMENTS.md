# Improvements

## Block A — The Windows engine (Docker without Docker Desktop)

### §DD55 The distribution name and the app root are state, not spelling

Two names in `EnginePaths` are the only ones the rename cannot simply overwrite.
`DistributionName` is `"dockerdesk"` (line 14), and the parameterless constructor roots
everything under `%LOCALAPPDATA%\DockerDesk` (line 28). Both are state on a machine
rather than text in a build: the distribution holds every image, container and volume
the user created, and `distro`, `downloads` and `bin` hang off that root — `bin` being
the directory the installer put on `PATH`.

A build that simply spells them `freewilly` and `%LOCALAPPDATA%\FreeWilly` starts an
empty engine beside a full one, reports nothing installed on a machine that has
everything, and leaves behind a distribution no uninstaller now knows about. The comment
over `DistributionName` already states why the name is owned and fixed: it makes the
uninstall exactly one command. The rename has to keep that sentence true across the
transition.

So the deliverable is the migration, not the constant: detect the old distribution and
the old root, and either move them or adopt them in place, once and idempotently, with
the old names spelled in one place so the next reader sees them as legacy rather than
current. `dockerdesk-engine.exe`, which the tray looks for beside itself, is the third
name in this set and moves with it. Whether an adopted distribution keeps its old WSL
name forever or is re-imported under the new one is the decision this task makes and
records.

### §DD56 The rival probe loses the collision it was written against and gains a real one

`RivalEngineProbe` carries a rule that exists for one reason: this tool's own
distribution is called `dockerdesk`, and a substring test for "docker" would make the
engine report itself as a rival. The comment on line 65 says exactly that, and
`RivalEngineProbeTests` line 141 spells the pair — `dockerdesk` and `docker-desktop` are
one substring rule apart — with an assertion that `Judge` finds nothing on a machine
whose only distribution is `dockerdesk`.

`freewilly` contains no "docker", so the collision the rule was written against
disappears and that assertion starts proving nothing. Deleting both is the obvious move
and the wrong one, because DD55's migration creates the case that replaces it: a machine
that ran an older build has a `dockerdesk` distribution on it, and after the rename that
distribution is no longer this tool's own by name. A probe that reads it as an
unidentified rival engine tells the user to uninstall the thing they are running.

What this needs is for the old name to stay known to the probe as this project's former
distribution rather than as a competitor, and for the test to assert that rather than
the substring accident it currently asserts. The `dockerdesk-absent-` and
`dockerdesk-test-` prefixes across four test files are fixture names, carry no such
meaning, and are DD54's mechanical sweep rather than this task's.

## Block B — The daemon client (talk to the engine)

## Block C — The window (claude-tray's elements)

### §DD61 A translucent backdrop is a leak no overlap check can see

Measured 2026-08-13, immediately after DD22's own overlap check passed. With nothing in
front of the window and the copy cropped to the painted frame, the PNG still carried a
legible blurred image of another application's window behind it: a browser conversation,
readable enough to identify. A Fluent window's backdrop is translucent by design and
composites what is behind it, so the pixels inside the window's rectangle genuinely are
partly somebody else's content.

That is a different failure from the one DD22 fixed. The overlap check enumerates what
is above the window and refuses; this intruder is below it and arrives through it, so no
amount of Z-order reasoning reaches it. `scripts\Capture-Window.ps1` now says so on
every run and points at `--capture-window`, which has no such problem, but a printed
warning is not a refusal and the script still defaults to the very window it cannot
safely photograph.

Two candidate answers, and they are not equivalent. Make the backdrop opaque for the
duration of the copy — the window already paints an opaque surface for its own render,
so the brush exists — and the transmitted image goes away at the source. Or refuse the
main window outright and make the script take a popup or nothing, which is what it is
actually for. The first keeps one script useful for both; the second is smaller and
admits that a screen copy of a translucent window is not a thing worth making safe.

## Block D — Container operations (what a user came to do)

## Block E — Images, volumes and networks

## Block F — Installer and distribution (free, Apache 2.0)

### §DD54 The tree spells the old name in four projects and three namespaces

`DockerDesk` is the tree's own spelling, not Docker's: four project files
(`DockerDesk.slnx`, `DockerDesk.Core.csproj`, `DockerDesk.Tray.csproj`,
`DockerDesk.Preflight.Tests.csproj`), three `RootNamespace` declarations, the
`AssemblyName` that produces `DockerDesk.exe`, `DockerDesk.ico`, and the `Product`,
`Company` and `Copyright` in `Directory.Build.props` that travel into the published
binary. Ninety-odd namespace and using lines follow from those three declarations and
change with them.

Nothing here is state on a user's machine, which is what makes it the first task rather
than the risky one: a directory rename, a namespace rewrite, and `build\build.cmd`,
`build\build-installer.cmd`, `check.yml` and `release.yml` following the paths. The test
harness comes with it — `dockerdesk-vm.env` and the five `DOCKERDESK_` variables
`scripts/vm.ps1` reads.

What must not move: no type in the tree is named after the product. `DockerApi`,
`DockerEvent`, `DockerContextProbe` and `RivalEngineProbe` name Docker or the thing they
probe, and `docker_engine` is the pipe the Docker CLI connects to by protocol, not a
name this project chose. Renaming any of them would be a second, wrong rename hiding
inside this one. `DistributionName`, `%LOCALAPPDATA%\DockerDesk` and the installer's own
identity are left to the tasks that follow this one in the set, because each of those is
a migration of state on somebody's machine rather than a spelling in a build.

### §DD57 The installer is one identity and six spellings

`build\installer.iss` states the product six times and identifies it once. `AppId` on
line 25 is `{{6B0E4D2A-9C77-4A31-8F5E-DOCKERDESK001}` — the old name is inside the GUID
itself — and Inno Setup treats that string, and only that string, as the product's
identity. Keep it and every future setup upgrades the old entry under a new label;
change it and a machine carrying the old build ends up with two entries in Add/Remove
Programs, two Run-key values, and one uninstaller that deletes the other's executable.

The rest is spelling that follows: `MyAppName`, `MyAppPublisher`, `MyAppExeName`,
`MyPublishDir`, `SetupIconFile`, `DefaultDirName={localappdata}\DockerDesk`, the
`OutputBaseFilename` that produces `DockerDesk-Setup.exe`, the Run-key `ValueName`, the
`DistroName` the uninstaller unregisters, and the two message boxes that name the
product back to the user. `release.yml` names the setup artefact, and
`dist\DockerDesk-0.1.0.exe` is in the tree already.

The decision this task makes and records is whether an old installation is upgraded in
place or asked to uninstall first. Either is defensible, and the choice is coupled to
DD55's: the uninstaller's own prompt offers to keep the engine root so that reinstalling
picks it up, which is the path a rename breaks quietly. What is not defensible is
shipping a setup that leaves two products behind, since the version this replaces was
published and is on machines.

## Block G — The agent surface (an agent operates this, and pays in tokens)

### §DD58 The invocation name is quoted in a file this project does not own

The surface an agent drives is invoked by name, and the name is quoted in a place this
project does not own. `CommandLine.ExecutableName` is `"DockerDesk.exe"`, the published
surface documents `dockerdesk read context`, `dockerdesk read doctor api`, `dockerdesk
read logs api` and `dockerdesk do compose up`, and beside them stands the allowlist
entry `Bash(dockerdesk read:*)` — a literal prefix match that a user pasted into their
own `settings.json`.

That is what separates this from a rename inside the tree. An allowlist pattern cannot
be migrated from here, so a session whose entry says `dockerdesk` starts asking for
approval on every read the moment the executable answers to something else — which is
exactly the cost DD24 and DD32 exist to remove. The name therefore has to be settled
before DD32 writes an install-time settings entry, or DD32 writes the wrong string into
the one file the install was finally allowed to touch.

The open question is the name itself. `freewilly read logs api --dedup --budget 1500`
puts nine characters of prefix on a line an agent emits constantly and a user reads
inside an approval prompt, and a shorter head — `willy`, or an abbreviation — is worth
weighing against a command that matches the product. Whichever is chosen, one spelling
has to serve the executable, the documented invocations and the allowlist pattern
together, because a pattern that disagrees with the executable matches nothing.

### §DD63 A stamp with nothing to stamp is half a promise

DD29 shipped the label, the plan and the confirm token, and every one of them is
exercised only against fixtures: `AgentSurface.All` holds one `do` verb and it starts an
engine. So `read changes` on a real machine answers "(nothing carries this session's
label)" and always will, and the first bullet of DD29's own section — everything created
through `do` is stamped — is true of an empty set.

What closes it is the first verb that creates: `agent-budget.json` already carries a
ceiling of 140 for `do compose up`, written when the surface was designed, so the shape
is budgeted before it exists. Whatever lands there takes its labels from
`SessionLabel.For` rather than assembling its own, because a create that forgot the
stamp is invisible to the undo and indistinguishable from the user's own work — which is
the symptom DD29 exists to remove, reintroduced one verb at a time.

The thing to be careful about is compose specifically: `docker compose up` labels what
it creates with its own project and service labels, and a container it made carries them
whether this tool stamped it or not. Two label sets on one object is fine. A reclaim
that inferred ownership from the compose project rather than from the session label
would not be, because a project outlives a session and the user's own `docker compose
up` writes the same project label.

### §DD64 A gate that goes red for the wrong reason stops being a gate

Two red runs now, both in `AgentBudgetTests` and both during a full suite:
`The_canonical_task_costs_what_the_budget_records` on 2026-08-13, and
`Re_discovery_is_the_largest_driver_and_not_the_inspect` shortly after. Neither
reproduces — roughly forty full runs since, including sixteen four-at-a-time to force
contention, and each passes in isolation immediately after failing.

The second failure refutes the first diagnosis. This section previously blamed the fake
daemon's recorded request count, on the reasoning that it was the only non-deterministic
assertion in that test. `Re_discovery` never looks at that count, so a double-counted
request cannot explain it.

What explains both is a short read. Both tests measure `TokenEstimate` over bodies
pulled with `StreamAsync` and `ReadToEndAsync`, while `FakeDockerDaemon.AnswerAsync`
writes the response, calls `WaitForPipeDrain` and disposes the pipe from a
fire-and-forget task. A client that has not finished reading when the server disposes
sees the stream end rather than an exception, so it returns a body that is merely
shorter. That lands as fewer tokens: a band violation in the first test, a broken
equality in the second.

So this is a defect in the harness rather than in what it measures — and it is the
assertion gating every cost claim here, so a red run reads as "a response got more
expensive". A gate that cries wolf is one somebody re-runs until it is green.

The fix is to make the read complete rather than to widen the assertions: the fake
should not dispose until the client has read what it was sent.

### §DD65 The benchmark refused to invent a number, and now one exists

`agent-budget.json` carries `"surface": { "exists": false }` beside an `about` naming
`read context`, `read doctor`, `read logs` and `read verify` as work that "does not
exist yet, so there is no number here and one is not invented". That refusal was right
when written and is now false: all four shipped, each with a measured ceiling in the
same file.

What is missing is the thing DD23 built the file to produce. The baseline is recorded —
6 calls, 11711 tokens for the canonical task through the Engine API — and the shaped
side is still blank. The `target` block asks for 5 calls and 5000 tokens, and nothing
yet says whether the surface met it.

The work is to measure the canonical task through the surface the way
`MeasureCanonicalTaskAsync` measures it through the API — same fixtures, same fake
daemon, same estimator — and record calls, tokens and the ratio, with a test that fails
if either side moves. Not to sum the per-shape ceilings: the claim is about a task, and
a task is calls as well as tokens.

Two cautions. DD64 is unfixed and it is the harness this would run on, so a number taken
before that is one that can drift under load. And the honest comparison is
task-for-task: the surface answers in fewer calls partly by answering a slightly
different question, and the record should say so rather than let a ratio imply the two
payloads are interchangeable.

## Block H — The public surface (the site a reader and an agent both read)

### §DD59 The published surface is a path, not just prose

The site does not merely name the product, it is served from a path that contains it.
Every route in `site-content.ts` is `/dockerdesk/…`, the canonical URL is
`https://alegauss.github.io/dockerdesk/`, `repoUrl` is
`https://github.com/alegauss/dockerdesk`, and the base path is what GitHub Pages derives
from the repository name. Renaming the repository moves every published URL at once, and
nothing serves the old ones: GitHub's redirect covers the repository, not a Pages path a
reader or an `llms.txt` consumer already recorded.

Inside the site, the title, the hero, the compare page and the terminal transcripts all
say DockerDesk, and `logo.svg`, `og.svg` and `llms.txt` exist in three copies each
(`site/public`, `site/dist`, `site/dist-server`) of which only `public` is authored.
`package.json` names the workspace `dockerdesk-site`, and `vite.config.ts` and
`prerender.mjs` carry the base path that the prerender's own route-pair assertion
checks. `README.md`, `CONTRIBUTING.md` and `NOTICE` name the product to a reader
arriving from the repository, and `docs/` still holds the previously published
`index.html`, `sitemap.xml` and `robots.txt` that Pages served before DD50 moved the
build.

The repository rename is not a write this project can make, so sequencing is the whole
risk: the base path and the repository name have to change in the same window, or the
published site returns 404 on every route it has.

### §DD60 The old name in the governed files leaves by verb or not at all

Fifteen lines of governed prose name the product: one in `ROADMAP.md` — the non-goal
beginning "A model, prompts or API keys", which says DockerDesk is the substrate an
external agent drives — five in `CHANGELOG.md`, and nine in `IMPROVEMENTS.md`. The guard
denies an `Edit` to every one of them, so each moves through the verb that owns it:
`restate` or `amend` for a task line, `section` for rationale prose, `record` for a
ledger entry, `non-goal amend` for the bullet.

The count only shrinks from here, which is an argument for doing this late rather than
first: a `ship` drops the rationale section it retires, so an old name inside an
unshipped section leaves with its task. Doing it at all is the point, though, because
`CHANGELOG.md` is the ledger a reader consults for what this product has done, and a
ledger naming a product nobody can find is the one file where the stale name actively
misleads.

Outside `docs/`, `.claude/skills/dockerdesk-roadmap-docs/` is a directory name that
`.claude/settings.json` and every invocation of the skill spell out, and the skill's own
prose names the project throughout. What does not change is `roadkeep.toml`'s `prefix =
"DD"`: renumbering the ids would rewrite every dependency, every section anchor and
every pushed commit message that cites one, to say the same thing in two different
letters.
