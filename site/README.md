# FreeWilly site

The public site — a self-contained Vite + React 19 + TypeScript + Tailwind v4 workspace,
and this repository's first Node workspace. It is standalone: `dotnet build` neither builds
nor needs it, and it never writes into `docs/`, which is roadkeep's (see
[`docs/specs/DD40-site-constitution.md`](../docs/specs/DD40-site-constitution.md) for the
constitution under Block H).

## Commands

```
npm install        # once
npm run dev        # dev server at /freewilly/
npm run build      # generate → tsc → client → og image → SSR → prerender
npm test           # the site's own claims, against what the build produced
npm run typecheck  # tsc -b, no emit
npm run preview    # serve the built dist/
```

`npm run build` is the gate, and it is one command on purpose: it regenerates the verb
surface from this repository's own source, type-checks, builds the client, rasterises the
social card, builds the SSR bundle and prerenders every route with its Markdown twin, its
`manifest.json`, its `sitemap.xml` and its `robots.txt`. A drifted `<head>` template or a
route with no page fails it. `npm test` then asserts the built output, so it runs after the
build rather than instead of it. CI runs both on every push.

GitHub Pages derives the base path from the repository name, so Vite's `base` is
`/freewilly/` and every asset path carries that prefix. Renaming the repository moves every
published URL at once — the prefix is written once, in [vite.config.ts](vite.config.ts).

## Where things live (DD40)

| Path | What |
|---|---|
| `src/lib/site-content.ts` | **All copy** — sections only render it, so a claim is one array element a reviewer can check (S3) |
| `src/lib/diagrams.ts` | The illustrative SVGs and the preflight terminal, kept verbatim as figures |
| `src/lib/theme.ts` + `index.html` pre-paint script + `src/index.css` tokens | **The theme follows the OS**, a stored choice overrides it, applied before first paint (S6) |
| `src/routes.tsx` | The route table and its metadata, asserted against each other at import time (S4) |
| `src/components/sections/` | One component per landing section; the composition (order, JSX) lives here |
| `src/App.tsx` | The landing page, section order = the argument |
| `scripts/` | The generator, the prerender and the tests that read `dist/` |

## Deliberate non-goals here

No third-party fonts fetched at page load (the product claims no telemetry; the site adds
none), no analytics, no cookie banner. Inter / JetBrains Mono are named with system
fallbacks rather than pulled from a CDN.

## What is not written down here

A list of what does not exist yet, which is what this file used to close with (DD94). It
named DD41 to DD51 as deliberately still to come and every one of them shipped, so the
document whose job is orientation was the one furthest out of date — and correcting it by
hand had already been necessary twice.

What is open now lives in [`docs/ROADMAP.md`](../docs/ROADMAP.md), where a tool keeps it
honest and a shipped task removes its own line. The table above says where things are and
the section before it says what the workspace refuses; between them there was nothing left
for a list of the future to do except go stale.
