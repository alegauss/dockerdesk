# Contributing

Thanks for looking. The project is a free, Apache-2.0 Windows desktop app for installing
and driving Docker — see [docs/ROADMAP.md](docs/ROADMAP.md) for what is open and
[docs/IMPROVEMENTS.md](docs/IMPROVEMENTS.md) for why each line exists.

## The files under `docs/` are written by a tool, not by hand

`docs/ROADMAP.md`, `docs/CHANGELOG.md` and `docs/IMPROVEMENTS.md` are governed by
[roadkeep](https://github.com/alegauss/roadkeep): the line format is validated at the point
of insertion, so a hand edit is refused rather than reviewed. Use the commands instead —
`roadkeep add`, `status`, `ship`, `retire`, `section add`, `non-goal add` — and
`roadkeep lint` tells you if anything drifted. Run it yourself before pushing — CI no longer
does.

`roadkeep pick` names the next task that is ready and why it was chosen; `roadkeep brief
<id>` prints everything it costs to start one, the non-goals included.

## The gates

Two workflows, and each runs exactly one of them so neither can be satisfied by a copy
nobody kept current:

| Workflow | When | What it holds |
|---|---|---|
| [`check.yml`](.github/workflows/check.yml) | every push and PR | builds and tests on **Windows**, then runs the published single-file `.exe` — that it starts at all is the failure a local build cannot see |
| [`release.yml`](.github/workflows/release.yml) | a `v*` tag | publishes the `.exe`, compiles the installer, attaches **the installer alone** with `SHA256SUMS.txt` as a **draft** release |

Three things CI cannot do, stated here rather than implied by a green tick:

- **It cannot tell you the governed files drifted.** The gate that ran `roadkeep lint` on
  every push was removed. The write-time hooks still refuse a hand edit, but that is a guard
  on the machine doing the writing — drift arriving any other way reaches `main` unremarked.
- **It cannot verify the engine install.** A hosted runner has no nested virtualization, so
  `--provision` has nowhere to import a WSL2 distribution. That is what the draft release is
  for: run the installer on a real machine, or drive the test guest with
  [`scripts/vm.ps1`](scripts/vm.ps1), before pressing Publish.
- **A tag must match `<Version>`** in [Directory.Build.props](Directory.Build.props). The
  release job refuses a mismatch rather than shipping an installer whose Add/Remove Programs
  entry disagrees with its own file name.
