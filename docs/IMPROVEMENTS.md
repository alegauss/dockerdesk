# Improvements

## Block A — The Windows engine (Docker without Docker Desktop)

## Block B — The daemon client (talk to the engine)

## Block C — The window (claude-tray's elements)

## Block D — Container operations (what a user came to do)

## Block E — Images, volumes and networks

## Block F — Installer and distribution (free, Apache 2.0)

### §DD116 The door that is documented and the one that opens

The engine is vendored at `.roadkeep` and the configuration says to run that copy:
`env.ROADKEEP_HOME` points at it and `permissions.allow` grants `Bash(python
.roadkeep/scripts/roadkeep.py:*)`. Nothing else does. The skill roadkeep installs into
this repository names a different path — `python ".claude/hooks/roadkeep-launch.py"` —
and calls it "this project's entry point", and that launcher takes `guard` and `mcp` and
refuses every verb with a usage line.

So the documented door is shut and the open one is documented only as a permission
entry, which is not where anybody looks. Measured on this session: with the MCP server
not connected, the launcher refusing, and a sibling checkout mid-refactor that would not
import, the way in was found by globbing the plugin cache for a version directory — a
route no document mentions because it is not supposed to be one. Several tasks were then
written through a copy the project does not use, and only a later reading of
`settings.json` said so.

That the output was identical is luck. The vendored copy is 0.1.888 and the sibling was
0.1.904 and modified; two versions are allowed to differ, which is the whole reason
`engines` exists.

The repair is mostly not code. `roadkeep install` rewrites the launcher and the skill,
and the session-start hook already reports both as drifted every single time — so what
is missing is that nothing acts on a warning printed on every turn, and no file states
the one command that works.

## Block G — The agent surface (an agent operates this, and pays in tokens)

## Block H — The public surface (the site a reader and an agent both read)
