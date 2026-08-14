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

### §DD98 The seam DD78 opened is held by nothing but memory

DD78 gave the read verbs a `MachineReads`, so the two inputs that forced the shaped
token figure into a 15% band arrive from the measurement instead of from Windows. The
figure is asserted exactly now, and it stays exact only while every future read verb
remembers to ask for its machine reads rather than construct them.

Nothing enforces that. A verb that writes `new HostPorts()` or `new
WindowsMachineFacts()` in its own body compiles, passes, and quietly makes the measured
figure this machine's again — and the failure is invisible, because the number still
looks precise. That is worse than the band, which at least said what it was.

The shape to copy is already here. `PaletteTests` fails the build on a hex colour
anywhere in the markup, and a guard holds `MainWindow.xaml.cs` under 300 lines; both
read source text, and both exist because the rule they hold is one a reviewer forgets.
The same guard over `AgentSurface.cs` — no direct construction of a machine read outside
the dispatcher — is the missing one.

One decision belongs to whoever takes this, because a guard written today goes red on
it. `CannotConnect` builds a `WindowsMachineFacts` to say which of the three causes of
"cannot connect" this machine has. It is off the measured path only because the
benchmark's daemon answers, and a measurement of the refusal path would want it seamed
too. Either it moves behind `MachineReads` or the guard names it as the one exception
and says why.

## Block H — The public surface (the site a reader and an agent both read)
