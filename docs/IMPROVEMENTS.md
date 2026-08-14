# Improvements

## Block A — The Windows engine (Docker without Docker Desktop)

## Block B — The daemon client (talk to the engine)

## Block C — The window (claude-tray's elements)

## Block D — Container operations (what a user came to do)

## Block E — Images, volumes and networks

## Block F — Installer and distribution (free, Apache 2.0)

### §DD121 Uninstall: stop what is running before deleting it

An uninstall that cannot delete the program it is uninstalling is not an uninstall. The
tray is a windowed process holding `{app}\FreeWilly.exe` open, so the delete fails and
the root stays — after the Run value is gone and the Add/Remove entry has already
disappeared. What is left is a folder nobody owns and no uninstaller will offer to take
again.

The professional shape is one page and four steps, in this order.

Ask first. The uninstaller gets a screen of its own, listing what is running and what
each choice deletes: stop FreeWilly and the engine, remove the distribution, keep
settings. The wizard page API is Setup-only, so this is a custom form. A silent
uninstall skips it and takes the safe default — stop, keep data.

Stop gracefully before forcing anything. The tray needs an exit verb it answers to; the
engine already has `--stop`, which stops the daemon and terminates the distribution.
Terminating is what makes the unregister clean: an open virtual disk is a directory that
survives its own deletion.

Restart Manager is the backstop, not the plan. `CloseApplications` catches the process a
graceful exit missed, and a forced kill is the last resort, announced rather than
silent.

Delete last, and verify. Unregister, remove the trees, and report what could not be
removed instead of exiting 0 over it.

Images and volumes stay where they are: still opt-in, still defaulting to keep, still
the one question here with no undo.

## Block G — The agent surface (an agent operates this, and pays in tokens)

## Block H — The public surface (the site a reader and an agent both read)
