// S2 — the generated figures. The status page and the landing summary read one module
// generated from `roadkeep export --json`; this asserts that module's own arithmetic, so a
// generation that dropped a task, miscounted a block, or pointed `next` at a shipped line
// fails the tests rather than shipping a page confidently wrong about its own progress.
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const siteDir = join(dirname(fileURLToPath(import.meta.url)), "..");

// The generated file is TypeScript, but its payload is pure JSON (JSON.stringify output),
// so it is read as text and the object literal is parsed out — no build step required.
function loadRoadmap() {
  const text = readFileSync(join(siteDir, "src", "lib", "roadmap.generated.ts"), "utf8");
  const anchor = text.indexOf("export const roadmap");
  const start = text.indexOf("{", anchor);
  const end = text.lastIndexOf("}");
  return JSON.parse(text.slice(start, end + 1));
}

test("totals match the arrays they summarise", () => {
  const r = loadRoadmap();
  assert.equal(r.totals.shipped, r.ledger.length, "shipped total vs ledger length");
  assert.equal(r.totals.open, r.open.length, "open total vs open length");
});

test("block counts sum to the totals", () => {
  const r = loadRoadmap();
  const sum = (k) => r.blocks.reduce((n, b) => n + b[k], 0);
  assert.equal(sum("shipped"), r.totals.shipped);
  assert.equal(sum("open"), r.totals.open);
  assert.equal(sum("retired"), r.totals.retired);
});

test("every task lands in a declared block", () => {
  const r = loadRoadmap();
  const labels = new Set(r.blocks.map((b) => b.label));
  for (const t of [...r.open, ...r.ledger]) {
    assert.ok(t.id && t.status && t.block, `task missing id/status/block: ${JSON.stringify(t)}`);
    assert.ok(labels.has(t.block), `task ${t.id} in undeclared block ${t.block}`);
  }
});

test("next is null or an open task", () => {
  const r = loadRoadmap();
  if (r.next !== null) {
    assert.ok(
      r.open.some((t) => t.id === r.next),
      `next ${r.next} is not an open task`,
    );
  }
});

test("ids are unique across the whole roadmap", () => {
  const r = loadRoadmap();
  const ids = [...r.open, ...r.ledger].map((t) => t.id);
  assert.equal(new Set(ids).size, ids.length, "a task id appears twice");
});
