# Improvements

## Block A — The Windows engine (Docker without Docker Desktop)

## Block B — The daemon client (talk to the engine)

## Block C — The window (claude-tray's elements)

## Block D — Container operations (what a user came to do)

## Block E — Images, volumes and networks

## Block F — Installer and distribution (free, Apache 2.0)

### §DD117 The commit that owns more than the task

`run-commit.cmd` stages everything relative to the working directory, by design and for
a good reason: a task's code, its tests and the three governed files are one commit, and
enumerating them by hand is how half of one gets left behind.

The cost is that a commit owns whatever else is lying around. Three times in one
session: `.claude/settings.json` was trimmed by the harness and rode into a commit about
a container row; it was trimmed again and caught only by reading `git status`; and a
`.pyc` a new test had just written landed in the commit adding that test. None was
noticed by the commit itself.

The answer is already on the tool this project uses for everything else. roadkeep's
`scope` declares what a commit owns — `claim <id> --path <p>` — and reads it back beside
what the tree holds that no claim names, which is exactly the analysis `git add -A`
cannot make. It prints the `git add --` line for what it declared. Nothing here calls
it.

What has to be settled is not whether to use it but where the boundary sits. `ship` and
`retire` already run that read and name the unclaimed paths in their own answer, so much
of this arrives free once a task declares a scope at all. Whether `run-commit.cmd` then
stages from the scope, or simply refuses when the tree holds something outside it, is
the choice — and the second is smaller and loses nothing.

### §DD118 The pin nobody revisits

DD116 made the hooks reach `.roadkeep`, which is what the vendoring was for. It also
made the version this project runs a thing this project owns: `roadkeep engines` reports
the writer as `0.1.888 untracked`, and the checkout it was copied from is at `0.1.910`.

Being behind is not a defect. Vendoring is a decision to run a known copy rather than
whatever a neighbour happens to have checked out, and the whole value of it is that the
version does not move underneath a session. What is missing is that nothing says *how
far* behind, or when a reason to move appeared.

Two things go stale differently. The **engine** gains verbs and refusals — a fix filed
from here lands upstream and this project keeps not having it, which is a slow way to
re-report a defect already closed. And the **wired surfaces**, the launcher and the
skill, are held in step by `install --check` against whichever engine answers — so after
DD116 that check compares them against the vendored copy, which is right, and reports
drift that is really the vendored copy's age.

The mechanism is a read rather than a policy. `engines` already answers `agreed`,
`behind` or `unpinnable` across the three copies in play, and exits non-zero on the two
that are not agreement. What is worth deciding is where that read belongs — a step in
`check.yml` states it on every push, and a note in the release checklist states it when
it matters most.

## Block G — The agent surface (an agent operates this, and pays in tokens)

## Block H — The public surface (the site a reader and an agent both read)
