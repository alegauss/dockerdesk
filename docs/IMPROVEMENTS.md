# Improvements

## Block A — The Windows engine (Docker without Docker Desktop)

### §DD137 DD137

The engine host is launched detached and hidden, which is right — a console window the
user did not ask for is not an improvement. The cost is that everything it says goes
nowhere. When it stops, the line naming what it saw is written to a window that was
never readable and is gone by the time anybody asks.

That was the expensive part of the failure DD134 repairs. The daemon's own log survives
inside the distribution and was decisive; the host's account of why it walked away was
not recoverable at all, and the difference between "the host decided the engine was
dead" and "something killed the host" had to be argued from Hyper-V events and a
sixty-second gap rather than read.

So the host keeps a log of its own beside the install, next to the provisioning log that
is already there. What goes in it is small: what it did, what it saw when it stopped,
and each restart it attempted with the reason. Not a trace of every poll — a file that
grows without bound is its own defect, and a quiet engine should write nothing.

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

## Block G — The agent surface (an agent operates this, and pays in tokens)

## Block H — The public surface (the site a reader and an agent both read)
