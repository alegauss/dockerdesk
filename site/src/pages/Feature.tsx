import { Nav } from "../components/Nav";
import { Footer } from "../components/Footer";
import { Rich } from "../components/ui/Rich";
import { RawSvg } from "../components/ui/RawSvg";
import { features, type FeatureRecord } from "../lib/features";
import { pipeDiagram, windowDiagram, preflightTerminal } from "../lib/diagrams";

function Figure({ kind }: { kind: FeatureRecord["figure"] }) {
  if (kind === "pipe") return <RawSvg className="shot-frame reveal" markup={pipeDiagram} />;
  if (kind === "window") return <RawSvg className="shot-frame reveal" markup={windowDiagram} />;
  if (kind === "preflightTerminal") {
    return (
      <div className="reveal">
        <div className="term">
          <div className="bar">
            <i />
            <i />
            <i />
            <span>freewilly --preflight</span>
          </div>
          <pre
            // eslint-disable-next-line react/no-danger
            dangerouslySetInnerHTML={{ __html: preflightTerminal }}
          />
        </div>
      </div>
    );
  }
  return null;
}

export function FeaturePage({ record }: { record: FeatureRecord }) {
  const idx = features.findIndex((f) => f.slug === record.slug);
  const prev = idx > 0 ? features[idx - 1] : null;
  const next = idx < features.length - 1 ? features[idx + 1] : null;

  return (
    <>
      <Nav />
      <header className="hero page-hero" id="top">
        <div className="wrap">
          <a className="feature-back" href="/dockerdesk/#pipe">
            ← Features
          </a>
          <div className="eyebrow">{record.eyebrow}</div>
          <h1>{record.heading}</h1>
          <p className="sub">
            <Rich runs={record.lead} />
          </p>
        </div>
      </header>

      <section>
        <div className="wrap">
          {record.figure && <Figure kind={record.figure} />}
          <div className="feature-body">
            {record.sections.map((s) => (
              <div className="feature-section reveal" key={s.heading}>
                <h2>{s.heading}</h2>
                {s.body && (
                  <p>
                    <Rich runs={s.body} />
                  </p>
                )}
                {s.list && (
                  <ul className="feat-list">
                    {s.list.map((item, i) => (
                      <li key={i}>
                        <span className="chk">✓</span>
                        <span>
                          <Rich runs={item} />
                        </span>
                      </li>
                    ))}
                  </ul>
                )}
                {record.slug === "agent-surface" && s.heading === "Configure it" && (
                  <p>
                    <a className="feature-link" href="/dockerdesk/claude-code/">
                      The /claude-code page →
                    </a>
                  </p>
                )}
              </div>
            ))}
          </div>

          <div className="feature-nav reveal">
            {prev ? (
              <a className="feature-nav-link" href={`/dockerdesk/features/${prev.slug}/`}>
                ← {prev.heading}
              </a>
            ) : (
              <span />
            )}
            {next ? (
              <a className="feature-nav-link next" href={`/dockerdesk/features/${next.slug}/`}>
                {next.heading} →
              </a>
            ) : (
              <span />
            )}
          </div>
        </div>
      </section>

      <Footer />
    </>
  );
}
