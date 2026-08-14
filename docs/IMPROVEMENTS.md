# Improvements

## Block A — The Windows engine (Docker without Docker Desktop)

### §DD101 The case a string cannot settle

DD96 gave the mounts row two certain answers: a drive-lettered source, and another
engine's host mapping. Both are recognisable from the string alone, so neither can be a
false diagnosis. It did not answer the third case, which DD96's design named as the one
that costs an afternoon — `/home/you/project`, typed in a WSL shell where `$(pwd)` is a
path this distribution does not have. The daemon does not refuse it: it creates the
directory, and behind the mount is nothing.

From the string it is unanswerable: that spelling is equally a legitimate path inside
the distribution, and `Wsl.WindowsFolderSpelledElsewhere` returns null on it for that
reason.

The distribution can be asked: `IWsl` already runs a command in it, and whether the
source holds anything is a fact a read verb could gather.

Two things to settle before writing it. **The false positive**: a bind source that is
genuinely empty is not a defect, and a row that calls it one is the thing DD26 puts
above every other consideration. So the finding is probably "empty, and the daemon
creates a missing source rather than refusing" — a warning naming both possibilities —
rather than a verdict.

**And the seam**: `read doctor` is measured to the token by `agent-budget.json`, so a
subprocess inside it has to arrive through `MachineReads` like the other machine reads,
or the benchmark starts running `wsl.exe`. DD98 is about that seam being held by nothing
but memory; this is the first change that would test it.

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

### §DD103 The suite cannot be run where the product is

`SingleTrayTests` claims the real named objects, deliberately: the names are the
product's, and parameterising them would test a different thing from the one that ships.
That reasoning is right and it has a consequence nobody wrote down — the suite cannot be
run on a machine where the product is running, which is every machine that uses it.

Three tests fail, and none of them says why.
`The_first_claim_wins_and_the_second_is_told_to_step _aside` reports that a claim
succeeded when it should not have; the actual cause is a tray in the notification area.
Hit while shipping DD97: a tray left over from a smoke test failed three tests that had
nothing to do with the change, and the message pointed at none of it.

What it should do is a decision rather than an obvious fix, and both options are
defensible. **Say so and skip**: detect at the start that something else already holds
`FreeWilly.tray`, and skip with "a tray is running on this session — quit it and
re-run". Honest, and a skipped test is one nobody reads. **Say so and fail**: keep the
failure but name the holder, so the message is the remedy. Neither hides that the
assertion was not made.

Worth knowing before starting. The mutex is unprefixed and therefore session-local, so
this is not about other users. `SingleTray.TryClaim` already answers false in exactly
this case, which is the detection — what is missing is a test-side reading of it before
the assertions run, and xUnit's `Assert.Skip` is the door.

## Block D — Container operations (what a user came to do)

## Block E — Images, volumes and networks

## Block F — Installer and distribution (free, Apache 2.0)

### §DD102 Nothing on the ordinary path reads installer.iss

`release.yml` runs `ISCC.exe` over `build/installer.iss`, and it triggers on `push:
tags: ["v*"]` alone. `check.yml` runs on every push and pull request and never compiles
it. So the first thing that reads the installer script is the release, and a syntax
error in it stops the release rather than the commit that caused it — with the tag
already pushed.

This is DD88's defect in a second file. The site build was broken for 21 commits because
its workflow was `workflow_dispatch` only, and it was invisible for exactly this reason:
nothing on the ordinary path read it.

`PackagingTests` is not the answer and should not be mistaken for one. It asserts over
the script as *text* — that a line says `ValueType: none`, that an AppId is spelled a
certain way — so it proves the file says what the author meant and cannot say whether
Inno accepts it. DD97 shipped `ValueType: none` with `uninsdeletevalue` on that evidence
alone.

The obvious repair is compiling the script in `check.yml`. What has to be decided with
it is the cost: the runner has no Inno by default and `choco install innosetup` on every
push is minutes. `release.yml` already carries the three-path probe and the install, so
the step exists to be copied; caching it, or running it only when `build/` changed, are
judgements about how often that file moves.

Worth knowing: the compile needs the published `.exe` to exist, because `[Files]` names
it. A check that builds anyway has already paid for that.

## Block G — The agent surface (an agent operates this, and pays in tokens)

## Block H — The public surface (the site a reader and an agent both read)
