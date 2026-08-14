# Improvements

## Block A — The Windows engine (Docker without Docker Desktop)

### §DD96 The translation has one door, and the failure has several

DD75 respells a Windows bind source inside the override `do compose up` generates, and
that is the only route it covers. Everything else reaches the daemon untranslated:
`docker.exe run -v D:\shop\data:/data` typed at a Windows prompt, the same command from
a WSL shell where `$(pwd)` is a Linux path the distribution does not have, a compose
project the user brings up themselves, an IDE plugin, Testcontainers.

Two failures, both measured against an upstream daemon (DD75). A drive-lettered source
is refused with `invalid mode: /data`, which is loud and names neither the path nor
Windows. A source that looks like a Linux path and is not there — `/home/you/project`
from a WSL shell — is accepted, created on the daemon side, and the container gets an
empty directory. The second costs an afternoon.

Translating for them is not on the table: this project does not wrap the CLI, and a shim
rewriting arguments would be the second Docker CLI the non-goals refuse.

What is on the table is naming it. `read doctor` already reads a container's `Mounts`
and renders a row per bind. A source the distribution cannot reach — no `/mnt/<drive>`
prefix and not a path inside the distribution — is a fact that row can state, with the
spelling that would have worked. That turns the silent case into a verdict and an
action.

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

### §DD70 §DD70 Motion that explains a change, which is the only motion Fluent asks for

Fluent's motion exists to answer one question: did that change, or did I misread it? The
engine dot now answers half of it — it breathes while `Starting` and stops the moment
the engine is running or stopped, which is the one state here that is a wait rather than
a settled answer.

**What is left is the list.** A refresh from the event stream replaces the rows under
the cursor with no transition, so a container that appeared and one that was always
there are drawn identically. The fade should say only where the eye should look, which
is the change.

That is larger than it reads, and the reason is in `Show()`: every page assigns
`ItemsSource` wholesale, so every row is new on every refresh. Fading on that signal
would flash the whole list on every poll — louder than no motion at all. It needs the
rows reconciled against what is shown, keyed by id, so only what joined fades in; and a
row that left has to outlive its removal to fade out. Three pages do this, so the
reconcile is shared.

The constraints are the ones DD69 and the dot have met. `Ui/Motion.cs` is the one gate —
`ClientAreaAnimation` off, no render tier, or a capture running — and a row mid-fade at
the capture's settle would break the byte-identical PNG the review harness rests on.
`Ui/Breathing.cs` is the worked example: it restores its end state rather than leaving
the value wherever the animation reached.

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

### §DD83 The shape borrowed, and the mark that is not

The reference screenshot is Docker Desktop's about dialog, and only its information
architecture is borrowed: a mark band across the top, the product version and build
stated once and large, a two column grid of component versions under it, and a footer of
links over a copyright line.

None of the artwork comes with it. The wordmark, the blue isometric drawing and the
whale are Docker trademarks and this is a competing product, so the band carries this
project's own mark over the water DD69 introduces. That is why this waits on DD69:
building the band before that task decides how the ocean reads would mean drawing it
twice.

A destination and not a dialog, per L2. A UserControl beside the three lists, built on
first navigation and kept collapsed, reached from the shell rather than from the tray
menu, which stays short by intent.

It draws with no daemon, per L6, so the engine rows read as unavailable rather than
blocking, and it takes a capture flag so the empty and the connected states are both
reviewable.

Every value is read and never typed: the build from BuildVersion, and the engine
version, the API version and the host architecture from VersionAsync, which already
returns them. Rows for components this install does not place yet are absent rather than
blank, so the grid grows as they land.

There is no Kubernetes row.

### §DD85 Amending L8, and what stays testable

This amends a law rather than fixing a defect, and L8 exists so that cannot happen
quietly: it states that shape carries state and colour only reinforces it, and
InkedPixels makes that testable. Under the requested scheme all three states share one
silhouette, so that test asserts nothing and the constitution changes in the same
commit.

Luminance replaces shape. Running is the mark in full colour, starting the mark in one
yellow tone, stopped the mark desaturated and dimmed. Those differ in brightness as well
as in hue, so the states stay separable in a black and white screenshot, which was the
bar L8 set, and the assertion becomes mean luminance ordering.

The artwork exists. icon.svg is already the mark at tray sizes, one wave tone and a
widened eye, and icon.mjs already rasterises it at every size Windows asks for. Three
variants come off that same script, committed like the ico for the reason that script
gives: a build needing Node to produce a Windows resource fails on a machine carrying
only the .NET SDK.

Rasterised rather than drawn, so each state ships every size instead of one Windows
stretches.

The tooltip already names the engine state and is asserted on all three. Only the first
one is empty, which DD82 owns.

## Block D — Container operations (what a user came to do)

## Block E — Images, volumes and networks

## Block F — Installer and distribution (free, Apache 2.0)

### §DD97 One registry value, two meanings

`HKCU\Software\Microsoft\Windows\CurrentVersion\Run\FreeWilly` is written by two things
that mean different things by it.

The installer's *Start FreeWilly with Windows* task writes `"<exe>" --tray` — the tray
icon at logon. `freewilly --autostart on` writes `"<exe>" --run` through
`Autostart.EntryName`, which is the engine serving the pipe until it is stopped. Same
key, same value name.

So they overwrite each other in whichever order they happen. Tick the box at install and
later run `--autostart on`, and the tray stops appearing at logon. Run `--autostart off`
afterwards and the value is deleted outright, which silently undoes the installer's box
— `Autostart.Disable` removes rather than blanks, on purpose, and it cannot know it is
removing somebody else's entry.

`PackagingTests` currently asserts the two spell the name identically, with a comment
saying that is what keeps the window and the uninstaller from touching two entries. That
reasoning holds for one feature and is exactly backwards for two.

Two Run values under two names is the obvious repair and probably the right one: they
are independent settings and a user may want either, both or neither. What has to be
decided with it is what `--autostart status` reports, since today it reads a value it
may not have written, and what the uninstaller removes — it should take its own and
leave one this product did not put there, which it cannot currently tell apart.

## Block G — The agent surface (an agent operates this, and pays in tokens)

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
