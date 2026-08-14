// S1 — the friction section on /claude-code argues with the agent surface's own numbers and
// its own inventory, and both are generated. This asserts the join in the direction the
// bundle cannot: that every shape a row cites is one the benchmark measures and every verb
// it prices is one the budget file gives a ceiling. A row whose subject was renamed or
// retired fails here rather than rendering a blank where its whole argument was.
//
// DD90 moved one more claim here. The verb list used to carry a shipped/designed mark per
// row, so a row for a verb that did not exist was merely marked; now the list *is* the
// claim, and the assertion that every row names a verb the registry dispatches is what
// stops a plan being written back into it.
//
// It also holds the generator to its sources: the shipped list has to be the registry's, and
// the measured baseline has to be the budget file's.
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const siteDir = join(dirname(fileURLToPath(import.meta.url)), "..");
const repoRoot = join(siteDir, "..");

// The generated modules are TypeScript whose payload is pure JSON (JSON.stringify output),
// so each is read as text and its object literal parsed out — no build step required.
function loadGenerated(file, exportName) {
  const text = readFileSync(join(siteDir, "src", "lib", file), "utf8");
  const anchor = text.indexOf(`export const ${exportName}`);
  assert.ok(anchor >= 0, `${file} no longer exports ${exportName}`);
  const start = text.indexOf("{", anchor);
  const end = text.lastIndexOf("}");
  return JSON.parse(text.slice(start, end + 1));
}

const surface = loadGenerated("surface.generated.ts", "surface");
const content = readFileSync(join(siteDir, "src", "lib", "site-content.ts"), "utf8");
const budget = JSON.parse(readFileSync(join(repoRoot, "agent-budget.json"), "utf8"));

// Every `shape:`, `verb:` and `k:` in the content module — the first two are the friction
// rows, the third is the verb list. Reading the source rather than importing it keeps this a
// plain node --test with no transpile step.
const cited = (field) => [
  ...new Set([...content.matchAll(new RegExp(`\\b${field}: "([^"]+)"`, "g"))].map((m) => m[1])),
];

test("the generated baseline is the budget file's own measurement", () => {
  assert.equal(surface.baseline.tokens, budget.baseline.measured.tokens);
  assert.equal(surface.baseline.calls, budget.baseline.measured.calls);
  assert.equal(surface.baseline.task, budget.baseline.task);
  assert.equal(surface.target.tokens, budget.surface.target.tokens);
  assert.equal(surface.target.calls, budget.surface.target.calls);
});

test("the shipped list is the verb registry's, verbatim", () => {
  const registry = readFileSync(
    join(repoRoot, "src", "FreeWilly.Tray", "Cli", "AgentSurface.cs"),
    "utf8",
  );
  // The third constructor argument is AgentVerb.Shape, which is how the verb is typed.
  const verbs = [
    ...registry.matchAll(/new\(AgentNamespace\.(?:Read|Do),\s*"[a-z]+",\s*"([a-z ]+)"/g),
  ].map((m) => m[1]);
  assert.ok(verbs.length > 0, "no verbs parsed out of AgentSurface.All");
  assert.deepEqual(surface.shipped, verbs);
});

test("every shipped verb has a ceiling the build can print", () => {
  for (const verb of surface.shipped) {
    assert.equal(
      typeof surface.ceilings[verb],
      "number",
      `verb "${verb}" exists with no ceiling in agent-budget.json`,
    );
  }
});

test("every shape a friction row cites is one the benchmark measures", () => {
  const measured = Object.keys(surface.baseline.shapes);
  for (const shape of cited("shape")) {
    assert.ok(
      measured.includes(shape),
      `a row cites shape "${shape}", measured: ${measured.join(", ")}`,
    );
  }
});

test("every verb a friction row prices is one the budget file knows", () => {
  const known = Object.keys(surface.ceilings);
  for (const verb of cited("verb")) {
    assert.ok(known.includes(verb), `a row prices verb "${verb}", priced: ${known.join(", ")}`);
  }
});

test("every verb the page lists is one the registry dispatches (DD90)", () => {
  // The page prints this list with no per-row mark, which asserts that all of it exists. It
  // listed five that did not — read disk, read path, do start, do rm, do prune — under a
  // "designed" badge, and a reader who ran one found out the page was a plan.
  const listed = cited("k");
  assert.ok(listed.length > 0, "the verb list is empty");
  for (const verb of listed) {
    assert.ok(
      surface.shipped.includes(verb),
      `the page lists "${verb}", which AgentSurface.All does not dispatch: `
        + `${surface.shipped.join(", ")}`,
    );
  }
});

test("the page lists every verb the registry has, not just some of them (DD90)", () => {
  // The other direction, and the one that goes wrong quietly: a verb that ships and never
  // reaches the page is a capability nobody is told about, which no badge would have caught.
  const listed = cited("k");
  for (const verb of surface.shipped) {
    assert.ok(listed.includes(verb), `the registry dispatches "${verb}" and the page omits it`);
  }
});

test("the friction section still has rows on both sides of the split", () => {
  // The section is the page's argument; an empty one would render as a heading and nothing,
  // and nothing else here would notice.
  assert.ok(cited("shape").length > 0, "no measured shape is cited anywhere");
  assert.ok(cited("verb").length > 0, "no verb is priced anywhere");
});
