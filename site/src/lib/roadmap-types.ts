// The shape of the generated roadmap module (S2). roadmap.generated.ts is written by
// scripts/roadmap.mjs from `roadkeep export --json` and is the only source the status page
// and the landing summary read — a figure about this project's own progress is generated
// or it is not on the page.

export type Marker = "📋" | "💭" | "⏳" | "🛠" | "✅" | "🗑";

export interface RoadmapTask {
  id: string;
  status: string;
  block: string;
  symptom: string;
  why: string;
  deps: string[];
}

export interface RoadmapBlock {
  label: string;
  title: string;
  open: number;
  shipped: number;
  retired: number;
}

export interface RoadmapData {
  prefix: string;
  blocks: RoadmapBlock[];
  totals: { open: number; shipped: number; retired: number };
  /** unshipped tasks, in roadmap order */
  open: RoadmapTask[];
  /** shipped tasks, in changelog order */
  ledger: RoadmapTask[];
  /** the next ready id, or null when nothing is ready */
  next: string | null;
}
