# Improvements

## Block A — The Windows engine (Docker without Docker Desktop)

## Block B — The daemon client (talk to the engine)

## Block C — The window (claude-tray's elements)

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

## Block E — Images, volumes and networks

## Block F — Installer and distribution (free, Apache 2.0)

## Block G — The agent surface (an agent operates this, and pays in tokens)

## Block H — The public surface (the site a reader and an agent both read)
