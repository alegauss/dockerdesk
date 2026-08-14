# Improvements

## Block A — The Windows engine (Docker without Docker Desktop)

## Block B — The daemon client (talk to the engine)

## Block C — The window (claude-tray's elements)

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
