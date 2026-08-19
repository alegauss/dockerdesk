import { Nav } from "../components/Nav";
import { Footer } from "../components/Footer";
import { Hero } from "../components/sections/Hero";
import { Operator } from "../components/sections/Operator";
import { Why } from "../components/sections/Why";
import { Preflight } from "../components/sections/Preflight";
import { Engine } from "../components/sections/Engine";
import { Pipe } from "../components/sections/Pipe";
import { Tray } from "../components/sections/Tray";
import { WindowSection } from "../components/sections/WindowSection";
import { FeatureIndex } from "../components/sections/FeatureIndex";
import { NotResident } from "../components/sections/NotResident";
import { NonGoals } from "../components/sections/NonGoals";
import { Download } from "../components/sections/Download";

// The landing page. The section order is the argument, not a feature list (§5): why →
// preflight → engine → the pipe (the mechanism) → tray → window → nothing resident →
// non-goals → the download. DD44/DD45 reshape the opening once the hero session and the
// two-actor/laws sections land.
//
// The page ends on the download (DD153), and DD156 is why nothing follows it: the four
// commands from a clone used to be the last section, because for the whole life of this site
// they were the only way to run the thing. A release exists now, so a page that closed by
// asking for the .NET SDK was answering a question the section above it had already settled
// — and the reader it sent to a git clone was the one who had just decided to install. From
// source stays documented where a contributor looks for it, in CONTRIBUTING.md.
export function Landing() {
  return (
    <>
      <Nav />
      <Hero />
      <Operator />
      <Why />
      <Preflight />
      <Engine />
      <Pipe />
      <Tray />
      <WindowSection />
      <FeatureIndex />
      <NotResident />
      <NonGoals />
      <Download />
      <Footer />
    </>
  );
}
