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
  renderStatic,
  ROUTE_META,
  canonicalUrl,
  outputDir,
  OG_IMAGE,
} from "../dist-server/entry-server.js";
import { htmlToMarkdown } from "./markdown.mjs";

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

const byteLength = (s) => Buffer.byteLength(s, "utf8");
const manifestRoutes = [];

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
    `<meta property="og:image" content="${escapeHtml(OG_IMAGE)}" />`,
    `<meta property="og:image:width" content="1200" />`,
    `<meta property="og:image:height" content="630" />`,
    `<meta name="twitter:card" content="summary_large_image" />`,
    `<meta name="twitter:image" content="${escapeHtml(OG_IMAGE)}" />`,
  ].join("\n    ");
  html = replaceOrThrow(html, "</head>", `  ${headBlock}\n  </head>`, "a </head>");

  html = replaceOrThrow(
    html,
    '<div id="root"></div>',
    `<div id="root">${body}</div>`,
    'the <div id="root"> mount point',
  );

  // The Markdown twin, converted from the same render (S5).
  const markdown = htmlToMarkdown(renderStatic(meta.path));

  const rel = outputDir(meta.path);
  const dir = join(distDir, rel);
  mkdirSync(dir, { recursive: true });
  writeFileSync(join(dir, "index.html"), html);
  writeFileSync(join(dir, "index.md"), markdown);

  const htmlPath = rel ? `${rel}/index.html` : "index.html";
  const mdPath = rel ? `${rel}/index.md` : "index.md";
  manifestRoutes.push({
    path: meta.path,
    url: canonical,
    title: meta.title,
    description: meta.description,
    html: htmlPath,
    markdown: mdPath,
    htmlBytes: byteLength(html),
    markdownBytes: byteLength(markdown),
  });
  console.log(
    `prerendered ${meta.path.padEnd(12)} -> ${htmlPath} + ${mdPath}  (${byteLength(markdown)} md bytes)`,
  );
}

// The manifest lists the routes, their twins and their sizes, so the discovery pattern
// DD25's context pack teaches is the one this site is found by (S5). No build timestamp —
// the same input builds byte-identical output.
const manifest = {
  name: "FreeWilly",
  base: "/freewilly/",
  llms: "llms.txt",
  routes: manifestRoutes,
};
writeFileSync(join(distDir, "manifest.json"), JSON.stringify(manifest, null, 2) + "\n");

console.log(
  `prerender: ${ROUTE_META.length} route(s) + twins + manifest.json written to dist/`,
);
