# DD33 — MCP is a second head, and it is not free

> Roadmap: [ROADMAP.md](../ROADMAP.md) Block **G** ·
> Constitution: [DD23-agent-first-dockerdesk.md](DD23-agent-first-dockerdesk.md) §2.1 ·
> Enforced by: [`agent-budget.json`](../../agent-budget.json) → `mcp`
> Status: **decided, not built.** This document is the decision, and the condition
> under which it is reopened.

---

## What was decided

**DockerDesk's agent surface is CLI-first, and there is no MCP head.** Every capability
lands as a `dockerdesk read` or `dockerdesk do` verb. This inverts the ordering the
constitution otherwise adapts wholesale, so the inversion is recorded here rather than
remembered.

This is not a non-goal. A non-goal is binding and this is not: it is a decision with a
stated price, and the price is the thing to re-examine, not the conclusion. What follows
is what would have to be true.

## Why a CLI verb is free per turn and a tool schema is not

The measurement is borrowed, and it is specific. Viglet Shio — whose ten design laws this
repository's constitution adapts — serves an MCP surface, and its `tools/list` payload is
re-sent **on every turn of every session, before any work happens**. Measured at roughly
**2 400 tokens across eleven tools**, with a recorded moment of *one token* of headroom
against its own ceiling.

That is the shape of the cost, and it is what makes the two heads different in kind
rather than in degree:

| | paid when | paid how often |
|---|---|---|
| A CLI verb | when it is called | once per call |
| A tool schema | before anything is called | every turn of every session |

Shio's own review (SH114) concluded that for an agent which has a terminal, a CLI verb
costs nothing per turn while a tool schema is a permanent tax — and that the tax is worth
paying only for a client with **no shell**, which could otherwise reach nothing at all.

Nobody operates Docker on Windows from a client with no shell. So here the fixed cost
buys nothing, and paying it would be worse than in the borrowed case, because the natural
tool count on this surface is higher: nine verbs exist today and each would want its own
schema, its own argument descriptions and its own examples.

## The one thing that reopens this

**Evidence of a real caller that has no shell and needs to drive Docker on Windows.**

Not a preference, not a client that merely *supports* MCP, and not the observation that
adding one would be easy. A caller that cannot reach `dockerdesk read ps` at all. Until
such a caller is named, the surface an agent has is the one it can already run.

## What it would look like if that happened

A second **head over the same methods** — never a parallel implementation. Two
implementations of one surface is how it acquires two sets of semantics, and the second
set is discovered by a user, in production, on the day they diverge.

Concretely, and these are the terms, not suggestions:

1. **Capped at six tools.** Not one per verb. The verbs collapse: a read tool, a diagnose
   tool, a write tool. Nine schemas is the failure this cap exists to prevent.
2. **The schema total is gated by [`agent-budget.json`](../../agent-budget.json)**, in the
   `mcp` block, by the same test that gates every response shape (DD23). A ceiling of
   **1 100 tokens** — under half the borrowed 2 400, and tighter than that figure
   pro-rata (eleven tools at 2 400 is roughly 218 each, so six would be about 1 300).
   The whole argument against a second head is its fixed cost, and a cap that merely
   matches the thing it learned from has learned nothing from it.
3. **It dispatches into `AgentSurface`**, the same registry the CLI dispatches on, so a
   verb is added once and both heads have it.
4. **The raise is argued in the commit that makes it**, naming the caller.

## What is enforced today

`agent-budget.json` carries `mcp.exists: false` beside the cap, and
`AgentDiscoveryTests` holds the two together: while the budget says no head exists, no
file under `src/` may reference an MCP implementation. Flipping the flag alone does not
pass — the test fails with what has to be measured first. So an MCP head cannot arrive
without editing the budget file in the same commit, which is where the argument goes.
