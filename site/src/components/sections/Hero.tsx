import { hero, repoUrl } from "../../lib/site-content";
import { Rich } from "../ui/Rich";

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
        <div className="hero-cta">
          <a className="btn btn-primary" href={repoUrl}>
            ★ View on GitHub
          </a>
          <a className="btn btn-ghost" href="#status">
            📋 Read the status
          </a>
        </div>
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
    </header>
  );
}
