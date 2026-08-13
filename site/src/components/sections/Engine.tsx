import { engine } from "../../lib/site-content";
import { Rich } from "../ui/Rich";

export function Engine() {
  return (
    <section id="engine">
      <div className="wrap">
        <div className="sec-head reveal">
          <div className="eyebrow">{engine.eyebrow}</div>
          <h2>{engine.heading}</h2>
          <p>
            <Rich runs={engine.intro} />
          </p>
        </div>
        <div className="steps reveal">
          {engine.steps.map((step) => (
            <div className="step" key={step.title}>
              <div className={step.ask ? "n ask" : "n"}>{step.n}</div>
              <h4>{step.title}</h4>
              <p>
                <Rich runs={step.body} />
              </p>
            </div>
          ))}
        </div>
        <div className="term reveal">
          <div className="bar">
            <i />
            <i />
            <i />
            <span>{engine.helpTitle}</span>
          </div>
          <pre>
            {engine.help}
            {"\n\n"}
            <span className="c">{engine.helpNote}</span>
          </pre>
        </div>
      </div>
    </section>
  );
}
