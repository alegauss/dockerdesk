import { Fragment } from "react";
import { compare as cmp } from "../lib/site-content";
import { Nav } from "../components/Nav";
import { Footer } from "../components/Footer";
import { Rich } from "../components/ui/Rich";

function cellClass(value: string): string {
  const head = value[0];
  if (head === "✓") return "cmp-cell yes";
  if (head === "~") return "cmp-cell partial";
  return "cmp-cell no";
}

export function Compare() {
  return (
    <>
      <Nav />
      <header className="hero page-hero" id="top">
        <div className="wrap">
          <div className="eyebrow">{cmp.eyebrow}</div>
          <h1>{cmp.heading}</h1>
          <p className="sub">
            <Rich runs={cmp.intro} />
          </p>
        </div>
      </header>

      <section>
        <div className="wrap">
          <div className="cmp-legend reveal">
            {cmp.legend.map((l) => (
              <span key={l.sym}>
                <span className={cellClass(l.sym)}>{l.sym}</span> {l.label}
              </span>
            ))}
          </div>
          <div className="cmp-scroll reveal">
            <table className="cmp-table">
              <thead>
                <tr>
                  <th className="cmp-cap-head" />
                  {cmp.columns.map((col, i) => (
                    <th key={col} className={i === 0 ? "cmp-col own" : "cmp-col"}>
                      {col}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {cmp.groups.map((group) => (
                  <Fragment key={group.law}>
                    <tr className="cmp-group">
                      <th colSpan={cmp.columns.length + 1}>{group.law}</th>
                    </tr>
                    {group.rows.map((row) => (
                      <tr key={row.cap}>
                        <td className="cmp-cap">{row.cap}</td>
                        {row.cells.map((cell, i) => (
                          <td key={i} className={i === 0 ? "cmp-td own" : "cmp-td"}>
                            <span className={cellClass(cell)}>{cell}</span>
                          </td>
                        ))}
                      </tr>
                    ))}
                  </Fragment>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      </section>

      <section>
        <div className="wrap">
          <div className="sec-head reveal">
            <div className="eyebrow">Credibility</div>
            <h2>{cmp.winsHeading}</h2>
          </div>
          <div className="grid reveal">
            {cmp.wins.map((w) => (
              <div className="card" key={w.name}>
                <h3>{w.name}</h3>
                <p>{w.body}</p>
              </div>
            ))}
          </div>
          <p className="cmp-wins-footer reveal">
            <Rich runs={cmp.winsFooter} />
          </p>
        </div>
      </section>

      <Footer />
    </>
  );
}
