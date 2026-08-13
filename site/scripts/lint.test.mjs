// S7 — only the reader moves the window. The rule, taken without the defect: a panel that
// keeps its own content in view scrolls its own element, never scrollIntoView, which scrolls
// every scrollable ancestor including the document. This is the source lint that prevents
// the autoplaying transcript from dragging a reader back to the hero (§DD44, §DD51).
//
// Also here: the site fetches no third-party font at page load (a stated non-goal), so no
// source file may link fonts.googleapis.com.
import { test } from "node:test";
import assert from "node:assert/strict";
import { readdirSync, readFileSync, statSync } from "node:fs";
import { dirname, join, extname } from "node:path";
import { fileURLToPath } from "node:url";

const siteDir = join(dirname(fileURLToPath(import.meta.url)), "..");

function walk(dir, out = []) {
  for (const name of readdirSync(dir)) {
    if (name === "node_modules" || name === "dist" || name === "dist-server") continue;
    const full = join(dir, name);
    if (statSync(full).isDirectory()) walk(full, out);
    else out.push(full);
  }
  return out;
}

const sourceFiles = walk(join(siteDir, "src")).filter((f) =>
  [".ts", ".tsx", ".js", ".jsx"].includes(extname(f)),
);

test("no source calls scrollIntoView (S7)", () => {
  // the call, not the word — a comment explaining why we avoid it is fine
  const offenders = sourceFiles.filter((f) => readFileSync(f, "utf8").includes("scrollIntoView("));
  assert.deepEqual(
    offenders.map((f) => f.replace(siteDir + "/", "")),
    [],
    "a panel must scroll its own element (scrollTop), never scrollIntoView",
  );
});

test("no source fetches a third-party font at page load", () => {
  const all = [...sourceFiles, join(siteDir, "index.html")];
  const offenders = all.filter((f) => readFileSync(f, "utf8").includes("fonts.googleapis.com"));
  assert.deepEqual(offenders.map((f) => f.replace(siteDir + "/", "")), []);
});

// The ocean loops by translating one whole repeat of its crests, so a period that does not
// divide that repeat puts a visible seam through the water once per cycle — every few
// seconds, forever. The arithmetic is the whole of the illusion, so it is asserted rather
// than trusted to whoever next retunes the swell.
test("every wave period closes on the repeat the drift translates by", () => {
  const src = readFileSync(join(siteDir, "src", "components", "ui", "Waves.tsx"), "utf8");
  const span = Number(/const SPAN = (\d+)/.exec(src)?.[1]);
  assert.ok(span > 0, "Waves.tsx no longer declares SPAN");
  // the drift animation moves by 50% of a band drawn at 200% — one repeat is half the span
  const repeat = span / 2;
  const periods = [...src.matchAll(/period: (\d+)/g)].map((m) => Number(m[1]));
  assert.ok(periods.length >= 3, "expected a period per wave layer");
  assert.deepEqual(
    periods.filter((p) => repeat % p !== 0),
    [],
    `every period must divide ${repeat}`,
  );
});
