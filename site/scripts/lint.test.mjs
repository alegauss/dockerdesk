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
