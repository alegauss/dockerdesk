// DD159 — the numbers come from the source, like the verbs already do.
//
// DD90 proved the shape: the agent verb list is read off the registry at build time,
// surface.test.mjs asserts the page against it, and in the audit that found this task the
// generated half of the site was the half with nothing wrong in it. Five of the eight drifts
// DD157 corrected were counts nobody could have kept — seven provisioning steps against
// eleven, three artefacts against five, four preflight rows against five, and a --help block
// edited by hand under a title claiming to be the command's output. Each was true when typed.
// None had a gate.
//
// So four readers, each a text parse of one committed source file — no build step, the same
// trick surface.mjs already uses:
//
//   EngineProvisioner.cs      the ProvisioningStep members: how many steps there are, and
//                             which of them acquire an artefact
//   engine-manifest.json      the artefact count, each version, and the hosts they come from
//                             — which the privacy claim on two pages also rests on
//   PreflightInspection.cs    the row ids, cross-checked against the calls Run actually makes
//   CommandLine.cs            the help text, resolved, so an excerpt is a slice of the real
//                             output rather than a retyping of it
//
// Then the copy states the reason and this states the number (S1, S2). Prose stays unchecked:
// "the ports are links" is a sentence a reviewer reads. The counts are the part that goes
// stale in silence.
//
// A malformed or missing source throws. A red build, never a page left confidently stale.
import { readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const siteDir = join(here, "..");
const repoRoot = join(siteDir, "..");
const outFile = join(siteDir, "src", "lib", "product.generated.ts");

const provisionerFile = join(repoRoot, "src", "FreeWilly.Core", "Engine", "EngineProvisioner.cs");
const manifestFile = join(repoRoot, "src", "FreeWilly.Core", "Engine", "engine-manifest.json");
const inspectionFile = join(
  repoRoot,
  "src",
  "FreeWilly.Core",
  "Preflight",
  "PreflightInspection.cs",
);
const commandLineFile = join(repoRoot, "src", "FreeWilly.Tray", "Cli", "CommandLine.cs");

function read(path, what) {
  try {
    return readFileSync(path, "utf8");
  } catch (err) {
    throw new Error(`product: cannot read ${what} at ${path}: ${err.message}`);
  }
}

/** The body of a brace-delimited block, starting from the first `{` after `anchor`. */
function block(text, anchor, what) {
  const at = text.indexOf(anchor);
  if (at < 0) {
    throw new Error(`product: ${what} no longer contains "${anchor}"`);
  }

  const open = text.indexOf("{", at);
  if (open < 0) {
    throw new Error(`product: ${what} has "${anchor}" with no block after it`);
  }

  let depth = 0;
  for (let i = open; i < text.length; i += 1) {
    if (text[i] === "{") depth += 1;
    if (text[i] === "}") {
      depth -= 1;
      if (depth === 0) return text.slice(open + 1, i);
    }
  }

  throw new Error(`product: ${what} has an unclosed block after "${anchor}"`);
}

// --- the provisioning steps (DD157) ---
// The enum is the source the installer's own progress bar is counted against — installer.iss
// carries ProvisioningSteps = 11 and a test holds it equal to the member count — so the page
// reading the same enum makes three files agree instead of two.

const provisioner = read(provisionerFile, "the provisioner");
const stepBody = block(provisioner, "public enum ProvisioningStep", "EngineProvisioner.cs");

// Members are the identifiers at the start of a line inside the enum; the XML comments above
// each are indented the same but always begin with a slash.
const steps = [...stepBody.matchAll(/^\s{4}([A-Z][A-Za-z]*),/gm)].map((m) => m[1]);

if (steps.length === 0) {
  throw new Error(
    `product: found no members in ProvisioningStep — ${provisionerFile} no longer matches the ` +
      "shape this script reads (one PascalCase member per line, comma-terminated)",
  );
}

// The acquire half, named by what each acquires. Five steps, one per artefact, and the join to
// the manifest is asserted below rather than assumed.
const acquire = steps
  .filter((name) => name.startsWith("Acquire"))
  .map((name) => name.slice("Acquire".length).toLowerCase());

// --- the pinned artefacts (DD157) ---

const manifest = JSON.parse(read(manifestFile, "the engine manifest"));
const artefactIds = Object.keys(manifest).filter((key) => key !== "comment");

if (artefactIds.length === 0) {
  throw new Error("product: engine-manifest.json declares no artefacts");
}

const versions = {};
const hosts = [];
for (const id of artefactIds) {
  const artefact = manifest[id];
  if (typeof artefact?.version !== "string" || typeof artefact?.url !== "string") {
    throw new Error(`product: engine-manifest.json "${id}" has no version and url`);
  }

  versions[id] = artefact.version;

  const host = new URL(artefact.url).hostname;
  if (!hosts.includes(host)) hosts.push(host);
}

// The join the copy rests on: "five of the eleven steps, one per artefact" is only true while
// the enum and the manifest agree about which five.
for (const id of acquire) {
  if (!artefactIds.includes(id)) {
    throw new Error(
      `product: ProvisioningStep acquires "${id}" and engine-manifest.json does not pin it — ` +
        `it pins ${artefactIds.join(", ")}`,
    );
  }
}

for (const id of artefactIds) {
  if (!acquire.includes(id)) {
    throw new Error(
      `product: engine-manifest.json pins "${id}" and no ProvisioningStep acquires it — ` +
        `the steps acquire ${acquire.join(", ")}`,
    );
  }
}

// --- the preflight rows (DD157) ---

const inspection = read(inspectionFile, "the preflight inspection");
const rowBody = block(inspection, "public static class Rows", "PreflightInspection.cs");
// [A-Za-z0-9]+ for the constant's own name, not just letters: `Wsl2` carries a digit, and a
// letters-only class silently dropped that row on the first run of this script — which is
// exactly the defect DD157 corrected by hand, reproduced by the gate meant to prevent it.
const rows = [...rowBody.matchAll(/public const string [A-Za-z0-9]+ = "([a-z0-9-]+)";/g)].map(
  (m) => m[1],
);

if (rows.length === 0) {
  throw new Error(
    `product: found no row ids in PreflightInspection.Rows — ${inspectionFile} no longer ` +
      "matches the shape this script reads",
  );
}

// The ids are constants and the report is a list of calls, and a constant declared but never
// added to that list would be a row the page counts and the product never prints. Counting the
// calls is what catches it.
const reported = [...block(inspection, "public static PreflightReport Run", "PreflightInspection.cs")
  .matchAll(/Check[A-Za-z0-9]+\(facts\)/g)].length;

if (reported !== rows.length) {
  throw new Error(
    `product: PreflightInspection declares ${rows.length} row id(s) and Run returns ` +
      `${reported} — one of them is wrong, and the page would state whichever it read`,
  );
}

// --- the help text (DD157) ---
// HelpText is an interpolated raw string, so the verb constants declared beside it are read
// first and substituted. Resolving it here is what makes the block on the page a slice of the
// real output: a renamed verb moves the line, and a removed one fails the slice.

const commandLine = read(commandLineFile, "the command line");
const constants = Object.fromEntries(
  [...commandLine.matchAll(/const string ([A-Za-z]+) = "([^"]+)";/g)].map((m) => [m[1], m[2]]),
);

// Read between the raw-string delimiters and not as a brace block: the first `{` after the
// declaration is an interpolation hole, so a brace scan comes back with one verb's name.
function rawString(text, anchor, what) {
  const at = text.indexOf(anchor);
  if (at < 0) {
    throw new Error(`product: ${what} no longer declares "${anchor}"`);
  }

  const open = text.indexOf('"""', at);
  const close = open < 0 ? -1 : text.indexOf('"""', open + 3);
  if (close < 0) {
    throw new Error(`product: ${what} has "${anchor}" with no raw string literal after it`);
  }

  return text.slice(open + 3, close);
}

const helpRaw = rawString(commandLine, "public static string HelpText", "CommandLine.cs");
if (!helpRaw.includes("--provision")) {
  throw new Error(
    "product: the block after CommandLine.HelpText does not look like the help text — " +
      "it names no --provision",
  );
}

const help = helpRaw
  .replace(/\{([A-Za-z]+)\}/g, (whole, name) => {
    if (constants[name] === undefined) {
      throw new Error(
        `product: HelpText interpolates {${name}}, which CommandLine.cs declares no constant for`,
      );
    }
    return constants[name];
  })
  // The raw-string literal's own indentation, which C# strips by the closing delimiter's
  // column. Eight spaces here, and taking it off is what makes the lines the output's own.
  .split("\n")
  .map((line) => (line.startsWith("        ") ? line.slice(8) : line.trimEnd()))
  .join("\n")
  .trim()
  .split("\n");

const data = {
  provisioning: { steps: steps.length, acquire },
  artefacts: { count: artefactIds.length, versions, hosts },
  preflight: { rows },
  help,
};

const banner =
  "// GENERATED by scripts/product.mjs from ProvisioningStep, engine-manifest.json,\n" +
  "// PreflightInspection.Rows and CommandLine.HelpText — do not edit by hand. Regenerated on\n" +
  "// every build (DD159): every count the copy states, and the help text an excerpt slices.\n";

writeFileSync(
  outFile,
  banner +
    'import type { ProductData } from "./product-types";\n\n' +
    "export const product: ProductData = " +
    JSON.stringify(data, null, 2) +
    ";\n",
);

console.log(
  `product: ${steps.length} provisioning step(s), ${artefactIds.length} artefact(s) over ` +
    `${hosts.length} host(s), ${rows.length} preflight row(s), ${help.length} help line(s) ` +
    "-> src/lib/product.generated.ts",
);
