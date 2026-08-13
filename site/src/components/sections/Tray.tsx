import { tray } from "../../lib/site-content";
import { stateIcons, trayMenuDiagram } from "../../lib/diagrams";
import { Rich } from "../ui/Rich";
import { RawSvg } from "../ui/RawSvg";

export function Tray() {
  return (
    <section id="tray">
      <div className="wrap">
        <div className="sec-head reveal">
          <div className="eyebrow">{tray.eyebrow}</div>
          <h2>{tray.heading}</h2>
          <p>
            <Rich runs={tray.intro} />
          </p>
        </div>
        <div className="states reveal">
          {tray.states.map((state) => (
            <div className={`state ${state.kind}`} key={state.kind}>
              <RawSvg markup={stateIcons[state.kind]} />
              <h3>{state.title}</h3>
              <p>
                <Rich runs={state.body} />
              </p>
            </div>
          ))}
        </div>
        <div className="split reveal" style={{ marginTop: "52px" }}>
          <div className="split-txt">
            <div className="eyebrow">{tray.splitEyebrow}</div>
            <h2>{tray.splitHeading}</h2>
            <ul className="feat-list">
              {tray.splitList.map((runs, i) => (
                <li key={i}>
                  <span className="chk">✓</span>
                  <span>
                    <Rich runs={runs} />
                  </span>
                </li>
              ))}
            </ul>
          </div>
          <RawSvg className="shot-frame" markup={trayMenuDiagram} />
        </div>
      </div>
    </section>
  );
}
