// DD159 — the join surface.test.mjs already asserts for the verbs, for the counts.
//
// Five of the eight drifts DD157 corrected were numbers: seven provisioning steps against
// eleven, three artefacts against five, four preflight rows against five, and a --help block
// edited by hand under a title claiming to be the command's output. Each was true when typed.
// This is the gate that was missing — it holds the generator to its four sources, and holds
// the copy to the generator, so a count cannot be typed back in.
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const siteDir = join(dirname(fileURLToPath(import.meta.url)), "..");
const repoRoot = join(siteDir, "..");

// The generated module is TypeScript whose payload is pure JSON, so it is read as text and its
// object literal parsed out — no build step required. The same trick surface.test.mjs uses.
function loadGenerated(file, exportName) {
  const text = readFileSync(join(siteDir, "src", "lib", file), "utf8");
  const anchor = text.indexOf(`export const ${exportName}`);
  assert.ok(anchor >= 0, `${file} no longer exports ${exportName}`);
  const start = text.indexOf("{", anchor);
  const end = text.lastIndexOf("}");
  return JSON.parse(text.slice(start, end + 1));
}

const product = loadGenerated("product.generated.ts", "product");
const content = readFileSync(join(siteDir, "src", "lib", "site-content.ts"), "utf8");
const featurePages = readFileSync(join(siteDir, "src", "lib", "features.ts"), "utf8");
const source = (...parts) => readFileSync(join(repoRoot, ...parts), "utf8");

/** How many top-level entries an array literal named `field:` in the copy module has. */
function entriesIn(field) {
  const anchor = content.indexOf(`${field}: [`);
  assert.ok(anchor >= 0, `site-content.ts no longer declares ${field}: [`);

  let depth = 0;
  let entries = 0;
  for (let i = content.indexOf("[", anchor); i < content.length; i += 1) {
    const c = content[i];
    if (c === "[") {
      depth += 1;
      if (depth === 2) entries += 1;
    }
    if (c === "]") {
      depth -= 1;
      if (depth === 0) return entries;
    }
  }

  throw new Error(`site-content.ts has an unclosed ${field}: [`);
}

test("the step count is ProvisioningStep's own member count", () => {
  const provisioner = source("src", "FreeWilly.Core", "Engine", "EngineProvisioner.cs");
  const body = provisioner.slice(provisioner.indexOf("public enum ProvisioningStep"));
  const members = [...body.slice(0, body.indexOf("\n}")).matchAll(/^\s{4}([A-Z][A-Za-z]*),/gm)];

  assert.ok(members.length > 0, "no members parsed out of ProvisioningStep");
  assert.equal(product.provisioning.steps, members.length);
});

test("the acquire steps and the pinned artefacts are the same set", () => {
  // What "one per artefact" rests on. A sixth artefact with no step to acquire it, or a step
  // acquiring something the manifest no longer pins, makes that sentence false in a way only
  // this direction catches.
  const manifest = JSON.parse(source("src", "FreeWilly.Core", "Engine", "engine-manifest.json"));
  const ids = Object.keys(manifest).filter((key) => key !== "comment");

  assert.deepEqual([...product.provisioning.acquire].sort(), [...ids].sort());
  assert.equal(product.artefacts.count, ids.length);
});

test("every pinned version and host is the manifest's, verbatim", () => {
  const manifest = JSON.parse(source("src", "FreeWilly.Core", "Engine", "engine-manifest.json"));

  for (const [id, version] of Object.entries(product.artefacts.versions)) {
    assert.equal(version, manifest[id].version, `${id} version`);
  }

  const hosts = new Set(
    Object.keys(manifest)
      .filter((key) => key !== "comment")
      .map((key) => new URL(manifest[key].url).hostname),
  );
  assert.deepEqual([...product.artefacts.hosts].sort(), [...hosts].sort());
});

test("the preflight rows are the ones PreflightInspection declares and Run returns", () => {
  // Both halves, because they can drift apart: a constant declared and never added to Run is a
  // row the page counts and the product never prints. The first run of the generator undercounted
  // both by one and agreed with itself — Wsl2 carries a digit, and a letters-only pattern dropped
  // it from each — which is why the ids are asserted here and not just the number.
  const inspection = source("src", "FreeWilly.Core", "Preflight", "PreflightInspection.cs");
  const declared = [
    ...inspection.matchAll(/public const string [A-Za-z0-9]+ = "([a-z0-9-]+)";/g),
  ].map((m) => m[1]);
  const returned = [...inspection.matchAll(/Check[A-Za-z0-9]+\(facts\)/g)].length;

  assert.deepEqual(product.preflight.rows, declared);
  assert.equal(product.preflight.rows.length, returned);
  assert.ok(product.preflight.rows.includes("wsl2"), "the WSL2 row is missing from the count");
});

test("the page lists exactly as many preflight rows as the product reports", () => {
  // The heading states the number and this list is what a reader counts against it. DD157 found
  // them disagreeing; the heading is generated now, and this is the other end of that.
  assert.equal(entriesIn("  rows"), product.preflight.rows.length);
});

test("the depth pages read their counts through the generated module too", () => {
  // features.ts carried its own copy of every number on the landing page — the preflight row
  // count in a title, an og:description and a heading, and the step count in three more. A
  // depth page is where a reader goes to check the summary.
  assert.match(featurePages, /from "\.\/product"/);
  for (const call of ["rowCount()", "stepCount()", "artefactCount()", "acquireCount()"]) {
    assert.ok(featurePages.includes(call), `features.ts no longer states its count with ${call}`);
  }
});

test("no count the generator states is typed in the copy as well", () => {
  // The regression this whole task is about. Each of these is a sentence that was true when it
  // was written and went stale with nothing to notice, so the exact wording is refused rather
  // than merely replaced — a count typed back in beside a generated one is the same defect.
  //
  // Both modules, because the depth pages carried their own copies of the same numbers and a
  // reader who goes to one to check the other is the person the drift reaches first.
  const typed = [
    "Eleven steps",
    "Eleven unattended",
    "eleven steps",
    "in five rows",
    "Five rows",
    "five common causes",
    "Five checks",
    "five artefacts",
    "the five pinned artefacts",
    "Five things this project has decided against",
    "Moby 29.7.2",
  ];

  for (const phrase of typed) {
    assert.ok(
      !content.includes(phrase),
      `site-content.ts types "${phrase}" — state it from scripts/product.mjs instead`,
    );
    assert.ok(
      !featurePages.includes(phrase),
      `features.ts types "${phrase}" — state it from scripts/product.mjs instead`,
    );
  }
});

test("the help block on the page is a slice of the real help text", () => {
  // DD157 found this block as a hand-picked half of the output under a title claiming to be the
  // whole of it, and the half had itself drifted: it printed a pipe path the command does not.
  // A slice cannot — the lines are the command's own.
  const anchors = [...content.matchAll(/helpExcerpt\("([^"]+)", "([^"]+)"\)/g)];
  assert.equal(anchors.length, 1, "the page no longer slices the help text");

  const [, from, to] = anchors[0];
  const names = (verb) => (line) => line.trimStart().startsWith(verb);
  const start = product.help.findIndex(names(from));
  const end = product.help.findIndex(names(to));

  assert.ok(start >= 0, `the help text no longer names "${from}"`);
  assert.ok(end >= start, `the help text no longer names "${to}" after "${from}"`);

  const excerpt = product.help.slice(start, end + 1);
  assert.ok(excerpt.length > 1, "the excerpt is one line, which is not the engine verbs");
  assert.ok(excerpt.some((line) => line.includes("--provision")), "the excerpt lost --provision");
});

test("the generated help text is CommandLine's, with its verb constants resolved", () => {
  // Resolved rather than raw: HelpText is an interpolated raw string, so an unresolved hole
  // would put "{PreflightVerb}" on the page where a verb belongs.
  const commandLine = source("src", "FreeWilly.Tray", "Cli", "CommandLine.cs");

  assert.ok(
    !product.help.some((line) => /\{[A-Za-z]+\}/.test(line)),
    "an interpolation hole survived into the generated help text",
  );

  for (const line of product.help) {
    for (const verb of line.trimStart().match(/^--[a-z-]+/) ?? []) {
      assert.ok(
        commandLine.includes(`"${verb}"`) || commandLine.includes(`${verb} `),
        `the help text names ${verb} and CommandLine.cs does not`,
      );
    }
  }
});

test("the copy reads its counts through the generated module", () => {
  // The import is the mechanism, and a page that stopped importing it would go back to typing
  // numbers with nothing to notice — which is the state this task found.
  assert.match(content, /from "\.\/product"/);
  for (const call of ["rowCount()", "stepCount()", "acquireCount()", "artefactCount()"]) {
    assert.ok(content.includes(call), `the copy no longer states its count with ${call}`);
  }
});
