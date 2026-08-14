---
name: freewilly-roadmap-docs
description: How work on FreeWilly is finished and committed — the one-task-one-commit rule (`run-commit.cmd -m "<ascii title>"` the moment a DD task is validated, code + roadkeep doc sync in that single commit), batching a run of tasks under /loop, and the fact that docs/ROADMAP.md, docs/CHANGELOG.md and docs/IMPROVEMENTS.md are owned by the roadkeep CLI and never hand-edited. Use whenever starting, shipping or retiring a DD task, adding new work to the backlog (reuse an existing block; a new block is a last resort and must be generic), working through a block or a list of DD-numbers, marking something shipped, or about to commit anything in this repo. Covers roadkeep, roadkeep.toml, DD numbering, block placement, and where the commit boundary falls.
---

# FreeWilly — finishing a task

## ⛔ READ FIRST — one task, one commit (non-negotiable)

**You may NOT do more than one task before committing.** This is the single most
violated rule, so it is stated up front and it is absolute:

- **One task → one `run-commit.cmd`.** The moment a task is complete and validated,
  do the roadkeep doc sync + `cd` to the repo root + `run-commit.cmd -m "<ascii
  title>"` **before touching the next task.** Finishing a task means *the commit
  landed* — code + `ROADMAP`/`CHANGELOG`/`IMPROVEMENTS` sync in that one commit.
- **Always pass `-m`** with a Conventional-Commits title, ASCII only. Without it the
  tool infers the message from the diff, and for a docs/ROADMAP commit that means
  prose about already-shipped work gets misread as `feat: implement <feature>`.
- **A multi-task request (a block, or a list of `DD<n>`s) is NOT permission to
  batch.** It is a request to run tasks **one-at-a-time, committing after each**.
  Never implement task 2 while task 1 is uncommitted. A single giant diff spanning
  many tasks with one commit (or no commit) at the end is the failure this rule
  exists to prevent.
- **For any batch of ≥2 tasks, drive it with the `/loop` skill** (self-paced):
  exactly one task per iteration, `run-commit.cmd` at the end of the iteration, then
  let the loop advance. Do not hand-roll a loop that defers commits.
- **Self-check before starting task N+1:** run `git status` / `git log -1`. If the
  previous task's work is not already committed, STOP and commit it first. If you
  are about to edit files for a new task and the working tree still shows the prior
  task's changes, the rule is already broken — commit now.

`run-commit.cmd` lives on the PATH (`D:\Dev\bin`), stages everything **relative to
the current working directory**, and calls `ai_commit.py` to write the body from the
staged diff. So `cd` to the repo root first, or a commit made from a subdirectory
will quietly leave the rest of the task behind.

---

## ⛔ READ SECOND — the three files under `docs/` are owned by `roadkeep`

[`roadkeep.toml`](../../../roadkeep.toml) declares this project's format (prefix `DD`),
and the roadkeep plugin — wired in [`.claude/settings.json`](../../settings.json), so a
clone gets it — carries the rest: a hook that **denies a hand-edit** to any of the
three files and names the command instead, and the `mcp__roadkeep__*` tools whose input
schema *is* the schema.

- Start a task with `brief <id>`, never by reading the files whole; `pick` names the
  next ready task and why.
- Change status with `ship` / `retire` / `status`, add work with `add`, rationale with
  `section add`, non-goals with `non-goal add`.
- `lint` is the gate, and CI runs it. Run it before committing.
- The doc sync for a task goes in **the same commit as the code**, so the docs never
  drift from what actually shipped.

## The split, so nothing is duplicated

| File | Single responsibility |
|---|---|
| [`docs/ROADMAP.md`](../../../docs/ROADMAP.md) | **Task status** — the only source of truth for what is unshipped (📋 designed · 💭 idea · ⏳ partial · 🛠 in-progress), one line per task |
| [`docs/CHANGELOG.md`](../../../docs/CHANGELOG.md) | What has **shipped**, indexed by block; `git log` is authoritative for the detail |
| [`docs/IMPROVEMENTS.md`](../../../docs/IMPROVEMENTS.md) | **Design rationale** for *unshipped* sections only — no status tables, no shipped implementation reports |

Non-goals are binding: check `ROADMAP.md` → "Non-goals" and `IMPROVEMENTS.md` §0
before proposing new work.

## Adding a task — reuse the block, don't grow the block list

New work joins an **existing** block. The block list is the roadmap's table of
contents, and it only stays readable if it grows far more slowly than the tasks in it.

- **Before `add`, look at what the blocks already are** (`list`, or `show` the block
  you have in mind) and place the task in the one whose theme covers it. "Related to
  something already there" is enough — the fit does not have to be perfect.
- **A new block is the last resort**, justified only when the work genuinely belongs
  to none of the existing themes — not because a task feels important, not to keep a
  feature's tasks visually together, and never one block per task.
- **If you do create one, make it generic**: name a durable *area* of the product that
  will plausibly hold several future tasks (like the blocks already there — an engine,
  a surface, a distribution story), never a single feature, a single task restated, or
  a sprint/date. If you cannot name the block without naming the one task going into
  it, that is the signal it belongs in an existing block instead.
- **Say which block you chose and why** before adding, so a wrong placement is cheap
  to correct.
