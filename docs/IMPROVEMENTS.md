# Improvements

## Block A — The Windows engine (Docker without Docker Desktop)

## Block B — The daemon client (talk to the engine)

## Block C — The window (claude-tray's elements)

## Block D — Container operations (what a user came to do)

## Block E — Images, volumes and networks

## Block F — Installer and distribution (free, Apache 2.0)

### §DD154 the tray tells you a release happened

claude-tray already does this and is the reference: `Updater.CheckAsync` reads the
`releases/latest` API, compares the tag to the running assembly version, and a hidden
menu item plus a balloon tip appear only when something newer exists. Applying downloads
the installer to `%TEMP%` and runs it `/SILENT`; the installer relaunches the app. One
check on launch, then a six-hour timer.

Four things differ here.

**The asset name carries the version.** claude-tray's asset is always
`ClaudeTray-Setup.exe`; DD152 ships `FreeWilly-Setup-<x.y.z>.exe`. The lookup matches a
pattern and refuses an ambiguous match rather than taking the first.

**It must verify what it downloads.** Every artefact provisioning fetches is checked
against a pinned digest, so a self-update running an unverified `.exe` would be the one
download this tool trusts blindly. `SHA256SUMS.txt` is published beside the installer.

**The engine is running.** An apply stops a tray that may be serving the pipe with
containers on it, so it asks, and never restarts the engine on the user's behalf.

**It is new outbound traffic.** Nothing here phones home is a non-goal, and the site
says the only traffic is the five pinned artefacts. A release check sends nothing about
the user, but it does reach `api.github.com` — which a proxy may block, and sixty
unauthenticated requests an hour is a shared NAT's whole budget. So: off unless turned
on, silent on failure, and both claims restated where they are made.

## Block G — The agent surface (an agent operates this, and pays in tokens)

## Block H — The public surface (the site a reader and an agent both read)
