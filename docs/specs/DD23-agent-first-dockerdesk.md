# DD23 — DockerDesk as an agent-first tool — concept, protocol & backlog

> Roadmap: [ROADMAP.md](../ROADMAP.md) Blocks **G–H** · Design: [IMPROVEMENTS.md](../IMPROVEMENTS.md) §DD23–§DD31
> Status: 📋 designed, not started · deps: **DD14** (an installer) and **DD15** (a release)
> Scope: this document is the **constitution** — who the operator is, the laws a
> feature is judged by, the protocol, and the decomposition into Blocks G and H.
> Each task gets its own rationale section in `IMPROVEMENTS.md`; this one is the
> premise they all rest on.

---

## 1. The shift

DockerDesk today is a **Windows desktop app for a human**: a preflight, an owned WSL2
distribution, a tray carrying the engine's state, a container list, a log window. That
foundation is right and none of it changes. What this document changes is **who is
expected to be driving it**:

> DockerDesk is a Docker installation whose primary operator is a coding agent.
> The agent runs, inspects and diagnoses. The human installs, approves and
> intervenes. Every design decision on the agent surface is judged by *tokens and
> round trips*, not by clicks.

This is not "add AI to a Docker GUI". DockerDesk hosts no model, no prompts and no API
keys, and it does not decide anything on the user's behalf. It is the **substrate an
external agent drives** — the thing on the other side of the tool call. The
differentiator is protocol quality, not intelligence.

### 1.1 Why this is worth doing here and not somewhere else

Three things are true of this repository and of almost nothing else:

- **The transport is already the Engine API, not `docker.exe`.**
  [`DockerApi`](../../src/DockerDesk.Core/Api/DockerApi.cs) speaks HTTP over
  `\\.\pipe\docker_engine` with nothing from NuGet, which means an agent-facing
  response can be *shaped* rather than parsed back out of a human's table.
- **It owns the engine's lifecycle.**
  [`EngineLifecycle`](../../src/DockerDesk.Core/Engine/EngineLifecycle.cs) can start the
  daemon. Every other route an agent has to Docker on Windows ends at "ask the user to
  open a GUI", which is a stalled conversation and the most expensive failure there is.
- **It is a Windows process, so it can answer what Docker structurally cannot.**
  Which PID holds port 8080. Whether a rival engine is answering the pipe (DD16).
  Whether a stale context is pointing the CLI elsewhere (DD20). Whether a bind mount's
  Windows path resolves on the WSL side. **These joins are the product.** Anything
  `docker` already answers well is not re-wrapped — see §7.

### 1.2 The two actors

| Actor | Interface | Job |
|---|---|---|
| **Agent** (Claude Code) | the `dockerdesk` CLI, over an ordinary shell | Run, inspect, diagnose, clean up after itself |
| **Human** | the installer, the tray, the container and log windows | Install, approve, intervene, uninstall |

The human path is not sacrificed. It is what DD14 and DD15 are for, and Block G does not
start until they ship.

---

## 2. The design laws

Binding. A feature that breaks one is wrong even if it was asked for. They are adapted
from the ten laws of Viglet Shio's `SH74`, which is the only other place this reasoning
has been worked through and paid for; where a law is stated differently here, §2.1 says
why.

**P1 — The shell is the surface.** Every agent-facing capability lands as a
`dockerdesk` **CLI verb first**. An MCP tool is a second head over the same method or it
does not exist. *(Inverted from Shio — see §2.1.)*

**P2 — One call replaces a session.** Learning what this machine is running is a product
feature, not a docs problem. If an agent needs six commands to learn the state of the
engine, that is a defect in DockerDesk.

**P3 — Tokens are a measured budget.** Every agent-facing response has a size ceiling,
the canonical task has a measured cost, and a regression fails the build. "It got
cheaper" must be a number.

**P4 — A file beats a stream.** An unbounded log read into the context is the single
largest token sink in this domain. Write it to disk and let the agent `Grep` it: it pays
for the lines that match instead of for the whole log.

**P5 — Names, not ids.** A 64-hex container id changes on every recreate and has to be
threaded across calls by hand. The address is the name — `svc:<project>/<service>` for a
compose service, the container name otherwise. Ids stay valid and stop being currency.

**P6 — Errors are instructions.** Every refusal carries what was wrong, what is allowed,
the nearest match and a minimal correct example. On this product it carries one thing
more: **the Windows fact that explains it**. An error that costs a round trip to
interpret is a defect.

**P7 — Never surprise the human.** Read and write are separated at the argv level so a
permission allowlist can tell them apart. Destructive operations take a confirm token.
Everything an agent creates is labelled with its session, and that label is the undo.

**P8 — The agent cannot see.** Give it cheap textual proof that what it started is
actually working — the port listens *from Windows*, the mount resolved, the service
answered. Otherwise every mistake costs a human cycle, which is the most expensive unit
in the system.

**P9 — Session N+1 is cheaper than session N.** A cursor and a change feed, so a
follow-up session reads the delta rather than re-deriving the machine.

**P10 — Compose, don't fork.** The agent surface is a *shape* over the Engine API and
over facts Windows already knows. It is not a second Docker CLI and never grows a
`build`, a `push` or a `compose up`.

### 2.1 The one law that is inverted, and the measurement behind it

Shio's P1 orders every capability **MCP → CLI → REST → console**, because a content
editor may be driving a client that has no shell. DockerDesk inverts it, and the
argument is Shio's own: its `tools/list` payload is re-sent on **every turn of every
session before any work happens**, measured at ~2 400 tokens across eleven tools, with a
recorded moment of *one token* of headroom. Its SH114 concluded that for Claude Code
specifically — an agent that has a terminal — a CLI verb costs nothing per turn while a
tool schema is a permanent tax.

Nobody operates Docker on Windows from a client with no shell. So the fixed cost buys
nothing here, and paying it would be worse than in Shio's case because the natural tool
count is higher. **CLI first; MCP only if a shell-less client ever matters, and then
capped at six tools with the same budget file gating it.**

---

## 3. Where the tokens actually go today

Honest accounting of a canonical task — *"bring this project's stack up and tell me why
the api container is not responding"* — against Docker as it stands on Windows. **These
are order-of-magnitude estimates, and replacing them with measurements is DD23, which is
the first task of Block G for exactly that reason.**

| Phase | Today | Cost driver |
|---|---|---|
| Learn the state | `docker ps -a`, re-run three to five times a session as state moves | A truncating human table, no cursor, no delta — full re-discovery each time |
| Diagnose | `docker inspect api` — 300–600 lines of JSON, of which four fields are read (`State.ExitCode`, `OOMKilled`, `PortBindings`, `Mounts`) | No projection: the whole entity tree is paid for |
| Read the log | `docker logs --tail 200`, carrying the same stack trace forty times from a restart loop | No dedup, no cursor, no level filter, no ceiling |
| Confirm the network | `docker port` + `inspect network` + a guess | The last question — *is the host port actually listening* — Docker does not answer at all |
| Get permission | every call is an allowlist decision | **A human round trip, the most expensive unit here** |
| Next session | all of it again | No memory |

Ballpark: **30–60k tokens, 15–30 calls, 1–3 human cycles**, of which `inspect` and
unbounded logs are the large majority. Two of the three human cycles exist only because
the agent cannot verify its own work.

### 3.1 The same task under this proposal

```
1. dockerdesk read context                     → engine, services, ports, disk, cursor  (~150 tok)
2. dockerdesk read doctor api                  → the verdict and the remedy             (~200 tok)
3. dockerdesk read logs api --dedup --budget 1500 --out .dockerdesk/logs/api.log
                                               → then Grep, paying for matches only     (~300 tok)
4. dockerdesk do  compose up --wait            → returns when ready or fails with why   (~100 tok)
5. dockerdesk read verify svc:shop/api         → the port answers from Windows: PASS    (~80 tok)
```

Target: **~2–5k tokens, ~5 calls, 0 human cycles for the diagnosis.** Steps 1, 2, 3 and
5 are reads, so a single allowlist entry — `Bash(dockerdesk read:*)` — removes every
permission prompt on the inspection path while step 4 still asks. **These are the
numbers DD23 must prove or falsify. They are acceptance criteria, not achievements.**

---

## 4. The surface

### 4.1 The read/do split is the highest-leverage decision in this document

```
dockerdesk read  context | doctor | logs | ps | ports | disk | changes | verify | path
dockerdesk do    start | stop | restart | rm | compose | engine | reclaim | prune
```

`docker ps` and `docker rm -f -v` are the same string to an allowlist, so a user either
grants `Bash(docker:*)` — which permits deleting a volume — or approves every call. No
Docker tool can express the rule the user actually wants, because `docker` mixes both in
one verb namespace. Splitting them in argv makes it one line of
`.claude/settings.json`, and what that buys is not fewer keystrokes: it is the removal of
human round trips from the 90% of agent Docker work that mutates nothing (P7, P8).

`read` is a promise, not a naming convention: a verb under it that writes is a defect.

### 4.2 The context pack — the single most valuable command

One deterministic, budgeted payload answering everything an agent asks at the start of a
session, in a terse line format rather than JSON, because entity JSON spends most of its
bytes on punctuation, repeated keys and authoring metadata nothing reads:

```
engine  running  wsl:dockerdesk  api=v1.43  pipe=docker_engine  ctx=default(ok)
api     up 4m    healthy   svc:shop/api      :8080→8080 listening
db      up 4m    healthy   svc:shop/db       :5432→5432 listening
worker  exited 137  ×3/2m  svc:shop/worker   OOM  limit=512m
disk    images 14G (4.2G dangling)  volumes 2.1G (1 unused)
compose ./docker-compose.yaml → shop  3 svc, 3 present
cursor  c:4f21a0
```

Roughly 130 tokens, against the five commands and ~20k it replaces — and it has already
answered the canonical task's question (`OOM limit=512m`) without a second call, which is
P2 doing its job. The properties that make it work are not cosmetic: **deterministic
order** so it caches and diffs, **name addressing** (P5), **a hard ceiling with an
explicit truncation cursor** rather than a silent cut, and **self-describing state** so
the agent never probes for a capability.

`--json` remains available for anything that needs to parse rather than read.

### 4.3 Verdicts, reused rather than invented

The preflight already carries the right vocabulary for this: a row, a
[`Verdict`](../../src/DockerDesk.Core/Preflight/Verdict.cs), and a remedy, assembled by
[`PreflightInspection`](../../src/DockerDesk.Core/Preflight/PreflightInspection.cs) and
rendered for a person or as JSON by
[`DockerDesk.Preflight`](../../src/DockerDesk.Preflight/Program.cs), with an exit code
that means something. `read doctor <name>` is that same model pointed at a container
instead of at a machine. This is reuse of a concept the repository already paid for, not
a new framework.

### 4.4 Teaching errors carry the Windows fact

RFC 9457 `application/problem+json`, with the field that no other Docker tool can fill:

```json
{ "type": "https://dockerdesk.dev/errors/port-allocated", "status": 409,
  "title": "Host port 8080 is already allocated",
  "heldBy": { "pid": 14032, "image": "node.exe", "path": "d:\\Git\\other-project" },
  "fix": "Stop process 14032, or change the host port in docker-compose.yaml:12",
  "example": "ports: [\"8081:8080\"]" }
```

`heldBy` is the whole argument for this product existing on the agent surface. The
daemon does not know it; a Windows process does.

### 4.5 Logs are the token sink, so they get their own contract

`read logs` takes `--since <cursor>`, `--level`, `--dedup` (collapse an identical repeat
to `× 47`), `--budget <tokens>` truncating **with a cursor and never in silence**, and
`--out <path>`. The last is P4: writing the log to `.dockerdesk/logs/<name>.log` turns an
unbounded read into a `Grep`, and the agent pays for the matching lines instead of for
the file.

---

## 5. Block G — the agent surface

The protocol and the economy. In order, and the order is not negotiable at the first
step: **the measurement is the first deliverable**, because every number in §3 is
currently an estimate and a cost that is argued rather than measured drifts quietly and
in somebody else's environment.

| Task | What it is |
|---|---|
| **DD23** | The benchmark and the budget file, gated in CI. Nothing else in this block is provable without it |
| **DD24** | `DockerDesk.Cli` — the head, the read/do split, JSON, exit codes that mean something |
| **DD25** | The context pack (§4.2) |
| **DD26** | `read doctor` — the diagnostic join over the preflight's verdict model (§4.3) |
| **DD27** | Logs with a cursor, dedup and a budget (§4.5) |
| **DD28** | Teaching errors with the Windows join (§4.4) |

## 6. Block H — guardrails, proof and distribution

| Task | What it is |
|---|---|
| **DD29** | Session labels and a scoped `do reclaim`, so cleanup is an undo and not a `prune` |
| **DD30** | `read verify` — the perception loop (P8): the port answers, the mount resolved |
| **DD31** | `read changes --since <cursor>` over the tray's event stream (P9) |
| **DD32** | The Claude Code plugin: the skill, the allowlist entry, a generated project brief |

DD31 is nearly free architecturally and is the only mechanism that makes session N+1
cheaper than session N. The tray is already a long-running process consuming `/events`
for DD7; a change feed is a cursor over a stream that is already open, where Shio had to
build a server to get the same thing.

---

## 7. Non-goals

Two are added to `ROADMAP.md` by this document, and they exist to stop the two ways this
scope balloons:

- **No model, no prompts, no API keys.** DockerDesk is the substrate; the intelligence
  is the caller's. This is what keeps it a free, offline, no-account tool.
- **Not a second Docker CLI.** The surface answers the joins the Engine API cannot, and
  what `docker` already does well is not re-wrapped. No `build`, no `push`, no registry
  auth. `do compose` shells out to the compose the user already has; it does not
  reimplement it.

The standing non-goals still bind, and one of them is the reason this is Blocks G and H
rather than Blocks A and B: **there is no resident background service**, so everything
here is either the tray the user already started or a process that exits.

---

## 8. What must be true before any of this starts

DD14 (an installer) and DD15 (a release built on a machine other than one developer's).
An agent-first CLI in a repository that ships no executable reaches nobody, and a token
benchmark on an unreleased tool measures a private opinion. The whole of Blocks G and H
depends on Block F, and no task here jumps that queue.
