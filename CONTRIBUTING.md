# Contributing

Thanks for looking. The project is a free, Apache-2.0 Windows desktop app for installing
and driving Docker — see [docs/ROADMAP.md](docs/ROADMAP.md) for what is open and
[docs/IMPROVEMENTS.md](docs/IMPROVEMENTS.md) for why each line exists.

## The files under `docs/` are written by a tool, not by hand

`docs/ROADMAP.md`, `docs/CHANGELOG.md` and `docs/IMPROVEMENTS.md` are governed by
[roadkeep](https://github.com/alegauss/roadkeep): the line format is validated at the point
of insertion, so a hand edit is refused rather than reviewed. Use the commands instead —
`roadkeep add`, `status`, `ship`, `retire`, `section add`, `non-goal add` — and
`roadkeep lint`, which CI runs, tells you if anything drifted.

`roadkeep pick` names the next task that is ready and why it was chosen; `roadkeep brief
<id>` prints everything it costs to start one, the non-goals included.
