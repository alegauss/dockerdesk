// The prerender (§DD41, S4/S8). One render per route, the <head> patched by
// replace-or-throw so a drifted template fails the build rather than shipping a page under
// another route's title. It reads the SSR bundle built by `vite build --ssr`, and the
// route metadata comes from the same table routes.tsx has already asserted against the
// component map — so a route missing from either side never reaches this loop.
import { readFileSync, writeFileSync, mkdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

import {
  render,
  ROUTE_META,
  canonicalUrl,
  outputDir,
} from "../dist-server/entry-server.js";

const here = dirname(fileURLToPath(import.meta.url));
const distDir = join(here, "..", "dist");
const template = readFileSync(join(distDir, "index.html"), "utf8");

/** Replace the one match of `find`, or throw — a missing anchor is a drifted template. */
function replaceOrThrow(html, find, replacement, label) {
  if (typeof find === "string") {
    if (!html.includes(find)) {
      throw new Error(`prerender: template no longer contains ${label}`);
    }
    return html.replace(find, replacement);
  }
  if (!find.test(html)) {
    throw new Error(`prerender: template no longer contains ${label}`);
  }
  return html.replace(find, replacement);
}

function escapeHtml(s) {
  return s
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

if (ROUTE_META.length === 0) {
  throw new Error("prerender: ROUTE_META is empty — nothing to render");
}

for (const meta of ROUTE_META) {
  const body = render(meta.path);
  const canonical = canonicalUrl(meta.path);

  let html = template;

  html = replaceOrThrow(
    html,
    /<title>[\s\S]*?<\/title>/,
    `<title>${escapeHtml(meta.title)}</title>`,
    "a <title>",
  );

  html = replaceOrThrow(
    html,
    /<meta\s+name="description"[\s\S]*?\/>/,
    `<meta name="description" content="${escapeHtml(meta.description)}" />`,
    'a <meta name="description">',
  );

  const headBlock = [
    `<link rel="canonical" href="${escapeHtml(canonical)}" />`,
    `<meta property="og:type" content="website" />`,
    `<meta property="og:title" content="${escapeHtml(meta.ogTitle)}" />`,
    `<meta property="og:description" content="${escapeHtml(meta.ogDescription)}" />`,
    `<meta property="og:url" content="${escapeHtml(canonical)}" />`,
    `<meta name="twitter:card" content="summary" />`,
  ].join("\n    ");
  html = replaceOrThrow(html, "</head>", `  ${headBlock}\n  </head>`, "a </head>");

  html = replaceOrThrow(
    html,
    '<div id="root"></div>',
    `<div id="root">${body}</div>`,
    'the <div id="root"> mount point',
  );

  const dir = join(distDir, outputDir(meta.path));
  mkdirSync(dir, { recursive: true });
  writeFileSync(join(dir, "index.html"), html);
  console.log(
    `prerendered ${meta.path.padEnd(12)} -> ${outputDir(meta.path) || "index.html"}  (${body.length} bytes, ${canonical})`,
  );
}

console.log(`prerender: ${ROUTE_META.length} route(s) written to dist/`);
