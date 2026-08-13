import { hero, heroSession, repoUrl } from "../../lib/site-content";
import { Rich } from "../ui/Rich";
import { HeroSession } from "../HeroSession";
import { Waves } from "../ui/Waves";

export function Hero() {
  return (
    <header className="hero" id="top">
      <div className="wrap">
        <img className="hero-icon" src="/dockerdesk/logo.svg" alt="DockerDesk logo" />
        <div className="badge">
          <span className="dot" /> {hero.badge}
        </div>
        <h1>
          {hero.titleLead}
          <br />
          <span className="grad">{hero.titleAccent}</span>
        </h1>
        <p className="sub">
          <Rich runs={hero.sub} />
        </p>
        {/* S5: the call to action is dropped from the Markdown twin by this attribute —
            it converts a reader and costs an agent the same forty words on every page. */}
        <div className="hero-cta" data-twin="omit">
          <a className="btn btn-primary" href={repoUrl}>
            ★ View on GitHub
          </a>
          <a className="btn btn-ghost" href="#status">
            📋 Read the status
          </a>
        </div>

        <div className="session-eyebrow">{heroSession.eyebrow}</div>
        <HeroSession />
        <div className="hero-meta">
          {hero.meta.map((item) => (
            <span key={item}>{item}</span>
          ))}
        </div>
        <div className="pills">
          {hero.pills.map((runs, i) => (
            <span className="pill" key={i}>
              <Rich runs={runs} />
            </span>
          ))}
        </div>
      </div>
      <Waves />
    </header>
  );
}
