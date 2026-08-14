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

### §DD106 The project a row belongs to

`ContainerSummary.Labels` is already on the list response and
`com.docker.compose.project` is in it — DD24 leans on that for name addressing. So the
hierarchy Docker Desktop draws costs no second call: group by that label, and a
container carrying none stays a top-level row.

Three things the flat list makes non-trivial.

**The key.** `LiveRows` reconciles by `row.Id`, so a group header needs an id of its own
— `compose:<project>` — and DD70's arrive-and-leave fade then works on projects too.

**The shape.** `ContainerRow.Shaped` sorts and filters one flat sequence. Grouped, the
sort runs inside a project and also orders the projects, and the filter has to keep a
header whose children matched while dropping one whose children all went. A header with
nothing under it is worse than no header.

**The state.** Whether a project is collapsed is presentation, and DD37 already says why
that cannot live in the ListView: the list is rebuilt on every engine event, so a
collapse would spring open while somebody was reading it. It belongs beside `_shape` on
the page.

The row is one template with a trigger, not two. A header fills the name column, the
running-of-total count and its chevron; the columns it has no answer for read as empty,
not as a container with no image. The children indent, which is the whole signal — and
the test asserting the header grid matches the row grid has to stay true through it.

## Block D — Container operations (what a user came to do)

### §DD107 One verb, the whole project

DD106 draws the parent row; this is what pressing something on it does. Docker Desktop's
answer is that the parent's verb is the project's verb — stop stops every service, start
starts them all — and that is what makes the hierarchy worth more than a heading.

Four things to settle.

**Which call.** `docker compose stop` wants the project's files, and this window holds a
container list, not a working directory. Every child id is already in hand, so the
honest implementation is DD8's four verbs fanned across the children: the same
`ContainerAction.InvokeAsync`, once per container, and no new engine surface.

**Order.** Compose stops in reverse dependency order and starts in forward order.
Fanning out in list order usually works and sometimes leaves a service talking to a
database that already went. The `depends_on` label rides on the container, so the
ordering is available to whoever decides it is worth the code.

**Where a partial failure lands.** Three of four stopped is not the project row's
failure, it is one child's. DD8's failure line is per row and should stay there; the
parent says how many did not, rather than repeating one child's sentence.

**Remove.** Removing a project is the destructive one and its dialog has to name the
count, not a container. Volumes stay: `compose down -v` is not what a Remove button may
quietly mean.

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
