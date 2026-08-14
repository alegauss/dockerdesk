# FreeWilly site

The public site — a self-contained Vite + React 19 + TypeScript + Tailwind v4 workspace,
and this repository's first Node workspace. It is standalone: `dotnet build` neither builds
nor needs it, and it never writes into `docs/`, which is roadkeep's (see
[`docs/specs/DD40-site-constitution.md`](../docs/specs/DD40-site-constitution.md) for the
constitution under Block H).

## Commands

```
npm install       # once
npm run dev        # dev server at /freewilly/
npm run build      # tsc -b && vite build  →  dist/
npm run preview    # serve the built dist/
npm run typecheck  # tsc -b, no emit
```

GitHub Pages derives the base path from the repository name, so Vite's `base` is
`/freewilly/` and every asset path carries that prefix. Renaming the repository moves every
published URL at once — the prefix is written once, in [vite.config.ts](vite.config.ts).

## Where things live (DD40)

| Path | What |
|---|---|
| `src/lib/site-content.ts` | **All copy** — sections only render it, so a claim is one array element a reviewer can check (S3) |
| `src/lib/diagrams.ts` | The illustrative SVGs and the preflight terminal, kept verbatim as figures |
| `src/lib/theme.ts` + `index.html` pre-paint script + `src/index.css` tokens | **The theme follows the OS**, a stored choice overrides it, applied before first paint (S6) |
| `src/components/sections/` | One component per landing section; the composition (order, JSX) lives here |
| `src/App.tsx` | The landing page, section order = the argument (§5) |

## What is DD40, and what is not

DD40 is the workspace and the landing page ported into it. Deliberately still to come:

- **DD41** — the prerender and the route table (no router yet; single client-rendered route).
- **DD42** — the Markdown twin per route, `manifest.json`, and `llms.txt`.
- **DD43** — `/status` and the status rows generated from `roadkeep export --json`. Built,
  then **reversed by DD91**: the board answered a question only the author has, so the site
  publishes no progress figure and the backlog is a link out rather than a page.
- **DD44–DD48** — the hero session, the ten laws and two actors, `/claude-code`,
  `/compare`, and the five `/features/*` depth pages.
- **DD49–DD51** — the social card, the publish job, and the site's own tests.

## Deliberate non-goals here

No third-party fonts fetched at page load (the product claims no telemetry; the site adds
none), no analytics, no cookie banner. Inter / JetBrains Mono are named with system
fallbacks rather than pulled from a CDN.
