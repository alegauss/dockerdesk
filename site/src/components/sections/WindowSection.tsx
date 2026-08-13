import { windowSection } from "../../lib/site-content";
import { emptyStateDiagram, windowDiagram } from "../../lib/diagrams";
import { Rich } from "../ui/Rich";
import { RawSvg } from "../ui/RawSvg";

export function WindowSection() {
  return (
    <section id="window">
      <div className="wrap">
        <div className="sec-head reveal">
          <div className="eyebrow">{windowSection.eyebrow}</div>
          <h2>{windowSection.heading}</h2>
          <p>
            <Rich runs={windowSection.intro} />
          </p>
        </div>
        <figure className="shot-frame reveal" style={{ margin: 0 }}>
          <RawSvg markup={windowDiagram} />
          <figcaption>
            <Rich runs={windowSection.caption} />
          </figcaption>
        </figure>
        <div className="split rev reveal" style={{ marginTop: "48px" }}>
          <RawSvg className="shot-frame" markup={emptyStateDiagram} />
          <div className="split-txt">
            <div className="eyebrow">{windowSection.detailsEyebrow}</div>
            <h2>{windowSection.detailsHeading}</h2>
            <ul className="feat-list">
              {windowSection.detailsList.map((runs, i) => (
                <li key={i}>
                  <span className="chk">✓</span>
                  <span>
                    <Rich runs={runs} />
                  </span>
                </li>
              ))}
            </ul>
          </div>
        </div>
      </div>
    </section>
  );
}
