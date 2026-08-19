# Improvements

## Block A — The Windows engine (Docker without Docker Desktop)

## Block B — The daemon client (talk to the engine)

## Block C — The window (claude-tray's elements)

## Block D — Container operations (what a user came to do)

## Block E — Images, volumes and networks

## Block F — Installer and distribution (free, Apache 2.0)

## Block G — The agent surface (an agent operates this, and pays in tokens)

## Block H — The public surface (the site a reader and an agent both read)

### §DD160 the tray section counts the menu the tray actually has

DD159 gated every count the copy states, and found one it could not gate: the tray
section counts the menu's items in a heading and then spends four bullets describing
them, so the number and the list have to move together. Both are now wrong.

`TrayMenu` ships six visible items — the window, the two engine verbs, the launch
setting, the release check, and Quit — plus one hidden until an update exists. The
heading says four. That half is DD159's own defect class one source away: the captions
are constants, countable exactly as `ProvisioningStep`'s members are.

The other half is not a count. Two bullets say Quit leaves the engine running and that
the asymmetry is the point — "a database another process is using does not die because
somebody closed an icon". DD128 reversed that: a running engine holds a WSL2 virtual
machine, and getting those gigabytes back meant remembering a second menu item first.
DD129 then took it down on a logoff too. So the page argues for a behaviour the product
deliberately stopped having, in the section about the complaint this project exists
over.

Which is why this is one task. Generating the count means naming the items, and naming
them means the bullets have to say what they do — a heading corrected over a list still
promising the old Quit reads as half-fixed, which is worse than uniformly stale.
