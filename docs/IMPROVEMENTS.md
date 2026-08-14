# Improvements

## Block A — The Windows engine (Docker without Docker Desktop)

## Block B — The daemon client (talk to the engine)

## Block C — The window (claude-tray's elements)

## Block D — Container operations (what a user came to do)

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
