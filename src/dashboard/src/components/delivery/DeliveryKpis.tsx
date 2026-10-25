import type { DeliveryFigures, DeliveryPeriod } from "@/lib/deliveryApi";
import { COMPARED_WITH, KPIS, formatDelta, formatValue, type Kpi } from "@/lib/delivery/deliveryFormat";

// 2026-10-25-0099c: the eight figures of the period, each with its change against the period before (hidden on a
// truncated answer, whose previous period is incomplete) and a sparkline of the period's slots.

export function DeliveryKpis({
  current, previous, slots, period, truncated,
}: { current: DeliveryFigures; previous: DeliveryFigures; slots: DeliveryFigures[]; period: DeliveryPeriod; truncated: boolean }) {
  return (
    <section className="kpis" aria-label="Key figures" data-testid="delivery-kpis">
      {KPIS.map((kpi) => (
        <Tile key={kpi.label} kpi={kpi} current={current} previous={truncated ? null : previous} slots={slots} period={period} />
      ))}
    </section>
  );
}

function Tile({ kpi, current, previous, slots, period }: {
  kpi: Kpi; current: DeliveryFigures; previous: DeliveryFigures | null; slots: DeliveryFigures[]; period: DeliveryPeriod;
}) {
  const value = kpi.value(current);
  const delta = previous === null ? null : formatDelta(kpi, value, kpi.value(previous));
  return (
    <div className="kpi" data-testid={`delivery-kpi-${kpi.label}`}>
      <div className="k">{kpi.label}</div>
      <div className="v">{formatValue(kpi.kind, value)}</div>
      <div className="d">
        {delta && (
          <>
            <span className={delta.better === null ? undefined : delta.better ? "up" : "down"}>{delta.text}</span>{" "}
          </>
        )}
        {COMPARED_WITH[period]}
      </div>
      <Sparkline values={slots.map(kpi.value)} />
    </div>
  );
}

/** The slots' values as a line; a slot without a value leaves a gap in the points, not a zero. */
export function Sparkline({ values }: { values: Array<number | null> }) {
  const points = values
    .map((v, i) => (v === null ? null : { x: values.length > 1 ? (i / (values.length - 1)) * 100 : 50, v }))
    .filter((p): p is { x: number; v: number } => p !== null);
  if (points.length === 0) return <svg viewBox="0 0 100 26" aria-hidden="true" />;
  const max = Math.max(...points.map((p) => p.v));
  const min = Math.min(...points.map((p) => p.v));
  const line = points.map((p) => `${p.x.toFixed(1)},${(24 - ((p.v - min) / (max - min || 1)) * 20).toFixed(1)}`).join(" ");
  return (
    <svg viewBox="0 0 100 26" preserveAspectRatio="none" aria-hidden="true" data-testid="delivery-spark">
      <polyline points={line} fill="none" stroke="var(--accent)" strokeWidth="1.4" vectorEffect="non-scaling-stroke" />
    </svg>
  );
}
