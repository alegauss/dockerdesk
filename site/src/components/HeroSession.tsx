import { useEffect, useRef } from "react";
import { heroSession } from "../lib/site-content";
import { Rich } from "./ui/Rich";

// The hero is a session (§DD44): an autoplaying transcript of the five-call agent task,
// because what is sold here is how cheaply an agent operates the machine. All steps render
// on the server and with no JS (so the twin and a crawler read the whole thing); the
// autoplay only reveals them one at a time after mount, which keeps SSR and the first
// client render identical and never trips hydration.
//
// S7 — only the reader moves the window. As each step lands the panel scrolls ITS OWN
// element (scrollTop), never scrollIntoView, which would drag a reader who has scrolled
// past the hero back to it every step.
export function HeroSession() {
  const panelRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const panel = panelRef.current;
    if (!panel) return;
    if (matchMedia("(prefers-reduced-motion: reduce)").matches) return;

    const steps = Array.from(panel.querySelectorAll<HTMLElement>(".session-step"));
    if (steps.length === 0) return;

    panel.classList.add("session--playing"); // CSS hides the steps until each gets .in
    let i = 0;
    let timer = 0;
    const tick = () => {
      if (i >= steps.length) return;
      steps[i].classList.add("in");
      panel.scrollTop = panel.scrollHeight; // own element only (S7)
      i += 1;
      timer = window.setTimeout(tick, 1250);
    };
    timer = window.setTimeout(tick, 450);
    return () => window.clearTimeout(timer);
  }, []);

  return (
    <div className="session reveal">
      <div className="session-ask">
        <span className="session-ask-tag">Task</span>
        <span className="session-ask-text">{heroSession.question}</span>
      </div>
      <div className="session-scroll" ref={panelRef}>
        {heroSession.steps.map((step) => (
          <div className="session-step" key={step.cmd}>
            <div className="session-cmd">
              <span className="session-prompt">›</span>
              <code>{step.cmd}</code>
              <span className="session-cost">{step.cost}</span>
            </div>
            {"pack" in step && step.pack ? (
              <pre className="session-out session-pack">{step.pack.join("\n")}</pre>
            ) : (
              <div className="session-out">{step.out}</div>
            )}
          </div>
        ))}
      </div>
      <div className="session-foot">
        <span className="session-cmp today">{heroSession.today}</span>
        <span className="session-cmp target">{heroSession.target}</span>
      </div>
      <p className="session-note">
        <Rich runs={heroSession.note} />
      </p>
    </div>
  );
}
