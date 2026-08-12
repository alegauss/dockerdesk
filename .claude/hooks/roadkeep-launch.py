#!/usr/bin/env python
"""The launcher for an environment the plugin never reaches (RK1108).

Why this file exists
--------------------
roadkeep ships as a Claude Code *plugin*, and on a developer machine that is the whole
install: `/plugin install` places it under the harness's config directory and its
``hooks/hooks.json`` registers the guard that denies a hand-edit of the governed files.
There is one environment where none of that happens. **Claude Code on the web has no
``/plugin`` command and installs no marketplace plugin** — it reads settings and files
committed to the repository. So the hooks and the MCP server never load, the guard is
absent, and an agent falls back to editing ROADMAP.md by hand: the drift this tool exists
to stop, in the environment with the least supervision.

This file is committed to the adopting repository, which that environment *does* read.
``roadkeep install --committed`` writes it to ``.claude/hooks/`` and points the hook and
the server at it, so the write path is enforced wherever a session runs.

It is deliberately standalone — it runs *before* an engine has been found, so it may not
import :mod:`roadkeep`. That makes it the one file here that restates a rule stated in the
package, and `tests/test_launching.py` holds the two together: :func:`_config_home` is
:func:`roadkeep.provenance.installed`'s first two lines, and a closure test fails when
either moves. A second implementation nobody checks is how the version this replaced came
to look under ``~/.claude`` alone (see below).

The engine is resolved in this order:

  1. ``$ROADKEEP_HOME/scripts/roadkeep.py``          an explicit override
  2. a sibling checkout ``../roadkeep``              two repositories cloned side by side
  3. a cached clone under the user cache directory   the web, second turn onward

Three rules keep it from ever making things worse:

  * **Defer to the plugin.** Where the harness has roadkeep enabled *for this project*, its
    own hook and server already run, so both modes here become a silent no-op — nothing
    double-fires and there is never a second ``roadkeep`` server or a doubled deny message.
    That question is a row in the harness's registry and **never a file on disk**: this is
    the defect the shipped version exists to fix. A hand-written copy globbed
    ``~/.claude/plugins`` for ``scripts/roadkeep.py``, which finds a marketplace clone and
    every cached version whether or not the project uses any of them — so under a
    ``CLAUDE_CONFIG_DIR`` pointing elsewhere it stood down in favour of a plugin that was
    never loaded, and a hand edit of two governed files passed a session with a guard.
  * **Never block a turn.** If no engine is found, every mode exits 0 and emits nothing. A
    missing roadkeep must degrade to "unenforced", never to a broken session.
  * **Never reach the network.** The cache is used where something else populated it; this
    file does not clone. A hook that fetches code is a hook that runs code the repository
    did not commit, and the environment this exists for is the one that reviews it least.

The engine invoked is ``scripts/roadkeep.py`` — roadkeep's own launcher, which puts its
``src`` on ``sys.path`` and calls ``roadkeep.cli.main``. So the arguments, exit codes and
refusals are that engine's own; this file only decides *which copy answers*.
"""

from __future__ import annotations

import json
import os
import subprocess
import sys
from pathlib import Path

#: The engine, relative to a roadkeep checkout — the same path :data:`roadkeep.installing.
#: LAUNCHER` names, restated here for the reason the module docstring gives.
ENGINE_REL = Path("scripts") / "roadkeep.py"

#: The name the plugin is published under, matched against the registry key's
#: ``<name>@<marketplace>`` left half.
PLUGIN = "roadkeep"

#: Where the harness keeps the registry of installed plugins, under its config directory.
REGISTRY = ("plugins", "installed_plugins.json")


def _valid(root: Path | None) -> Path | None:
    """The engine path under *root*, if the file is actually there."""
    if root is None:
        return None
    engine = root / ENGINE_REL
    return engine if engine.is_file() else None


def _config_home() -> Path:
    """The harness's config directory — ``$CLAUDE_CONFIG_DIR`` or ``~/.claude``.

    The same pair Claude Code itself resolves, and the same pair
    :func:`roadkeep.provenance.installed` resolves. A hardcoded ``~/.claude`` reads a
    directory the running harness may not be using, and on a machine with two config
    directories it then answers about the wrong install.
    """
    return Path(os.environ.get("CLAUDE_CONFIG_DIR") or Path.home() / ".claude")


def _plugin_is_wired(root: Path) -> bool:
    """Whether the harness has roadkeep enabled **for this project** (RK1108).

    The signal to stand down, and deliberately not "is there a copy on disk": a marketplace
    clone and every cached version live under ``plugins/`` whether or not this project uses
    them, so a glob for the engine finds a file in cases where no hook is loaded — and then
    this launcher defers to a plugin that never ran and nothing guards the write.

    Read defensively and never written: the file is the harness's. A registry this cannot
    parse answers False, which is the safe side of the two — a doubled deny message is
    cosmetic and an absent guard is the drift roadkeep exists to stop.
    """
    try:
        payload = json.loads(
            _config_home().joinpath(*REGISTRY).read_text(encoding="utf-8")
        )
        wanted = root.resolve()
    except (OSError, ValueError):
        return False
    plugins = payload.get("plugins") if isinstance(payload, dict) else None
    for key, rows in (plugins if isinstance(plugins, dict) else {}).items():
        if not isinstance(key, str) or key.partition("@")[0] != PLUGIN:
            continue
        for row in rows if isinstance(rows, list) else ():
            stated = row.get("projectPath") if isinstance(row, dict) else None
            if not isinstance(stated, str) or not stated:
                continue
            try:
                # Resolved and never compared as text: the harness writes the path the way
                # its platform spells it, and a repository reached through a junction or a
                # symlink is one project written twice.
                if Path(stated).resolve() == wanted:
                    return True
            except OSError:
                continue
    return False


def _repo_root() -> Path:
    """This checkout's root — ``.claude/hooks/roadkeep-launch.py`` up three."""
    stated = os.environ.get("CLAUDE_PROJECT_DIR")
    return Path(stated) if stated else Path(__file__).resolve().parents[2]


def _cache_engine() -> Path | None:
    """A clone something else populated. Never created here — see the third rule."""
    base = os.environ.get("XDG_CACHE_HOME") or (Path.home() / ".cache")
    return _valid(Path(base) / "roadkeep-src" / "roadkeep")


def _resolve() -> Path | None:
    """An engine to run, or None. Not the deferral check — that is asked first and separately."""
    home = os.environ.get("ROADKEEP_HOME")
    return (
        (_valid(Path(home)) if home else None)
        or _valid(_repo_root().parent / "roadkeep")
        or _cache_engine()
    )


def _run(mode: str, argv: list[str], payload: bytes | None) -> int:
    if _plugin_is_wired(_repo_root()):
        return 0  # the plugin's own surface already runs; do not double-fire.
    engine = _resolve()
    if engine is None:
        return 0  # unenforced beats broken.
    if mode == "mcp":
        # `execv`, so the server owns this process's stdio rather than talking through a pipe
        # to a parent that would have to shuttle every frame.
        os.execv(sys.executable, [sys.executable, str(engine), "mcp", *argv])
    return subprocess.run(
        [sys.executable, str(engine), mode, *argv], input=payload, check=False
    ).returncode


def main(argv: list[str]) -> int:
    if argv[:1] == ["guard"]:
        # The payload is read here and handed on, because a stream is readable once.
        return _run("guard", argv[1:], sys.stdin.buffer.read())
    if argv[:1] == ["mcp"]:
        return _run("mcp", argv[1:], None)
    sys.stderr.write("usage: roadkeep-launch.py {guard|mcp}\n")
    return 2


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
