# Improvements

## Block A — The Windows engine (Docker without Docker Desktop)

## Block B — The daemon client (talk to the engine)

## Block C — The window (claude-tray's elements)

### §DD99 The manifest asks for sharp and the code hands over 16

`app.manifest` opts this process into `PerMonitorV2` and says exactly why: "a tray icon
drawn for 96 DPI and scaled up by Windows is the blurry square". Under that awareness
Windows does not scale for the app — it expects the app to supply the size the display
asks for. `StateIcon.Icon(state)` takes `size = 16` and every caller uses the default,
so above 100% the shell is handed 16 pixels where it wanted 24 and scales anyway.

It matters more since DD85 than it did before. Three abstract rings survive a bad
resample; a traced orca with an eye and a wave does not, and the badge that carries the
state is 0.44 of an edge that was already small.

The fix is not a bigger constant. The notification area's size comes from the monitor
the icon is on, so it is read rather than assumed, and it changes while the process runs
— a laptop docked to a 4K display re-scales without a restart, and `NotifyIcon` has to
be handed a new image when it does.

Worth knowing before starting. `Icon.FromHandle(bitmap.GetHicon())` builds a
single-image icon whatever size it is handed, so supplying several sizes at once means
constructing an `.ico` in memory rather than passing a bigger bitmap — `build/icon.mjs`
already writes that container and its layout is the reference. And the mark's frames
below 48 come from `build/icon.svg`, so the 24 and 32 the shell asks for are already the
drawing made for a tray.

### §DD100 A coverage test with nothing to enumerate

`Every_verb_that_routes_somewhere_is_in_the_help_text` reads as a coverage test and is
not one. It loops over `EngineVerbs` — a set the router already owns — and then names
five more verbs one at a time, by hand. A verb added to `CommandLine.Of` and not to that
list is documented nowhere and asserted by nothing, which is the exact failure the
test's own comment says it exists to prevent.

This is not hypothetical. `--capture-window` and `--tray` were both missing from it,
silently, until DD67 added a sixth verb and the list was read closely enough to notice.
Two of the executable's console faces were undocumented as far as this test was
concerned, and it had been green throughout.

The obstacle is that the routes are not enumerable the way `EngineVerbs` is.
`Surface.Tray` has no verb, `Surface.Agent` is reached by the bare words `read` and `do`
rather than a flag, and the rest are constants scattered down the class. So the fix is a
declaration: one table the router reads and the help renders, with those two spelled as
what they are rather than left out. Then the test loops over the table and the list
cannot drift.

Worth deciding: whether the help text is generated from the table or still written by
hand and merely checked against it. The text carries grouping, blank lines and prose the
table has no place for, so checking is likely to beat generating — but that is a
judgement about the help's shape rather than about the router's.

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
