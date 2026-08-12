# Shipped Ledger

## Block A — The Windows engine (Docker without Docker Desktop)

- ✅ **DD1** **A Windows user cannot tell why Docker will not run here: WSL2 missing, virtualization off, or a rival engine** — `dockerdesk-preflight` reports the Windows build, virtualization, the WSL2 kernel and any rival engine one row each with the action that fixes it, and exits 1 while a blocking row is not green.
- ✅ **DD2 (download and verification)** **There is no unattended way to put a container engine on Windows without installing Docker Desktop** — `dockerdesk-engine --acquire` downloads the pinned rootfs, engine and Windows CLI and refuses any whose digest is not the one this build states, naming both digests.
- ✅ **DD17 (the host side)** **No clean Windows is reachable from here, so a red preflight row and a real install have never been executed** — `vm.ps1 doctor` reports whether vmrun, the encrypted VM, a snapshot and the guest agent answer, and runs nothing there until they do.

## Block B — The daemon client (talk to the engine)

## Block C — The window (claude-tray's elements)

## Block D — Container operations (what a user came to do)

## Block E — Images, volumes and networks

## Block F — Installer and distribution (free, Apache 2.0)
