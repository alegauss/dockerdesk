import { status } from "../../lib/site-content";
import { Rich } from "../ui/Rich";

const rowClass: Record<string, string> = {
  done: "row done",
  now: "row now",
  open: "row",
};

const markGlyph: Record<string, string> = {
  done: "✅",
  now: "🛠",
  open: "📋",
};

export function Status() {
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
        <div className="legend reveal">
          {status.legend.map((item) => (
            <span key={item.label}>
              {item.mark} {item.label}
            </span>
          ))}
          <span>
            <Rich runs={status.legendNote} />
          </span>
        </div>
        <div className="rows reveal">
          {status.rows.map((row) => (
            <div className={rowClass[row.mark]} key={row.id}>
              <div className="mark">{markGlyph[row.mark]}</div>
              <div className="id">{row.id}</div>
              <div className="what">
                <b>{row.title}</b>
                <span>
                  <Rich runs={row.body} />
                </span>
              </div>
            </div>
          ))}
        </div>
        <p
          style={{
            textAlign: "center",
            color: "var(--muted-2)",
            fontSize: ".9rem",
            marginTop: "28px",
          }}
        >
          The full list, with the reason each line exists, is in{" "}
          <a href={status.roadmapUrl} style={{ color: "var(--accent-strong)" }}>
            docs/ROADMAP.md
          </a>{" "}
          and{" "}
          <a href={status.improvementsUrl} style={{ color: "var(--accent-strong)" }}>
            docs/IMPROVEMENTS.md
          </a>
          .
        </p>
      </div>
    </section>
  );
}
