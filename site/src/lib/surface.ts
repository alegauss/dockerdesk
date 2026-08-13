import { surface } from "./surface.generated";
import type { SurfaceData } from "./surface-types";

// The one place the site reads the agent surface's own costs and its own inventory from
// (S1, S2). Everything here derives from the generated module, so a shipped verb moves its
// badge wherever it appears and no figure is retyped.
export { surface };
export type { SurfaceData };

/**
 * The measured cost of one response shape, in estimated tokens.
 *
 * Throws on a key the benchmark does not measure. A friction row citing a shape that no
 * longer exists would otherwise render as a blank where its whole argument was, so the
 * build refuses instead — this is the S1 gate for the numbers, and surface.test.mjs is the
 * same gate outside the bundle.
 */
export function measured(shape: string): number {
  const value = surface.baseline.shapes[shape];
  if (value === undefined) {
    throw new Error(
      `surface: agent-budget.json measures no shape "${shape}" — ` +
        `it measures ${Object.keys(surface.baseline.shapes).join(", ")}`,
    );
  }
  return value;
}

/** A verb's ceiling in estimated tokens, or null where the budget file sets none yet. */
export function ceiling(verb: string): number | null {
  return surface.ceilings[verb] ?? null;
}

/** Whether the registry dispatches this verb today, keyed as it is typed: "read context". */
export function isShipped(verb: string): boolean {
  return surface.shipped.includes(verb);
}

/** How many verbs exist today. */
export function shippedCount(): number {
  return surface.shipped.length;
}

/**
 * A figure with a thousands separator, so 11711 reads as 11,711.
 *
 * Intl is avoided on purpose: the prerender runs in Node and the same markup hydrates in a
 * browser, and a locale-sensitive format is the classic way those two disagree about a
 * character and throw away the whole server render.
 */
export function thousands(n: number): string {
  return String(n).replace(/\B(?=(\d{3})+(?!\d))/g, ",");
}
