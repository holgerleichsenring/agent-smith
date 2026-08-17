"use client";

import { areaPath, formatChars, linePath, scaleSeries, type PlotPoint } from "./callSeries";

// p0423b: ONE measure of the call series, on its own panel with its own scale. Two panels
// stacked and sharing the call order on x is what replaces a dual-axis chart: a 357k prompt
// and a 969-byte answer on one y-axis would draw the answer as a flat line on the floor,
// which is precisely the collapse the operator needs to see.

const WIDTH = 720;
const HEIGHT = 132;

export interface CallSizePanelProps {
  title: string;
  values: number[];
  color: string;
  wash: string;
  max: number;
  /** Positions (0-based) worth a marker of their own — a call that returned nothing. */
  marked?: number[];
  hovered: number | null;
  onHover: (position: number | null) => void;
  testId: string;
}

export function CallSizePanel({
  title, values, color, wash, max, marked = [], hovered, onHover, testId,
}: CallSizePanelProps) {
  const points = scaleSeries(values, WIDTH, HEIGHT, max);
  const slotWidth = WIDTH / Math.max(1, values.length);

  return (
    <figure style={{ margin: 0 }} data-testid={testId} data-points={values.length}>
      <figcaption
        style={{ display: "flex", justifyContent: "space-between", alignItems: "baseline", gap: 12 }}
      >
        <span style={{ fontSize: "12.5px", fontWeight: 600, color: "var(--ink)" }}>
          <span
            aria-hidden="true"
            style={{
              display: "inline-block", width: 8, height: 8, borderRadius: 2,
              background: color, marginRight: 6,
            }}
          />
          {title}
        </span>
        <span className="mono" style={{ fontSize: "11px", color: "var(--ink-3)" }}>
          peak {formatChars(max)}
        </span>
      </figcaption>
      <svg
        viewBox={`0 0 ${WIDTH} ${HEIGHT}`}
        style={{ width: "100%", height: "auto", display: "block", marginTop: 4 }}
        role="img"
        aria-label={`${title}. ${values.length} calls, peak ${formatChars(max)} characters.`}
        onMouseLeave={() => onHover(null)}
      >
        <line x1="0" y1={HEIGHT - 0.5} x2={WIDTH} y2={HEIGHT - 0.5} stroke="var(--line)" strokeWidth="1" />
        <path d={areaPath(points, HEIGHT)} fill={wash} />
        <path d={linePath(points)} fill="none" stroke={color} strokeWidth="2" strokeLinejoin="round" />
        {marked.map((position) => (
          <circle
            key={position}
            data-testid={`${testId}-marker`}
            cx={points[position]?.x ?? 0}
            cy={points[position]?.y ?? HEIGHT}
            r="4"
            fill="var(--bad)"
            stroke="var(--bg)"
            strokeWidth="2"
          />
        ))}
        <Crosshair point={hovered == null ? null : points[hovered]} color={color} />
        {values.map((_, position) => (
          <rect
            key={position}
            x={position * slotWidth}
            y="0"
            width={slotWidth}
            height={HEIGHT}
            fill="transparent"
            onMouseEnter={() => onHover(position)}
          />
        ))}
      </svg>
    </figure>
  );
}

function Crosshair({ point, color }: { point: PlotPoint | undefined | null; color: string }) {
  if (!point) return null;
  return (
    <g pointerEvents="none">
      <line x1={point.x} y1="0" x2={point.x} y2={HEIGHT} stroke="var(--ink-3)" strokeWidth="1" />
      <circle cx={point.x} cy={point.y} r="4" fill={color} stroke="var(--bg)" strokeWidth="2" />
    </g>
  );
}
