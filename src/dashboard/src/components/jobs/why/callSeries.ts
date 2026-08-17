// p0423b: the geometry behind the call-size plot, kept pure so the shape can be asserted
// without a DOM. Two measures of very different scale (a 357k prompt against a 969-byte
// answer) are NEVER drawn on two y-axes: each gets its own panel, both share the call
// order on x, and the pair is read by looking down the same column.

export interface PlotPoint {
  x: number;
  y: number;
}

/** Rounds a maximum up to a readable ceiling so the axis label is not a raw sample. */
export function niceMax(max: number): number {
  if (max <= 0) return 1;
  const magnitude = 10 ** Math.floor(Math.log10(max));
  return Math.ceil(max / magnitude) * magnitude;
}

/**
 * Maps a series onto the panel. x is the position in call order (not the call index), so a
 * truncated series still fills its panel; y is measured from the baseline, because what the
 * eye reads here is a value falling to zero.
 */
export function scaleSeries(
  values: number[],
  width: number,
  height: number,
  max: number,
): PlotPoint[] {
  const span = Math.max(1, values.length - 1);
  const ceiling = Math.max(1, max);
  return values.map((value, i) => ({
    x: values.length === 1 ? width / 2 : (i / span) * width,
    y: height - (Math.max(0, value) / ceiling) * height,
  }));
}

export function linePath(points: PlotPoint[]): string {
  if (points.length === 0) return "";
  return points.map((p, i) => `${i === 0 ? "M" : "L"}${round(p.x)},${round(p.y)}`).join(" ");
}

export function areaPath(points: PlotPoint[], height: number): string {
  if (points.length === 0) return "";
  const first = points[0];
  const last = points[points.length - 1];
  return `${linePath(points)} L${round(last.x)},${height} L${round(first.x)},${height} Z`;
}

/** Compact character counts — "357k", "3,886", "0". Sizes are read, not summed. */
export function formatChars(chars: number): string {
  if (chars >= 1_000_000) return `${(chars / 1_000_000).toFixed(1)}M`;
  if (chars >= 10_000) return `${Math.round(chars / 1000)}k`;
  return chars.toLocaleString("en-US");
}

export function formatMs(ms: number): string {
  if (ms < 1000) return `${Math.round(ms)}ms`;
  if (ms < 60_000) return `${(ms / 1000).toFixed(1)}s`;
  return `${Math.round(ms / 60_000)}m`;
}

function round(value: number): number {
  return Math.round(value * 100) / 100;
}
