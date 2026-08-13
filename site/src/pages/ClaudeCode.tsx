import { claudeCode as cc } from "../lib/site-content";
import { Nav } from "../components/Nav";
import { Footer } from "../components/Footer";
import { Rich } from "../components/ui/Rich";
import { CopyButton } from "../components/ui/CopyButton";

function VerbList({ heading, verbs }: { heading: string; verbs: { v: string; d: string }[] }) {
  return (
    <div className="verbs-col reveal">
      <h3 className="verbs-head">{heading}</h3>
      <div className="verbs">
        {verbs.map((verb) => (
          <div className="verb" key={verb.v}>
            <code className="verb-name">{verb.v}</code>
            <span className="verb-desc">{verb.d}</span>
          </div>
        ))}
      </div>
    </div>
  );
}

export function ClaudeCode() {
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
            <Rich runs={cc.status} /> <a href="status/">See the status →</a>
          </p>
        </div>
      </header>

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
