import { notResident } from "../../lib/site-content";
import { Rich } from "../ui/Rich";

export function NotResident() {
  return (
    <section style={{ paddingTop: "20px" }}>
      <div className="wrap reveal">
        <div className="banner">
          <div className="lock">{notResident.icon}</div>
          <h2>{notResident.heading}</h2>
          {notResident.body.map((runs, i) => (
            <p key={i}>
              <Rich runs={runs} />
            </p>
          ))}
        </div>
      </div>
    </section>
  );
}
