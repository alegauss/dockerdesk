# Improvements

## Block A — The Windows engine (Docker without Docker Desktop)

## Block B — The daemon client (talk to the engine)

## Block C — The window (claude-tray's elements)

### §DD110 One projection, called twice

`RefreshAsync` calls `ContainerRow.From` on every container to read its project label,
so that `RowActivity.Prune` can be handed the header ids alongside the container ids —
and then calls it again, three lines below, on the same containers, to build the rows it
actually draws. Every row is projected twice per refresh, and half of those projections
are thrown away after one property is read off them.

It is not free and it is not expensive. `From` deduplicates ports through a `HashSet`
and allocates a row and a list; on forty containers that is forty of each, discarded, on
every engine event — and this window redraws on every engine event by design (DD70), so
the waste is proportional to how busy the machine is rather than to anything the user
did.

The reason it is worth a line is not the allocations. It is that two calls to the same
projection in one method is the shape a reader trips over: the second one looks like the
first was for something else.

The repair is ordering rather than new code — build the rows first, prune from those,
and the label is already on them. What has to hold is that pruning still happens before
the rows are dressed, since `Dress` reads the state `Prune` is about to drop. The
failure path is worth a look too: that branch leaves the rows empty, and whether a prune
should run when the engine just went away is its own question.

### §DD111 Two rows behind one type

DD106 chose one row type and one template with a trigger, and that choice was right: two
templates would have meant two column layouts, and the guard that pairs a header's
columns with its rows exists because those drift. The type behind the template followed
by accretion.

`ContainerRow` now carries `IsProject`, `Running`, `Total`, `Collapsed`, `Chevron`,
`ProjectCount` and `ProjectId` — meaningless on a container — beside `Image`, `State`,
`Ports`, `ExitCode`, `StateEvidence`, `CanShell` and `Service`, meaningless on a header.
Nothing says which is which. It is discovered by reading `ProjectHeader` and noticing
what it leaves at its default, and three members carry an `IsProject` or `IsContainer`
guard written one at a time as each was found to be wrong.

The cost is not the size. It is that the next member added has no rule to follow, so the
question "does a header answer this?" is asked again per property and answered from
memory. `AnyUp` exists because `IsLive` was answered from `State` and a header has none;
the next one like it is found the way that was, by looking at the window.

What a repair may not do is split the template. Beyond that the shape is open: a
discriminated pair projected into one binding surface, or the two groups named and a
test asserting a header answers its own and the defaults for the rest. The second is
cheap and is a rule the next member can be checked against, which is the part that is
actually missing.

## Block D — Container operations (what a user came to do)

### §DD108 The wait a header cannot end

DD8 established the rule the rest of the window follows: a 204 from `/stop` means the
daemon accepted the call, not that the container is down, so the `die` event is what
ends a row's wait. Every container row obeys it. The project header does not, and cannot
as written — it has no event of its own, and `SendProject` settles it when the last HTTP
call has been answered.

What that looks like. Press Stop on a four-service project: the four calls return in
milliseconds, the header stops saying `Stopping…`, and the four rows under it go on
saying it for the daemon's full ten-second grace period. The one row that exists to
report on the group is the row that reports first and is wrong.

Settling it on the children is the obvious repair and has a trap in it: a container that
will never emit an event would leave the header spinning forever, which is worse than
early. So the condition is that no child is still pending, evaluated where the rows are
dressed rather than inside the fan-out, which keeps only the refusal count it carries
now.

Worth knowing. `RowActivity` is keyed by id and knows nothing about projects; the page
is where a header's children are known. And the header is dressed after `Grouped`, which
is already the one place that has both the header and the rows under it.

## Block E — Images, volumes and networks

## Block F — Installer and distribution (free, Apache 2.0)

### §DD109 The cleanup attached to the script and not to the build

The WPF SDK writes a `<name>_<random>_wpftmp.csproj` beside the project while it
compiles XAML, and normally deletes it. An interrupted build leaves one behind. The next
build finds it, treats it as a project, and dies on a generated file it cannot locate:

    CSC : error CS2001: source file '...\obj\...\Ui\Pages\VolumesPage.g.cs' cannot be found

Nothing in that message names the stale file, the interrupted build or the fix. Hit
twice in one session, and both times the repair was `rm
src/FreeWilly.Tray/*_wpftmp.csproj` — which is knowledge that lives in one comment
inside `build/build.cmd` and nowhere a reader of the error would look.

`build.cmd` already deletes them, and that is the whole shape of the defect: the cleanup
is attached to the script rather than to the build. Anybody running `dotnet build`
directly — CI, an agent, a developer who did not use the script — gets the failure the
script exists to prevent, and gets it in a form that reads as a corrupt working tree.

Two ways in, and they are not equivalent. A `BeforeBuild` target in the `.csproj` puts
the cleanup where every entry point reaches it, which is the whole point; it also runs
inside the build the stale file already broke, so whether MSBuild has finished globbing
by then is the thing to establish first. Failing that, the message can at least be made
to name its own cure — but a build that explains a defect it could have removed is the
weaker answer.

## Block G — The agent surface (an agent operates this, and pays in tokens)

## Block H — The public surface (the site a reader and an agent both read)
