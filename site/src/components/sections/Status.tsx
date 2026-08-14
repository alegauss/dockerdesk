import { status } from "../../lib/site-content";
import { Rich } from "../ui/Rich";

// The honest-status section, and only that (DD91). It used to carry the apparatus as well:
// shipped/open totals, a chip per block with its progress, and the next ready task — every
// figure generated from `roadkeep export --json`, which was the right answer to DD43's
// defect and stayed correct for as long as it existed.
//
// What went is the subject, not the mechanism. A reader arriving to ask whether they can
// have Docker without the licence was handed a burndown of somebody else's backlog, and the
// open lines it counted are window polish and a benchmark seam. The sentence that says there
// is nothing to download is the part that was ever theirs, so that is the part that stays.
export function Status() {
  return (
    <section id="status">
      <div className="wrap">
        <div className="sec-head reveal">
          <div className="eyebrow">{status.eyebrow}</div>
          <h2>{status.heading}</h2>
          <p>
            <Rich runs={status.intro} />
          </p>
        </div>

        {/* The backlog is still readable, by the one reader it answers: an agent following
            a link into the governed files. That is a link, not a projection of them. */}
        <p className="status-links reveal">
          <a href={status.roadmapUrl}>What is open, and why each line exists →</a>
        </p>
      </div>
    </section>
  );
}
