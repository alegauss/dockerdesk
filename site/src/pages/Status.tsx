import { Nav } from "../components/Nav";
import { Footer } from "../components/Footer";
import { StatusBoard } from "../components/StatusBoard";

// The /status page: the whole roadmap, generated from roadkeep (S2, §DD43).
export function StatusPage() {
  return (
    <>
      <Nav />
      <header className="hero status-hero" id="top">
        <div className="wrap">
          <div className="eyebrow">Where it actually is</div>
          <h1>Status</h1>
          <p className="sub">
            Every <b>DD</b> line, its marker and its block — generated from{" "}
            <code>roadkeep export --json</code> on every build, so a shipped task moves its
            own row and this page cannot be confidently wrong about the project's progress.
          </p>
          <div className="legend">
            <span>✅ shipped</span>
            <span>🛠 in progress</span>
            <span>📋 designed</span>
            <span>💭 idea</span>
          </div>
        </div>
      </header>
      <section className="status-board">
        <StatusBoard />
      </section>
      <Footer />
    </>
  );
}
