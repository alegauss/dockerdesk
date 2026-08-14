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

### §DD85 Amending L8, and what stays testable

This amends a law rather than fixing a defect, and L8 exists so that cannot happen
quietly: it states that shape carries state and colour only reinforces it, and
`InkedPixels` makes that testable. Under the requested scheme all three states share one
silhouette, so that test asserts nothing and the constitution changes in the same
commit.

Luminance replaces shape. Running is the mark in full colour, starting the mark in one
yellow tone, stopped the mark desaturated and dimmed. Those differ in brightness as well
as in hue, so the states stay separable in a black and white screenshot, which was the
bar L8 set, and the assertion becomes mean luminance ordering.

The artwork exists: `build/icon.svg` is already the mark at tray sizes. Rasterised
rather than drawn, so each state ships every size instead of one Windows stretches.
`icon.mjs` already reaches resvg and writes an `.ico` of seven sizes; three tinted
siblings are a tone function over the RGBA it has. The tint goes per raster, not per
SVG, or the entries below 48 stop coming from `icon.svg`.

Two things the next attempt should know. `StateIcon.Draw` returns a `Bitmap` the tray
wears and the tests measure, so reading from an `.ico` changes that signature and its
callers. And the shape test records two near-misses — a 360-degree arc one pixel
smaller, and an ink threshold that would have survived a pen change — so the luminance
ordering needs the same treatment: assert the gaps, not just the order.

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
