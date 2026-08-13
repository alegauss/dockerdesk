import { pipe } from "../../lib/site-content";
import { pipeDiagram } from "../../lib/diagrams";
import { Rich } from "../ui/Rich";
import { RawSvg } from "../ui/RawSvg";

export function Pipe() {
  return (
    <section id="pipe">
      <div className="wrap">
        <div className="sec-head reveal">
          <div className="eyebrow">{pipe.eyebrow}</div>
          <h2>
            <Rich runs={pipe.headingRuns} />
          </h2>
          <p>
            <Rich runs={pipe.intro} />
          </p>
        </div>
        <RawSvg className="shot-frame reveal" markup={pipeDiagram} />
        <div className="grid" style={{ marginTop: "34px" }}>
          {pipe.cards.map((card) => (
            <div className="card reveal" key={card.title}>
              <div className="ico">{card.icon}</div>
              <h3>{card.title}</h3>
              <p>
                <Rich runs={card.body} />
              </p>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
