# Improvements

## Block A — The Windows engine (Docker without Docker Desktop)

### §DD52 Wrapping a detail that contains paths

Measured after DD16: the rival row's detail is 254 characters, because it now carries
every signal the row was found by — a resolved command, a registered distribution and an
installer path. A terminal wraps it, so nothing is lost, and that is why this is an idea
rather than a defect.

The obvious fix was tried and reverted in the same session. `ReportText.Wrap` breaks on
spaces, which is right for the remedy — prose — and wrong here: aligned under the detail
column it produced `…\Programs\DockerDesktop\Docker` on one line and `Desktop.exe)` on
the next, and a path split at a space is one nobody can copy or grep. That is worse than
a long line, which is the whole reason the change went back.

So whatever is done here has to treat a path as atomic. One evidence item per line is
the shape that avoids the problem rather than working around it, and it needs the detail
to stop being a single joined string — which is a change to what a row carries, not to
how a row is printed.

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

### §DD34 One meaning, one declaration, and every window reduced to what is its own

There is no `App.xaml`, no `ResourceDictionary` and no `Brand`. Every style this app has
is declared inside the window that first needed it, which is why `MainWindow.xaml` and
`LogWindow.xaml` each carry their own `BooleanToVisibilityConverter`, their own
`BasedOn` button style with the same comment explaining the same Fluent trap, and the
same `ThemeMode`, `FontFamily` and font-stack spelled out twice.

The colour is the sharper case. `#E5484D` means one thing — *this is the engine's
refusal, or stderr* — and it is written four times: three in `MainWindow.xaml` and once
in `LogWindow.xaml`. None of the four is pinned by a test, so all four can move
independently, and the failure is quiet: a failure line under a container row and a
stderr line in its log window in two reds, saying two things where the point was one.
The engine's own three states have a second, separate source in `StateIcon.ColourFor`,
which is GDI+, and `ShowEngine` converts it to a WPF brush by hand at one call site.

claude-tray met this exact problem and its answer is `Brand.cs`: the value, as bytes,
declared once, with each edge converting — GDI+ for the tray icon, a frozen `Brush` for
WPF, a hex string for anything that wants text. Its docstring says why a value and not a
brush, and the reasoning transfers unchanged.

So: one `Theme.xaml` merged at application scope for what is markup, one `Palette` for
what is a value, and every window reduced to what is actually its own.

### §DD35 The shell owns the chrome and a list owns its page

`MainWindow.xaml` is 447 lines and `MainWindow.xaml.cs` is 586, and between them they
own the engine banner, three lists, three header rows, three empty states, two prune
confirmations, the log windows and the terminal launch. The three lists are not variants
of one thing in the file; they are three hand-written copies of the same stanza — a
header `Grid`, a `ListView` with a duplicated column set, an `Empty` panel, a
`Refresh…Async`, a `Redress…`. DD12 and networks each add a fourth and a fifth copy.

claude-tray splits this the other way. `MainWindow` there is 104 lines of XAML and 129
of code-behind, and owns *only* the chrome: a nav strip of `RadioButton`s in one group,
and a `DestinationHost` grid. Each destination is a `UserControl` with its own header,
navigation and footer, built the first time it is navigated to and then kept alive
collapsed, so a scan, a chart's history and a half-edited setting survive switching
away. The heavier ones are split again by concern across partial classes.

The parts of that worth taking are the ones that are structure and not taste: the shell
owning the chrome, one page per list, pages built lazily and kept alive. The tab strip
is a smaller question — WPF's default `TabItem` headers are the least-designed pixels in
this window, and the accent-underline strip is what the sibling app already looks like.

The test that pins each header's columns to its rows moves with the page it belongs to.

### §DD36 A row carries its state, not a wall of captions

A container row today is five plain `TextBlock`s and up to six `Button`s with word
captions — Logs, Shell, Start, Stop, Restart, Remove — in a fixed 320px column. At forty
containers that is two hundred captions, and the eye has nothing to skip past. Nothing
highlights on hover, so there is no feedback that a row is a row; a click anywhere but a
button does nothing at all.

State is the column actually scanned and the one drawn with the least: `running` and
`exited` in the same tertiary grey as the status beside them, which restates it in the
daemon's words. claude-tray's rows put that kind of fact in a tinted chip — a rounded
`Border`, a translucent tint that works on both surfaces, and a tooltip carrying the
evidence behind the claim, because a chip is an assertion. `RowStyle` resolves those
brushes once per render, not per row.

The actions are pressed once a session. Hiding them behind hover or a context menu — the
pattern `SourceRowTemplate` already uses — costs a discovery problem, so the answer is
not all-or-nothing: keep the one or two verbs a row is actually opened for visible, and
move the rest. Logs stays visible in every state, for the reason DD9 gives.

What must not be lost: the pending word, the engine's own refusal under the row, and
Shell disabled-with-a-tooltip rather than hidden. Those are answers, and a redesign that
drops them is a worse row that looks better.

### §DD37 A heading that sorts, and a box that narrows

Every heading in this window is a dead `TextBlock`. NAME, IMAGE, STATE, REPOSITORY, SIZE
— none does anything when clicked, and the order a list arrives in is the order it is
read in. Images are sorted by size, which DD11 chose and is right as a default;
containers are in whatever order the daemon returned them, which is creation order and
answers nothing.

There is no filter anywhere. A developer's machine carries thirty to sixty images and a
dozen containers, and the only way to reach one is the scrollbar. The window is rarely
opened to survey the machine; it is opened with one container in mind, and that is the
case it serves worst.

claude-tray templates a sorting heading as a `Button` whose template is a `TextBlock`:
it looks like a label, behaves like a control, and its own comment gives the reason — a
heading that reorders the list on click and offers no affordance is a feature only its
author finds. Hover brightens it, and the sorted column carries a glyph.

So: every heading sorts, each list keeps its default, and one filter box per list
narrows by the fields already on the row — a name, an image, a repository, a port.
Filtering is over the rows in hand, never a second call to the daemon, and it survives a
refresh from the event stream — the part easy to get wrong. An empty result is a third
empty state, saying what was typed.

### §DD38 A window is drawn from a fixture, not from whatever is running

Nothing here can be looked at without a running daemon behind it. Every window takes a
`DockerApi` and asks it for the rows it draws, so seeing the images tab means having
images, seeing a failure line means causing a failure, and seeing a pending row means
catching a stop mid-flight. The three empty states, designed prose, are the hardest to
reach on purpose.

That has two costs. A change to the window is reviewed by describing it, and a
screenshot is whatever the machine happened to be running that afternoon — which is also
somebody's container names in a public README. And it is the reason DD22 exists at all:
a capture verb needs something to capture, and today that something is the live screen.

claude-tray solves it with fixtures and flags. `ContextFixture`, `AccountFixture`,
`EnvironmentFixture` and `SampleRoot` build a known machine; `--settings`, `--stats`,
`--context --window` and the capture flags render one page from it; `PageWindow` is a
bare host that exists so a preview is the page and not the shell around it. The captures
are deterministic, which is what makes them reviewable.

The equivalent here is a fake `DockerApi` — a small set of containers, images and
volumes chosen to cover running, exited, published and exposed ports, dangling and
in-use images, an anonymous volume, a pending row and a refusal — plus a flag that opens
any page against it. The fixture is also the fastest route to the empty states, which
are otherwise reached by deleting things.

### §DD39 The window remembers where it was and what was being read

`WindowStartupLocation="CenterScreen"`, `Width="1040" Height="560"`, and nothing
remembered. A tool opened several times a day lands in the middle of the primary monitor
every time, at a size chosen for a laptop, on a desk that has two screens and a place
this window belongs. The log window is `CenterOwner`, which is right for a child, and it
too forgets whatever it was resized to.

The tab is the sharper loss. Somebody clearing disk space works in Images; somebody
debugging works in Containers. The tab is a piece of state the user set on purpose, and
it is discarded the moment the window closes — including when it closes because the
engine was restarted.

None of this needs a settings file of its own. Window placement, the last destination,
and later the sort and filter DD37 adds are a handful of values, and there is already an
`ArtefactStore` and an `EnginePaths` that know where this application's data lives.

Two things it must get right, because both are how this feature is usually got wrong. A
saved rectangle is restored only if it still lands on a monitor that exists — a window
remembered onto a laptop's docked second screen is a window that opens off-screen the
next morning. And a maximised window is remembered as maximised plus its restore bounds,
not as a rectangle the size of the screen.

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

### §DD53 The guest drifts, and nothing takes it back

`scripts/vm.ps1` line 14 says the guest "can be reverted to a clean snapshot between
destructive runs", and line 18 says nothing reverts a snapshot "unless you ask for it by
name". There is no name to ask: the `ValidateSet` on line 57 offers `doctor`,
`preflight`, `run`, `start`, `engine` and `screenshot`, and none of them reverts.

Measured while shipping DD18. That row was specified against a fresh Windows 11 guest,
build 26200, that never had WSL — `wsl --version` hung there and the row cost fifteen
seconds. Running the fix against the same guest reported `WSL 2.7.11.0, kernel
6.18.33.2`: WSL had been installed there by earlier work, so the one machine the defect
existed on no longer had it, and the exit-50 path shipped verified by unit tests alone.
`Snapshot 1` was sitting right there in the doctor's own output.

What this needs is a `revert` action that names the snapshot it is going to discard and
refuses without confirmation, since a revert throws away whatever the guest holds. The
harness already reads the snapshot list for the doctor, so the fact it needs is one it
already has. The reason this is worth a task rather than a footnote is that DD19 and
DD20 are both specified against particular machine states too, and each one is
verifiable exactly once until this exists.

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
