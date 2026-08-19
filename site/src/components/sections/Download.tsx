import { download, releasesUrl } from "../../lib/site-content";
import { Rich } from "../ui/Rich";

// DD153. The section the argument now ends in: the reader who accepted it can have the
// thing. The two buttons carry data-twin="omit" for the same reason the hero's does (S5) —
// a call to action converts a person and costs an agent the same words on every page — but
// the prose around them does not, because what the installer touches, what it needs and
// what the uninstall asks are facts an agent evaluating this tool is right to want.
export function Download() {
  return (
    <section id="download">
      <div className="wrap">
        <div className="sec-head reveal">
          <div className="eyebrow">{download.eyebrow}</div>
          <h2>{download.heading}</h2>
          <p>
            <Rich runs={download.intro} />
          </p>
        </div>
        <div className="hero-cta" data-twin="omit">
          <a className="btn btn-primary" href={releasesUrl}>
            {download.cta}
          </a>
          <a className="btn btn-ghost" href={releasesUrl}>
            {download.secondary}
          </a>
        </div>
        <div className="hero-meta">
          {download.facts.map((fact) => (
            <span key={fact}>{fact}</span>
          ))}
        </div>
        <p
          style={{
            maxWidth: "720px",
            margin: "26px auto 0",
            textAlign: "center",
            color: "var(--muted-2)",
            fontSize: ".9rem",
          }}
        >
          <Rich runs={download.note} />
        </p>
      </div>
    </section>
  );
}
