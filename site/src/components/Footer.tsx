import { footer } from "../lib/site-content";

export function Footer() {
  return (
    <footer>
      <div className="wrap">
        <div className="foot-grid">
          <a className="foot-brand" href="/dockerdesk/">
            <img src="/dockerdesk/logo.svg" alt="" />
            DockerDesk
          </a>
          <div className="foot-links">
            {footer.links.map((link) => (
              <a key={link.href} href={link.href}>
                {link.label}
              </a>
            ))}
          </div>
        </div>
        <p className="disclaimer">{footer.disclaimer}</p>
      </div>
    </footer>
  );
}
