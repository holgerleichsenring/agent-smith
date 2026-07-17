import type { ReactNode } from "react";
import { cn } from "@/lib/utils";
import type { MissionMetrics } from "./missionBuckets";

// p0343: the mission-control metric strip — a joined field-block row (label over
// value, hairline-divided) in the "metric" content mode. Every value is derived
// client-side from the same run list the sections render, so the strip and the
// sections can never disagree. Green stays reserved (Smith-green = the CTA and
// the success signal); "needs you" glows amber only when it is non-zero.

interface Cell {
  label: string;
  value: ReactNode;
  hot?: boolean;
  testId: string;
}

function money(usd: number): string {
  return `$${usd.toFixed(2)}`;
}

export function MetricStrip({ metrics }: { metrics: MissionMetrics }) {
  const cells: Cell[] = [
    { label: "Needs you", value: metrics.needsYou, hot: metrics.needsYou > 0, testId: "metric-needs-you" },
    { label: "Running", value: metrics.running, testId: "metric-running" },
    { label: "Queued", value: metrics.queued, testId: "metric-queued" },
    {
      label: "Finished today",
      value: (
        <span className="flex items-baseline gap-2">
          {metrics.finishedToday}
          <span className="dsh-label font-normal text-stone-400">
            {metrics.okToday} ✓ · {metrics.failToday} ✕
          </span>
        </span>
      ),
      testId: "metric-finished",
    },
    { label: "Cost today", value: <span className="font-mono">{money(metrics.costTodayUsd)}</span>, testId: "metric-cost" },
  ];

  return (
    <div
      data-testid="mission-metric-strip"
      className="grid grid-cols-2 overflow-hidden rounded-md border border-stone-200 sm:grid-cols-5"
    >
      {cells.map((cell, i) => (
        <div
          key={cell.testId}
          data-testid={cell.testId}
          className={cn(
            "border-stone-200 p-3",
            i > 0 && "border-l",
            i < 4 && "max-sm:border-b",
            cell.hot ? "bg-amber-50" : "bg-[var(--color-canvas-soft)]",
          )}
        >
          <div className="eyebrow-uppercase text-stone-400">{cell.label}</div>
          <div
            className={cn(
              "mt-1 dsh-h2 font-semibold tabular-nums",
              cell.hot ? "text-amber-700" : "text-stone-900",
            )}
          >
            {cell.value}
          </div>
        </div>
      ))}
    </div>
  );
}
