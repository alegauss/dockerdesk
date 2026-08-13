// The shape of the generated agent-surface module (S1, S2). surface.generated.ts is written
// by scripts/surface.mjs from agent-budget.json and the AgentSurface verb registry, and it
// is the only source the friction section reads — a cost or a shipped/designed badge about
// this project's own surface is generated or it is not on the page.

export interface SurfaceData {
  /** What the canonical task actually cost through the transport an agent reaches today. */
  baseline: {
    /** The task, in the benchmark's own words. */
    task: string;
    calls: number;
    tokens: number;
    /** Estimated tokens per response shape: "containers.list", "container.inspect", … */
    shapes: Record<string, number>;
  };
  /** The constitution's §3.1 acceptance criteria for the whole task. */
  target: { calls: number; tokens: number };
  /** Estimated-token ceiling per verb, keyed as the verb is typed: "read context". */
  ceilings: Record<string, number>;
  /** Every verb the registry actually dispatches today, keyed the same way. */
  shipped: string[];
}
