# Improvements

## Block A — The Windows engine (Docker without Docker Desktop)

## Block B — The daemon client (talk to the engine)

## Block C — The window (claude-tray's elements)

### §DD165 The engine, on the same footing as a container

A container that exits two seconds in leaves an artefact the window will show: click it,
read the log, follow it live, copy the lot. The engine those containers run on leaves
the same artefact and the window shows none of it. Its journal is engine.log beside the
install — a path a user only learns by being told, and a file they open in Notepad while
the thing it describes carries on without them.

That asymmetry is the whole of this, and nothing new has to be invented to close it.
LogWindow already follows a stream, throttles its redraws, holds an empty state and
copies everything, and the nav strip already carries five destinations. An Engine page
is those two put together over a file rather than over a pipe — tailed, because a file a
detached process appends to is what there is.

A page and not a second window, and the difference earns its keep. The log is not the
only thing a reader wants at that moment: whether the engine is up, how many times it
has been brought back, how many attempts remain before the host falls back to its long
interval, and where the file is if they mean to attach it to a bug. A window titled
after a log can only hold the log. A page holds the answer, with the log underneath it.

## Block D — Container operations (what a user came to do)

## Block E — Images, volumes and networks

## Block F — Installer and distribution (free, Apache 2.0)

## Block G — The agent surface (an agent operates this, and pays in tokens)

## Block H — The public surface (the site a reader and an agent both read)
