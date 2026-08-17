# Improvements

## Block A — The Windows engine (Docker without Docker Desktop)

### §DD141 The error that knows the answer

An agent driving this install hits a stopped engine as a raw connection failure: "failed
to connect to the docker API at npipe:////./pipe/docker_engine … check if the daemon is
running". That message is docker's own, written for a world where the daemon could be
anyone's. Here it is not: FreeWilly ships the docker.exe on PATH and knows the engine is
its own, so the one thing the reader needs — freewilly do engine start — is known where
the error is printed and left out of it.

Observed three times in one working session, driving compose builds for an unrelated
project. Each time it read as a broken Docker install rather than a stopped service, and
recovery meant going to read the CLI help. The `read ps` verb already answers this well,
reporting "engine stopped, nothing is answering the pipe" — the gap is that nothing
points at it from where the failure surfaces, which is the docker command already run.

The smallest form is the shim recognising the connection error for its own pipe and
appending one line naming the verb. It need not start anything: an agent told what to
run will run it, and starting a daemon as a side effect of an unrelated command is a
bigger decision than this warrants.

Related to DD137, which keeps evidence of why the host stopped. This is the other half:
what the reader of the failure does next.

### §DD142 The flap the host survives and the client does not

The failure arrives in bursts. Inside one, every client fails identically:
`docker ps`, `docker version`, `docker network ls`, `docker compose ls` and
`docker compose up` all return "failed to connect to the docker API at
npipe:////./pipe/docker_engine … cannot find the file". Outside one, all return
0. Nothing is done in between — no restart, no wait longer than the next
command.

Measured: six consecutive runs of the project's compose script failed, then the same
invocation from the same shell moments later went green. In one burst `up` failed twice
with no `down` before it, so teardown is not the trigger.

This corrects two readings recorded here earlier. It is not one command losing the pipe
while its neighbours keep it — a burst takes everything. And it is not a spawned client
being refused where a shell one is served: inside a burst both fail, outside it both
work. A retry is useless within a burst and unnecessary outside one, so the remedy first
proposed here is wrong.

`read ps` reports healthy throughout, which is the part worth chasing: the engine
believes it serves a pipe no client can open. DD137, which keeps a log of what the host
saw and every restart it attempted, is what would say whether the host cycles the pipe
underneath its own status.

For whoever measures this: existsSync on the pipe path from Node reports absent even
when docker works. PowerShell Test-Path is the one that tells the truth.

### §DD143 The compose project the verb does not see

Reproduced in an empty directory with two files. docker-compose.yml declares a service
`base`; docker-compose.override.yml declares `extra`. Docker Compose applies the second
by convention: `compose config --services` lists both, and `compose up -d` starts
fwtest-base-1 and fwtest-extra-1.

`freewilly do compose up` in the same directory prints "compose up docker-compose.yml 1
service(s)" and starts only base. Nothing says a file was skipped. The line it prints
names what it read, which is honest, but an agent reading that line has no way to know
the project said more.

This is the documented path. The site tells an agent to reach for `do compose up
--wait`, and the design note says the surface is a shape over the Engine API rather than
a second Docker CLI. Both are good reasons not to reimplement Compose's flags — and both
argue for delegating project loading to Compose instead of resolving one filename.

The same gap has a second face: `do compose up` takes no arguments at all, so a project
split across a base and an explicit override cannot use the verb even knowingly. It has
to fall back to `docker compose -f a -f b`, which is the raw path the verb exists to
replace.

Smallest honest fix is to refuse rather than diverge: when the directory holds files the
verb will not read, say so and stop. Better is to load the project the way Compose does,
and print every file that went into it.

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
