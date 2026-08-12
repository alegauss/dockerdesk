# Shipped Ledger

## Block A — The Windows engine (Docker without Docker Desktop)

- ✅ **DD1** **A Windows user cannot tell why Docker will not run here: WSL2 missing, virtualization off, or a rival engine** — `dockerdesk-preflight` reports the Windows build, virtualization, the WSL2 kernel and any rival engine one row each with the action that fixes it, and exits 1 while a blocking row is not green.
- ✅ **DD2** **There is no unattended way to put a container engine on Windows without installing Docker Desktop** — `dockerdesk-engine --provision` puts upstream Moby 29.7.2 in an owned WSL2 distro and docker.exe where an installer can add it to PATH.
- ✅ **DD17** **No clean Windows is reachable from here, so a red preflight row and a real install have never been executed** — `vm.ps1` runs the product preflight inside a Windows 11 guest through vmrun and reads back what it said.
- ✅ **DD3** **Nothing starts or stops the engine, and a UI that reports running before the socket answers is lying** — `dockerdesk-engine --run` starts the distro and daemon, serves \.\pipe\docker_engine, and reports Running only once the engine answers.

## Block B — The daemon client (talk to the engine)

## Block C — The window (claude-tray's elements)

## Block D — Container operations (what a user came to do)

## Block E — Images, volumes and networks

## Block F — Installer and distribution (free, Apache 2.0)
