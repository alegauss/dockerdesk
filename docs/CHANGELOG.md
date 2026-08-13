# Shipped Ledger

## Block A — The Windows engine (Docker without Docker Desktop)

- ✅ **DD1** **A Windows user cannot tell why Docker will not run here: WSL2 missing, virtualization off, or a rival engine** — `dockerdesk-preflight` reports the Windows build, virtualization, the WSL2 kernel and any rival engine one row each with the action that fixes it, and exits 1 while a blocking row is not green.
- ✅ **DD2** **There is no unattended way to put a container engine on Windows without installing Docker Desktop** — `dockerdesk-engine --provision` puts upstream Moby 29.7.2 in an owned WSL2 distro and docker.exe where an installer can add it to PATH.
- ✅ **DD17** **No clean Windows is reachable from here, so a red preflight row and a real install have never been executed** — `vm.ps1` runs the product preflight inside a Windows 11 guest through vmrun and reads back what it said.
- ✅ **DD3** **Nothing starts or stops the engine, and a UI that reports running before the socket answers is lying** — `dockerdesk-engine --run` starts the distro and daemon, serves \.\pipe\docker_engine, and reports Running only once the engine answers.

## Block B — The daemon client (talk to the engine)

- ✅ **DD4** **Nothing in this project can ask the engine anything: no client for the Docker API over the Windows named pipe** — DockerApi speaks the Engine API over the named pipe with no NuGet dependency: ping, version, containers, and a stream for endpoints that never end.
- ✅ **DD5** **A container started in a terminal never appears in the window, and the list is only as fresh as the last refresh** — EngineEvents reads /events as the daemon writes it and re-opens the stream after every break, so nothing here polls.

## Block C — The window (claude-tray's elements)

- ✅ **DD6** **Answering is Docker up? costs opening a window, and starting the engine costs a command line** — A tray icon carries the engine state as a shape, and its menu starts the engine in a process that outlives the tray or stops it.
- ✅ **DD7** **There is no window: a user cannot see which containers exist, their state, or the ports they publish** — A WPF window lists containers with their ports as links, refreshed by the event stream, and says something designed when it is empty.

## Block D — Container operations (what a user came to do)

- ✅ **DD8** **The list is read-only: a container cannot be started, stopped, restarted or removed from it** — Start, stop, restart and remove on every row: the click lands in a pending state, the event stream is what ends it, and a refusal shows the daemon's own sentence where the button is.
- ✅ **DD9** **A container that exits immediately shows a state and nothing about the cause, so the user leaves for a terminal** — A window per container: frame headers stripped, stderr told from stdout, follow on by default, copy-all to the clipboard, and a buffer capped at 5,000 lines that drops from the front.
- ✅ **DD10** **There is no way into a running container: no shell, so anything the log does not say is unreachable** — The terminal the user already has, running docker exec: the image is asked which shell it has first, so one with neither says so on the row instead of opening a window that closes.

## Block E — Images, volumes and networks

- ✅ **DD11** **Tens of gigabytes of layers accumulate and nothing says which images are dangling or still in use** — Images sorted by size, each row saying whether a container holds it or it is dangling, with per-image removal and a dangling-only prune that names the space before the click and after it.
- ✅ **DD12** **Volumes are invisible: a user cannot see which exist, what they cost on disk, or which containers mount them** — Volumes with their sizes and what mounts them, the compose project read off the name, and a deletion that names all of it first because a volume is the one thing here that does not come back.

## Block F — Installer and distribution (free, Apache 2.0)

## Block G — The agent surface (an agent operates this, and pays in tokens)
