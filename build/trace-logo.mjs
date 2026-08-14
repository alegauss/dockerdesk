// The product mark, traced from build/logo-source.png into site/public/logo.svg and build/icon.svg.
//
// The artwork arrived as a raster of flat colour, and a logo that exists only as a 575px PNG is
// one that cannot be put on an installer, a favicon or anything printed. Redrawing it by eye is
// how a mark stops being the same mark, so nothing here is drawn: every pixel is classified to
// the nearest colour in the source's own palette, and each resulting mask is handed to potrace.
// The regions tile the image, so painting them back to front repaints it.
//
// Not part of any build, for the same reason build/icon.mjs is not: the outputs are committed, and
// a build that needs Node and the site's dependencies to produce a Windows resource is a build
// that fails on a machine with only the .NET SDK. This exists so the two SVGs have a provenance
// that can be re-run when the artwork changes, rather than being files nobody can regenerate.
//
//   cd site && npm ci             # once, if site/node_modules is not there
//   node build/trace-logo.mjs     # from the repository root
//   node build/icon.mjs           # the .ico reads what this wrote
//
// potrace and jimp are declared in site/package.json for the same reason resvg is reached from
// there: one Node project in the tree is easier to keep current than two, and neither the site
// build nor the .NET build runs this file.

import { writeFileSync } from "node:fs";
import { fileURLToPath, pathToFileURL } from "node:url";
import { dirname, join } from "node:path";

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const from = (...parts) => pathToFileURL(join(root, "site", "node_modules", ...parts)).href;

// A file URL, not a path: an absolute Windows path passed to import() is read as a URL scheme
// ("d:" is not one) and fails with ERR_UNSUPPORTED_ESM_URL_SCHEME.
const { Jimp } = await import(from("jimp", "dist", "esm", "index.js"));
const { default: potrace } = await import(from("potrace", "lib", "index.js"));

const source = join(root, "build", "logo-source.png");
const image = await Jimp.read(source);
const { width, height, data } = image.bitmap;

// The palette, measured off the source rather than declared: these are the classification
// anchors, and the colour each layer is finally painted with is averaged from that layer's own
// interior further down. The background is listed so its pixels can be recognised and dropped.
const BACKGROUND = { name: "background", rgb: [242, 242, 242] };
const PALETTE = [
  { name: "shell", rgb: [59, 59, 75] },    // the dark violet disc the whole mark sits in
  { name: "orca", rgb: [46, 46, 54] },     // the orca's own black, a shade off the shell
  { name: "foam", rgb: [222, 222, 222] },  // the pale belly and the crest of the wave
  { name: "wave-light", rgb: [152, 216, 220] },
  { name: "wave-mid", rgb: [104, 196, 216] },
  { name: "wave-dark", rgb: [70, 166, 190] },
  { name: "eye", rgb: [255, 255, 255] },
];

const classes = [BACKGROUND, ...PALETTE];
const nearest = new Uint8Array(width * height);
for (let p = 0; p < width * height; p++) {
  const i = p * 4;
  let best = 0;
  let shortest = Infinity;
  for (let c = 0; c < classes.length; c++) {
    const [r, g, b] = classes[c].rgb;
    const distance = (data[i] - r) ** 2 + (data[i + 1] - g) ** 2 + (data[i + 2] - b) ** 2;
    if (distance < shortest) { shortest = distance; best = c; }
  }
  nearest[p] = best;
}

// The eye is white, and so is the page behind the mark. Whether a white pixel is the eye or the
// background is not a question about its colour: it is whether the mark encloses it. Flood inwards
// from the border through everything pale, and what the flood cannot reach is inside.
const outside = new Uint8Array(width * height);
const pending = [];
for (let x = 0; x < width; x++) pending.push(x, (height - 1) * width + x);
for (let y = 0; y < height; y++) pending.push(y * width, y * width + width - 1);
const isPale = (p) => ["background", "eye"].includes(classes[nearest[p]].name);
while (pending.length) {
  const p = pending.pop();
  if (outside[p] || !isPale(p)) continue;
  outside[p] = 1;
  const x = p % width;
  const y = (p - x) / width;
  if (x > 0) pending.push(p - 1);
  if (x < width - 1) pending.push(p + 1);
  if (y > 0) pending.push(p - width);
  if (y < height - 1) pending.push(p + width);
}

const inside = new Uint8Array(width * height);
for (let p = 0; p < inside.length; p++) inside[p] = outside[p] ? 0 : 1;

function shift(mask, grow) {
  const moved = new Uint8Array(mask.length);
  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      const p = y * width + x;
      const edge = x === 0 || x === width - 1 || y === 0 || y === height - 1;
      const neighbours = edge ? [] : [mask[p - 1], mask[p + 1], mask[p - width], mask[p + width]];
      if (grow) moved[p] = mask[p] || neighbours.some(Boolean) ? 1 : 0;
      else moved[p] = mask[p] && !edge && neighbours.every(Boolean) ? 1 : 0;
    }
  }
  return moved;
}

const dilate = (mask, times = 1) => Array.from({ length: times }).reduce((m) => shift(m, true), mask);
const erode = (mask, times = 1) => Array.from({ length: times }).reduce((m) => shift(m, false), mask);

// Along its own outline the source fades from violet into the page, and those blended pixels
// classify as whichever pale colour they land nearest — a rim of foam and cyan flecks ringing the
// mark. Only the shell is allowed to reach the outline; everything else is confined to the
// silhouette eroded by two pixels, and the shell paints the rim.
const core = erode(inside, 2);

// The shell's violet and the orca's black are nearly the same colour, and the source darkens very
// slightly along its contour — enough to flip those pixels from one to the other and scallop a
// black fringe around the mark. The real regions run to thousands of pixels and the fringe comes
// in lumps of tens, so each mask keeps only its large parts.
const MIN_REGION = 200;
function largestParts(mask) {
  const kept = new Uint8Array(mask.length);
  const seen = new Uint8Array(mask.length);
  for (let start = 0; start < mask.length; start++) {
    if (!mask[start] || seen[start]) continue;
    const part = [];
    const queue = [start];
    seen[start] = 1;
    while (queue.length) {
      const p = queue.pop();
      part.push(p);
      const x = p % width;
      const y = (p - x) / width;
      const neighbours = [];
      if (x > 0) neighbours.push(p - 1);
      if (x < width - 1) neighbours.push(p + 1);
      if (y > 0) neighbours.push(p - width);
      if (y < height - 1) neighbours.push(p + width);
      for (const n of neighbours) if (mask[n] && !seen[n]) { seen[n] = 1; queue.push(n); }
    }
    if (part.length >= MIN_REGION) for (const p of part) kept[p] = 1;
  }
  return kept;
}

// A 575px source traced at its own resolution gives potrace stair-steps to follow, and it follows
// them: jagged outlines, and one region costing 40 KB of path. Enlarging the mask and blurring it
// turns each staircase back into the edge the artwork meant.
const SUPERSAMPLE = 4;

async function trace(mask) {
  const bitmap = new Jimp({ width, height, color: 0xffffffff });
  for (let p = 0; p < mask.length; p++) {
    if (!mask[p]) continue;
    const i = p * 4;
    bitmap.bitmap.data[i] = bitmap.bitmap.data[i + 1] = bitmap.bitmap.data[i + 2] = 0;
  }
  bitmap.resize({ w: width * SUPERSAMPLE, h: height * SUPERSAMPLE });
  bitmap.blur(2 * SUPERSAMPLE);
  const png = await bitmap.getBuffer("image/png");
  const tracer = new potrace.Potrace({
    threshold: 128,
    turdSize: 6 * SUPERSAMPLE ** 2,  // specks, counted in source pixels
    alphaMax: 1.2,                   // let a corner round where the source rounds it
    optCurve: true,
    optTolerance: 0.4 * SUPERSAMPLE,
  });
  return new Promise((resolve, reject) => {
    tracer.loadImage(png, (error) => (error ? reject(error) : resolve(tracer.getPathTag())));
  });
}

function measure(mask) {
  let r = 0, g = 0, b = 0, n = 0;
  for (let p = 0; p < mask.length; p++) {
    if (!mask[p]) continue;
    const i = p * 4;
    r += data[i]; g += data[i + 1]; b += data[i + 2]; n++;
  }
  const channel = (sum) => Math.round(sum / n).toString(16).padStart(2, "0");
  return `#${channel(r)}${channel(g)}${channel(b)}`;
}

function maskOf(names) {
  const wanted = names.map((name) => classes.findIndex((c) => c.name === name));
  const mask = new Uint8Array(width * height);
  for (let p = 0; p < mask.length; p++) {
    if (core[p] && wanted.includes(nearest[p])) mask[p] = 1;
  }
  return mask;
}

// Back to front. The shell is not its own colour but the whole silhouette, so every layer above it
// has something solid behind it and no seam between two paths can reach the page. Each layer is
// then grown by a pixel, because two exactly-abutting paths show a hairline where a renderer
// antialiases both edges.
async function build({ layers, grow }) {
  const drawn = [];
  for (const layer of layers) {
    const own = layer.name === "shell" ? inside : largestParts(maskOf(layer.of));
    const painted = layer.name === "eye" ? dilate(own, grow) : dilate(own);
    const colour = measure(erode(layer.name === "shell" ? maskOf(["shell"]) : own, 2));
    const path = (await trace(painted)).replace(/fill="[^"]*"/, `fill="${colour}"`);
    drawn.push({ ...layer, colour, path });
    console.log(`  ${layer.name.padEnd(11)} ${colour}  ${path.length} chars`);
  }
  return drawn;
}

// The source has margins the mark does not need: an icon is scaled to its box, so padding baked
// into the viewBox is padding no caller can take back out.
let [minX, minY, maxX, maxY] = [width, height, 0, 0];
for (let p = 0; p < inside.length; p++) {
  if (!inside[p]) continue;
  const x = p % width;
  const y = (p - x) / width;
  if (x < minX) minX = x;
  if (x > maxX) maxX = x;
  if (y < minY) minY = y;
  if (y > maxY) maxY = y;
}
const viewBox = [minX, minY, maxX - minX + 1, maxY - minY + 1].join(" ");

function compose(layers, note) {
  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="${viewBox}" role="img" aria-label="FreeWilly">
  <!-- ${note}
       Generated by build/trace-logo.mjs from build/logo-source.png. Edit the artwork, not this. -->
  <g transform="scale(${1 / SUPERSAMPLE})">
${layers.map((l) => `    <!-- ${l.name} -->\n    ${l.path}`).join("\n")}
  </g>
</svg>
`;
}

console.log("site/public/logo.svg");
const full = await build({
  layers: [
    { name: "shell", of: ["shell"] },
    { name: "foam", of: ["foam"] },
    { name: "wave-dark", of: ["wave-dark"] },
    { name: "wave-mid", of: ["wave-mid"] },
    { name: "wave-light", of: ["wave-light"] },
    { name: "orca", of: ["orca"] },
    { name: "eye", of: ["eye"] },
  ],
  grow: 0,
});
writeFileSync(join(root, "site", "public", "logo.svg"), compose(full, "The product mark."));

// The tray icon is drawn at 16 and 24 pixels, where three bands of cyan average into one and the
// eye closes: the detail that makes the mark at 256 is what makes it a smudge at 16. So the small
// sizes get their own file — the wave as a single tone, and an eye grown until it survives the
// resampling. Same trace, same artwork, fewer things asked of eleven pixels.
console.log("build/icon.svg");
const simplified = await build({
  layers: [
    { name: "shell", of: ["shell"] },
    { name: "foam", of: ["foam"] },
    { name: "wave", of: ["wave-light", "wave-mid", "wave-dark"] },
    { name: "orca", of: ["orca"] },
    { name: "eye", of: ["eye"] },
  ],
  grow: 6,
});
writeFileSync(join(root, "build", "icon.svg"), compose(simplified, "The mark at tray sizes: one wave tone, a wider eye."));
