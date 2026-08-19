import { preflight } from "../../lib/site-content";
import { preflightTerminal } from "../../lib/diagrams";
import { Rich } from "../ui/Rich";

export function Preflight() {
  return (
    <section id="preflight">
      <div className="wrap">
        <div className="sec-head reveal">
          <div className="eyebrow">{preflight.eyebrow}</div>
          <h2>{preflight.heading}</h2>
          <p>
            <Rich runs={preflight.intro} />
          </p>
        </div>
        <div className="reveal">
          <div className="term">
            <div className="bar">
              <i />
              <i />
              <i />
              <span>{preflight.terminalTitle}</span>
            </div>
            <pre
              // eslint-disable-next-line react/no-danger
              dangerouslySetInnerHTML={{ __html: preflightTerminal }}
            />
          </div>
        </div>
        <ul className="feat-list two reveal" style={{ marginTop: "34px" }}>
          {/* The rows and the notes about them render as one list and are two arrays, so the
              row count the heading states is a number a test can hold the page to (DD159). */}
          {[...preflight.rows, ...preflight.notes].map(([lead, rest]) => (
            <li key={lead}>
              <span className="chk">✓</span>
              <span>
                <b>{lead}</b>
                {rest}
              </span>
            </li>
          ))}
        </ul>
        <p
          style={{
            textAlign: "center",
            color: "var(--muted-2)",
            fontSize: ".9rem",
            marginTop: "30px",
          }}
        >
          <Rich runs={preflight.note} />
        </p>
      </div>
    </section>
  );
}
