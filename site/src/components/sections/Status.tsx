import { status } from "../../lib/site-content";
import { blocksWithTasks, blockName, nextTask, roadmap } from "../../lib/roadmap";
import { Rich } from "../ui/Rich";

// The landing summary. Like the full /status page, it reads the generated module and
// nothing else (S2): the intro copy stays in the content module, every figure is derived.
export function Status() {
  const blocks = blocksWithTasks();
  const next = nextTask();
  return (
    <section id="status">
      <div className="wrap">
        <div className="sec-head reveal">
          <div className="eyebrow">{status.eyebrow}</div>
          <h2>{status.heading}</h2>
          <p>
            <Rich runs={status.intro} />
          </p>
        </div>

        <div className="status-totals reveal">
          <span>
            <b>{roadmap.totals.shipped}</b> shipped
          </span>
          <span>
            <b>{roadmap.totals.open}</b> open
          </span>
          <span className="status-generated">
            generated from <code>roadkeep export --json</code>
          </span>
        </div>

        <div className="block-summary reveal">
          {blocks.map((block) => (
            <a className="block-chip" href={`status/#block-${block.label}`} key={block.label}>
              <div className="block-chip-head">
                <span className="block-label">{block.label}</span>
                <span className="block-chip-name">{blockName(block)}</span>
                <span className="block-chip-count">
                  {block.shipped}/{block.total}
                </span>
              </div>
              <span className="bar-track" aria-hidden="true">
                <span className="bar-fill" style={{ width: `${Math.round(block.progress * 100)}%` }} />
              </span>
            </a>
          ))}
        </div>

        {next && (
          <div className="next-line reveal">
            <span className="next-tag">Next</span>
            <span className="next-id">{next.id}</span>
            <span className="next-symptom">{next.symptom}</span>
          </div>
        )}

        <p className="status-more reveal">
          <a href="status/">See every line on the status page →</a> The full roadmap, with the
          reason each exists, is in{" "}
          <a href={status.roadmapUrl}>docs/ROADMAP.md</a> and{" "}
          <a href={status.improvementsUrl}>docs/IMPROVEMENTS.md</a>.
        </p>
      </div>
    </section>
  );
}
