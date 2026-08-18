# Improvements

## Block A — The Windows engine (Docker without Docker Desktop)

## Block B — The daemon client (talk to the engine)

## Block C — The window (claude-tray's elements)

## Block D — Container operations (what a user came to do)

## Block E — Images, volumes and networks

## Block F — Installer and distribution (free, Apache 2.0)

## Block G — The agent surface (an agent operates this, and pays in tokens)

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
