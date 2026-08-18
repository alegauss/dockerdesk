# Improvements

## Block A — The Windows engine (Docker without Docker Desktop)

## Block B — The daemon client (talk to the engine)

## Block C — The window (claude-tray's elements)

## Block D — Container operations (what a user came to do)

## Block E — Images, volumes and networks

## Block F — Installer and distribution (free, Apache 2.0)

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

### §DD148 The project the verb cannot be asked to read

The other face of DD143, left out of it on purpose because it is a different symptom.
That task was about a project the verb read too narrowly without saying so; this one is
about a project it cannot be asked to read at all.

`do compose up` takes no arguments — `unexpected argument x: do compose up takes nothing
else` — so its whole idea of a project is what the working directory holds. DD143 made
that idea match compose's own convention, which covers the ordinary case completely.
What it cannot cover is a project that names its files deliberately: a base plus a
staging override, or the arrangement any repository reaches once it has more than one
environment. For those the answer is still to run `docker compose -f a -f b` by hand,
which is the raw path this surface exists to replace — abandoned at exactly the moment
the stamping matters most, because `do reclaim --session` then has nothing to take back.

So the verb accepts `-f`, repeatable, and means by it what compose means. Given any,
they are the project and no convention is consulted; given none, DD143's discovery
stands unchanged. The generated stamp still goes last and still lives outside the
project.

The refusal that guards it stays as sharp as it is: an argument this surface does not
have must keep being named rather than dropped, because this verb creates containers and
a silently ignored flag is a wrong outcome nobody notices.

## Block H — The public surface (the site a reader and an agent both read)
