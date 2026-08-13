// Renders an illustrative SVG figure kept as verbatim markup in lib/diagrams.ts. The
// content is static and author-controlled (no interpolation), so dangerouslySetInnerHTML
// is the faithful way to keep the drawing byte-identical to the hand-written original.
export function RawSvg({
  markup,
  className,
}: {
  markup: string;
  className?: string;
}) {
  return (
    <div
      className={className}
      // eslint-disable-next-line react/no-danger
      dangerouslySetInnerHTML={{ __html: markup }}
    />
  );
}
