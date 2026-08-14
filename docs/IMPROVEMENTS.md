# Improvements

## Block A — The Windows engine (Docker without Docker Desktop)

## Block B — The daemon client (talk to the engine)

## Block C — The window (claude-tray's elements)

## Block D — Container operations (what a user came to do)

### §DD107 One verb, the whole project

DD106 draws the parent row; this is what pressing something on it does. Docker Desktop's
answer is that the parent's verb is the project's verb — stop stops every service, start
starts them all — and that is what makes the hierarchy worth more than a heading.

Four things to settle.

**Which call.** `docker compose stop` wants the project's files, and this window holds a
container list, not a working directory. Every child id is already in hand, so the
honest implementation is DD8's four verbs fanned across the children: the same
`ContainerAction.InvokeAsync`, once per container, and no new engine surface.

**Order.** Compose stops in reverse dependency order and starts in forward order.
Fanning out in list order usually works and sometimes leaves a service talking to a
database that already went. The `depends_on` label rides on the container, so the
ordering is available to whoever decides it is worth the code.

**Where a partial failure lands.** Three of four stopped is not the project row's
failure, it is one child's. DD8's failure line is per row and should stay there; the
parent says how many did not, rather than repeating one child's sentence.

**Remove.** Removing a project is the destructive one and its dialog has to name the
count, not a container. Volumes stay: `compose down -v` is not what a Remove button may
quietly mean.

## Block E — Images, volumes and networks

## Block F — Installer and distribution (free, Apache 2.0)

## Block G — The agent surface (an agent operates this, and pays in tokens)

## Block H — The public surface (the site a reader and an agent both read)
