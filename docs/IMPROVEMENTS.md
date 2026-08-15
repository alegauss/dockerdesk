# Improvements

## Block A — The Windows engine (Docker without Docker Desktop)

## Block B — The daemon client (talk to the engine)

## Block C — The window (claude-tray's elements)

### §DD126 The build details link addresses a dashboard that is not there

Buildx ends every build with `View build details: docker-desktop://dashboard/build/…`,
and no configuration silences it: `DOCKER_CLI_HINTS`, `BUILDX_EXPERIMENTAL` and
`BUILDX_NO_DEFAULT_ATTESTATIONS` were each measured and each still printed. So the line
arrives on a machine where that scheme is registered to nothing, on the product whose
whole argument is Docker without Docker Desktop.

The two halves of the URL differ, which is what makes this worth doing. The ref is real
— it names a record the daemon kept, and `buildx history inspect <ref> --format json`
returns it structured. Only the address is dead, so the work is to make an address
resolve, not to invent data or suppress a line.

Claiming another vendor's scheme is the cost. `ToWindowsPath` already refuses to map
Docker Desktop's paths, on the grounds that it would be this tool claiming another
engine's layout, and this is that move one level up. Against it: the key is HKCU and
leaves on uninstall, the preflight refuses to install beside a rival, and a Desktop
installed later overwrites the handler — which is the right way for that to resolve.

Scope is unsettled. The one record the URL names is all that was asked; a builds list is
more useful and more work, and it is not obvious the second earns its cost yet. Either
is bound by L2, so a destination and not a window, and by L6, so a fixture draws it.

## Block D — Container operations (what a user came to do)

## Block E — Images, volumes and networks

## Block F — Installer and distribution (free, Apache 2.0)

## Block G — The agent surface (an agent operates this, and pays in tokens)

## Block H — The public surface (the site a reader and an agent both read)
