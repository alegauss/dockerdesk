import { useEffect } from "react";
import { Nav } from "./components/Nav";
import { Footer } from "./components/Footer";
import { Hero } from "./components/sections/Hero";
import { Why } from "./components/sections/Why";
import { Preflight } from "./components/sections/Preflight";
import { Engine } from "./components/sections/Engine";
import { Pipe } from "./components/sections/Pipe";
import { Tray } from "./components/sections/Tray";
import { WindowSection } from "./components/sections/WindowSection";
import { NotResident } from "./components/sections/NotResident";
import { NonGoals } from "./components/sections/NonGoals";
import { Status } from "./components/sections/Status";
import { Build } from "./components/sections/Build";

// The landing section order is the argument, not a feature list (§5): why → preflight →
// engine → the pipe (the mechanism) → tray → window → nothing resident → non-goals → the
// honest status → build from source. DD44/DD45 reshape the opening once the hero session
// and the two-actor/laws sections land.
export function App() {
  useEffect(() => {
    const els = Array.from(document.querySelectorAll<HTMLElement>(".reveal"));
    if (!("IntersectionObserver" in window)) {
      els.forEach((el) => el.classList.add("in"));
      return;
    }
    // S7: this only toggles an element's own opacity class; it never scrolls anything.
    const io = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          if (entry.isIntersecting) {
            entry.target.classList.add("in");
            io.unobserve(entry.target);
          }
        });
      },
      { threshold: 0.12 },
    );
    els.forEach((el) => io.observe(el));
    return () => io.disconnect();
  }, []);

  return (
    <>
      <Nav />
      <Hero />
      <Why />
      <Preflight />
      <Engine />
      <Pipe />
      <Tray />
      <WindowSection />
      <NotResident />
      <NonGoals />
      <Status />
      <Build />
      <Footer />
    </>
  );
}
