import { roadmap } from "./roadmap.generated";
import type { RoadmapBlock, RoadmapTask } from "./roadmap-types";

// The one place the site reads its own progress from (S2). Everything here derives from the
// generated module, so a shipped task moves wherever it appears and no figure is retyped.
export { roadmap };
export type { RoadmapBlock, RoadmapTask };

export interface BlockView extends RoadmapBlock {
  /** shipped first, then the open lines in roadmap order */
  tasks: RoadmapTask[];
  total: number;
  /** shipped / total, 0..1 */
  progress: number;
}

/** Every block with its tasks joined in, shipped lines before open ones. */
export function blocksWithTasks(): BlockView[] {
  return roadmap.blocks.map((block) => {
    const shipped = roadmap.ledger.filter((t) => t.block === block.label);
    const open = roadmap.open.filter((t) => t.block === block.label);
    const tasks = [...shipped, ...open];
    const total = block.shipped + block.open + block.retired;
    return {
      ...block,
      tasks,
      total,
      progress: total === 0 ? 0 : block.shipped / total,
    };
  });
}

/** A block's title with its "<label> — " lead stripped, e.g. "The public surface". */
export function blockName(block: RoadmapBlock): string {
  return block.title.replace(/^.*?—\s*/, "");
}

/** The next ready task, resolved to its line, or null. */
export function nextTask(): RoadmapTask | null {
  if (!roadmap.next) return null;
  return roadmap.open.find((t) => t.id === roadmap.next) ?? null;
}
