# Improvements

## Block A — The Windows engine (Docker without Docker Desktop)

## Block B — The daemon client (talk to the engine)

## Block C — The window (claude-tray's elements)

### §DD112 The set that is never pruned

`RowActivity.Prune` exists because the list is rebuilt from the daemon on every event
and state keyed by id would otherwise outlive the containers it is about. The page's
other keyed set has no such thing: `_collapsed` holds a project name from the moment
somebody folds it away and never lets go.

Two consequences, and the second is the one that is wrong. It grows — one string per
project ever folded, for the life of the window. And it *remembers*: a project taken
down, and later brought up under the same name, comes back folded, carrying a chevron
pointing the way somebody left it a day ago.

Whether that second one is a defect or a feature is the thing to decide rather than
assume. An argument exists for keeping it — a person who folds `shop` away is saying
they are not working on `shop` — and it is not obviously weaker than the argument for
dropping it. What is not defensible is that it happens by omission: nothing chose it,
and no line says which was meant.

The mechanism either way is the one already here. `Prune` is handed the ids that are
still present on every refresh and the page now has the projected rows in hand when it
calls it (DD110), so the projects that still exist are known at exactly the point the
collapsed set would be narrowed against them.

### §DD113 A fixture that cannot show the ordering

`SampleMachine` is what `--capture-window --fixture` draws, and DD38's whole point is
that a window can be photographed without a daemon: the rows, the ports, the sizes and
the states are the same on every machine and in CI. It carries a `sample` compose
project of three services, which is why DD106's header could be verified by looking at
it.

It carries no `com.docker.compose.depends_on` on any of them. So `ComposeOrder` — the
part of DD107 that decides whether an api is stopped before the postgres it talks to —
sees a project with no edges every single time the fixture is used, falls back to list
order, and looks exactly as it would if it did not exist.

That is a gap in the fixture rather than in the code: the ordering has unit tests and
they cover the cycle, the scaled service and the missing label. What no capture and no
fixture run can show is the ordering working on the machine the window is drawn from,
which is the one thing the fixture exists to make showable.

Three labels is the whole change — `api` on `db`, `worker` on `db` — and then the
fallback stops being what the fixture exercises. What has to be settled with it is that
`--capture-window` must stay byte-deterministic: the fixture's list order and its
dependency order would then differ, so anything that renders in list order stays put and
anything that does not is a real difference worth seeing.

## Block D — Container operations (what a user came to do)

## Block E — Images, volumes and networks

## Block F — Installer and distribution (free, Apache 2.0)

## Block G — The agent surface (an agent operates this, and pays in tokens)

## Block H — The public surface (the site a reader and an agent both read)
