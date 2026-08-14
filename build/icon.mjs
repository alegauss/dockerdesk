// The app icon, rasterised from site/public/logo.svg into src/FreeWilly.Tray/FreeWilly.ico.
//
// Not part of any build: the .ico is committed, because a build that needs Node and the site's
// dependencies to produce a Windows resource is a build that fails on a machine with only the .NET
// SDK. This script is here so the file has a provenance that can be re-run rather than a binary
// nobody can regenerate.
//
//   cd site && npm ci        # once, if site/node_modules is not there
//   node build/icon.mjs      # from the repository root
//
// resvg is the same rasteriser the social card already uses (DD49), reached from where it already
// lives rather than added as a second dependency.

import { readFileSync, writeFileSync } from "node:fs";
import { fileURLToPath, pathToFileURL } from "node:url";
import { dirname, join } from "node:path";

const root = dirname(dirname(fileURLToPath(import.meta.url)));

// A file URL, not a path: an absolute Windows path passed to import() is read as a URL scheme
// ("d:" is not one) and fails with ERR_UNSUPPORTED_ESM_URL_SCHEME.
const { Resvg } = await import(
  pathToFileURL(join(root, "site", "node_modules", "@resvg", "resvg-js", "index.js")).href
);

// The published mark is the site's own asset and the tray mark is never published, so each
// is read where it lives (DD89). They were both under docs/ while that folder was a web
// root; it is not one any more.
const mark = readFileSync(join(root, "site", "public", "logo.svg"), "utf8");
const small = readFileSync(join(root, "build", "icon.svg"), "utf8");

// Every size Windows asks for: 16 and 24 in the Explorer list, 32 in the title bar and Alt+Tab, 48
// on the desktop, 64 and 128 in the larger Explorer views, 256 for the Add/Remove Programs entry
// and the installer's own icon. Missing a size means Windows scales a neighbour, which is the
// blurry-icon look, and the whole file is under 100 KB.
const sizes = [16, 24, 32, 48, 64, 128, 256];

// An .ico is a file of separate pictures, not one picture resampled, which is the whole reason it
// carries every size: the small entries can be a *different drawing*. Below 48 the mark's three
// cyan tones average into one and its eye closes, so those entries come from build/icon.svg, which
// is the same artwork traced with the wave as one tone and the eye grown to survive the size.
const pngs = sizes.map((size) => {
  const svg = size < 48 ? small : mark;
  const rendered = new Resvg(svg, { fitTo: { mode: "width", value: size } });
  return rendered.render().asPng();
});

// ICONDIR: 6 bytes, then one 16-byte ICONDIRENTRY per image, then the images. The entries carry a
// PNG rather than a DIB, which Windows has read since Vista and which keeps the 256 entry small.
const header = Buffer.alloc(6);
header.writeUInt16LE(0, 0); // reserved
header.writeUInt16LE(1, 2); // 1 = icon
header.writeUInt16LE(sizes.length, 4);

let offset = header.length + sizes.length * 16;
const entries = sizes.map((size, index) => {
  const entry = Buffer.alloc(16);
  // 256 is written as 0: the field is one byte, so the largest size has no other spelling.
  entry.writeUInt8(size === 256 ? 0 : size, 0);
  entry.writeUInt8(size === 256 ? 0 : size, 1);
  entry.writeUInt8(0, 2); // palette entries — 0, this is truecolour
  entry.writeUInt8(0, 3); // reserved
  entry.writeUInt16LE(1, 4); // colour planes
  entry.writeUInt16LE(32, 6); // bits per pixel
  entry.writeUInt32LE(pngs[index].length, 8);
  entry.writeUInt32LE(offset, 12);
  offset += pngs[index].length;
  return entry;
});

const out = join(root, "src", "FreeWilly.Tray", "FreeWilly.ico");
writeFileSync(out, Buffer.concat([header, ...entries, ...pngs]));
console.log(`${out}  ${sizes.length} sizes, ${offset} bytes`);
