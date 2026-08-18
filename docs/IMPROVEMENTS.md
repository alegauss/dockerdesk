# Improvements

## Block A — The Windows engine (Docker without Docker Desktop)

## Block B — The daemon client (talk to the engine)

## Block C — The window (claude-tray's elements)

## Block D — Container operations (what a user came to do)

## Block E — Images, volumes and networks

## Block F — Installer and distribution (free, Apache 2.0)

### §DD145 Measuring a wizard page instead of reading it

Four tasks have built a wizard page in Pascal — DD121's uninstall form, DD123's tasks
page, DD131's blocked-install page and DD132's button on it — and every one was checked
by reading the script. The failures that misses are the ones it has already produced: a
caption assigned before its width wrapped at column zero, a page that rendered correctly
above a screenful of blank space, and a Copy button nine pixels below the box it belongs
to, because an edit sizes itself to its font and a button does not. Each was found by
running an installer, and the last only because a throwaway harness happened to exist
that afternoon.

That harness is the proposal. A page's geometry is readable from inside Setup: a script
that builds the page, reports every control's rectangle and visibility, then closes
itself needs no clicks, no screenshot and nobody watching. Built and deleted three times
in one session, it answered which controls overlapped, which were hidden in which state,
and what the buttons said — the whole of what reading Pascal cannot.

So it is committed rather than rebuilt: one script rendering a named page under a named
state, and a test that fails on an overlap, on a control off the bottom of the surface,
or on a caption that does not match the state asked for. The [Code] section is the input
either way, so the harness compiles the text the installer ships and cannot drift from
it.

### §DD146 The report a successful install stopped keeping

A loose end DD130 left behind rather than a defect it introduced. The preflight used to
run at ssPostInstall and wrote `{app}\preflight.txt` whatever it found, so every install
left a record of what the machine looked like when it was cleared. Moving the read in
front of the copy moved the write with it, and the write now happens only when something
blocks — because that is when somebody needs the file, and on a blocked fresh install
there is no `{app}` to put it in.

What that costs is the successful case. An install that went through leaves nothing
saying what was read, so the first question after a machine starts misbehaving — was
this row green when it was installed, or has something changed since — has no answer on
disk. The uninstall still deletes `{app}\preflight.txt`, and a delete of a file nothing
writes is the kind of line that outlives the reason for it.

The read has already happened by then and its verdict is remembered, so the write is
where the directory finally exists: `{app}` is created during the copy, and
ssPostInstall is the step that knows both. What is written is the same string the page
would have shown, which keeps one rendering rather than two, and the blocked path keeps
writing to TEMP exactly as it does now.

## Block G — The agent surface (an agent operates this, and pays in tokens)

### §DD147 The number stops being typed

DD144 is the evidence, and it is worth stating plainly: the gate was red from the moment
it was last raised. DD101 changed the shaped mounts row, the task got two tokens
cheaper, the file was told one, and every run of the suite failed for the whole life of
that commit and eleven after it. Nothing about the surface had drifted. A figure had
been typed.

DD78 cannot catch this and never could. Making the assertion exact binds the recorded
number tightly to the measurement, which is the whole value of it — but only once
somebody has written down a number the measurement produced. An exact gate over a typo
is red forever, and a red gate is one nobody reads, which is where the old 15% band left
things by the opposite route.

So the number stops being typed. The measurement already runs in the suite and already
knows every figure the file records, so it can write them: a mode that prints the block
the file should hold, or updates it in place, leaves an author with a diff to read
rather than four integers to transcribe. Raising a ceiling stays as deliberate as it is
now — the diff is reviewed, and the commit still says what the tokens bought.

What must not follow is a run that silently rewrites the file whenever it disagrees.
That is not a gate at all. The write is asked for, never automatic.

## Block H — The public surface (the site a reader and an agent both read)
