# Improvements

## Block A — The Windows engine (Docker without Docker Desktop)

### §DD73 Compose is a plugin nobody placed

The manifest pins three artefacts and the Windows zip carries one binary: docker.exe.
Compose v2 is a separate upstream release with its own digest, so placing it is a fourth
pinned artefact and nothing more — the same download, verify, place shape PlaceCli
already has.

Where it lands is the decision. The CLI finds a plugin under the Docker config
directory, which is the user's own and is the one place this project has refused to
write ever since DD32 left the agent files beside the install and printed the two
commands instead. Three candidates, in order of how much they touch: a plugins directory
inside this install with DOCKER_CONFIG naming it, which fixes the bundled call and
leaves a plain shell without compose; the same directory offered to the user as one
command the after-install page prints; or writing the user's config directory outright,
which is what a rival does.

DD63 raises the cost of getting this wrong: the verb ships, shells into docker compose,
and on a machine that never had Docker Desktop the refusal it returns is about a
subcommand that does not exist rather than about the project it was asked to bring up.

### §DD74 Build without BuildKit is build without most Dockerfiles

BuildKit is not an option a modern Dockerfile takes: a cache mount, a heredoc, a secret
mount and a multi-platform build are all syntax the classic builder cannot parse, and
the classic builder is what a CLI with no buildx plugin falls back to — with a
deprecation notice, on the versions that still have it at all.

So the gap is not that builds are slower. It is that a Dockerfile a developer already
has, which builds on any machine with a current Docker, fails here on a line the error
message blames on the file rather than on the missing plugin. That reads as this tool
being unable to build, which is the impression the whole project exists to avoid.

The mechanics are the compose task's mechanics — a pinned upstream release, a digest,
and a plugins directory — so whichever placement that one settles on, this one follows
it and adds no new decision. What is worth measuring first is what docker build actually
does on the pinned CLI with no plugin present, because a version that removed the
classic builder turns a slow path into a dead one.

### §DD75 A bind source is a path the daemon reads, and the daemon is Linux

A bind source is a path the daemon resolves, and the daemon is Linux. Docker Desktop
hides this: something in its stack rewrites a Windows drive path into a host mount
inside the VM, which is why an inspect there reports a source under
/run/desktop/mnt/host — the convention DD26 deliberately refuses to recognise. Nothing
in this project does that rewriting, and the Windows CLI sends what it was given.

So the paths that work here are the ones already spelled the distribution's way, through
the automatic drive mounts Wsl.ToDistributionPath produces, and the mount row in doctor
and verify was written to that expectation. The paths that fail are the ones a user
actually types and the ones compose computes for them: a relative volume in a compose
file is resolved against the project by a Windows binary, so it arrives as a drive path
nobody chose.

What has to be measured before anything is designed is which of the two failures this is
— a daemon refusing a source that is not absolute, or a daemon quietly accepting a
string and giving the container an empty directory. The second is the one the mount row
exists to catch, and it is also the one that costs a user an afternoon.

### §DD76 The other side of the pipe, and what it would cost

A developer whose shell is Ubuntu under WSL2 has no engine here. The daemon listens on a
unix socket inside the owned distribution, the only way out is a Windows named pipe, and
a Linux client cannot dial one. Docker Desktop answers this with a per-distribution
toggle that mounts its own Linux CLI and a socket into each distribution the user ticks.

That answer is in tension with two things this project has already decided. The owned
distribution exists so that nothing of the user's is touched, and the same instinct kept
the install out of a .claude directory; writing a CLI and a socket into somebody's
Ubuntu is the largest version of exactly that. And the pipe is a pipe rather than a port
because its ACL restricts the Engine API to one account, which a socket reachable from
any distribution starts to give away.

So this is filed as an idea and not as a design. The cheap intermediate is to say the
true thing in the after-install page and in the doctor rows a WSL shell would reach: the
engine is on the Windows side, docker.exe is what reaches it, and a bind mount typed in
a WSL shell carries a path the daemon reads differently.

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

### §DD80 Why the default inverts instead of the shortcuts

The launch contract is inverted here rather than in the shortcuts, because the shortcuts
are not the only way in: a developer who installs by building runs the executable
straight out of the publish folder, and that path carries no arguments either.

So a bare argv becomes tray plus window, and the silent start becomes the thing that has
to ask for itself. Only one caller wants silence, the Run key the installer writes for
the start with Windows task, and a window in the face at every logon would be the
regression this change could otherwise cause. That value gains an explicit flag and
nothing else moves.

The window verb stays accepted as a synonym that does nothing, because shortcuts already
created carry it and a user edits one of those by hand.

What this buys over DD21 is a remedy that does not depend on somebody reading an
after-install page. DD21 measured that the icon lands in the overflow and that nothing
here can promote it out of one; the window is the feedback channel that needs no
promoting, and the taskbar button it raises already carries the mark. Identity arrives
without the tray icon giving up shape as its way of stating engine state.

### §DD81 One tray, and what the second launch does instead

One mutex, held only by the tray surface. The console verbs stay concurrent: an agent
running the read verb while the tray is open must not be refused, so the guard sits
inside the tray branch of Main and never around the whole of it.

Local rather than global, so two logins on one machine each get a tray, and a handle
abandoned by a crashed process is claimed rather than read as a live holder.

The second instance does not report an error. A double click from Explorer has no
console to print to, and a message box on every accidental double click would be worse
than the silence being fixed here. It signals the live instance, which raises and
activates its window, then exits zero. That is the message, and it is what every Windows
application does. Where a console is attached the second instance also writes one line
to standard error, because there the caller typed a command and expects prose back.

The two states then look identical from outside: launching shows a window, and launching
again shows the same window. Somebody who clicks four times ends with one process and
one window, which is the failure that produced this line.

### §DD82 Why the add goes out empty

One construction in the wrong order. The visibility setter is what emits the shell
notify add, and at that moment the icon holder carries neither an image nor a tip, so
the add goes out without the icon flag and with an empty string. The state call on the
very next line repairs the image with a modify, which is why an icon appears at all, but
the tooltip Windows persisted at add time stays the empty one.

Measured rather than inferred: the notify icon settings entry for this executable holds
a tooltip of zero length beside an icon snapshot that decoded fine.

It matters because of where the icon actually lives. DD21 established that Windows files
a first seen icon into the overflow and that nothing here can promote it out, and the
overflow flyout labels each entry with exactly that persisted tooltip. So the one
surface a user has to read in order to find this tool is the one naming nothing at all.

The repair is to build the image and the text first and set visibility last, so the add
carries both. The state ring and its wording are unchanged; only the order of two
statements moves.

## Block D — Container operations (what a user came to do)

## Block E — Images, volumes and networks

## Block F — Installer and distribution (free, Apache 2.0)

## Block G — The agent surface (an agent operates this, and pays in tokens)

### §DD77 A migration with no way to see itself finish

DD72 made every read match either `freewilly.session` or `dockerdesk.session`, which is
right and is also permanent as it stands. Dropping the second key is a decision that
needs one fact — whether anything on this machine still carries it — and nothing on this
surface can answer that, so the honest thing to do at any future date is keep both.

The fact is already in hand at every read. `read changes --session` and `do reclaim
--session` both walk the labels of every container and volume, so which key matched is
known at the moment of matching and is then thrown away. A plan that said `made-earlier
(labelled before the rename)` beside its row would cost one word and would turn "keep
both forever" into a thing a user can watch reach zero.

What this is not is a relabelling. The Engine API sets labels at creation and offers no
way to change one without recreating the container, which is exactly what this surface
refuses to do to somebody's work — so the old generation leaves when the objects do, and
the only question worth answering is when that has happened.

The place it belongs is the reclaim plan and the changes listing rather than a verb of
its own: both already enumerate the objects, and a third verb that answered "any legacy
labels?" would be a call an agent has to know to make.

### §DD78 A gate is only as tight as its least deterministic input

DD65 recorded the shaped side at 4 calls, 16 requests and 774 tokens. The request count
is asserted exactly, because the fixtures fully determine it. The token figure is banded
at the file's own 15% — 658 to 890 — for two inputs that are not fixtures at all:

- `ContextPack` names the Docker client from `WindowsMachineFacts()`, so the engine line
  differs between a developer's machine and a CI runner with no Docker.
- `ContainerDoctor` asks `new HostPorts().Listening()` whether anything on Windows holds
  port 8080, so the fixture container's ports row is a pass on one machine and a failure
  on another.

Measured variance is perhaps 5%, so the band is three times wider than what it exists to
absorb, and a response that grew by 100 tokens would land inside it silently. That is
the defect the whole file exists to prevent, arrived at from the other direction.

Both are constructed inside the verb rather than passed to it, so there is no seam to
supply either. `AgentSurface.ReadVerify` already takes an `IServiceProbe` for exactly
this reason and is the shape to follow — the argument is optional, defaulted to the real
one, and only a measurement passes anything else.

The band on the baseline stays: that one exists for a different reason, which is a
fixture somebody shrinks, and it is honest about being one.

### §DD79 A single write point that is not the write point

`SessionLabel.For(session)` is documented as "the labels to stamp on anything created".
No caller in `src/` uses it. The one thing that creates anything, `ComposeUp`, appends
`SessionLabel.Key` into the generated YAML by hand, and the only exercise `For` gets is
an assertion written for DD72.

Nothing is wrong today, because both spell the same constant. What is wrong is the
invitation: a reader looking for where a label is written finds a helper that says it is
the answer and is not, and the next change that adds a second label — or that has to
spell a value differently for compose than for the API — lands in one of the two places.

DD72 is the evidence this matters. The key was respelled in `SessionLabel` and the
change reached the compose writer only because that writer names the constant; a helper
returning a dictionary would have carried it either way, and a second label added to
`For` would still not reach compose today.

The direction is for `ComposeUp` to render whatever `For` returns rather than one key it
names, which also removes the assumption that there is exactly one label. Whether `For`
survives at all is the other option worth weighing: a helper with one caller and one
shape is not obviously better than the caller spelling it, and deleting it says the same
thing honestly.

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

### §DD71 The public prose fell through the gap the rename set carved

The rename set carved the tree up by ownership, and the public prose fell through the
gap. DD60 covers the three files under `docs/` that the guard governs, DD59 covers the
site's base path and waits on the GitHub rename, and DD58 covered the invocations inside
`README.md` — which leaves the product's own name in the one file a visitor reads first.

`README.md` opens with `# DockerDesk`, says the installer puts things in
`%LOCALAPPDATA%\DockerDesk`, and lists the verbs as `DockerDesk.exe --preflight` and the
rest. `site/src/lib/site-content.ts` carries the same name about fifty times, and
`site/public/llms.txt` is the copy an agent reads.

One line is not merely stale but wrong: "the engine lives in a WSL2 distribution called
`dockerdesk`". After DD55 a fresh install creates `freewilly`, and `dockerdesk` is what
an adopted install kept. A reader who runs `wsl --list` after installing sees a name the
README says they should not have, and the uninstall paragraph names the same thing.

So this is two different jobs wearing one heading. The product name is a sweep with no
decisions in it. The distribution sentence needs rewriting rather than replacing,
because the honest version has two names in it and has to say which one a reader will
actually see — which is a paragraph somebody writes, not a string somebody swaps.
