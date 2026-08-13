import { blocksWithTasks, blockName, roadmap } from "../lib/roadmap";
import type { RoadmapTask } from "../lib/roadmap";

// The full status list for /status — every DD line, its marker and its block, generated
// from roadkeep (S2). Nothing here is typed: a shipped task is in the ledger and lands in
// its block, an open one is in the roadmap and lands in the same block.

function rowClass(status: string): string {
  if (status === "✅") return "row done";
  if (status === "🛠" || status === "⏳") return "row now";
  return "row";
}

function Row({ task }: { task: RoadmapTask }) {
  return (
    <div className={rowClass(task.status)}>
      <div className="mark">{task.status}</div>
      <div className="id">{task.id}</div>
      <div className="what">
        <b>{task.symptom}</b>
        <span>{task.why}</span>
      </div>
    </div>
  );
}

export function StatusBoard() {
  const blocks = blocksWithTasks();
  return (
    <div className="wrap">
      <div className="status-totals reveal">
        <span>
          <b>{roadmap.totals.shipped}</b> shipped
        </span>
        <span>
          <b>{roadmap.totals.open}</b> open
        </span>
        {roadmap.totals.retired > 0 && (
          <span>
            <b>{roadmap.totals.retired}</b> retired
          </span>
        )}
        <span className="status-generated">generated from the roadmap</span>
      </div>

      {blocks.map((block) => (
        <section className="block reveal" key={block.label} id={`block-${block.label}`}>
          <div className="block-head">
            <h2>
              <span className="block-label">{block.label}</span> {blockName(block)}
            </h2>
            <div className="block-counts">
              {block.shipped}/{block.total} shipped
              <span className="bar-track" aria-hidden="true">
                <span className="bar-fill" style={{ width: `${Math.round(block.progress * 100)}%` }} />
              </span>
            </div>
          </div>
          <div className="rows">
            {block.tasks.map((task) => (
              <Row key={task.id} task={task} />
            ))}
          </div>
        </section>
      ))}
    </div>
  );
}
