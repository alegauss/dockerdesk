import { footer } from "../lib/site-content";
import { Waves } from "./ui/Waves";

export function Footer() {
  return (
    <footer>
      <Waves className="waves--footer" />
      <div className="wrap">
        <div className="foot-grid">
          <a className="foot-brand" href="/dockerdesk/">
            <img src="/dockerdesk/logo.svg" alt="" />
            FreeWilly
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
