import { build, repoUrl } from "../../lib/site-content";
import { Rich } from "../ui/Rich";
import { CopyButton } from "../ui/CopyButton";

export function Build() {
  return (
    <section id="build">
      <div className="wrap">
        <div className="sec-head reveal">
          <div className="eyebrow">{build.eyebrow}</div>
          <h2>{build.heading}</h2>
          <p>
            <Rich runs={build.intro} />
          </p>
        </div>
        <div className="steps reveal">
          {build.steps.map((step) => (
            <div className="step" key={step.title}>
              <div className="n">{step.n}</div>
              <h4>{step.title}</h4>
              <p>
                <Rich runs={step.body} />
              </p>
            </div>
          ))}
        </div>
        <div style={{ maxWidth: "720px", margin: "0 auto" }}>
          {build.commands.map((command) => (
            <div className="codeblock copy" style={{ marginBottom: "12px" }} key={command.id}>
              <code>{command.text}</code>
              <CopyButton text={command.text} label={command.label} />
            </div>
          ))}
          <p
            style={{
              textAlign: "center",
              color: "var(--muted-2)",
              fontSize: ".85rem",
              marginTop: "16px",
            }}
          >
            <Rich runs={build.planNote} />
          </p>
        </div>
        <div style={{ textAlign: "center", marginTop: "30px" }} data-twin="omit">
          <a className="btn btn-primary" href={repoUrl}>
            ★ View on GitHub
          </a>
        </div>
      </div>
    </section>
  );
}
