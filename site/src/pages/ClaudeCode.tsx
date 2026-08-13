import { claudeCode as cc, friction } from "../lib/site-content";
import { ceiling, isShipped, measured, shippedCount, surface, thousands } from "../lib/surface";
import { hasShipped, taskById } from "../lib/roadmap";
import { Nav } from "../components/Nav";
import { Footer } from "../components/Footer";
import { Rich } from "../components/ui/Rich";
import { CopyButton } from "../components/ui/CopyButton";

type Verb = { k: string; v: string; d: string };

function VerbList({ heading, verbs }: { heading: string; verbs: Verb[] }) {
  return (
    <div className="verbs-col reveal">
      <h3 className="verbs-head">{heading}</h3>
      <div className="verbs">
        {verbs.map((verb) => {
          // S1: the badge is read off the verb registry in the build (scripts/surface.mjs),
          // never typed here — so the day a verb lands, its own row stops saying "designed".
          const shipped = isShipped(verb.k);
          const cap = ceiling(verb.k);
          return (
            <div className={shipped ? "verb verb-shipped" : "verb"} key={verb.k}>
              {/* a <p>, so the twin keeps the verb, its badge and its ceiling on one line */}
              <p className="verb-head">
                <code className="verb-name">{verb.v}</code>{" "}
                <span className={shipped ? "verb-mark shipped" : "verb-mark"}>
                  {shipped ? "shipped" : "designed"}
                </span>{" "}
                {cap !== null && <span className="verb-cap">≤ {cap} tok</span>}
              </p>
              <span className="verb-desc">{verb.d}</span>
            </div>
          );
        })}
      </div>
    </div>
  );
}

/**
 * One friction: what the command costs today, and the verb that replaces it.
 *
 * Both figures are generated. `shape` keys into the measured baseline and throws on a shape
 * the benchmark does not measure; `task` keys into the roadmap and throws on an id it does
 * not carry. So a row cannot outlive the thing it is about (S1, S2).
 */
function Friction({ item }: { item: (typeof friction.items)[number] }) {
  const cost = "shape" in item.today && item.today.shape ? measured(item.today.shape) : null;
  const cap = "verb" in item.here && item.here.verb ? ceiling(item.here.verb) : null;
  const task = taskById(item.here.task);
  const shipped = hasShipped(item.here.task);

  return (
    <article className="fr reveal">
      <h3 className="fr-t">{item.t}</h3>
      {/* The label, the command and the cost are <p>s carrying inline children rather than
          divs, because the twin generator renders a paragraph as one line and recurses a div
          into one line per child — which shattered "1,906 tok measured" into two (S5). */}
      <div className="fr-pair">
        <div className="fr-side fr-today">
          <p className="fr-label">{friction.todayLabel}</p>
          <p className="fr-cmd">
            <code>{item.today.cmd}</code>
          </p>
          {cost !== null && (
            <p className="fr-cost">
              <b>{thousands(cost)}</b> tok measured
            </p>
          )}
          <p className="fr-body">
            <Rich runs={item.today.body} />
          </p>
        </div>
        {/* decorative: the twin reads the two labelled sides, so the glyph carries nothing */}
        <div className="fr-arrow" aria-hidden="true" data-twin="omit">
          →
        </div>
        <div className="fr-side fr-here">
          <p className="fr-label">
            {friction.hereLabel}{" "}
            <span className={shipped ? "fr-badge shipped" : "fr-badge"}>
              {task.id} {shipped ? "shipped" : "designed"}
            </span>
          </p>
          <p className="fr-cmd">
            <code>{item.here.cmd}</code>
          </p>
          {cap !== null && (
            <p className="fr-cost">
              <b>≤ {cap}</b> tok {shipped ? "ceiling" : "budgeted"}
            </p>
          )}
          <p className="fr-body">
            <Rich runs={item.here.body} />
          </p>
        </div>
      </div>
    </article>
  );
}

export function ClaudeCode() {
  const { baseline, target } = surface;
  return (
    <>
      <Nav />
      <header className="hero page-hero" id="top">
        <div className="wrap">
          <div className="eyebrow">{cc.eyebrow}</div>
          <h1>{cc.heading}</h1>
          <p className="sub">
            <Rich runs={cc.intro} />
          </p>
          <p className="page-status">
            <b>{shippedCount()}</b> {cc.statusLead} <Rich runs={cc.status} />{" "}
            <a href="status/">See the status →</a>
          </p>
        </div>
      </header>

      <section id="friction">
        <div className="wrap">
          <div className="sec-head reveal">
            <div className="eyebrow">{friction.eyebrow}</div>
            <h2>{friction.heading}</h2>
            <p>
              <Rich runs={friction.intro} />
            </p>
          </div>

          {/* Every figure here is read from agent-budget.json at build time (S2). A <p> for
              the same reason as the rows below: the twin keeps it as one legible line. */}
          <p className="fr-baseline reveal">
            <span className="fr-baseline-lead">{friction.baselineLead}</span>{" "}
            <span className="fr-baseline-task">{baseline.task}</span>{" "}
            <span className="fr-baseline-fig">
              <b>{thousands(baseline.tokens)}</b> est. tokens
            </span>{" "}
            <span className="fr-baseline-fig">
              <b>{baseline.calls}</b> calls
            </span>{" "}
            <span className="fr-baseline-fig target">
              target <b>≤ {thousands(target.tokens)}</b> over <b>{target.calls}</b>
            </span>
          </p>
          <p className="fr-baseline-note reveal">
            <Rich runs={friction.baselineNote} />
          </p>

          <div className="frictions">
            {friction.items.map((item) => (
              <Friction item={item} key={item.t} />
            ))}
          </div>

          <p className="fr-footer reveal">
            <Rich runs={friction.footer} />
          </p>
        </div>
      </section>

      <section>
        <div className="wrap narrow">
          <div className="sec-head reveal" style={{ marginBottom: "26px" }}>
            <h2>{cc.allowlistHeading}</h2>
          </div>
          <p className="allowlist-lead reveal">{cc.allowlistLead}</p>
          <div className="codeblock copy reveal" style={{ maxWidth: "520px", margin: "0 auto" }}>
            <code>{cc.allowlistLine}</code>
            <CopyButton text={cc.allowlistLine} label="Copy the allowlist line" />
          </div>
          <p className="allowlist-note reveal">
            <Rich runs={cc.allowlistNote} />
          </p>
        </div>
      </section>

      <section>
        <div className="wrap">
          <div className="verbs-split">
            <VerbList heading={cc.readHeading} verbs={cc.read} />
            <VerbList heading={cc.doHeading} verbs={cc.do} />
          </div>
        </div>
      </section>

      <section>
        <div className="wrap narrow">
          <div className="sec-head reveal">
            <div className="eyebrow">Discovery</div>
            <h2>{cc.pluginHeading}</h2>
            <p>
              <Rich runs={cc.pluginBody} />
            </p>
          </div>
        </div>
      </section>

      <section>
        <div className="wrap">
          <div className="sec-head reveal">
            <div className="eyebrow">Scope</div>
            <h2>{cc.refusesHeading}</h2>
            <p>
              <Rich runs={cc.refusesLead} />
            </p>
          </div>
          <div className="refuses reveal">
            {cc.refuses.map((r) => (
              <div className="refuse" key={r.t}>
                <h4>
                  <em>✗</em> {r.t}
                </h4>
                <p>{r.b}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      <Footer />
    </>
  );
}
