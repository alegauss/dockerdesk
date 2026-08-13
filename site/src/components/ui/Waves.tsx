// The ocean the mark swims in. The product logo is an orca cresting a wave, so the page it
// introduces surfaces out of water and settles back into it: one band closing the hero, one
// opening the footer, drawn in the mark's own three blues and its foam.
//
// Seamlessness here is arithmetic, not luck. Every crest is drawn twice across a 2880-unit
// viewBox laid out at 200% of the band, so 1440 units is exactly one band width; the drift
// translates by 50% — one whole repeat — which means the frame after the last is the first.
// Each period below divides 1440 for the same reason: a wave that does not close on 1440
// shows a seam once per cycle, and once per cycle is every few seconds.
//
// The drift and the swell sit on two elements on purpose. Both are transforms, and two
// animations on one element are one property overwriting the other — so the outer div rises
// and falls, and the inner svg travels sideways.
//
// Decorative only: it carries no copy, so it is hidden from the accessibility tree and
// dropped from the Markdown twin (§S5), and it stops moving under prefers-reduced-motion.

const SPAN = 2880; // two identical repeats of 1440
const FLOOR = 200; // the viewBox floor the water is filled down to

// One crest as an open line: half a period up, half a period down, repeated across the span.
// The control points sit at a quarter and three quarters of each half, which makes the
// outgoing tangent of every segment equal the incoming tangent of the next — so the joins,
// and the wrap at 1440, are smooth rather than kinked.
function crest(baseline: number, amplitude: number, period: number): string {
  const half = period / 2;
  const c1 = half * 0.25;
  const c2 = half * 0.75;
  let d = `M0 ${baseline}`;
  for (let x = 0; x < SPAN; x += period) {
    d += ` c${c1} ${-amplitude} ${c2} ${-amplitude} ${half} 0`;
    d += ` c${c1} ${amplitude} ${c2} ${amplitude} ${half} 0`;
  }
  return d;
}

// The same crest, closed down to the floor: the water under the line.
const water = (baseline: number, amplitude: number, period: number): string =>
  `${crest(baseline, amplitude, period)} V${FLOOR} H0 Z`;

// Back to front. Nearer water is drawn lower, shorter and shallower, which is what reads as
// depth; the speeds that go with it are in the stylesheet, next to the colours.
const LAYERS = [
  { key: "back", baseline: 72, amplitude: 26, period: 720 },
  { key: "mid", baseline: 100, amplitude: 16, period: 480 },
  { key: "front", baseline: 122, amplitude: 11, period: 360 },
] as const;

export function Waves({ className }: { className?: string }) {
  return (
    <div
      className={className ? `waves ${className}` : "waves"}
      aria-hidden="true"
      data-twin="omit"
    >
      {LAYERS.map((layer) => (
        <div className={`wave-swell wave-${layer.key}`} key={layer.key}>
          <svg
            className="wave-drift"
            viewBox={`0 0 ${SPAN} ${FLOOR}`}
            preserveAspectRatio="none"
            focusable="false"
          >
            <path
              className="wave-water"
              d={water(layer.baseline, layer.amplitude, layer.period)}
            />
            {/* The foam rides the nearest crest, and is drawn in that layer's own svg so it
                cannot drift out of step with the water it sits on. */}
            {layer.key === "front" && (
              <path
                className="wave-foam"
                d={crest(layer.baseline - 7, layer.amplitude, layer.period)}
              />
            )}
          </svg>
        </div>
      ))}
    </div>
  );
}
