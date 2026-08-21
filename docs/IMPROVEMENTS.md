# Improvements

## Block A — The Windows engine (Docker without Docker Desktop)

### §DD163 The events either side of the engine

DD137 was deliberate about writing nothing while the engine is quiet, and that rule is
right: a line every two seconds is a file that says nothing at great length. What the
run of 21 August shows is that the rule was applied one step too widely. The journal
holds the engine's own states and nothing about the machine underneath, so the reader
has to guess at everything the engine was reacting to.

Three gaps, each observed. The host learns from Windows that the machine resumed and
acts on it — Serve subscribes to PowerModeChanged — and writes nothing down, so a
suspend that cost the virtual machine reads exactly like a daemon that died at a desk
nobody left. The host never announces that it started or that it is ending, so a file
whose last line is a status cannot be told from a file whose writer was killed
mid-sentence. And the tray, the one process that knows the event stream went quiet at
14:35 and that a human clicked Start engine at 15:45, writes to the journal not at all.

None of that is a poll. Every line named here is something that happened, which is the
test DD137 set and the reason the file is worth opening. What it buys is that a gap in
the file has a cause beside it: the reader stops arguing from Event Viewer and a
sixty-second hole, and reads instead.

### §DD164 Giving up is not the same as stopping

DD136 bounded the retries, and its reason was sound: an engine that cannot come up is a
fact the user needs, and a loop that retries forever turns that fact into a machine
quietly doing nothing. The bound it chose was five attempts, about a minute of waiting.
On 21 August that minute was seven, because an attempt spends up to sixty seconds
waiting for a pipe. At 14:42 the host said it had given up, stopped serving, and exited.
The machine then sat offline for an hour, and what ended that was a human clicking Start
engine.

So the fact reached nobody. The sentence DD136 wanted the user to have was written to a
file nobody had been told to open, by a process that then stopped existing — and from
where the user sits the outcome is the silence the bound was meant to prevent.

What was wrong is that giving up and exiting were one act. They need not be. The five
quick attempts stay as they are and running out of them still says so; what changes is
that the host then falls back to a long interval instead of ending, and keeps trying
until the engine is back or somebody asks it to stop. The failure is still announced —
once, out loud, where the user is looking — and the machine now recovers by itself when
whatever was wrong is fixed.

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
