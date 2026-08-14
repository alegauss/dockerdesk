# Improvements

## Block A — The Windows engine (Docker without Docker Desktop)

### §DD68 A guard that can be satisfied by undoing a shipped fix

`A_long_remedy_wraps_rather_than_running_off_the_console` ends with `Assert.All(lines,
line => line.Length <= 100)`, and it means it about every line in the report. DD52 then
shipped a row whose evidence lines must not be wrapped at all: a path is atomic, and
breaking one at a space is the defect that made the first attempt at DD52 get reverted.

The two rules now contradict each other, and the only reason the suite is green is the
fixture. That test builds its report from `C:\Program Files\Docker\x.exe`, which lands
at 58 characters. The real machine this was measured on renders `docker resolves to
C:\Users\alexa\AppData\Local\Programs\DockerDesktop\resources\bin\docker.exe` at 113.

So the next contributor who makes that fixture realistic gets a red test that names a
line length, and the obvious repair is to wrap the line — which reintroduces the split
path DD52 exists to prevent, with a test now demanding it. A guard that can be satisfied
by undoing a shipped fix is worse than no guard, because it argues for the defect.

The assertion is about the remedy and should say so: it belongs to the wrapped block,
not to every line the renderer emits. Whatever replaces it should also state the other
half out loud — that an evidence line is allowed to be as long as the thing it names —
so the two rules read as one decision rather than as an exception somebody has to
rediscover.

## Block B — The daemon client (talk to the engine)

## Block C — The window (claude-tray's elements)

### §DD67 Nothing drives a popup open, so the path that captures one is unreachable

DD61 settled what each capture path is for: `--capture-window` renders the window's own
visual tree, and `scripts\Capture-Window.ps1` is the screen copy for a popup, because a
popup is its own top-level window and is not in that tree. The refusal DD61 added makes
that division real rather than advisory — the script now declines the Fluent shell
outright.

What it does not do is reach a popup. A menu exists only while it is open, and nothing
opens one: the script launches the executable with `--window` and copies whatever it
finds, and the only thing it finds is the window it has just been taught to refuse. The
search was widened for `#32768`, `tooltips_class32` and the WinForms popup class, so an
open menu would be found first — Z order puts it above the shell — but the default run
refuses and writes nothing.

So the tray's context menu, the balloon tip DD21 measured, and every other popup this
product draws have never been photographed by anything.

Two shapes worth weighing. The script could open the menu itself, which means a Win32
click against the notification area — reaching into another process's UI, and the
overflow makes finding the icon its own problem. Or the product could show its own popup
behind a verb, the way `--capture-window` already renders on request, which keeps the
driving inside the process that owns the menu and costs one more surface.

### §DD69 §DD66 The water belongs at the foot of the window, and only moves because the engine does

The site's identity is water: the mark is an orca cresting a wave, so `Waves.tsx` closes
the hero into it and opens the footer out of it, in three blues and a foam. None of that
reaches the window, whose lowest strip is sixteen pixels of margin and then the frame —
the one place here with nothing to say.

The obvious version is wrong. A decorative band inside a utility window is scenery, and
`index.css` already states the test it fails: scenery that outshines the copy is a bug.
Fluent accepts perpetual motion only where the motion informs. So the water drifts while
the engine runs and lies flat and still while it does not — a second reading of the
state the dot and its word already carry, which is L8's order: shape and word first,
this only reinforcing.

The geometry is the site's own numbers rather than a lookalike — three layers at
72/26/720, 100/16/480 and 122/11/360 over a 2880 span, drifting in 43s, 31s and 22s,
foam on the front crest. Seamless because 1440 divides every period. Colour joins
`Palette` as bytes, translucent the way `RowStyle`'s chip fill already is, so one set
serves both surfaces.

Three things must switch it off: a hidden window, `ClientAreaAnimation` off, and
`RenderCapability.Tier` 0. And `--capture-window` must stay byte-identical, so a capture
holds phase zero — the review harness rests on that.

### §DD70 §DD70 Motion that explains a change, which is the only motion Fluent asks for

Fluent's motion exists to answer one question: did that change, or did I misread it?
Nothing in this window answers it. A refresh arriving from the event stream replaces the
rows under the cursor with no transition, so a container that appeared and a container
that was always there are drawn identically. The engine dot is amber while `Starting`
and then green, and amber is the only thing saying that a wait is in progress rather
than a state that has settled.

Two moves, both of the kind that ends: a dot that breathes while the engine is starting
and stops the moment it is running or stopped, and a row that fades in as it joins the
list and out as it leaves. Neither invents information. The dot restates `Starting`,
which the label beside it already says in words; the fade says only where the eye should
look, which is the change.

This is ranked above §DD69 on interface quality and below it on identity, so it is a
second task rather than a rider on the first: one is scenery made honest, the other is
the framework's own contract with a reader.

The constraints are §DD69's, and for the same reasons. Under `ClientAreaAnimation` off
or `RenderCapability.Tier` 0, both moves must resolve to their end state immediately
rather than to a slower version of themselves. And `--capture-window` must still produce
a byte-identical PNG, which a row mid-fade at the one-second settle would break.

## Block D — Container operations (what a user came to do)

## Block E — Images, volumes and networks

## Block F — Installer and distribution (free, Apache 2.0)

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

The name is settled: `FreeWilly`, and `freewilly` wherever the spelling is lower-case —
the WSL distribution and the invocation. It was weighed against a shorter head, since
`freewilly read logs api --dedup --budget 1500` puts nine characters of prefix on a line
an agent emits constantly and a user reads inside an approval prompt, and `willy` costs
four fewer. A command that matches the product won.

That one spelling serves the executable, the documented invocations and the allowlist
pattern together, because a pattern that disagrees with the executable matches nothing.

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

What is left is the transport half, plus a correction the next attempt needs.

A third red run — `An_unknown_argument_is_refused_rather_than_dropped(--plan
--nonsense)`, twice in five full runs — refuted the transport diagnosis for its own
case, because `--plan` reaches no daemon at all. It was `Console`: four classes touch
the process's, three by swapping and restoring it, and xUnit runs classes in parallel,
so one restore lands over another's capture. They are one collection now, and eight full
runs are green against two in five red before.

The transport half keeps its diagnosis and loses its prescribed fix. "The fake should
not dispose until the client has read what it was sent" was implemented twice. Waiting
for the client to hang up went green six of six and took the suite from 20 seconds to
38: a client that pools its connection never hangs up, so the wait ends on its timeout.
Holding every pipe open until the fake is disposed hung the suite outright —
`Connection: close` is how a streaming read learns it reached the end, so a pipe never
closed is a read that never returns.

The close is load-bearing, and the race is in its timing rather than its existence. What
replaces it has to end the stream for a client waiting on EOF and not for one still
draining a counted body — a distinction this fake cannot make, because it does not know
which of the two it wrote.

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
