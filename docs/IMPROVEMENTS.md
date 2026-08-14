# Improvements

## Block A — The Windows engine (Docker without Docker Desktop)

## Block B — The daemon client (talk to the engine)

## Block C — The window (claude-tray's elements)

## Block D — Container operations (what a user came to do)

### §DD114 Which side of the split a restart is on

`SendProject` picks its order with one condition: `Start` walks the dependency order
forward and everything else walks it back. That is right for two of the three and wrong
for `Restart`, which is not a stop — it is a stop and a start per container, atomically,
and the question is which state each one is left in.

Walked backwards, `api` restarts while `db` is still up, then `db` restarts under it.
The project settles with every dependent pointing at a database that restarted *after*
they did — a pool full of dead sockets, and a row saying `running` about a service that
is not working. Walked forwards, `db` goes first and `api` restarts against one already
back.

Neither order is clean at the instant it runs; the difference is entirely in where the
project lands. Compose restarts in dependency order for this reason, and it is the same
reason `ToStart` exists.

The change is one condition, and what makes it worth a line rather than a silent edit is
that the current shape reads as deliberate: `Start` against everything else looks like
the distinction is between bringing up and taking down, and it is not — it is between
ending up depended-on-first and ending up depended-on-last. `Restart` belongs on the
first side with `Start`.

Worth checking with it: `Remove` stays on the second side, and so does `Stop`. Removing
a database before the services holding connections to it is the same defect this fixes,
pointed the other way.

## Block E — Images, volumes and networks

## Block F — Installer and distribution (free, Apache 2.0)

### §DD115 The configuration nothing guards

`.claude/settings.json` is committed on purpose: it grants this project's tools, enables
the roadkeep plugin and names the marketplace, so a clone works rather than prompting on
every call. The installer ships the agent's half of the same story —
`build/agent/SKILL.md` and `settings-snippet.json` are `[Files]` entries — so what an
agent is handed is a shipped artefact of this repository.

It is also rewritten by whatever session happens to be open. Twice in one session
fifteen entries vanished from `permissions.allow` — `CronCreate`, `ReportFindings`,
`ExitWorktree` and a dozen more — with nothing said. The first was caught by reading
`git status` before a commit. The second was not, and rode into an unrelated commit
because `run-commit.cmd` stages everything relative to the working directory by design.

The loss is quiet on both ends. Nothing fails: a clone missing those entries simply
starts asking permission for tools this project already granted, which reads as the
harness being cautious rather than as a file having been trimmed. And the diff is
invisible in review precisely because it arrives inside a commit about something else.

A test is the cheap half and this repository already asserts over committed
configuration — `PackagingTests` reads `installer.iss` as text for exactly this reason.
Asserting the file parses and still grants a named floor would fail the build on a trim
instead of shipping it.

What it cannot do is stop the rewrite, which happens outside the repository. Whether
that is worth more than a guard is the part to settle.

## Block G — The agent surface (an agent operates this, and pays in tokens)

## Block H — The public surface (the site a reader and an agent both read)
