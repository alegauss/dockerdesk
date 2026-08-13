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

/**
 * One task by id, from either list.
 *
 * Throws on an id the roadmap does not carry. A page that names a task is making a claim
 * about this project's own state (S1, S2), so a renamed or retired id fails the build rather
 * than rendering a row with no marker on it.
 */
export function taskById(id: string): RoadmapTask {
  const found =
    roadmap.ledger.find((t) => t.id === id) ?? roadmap.open.find((t) => t.id === id);
  if (!found) {
    throw new Error(`roadmap: no task "${id}" — it was renamed or retired`);
  }
  return found;
}

/** Whether a task has shipped. The ledger is the shipped list, so membership is the answer. */
export function hasShipped(id: string): boolean {
  return taskById(id).status === "✅";
}

/** The next ready task, resolved to its line, or null. */
export function nextTask(): RoadmapTask | null {
  if (!roadmap.next) return null;
  return roadmap.open.find((t) => t.id === roadmap.next) ?? null;
}
