# Improvements

## Block A — The Windows engine (Docker without Docker Desktop)

## Block B — The daemon client (talk to the engine)

## Block C — The window (claude-tray's elements)

## Block D — Container operations (what a user came to do)

## Block E — Images, volumes and networks

## Block F — Installer and distribution (free, Apache 2.0)

### §DD123 The task list is unreadable at 200% scaling

Observed and not yet diagnosed, which is why this says what was seen rather than what to
change. Windows 11, 3840x2160 at 200% scaling, 2026-08-14: on Select Additional Tasks
every box is drawn narrower than the glyph it holds. An unticked one is a vertical
sliver and a ticked one a fragment of a check. Three of the four tasks there are on by
default, so what a reader cannot make out is which of them they are agreeing to.

It is Inno's own control and not a page this script builds, so nothing here sets that
width. That makes it worth confirming before touching anything: the same page at 100%
and at 150%, and the same page under a current Inno Setup. Neither the workflow nor
`build-installer.cmd` pins a version — both find whatever ISCC.exe the machine has — so
a fix released upstream would arrive without anybody noticing, and a regression the same
way.

What the answer must not be is fewer choices on that page. The engine download is a
quarter of a gigabyte over somebody's connection and the Run value decides what happens
at every logon; both are the user's, and hiding one to dodge a drawing bug would trade a
legible wizard for a decision made on their behalf. If the control cannot be fixed, the
tasks belong on a page this script draws, where DD121 already proved a custom form
works.

## Block G — The agent surface (an agent operates this, and pays in tokens)

## Block H — The public surface (the site a reader and an agent both read)
