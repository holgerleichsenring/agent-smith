import type { ReactNode } from "react";
import { cn } from "@/lib/utils";
import type { MissionMetrics } from "./missionBuckets";

// p0343: the mission-control metric strip. Every value is derived client-side
// from the same run list the sections render, so the strip and the sections can
// never disagree. "Needs you" glows amber (mock .metric.hot) only when non-zero.
// p0343c (pixel identity): emits the runs-list.html .health/.metric DOM
// verbatim — 5 honest cells (the mock's capacity cell has no live data source
// on the overview payload and is omitted rather than faked).

interface Cell {
  label: string;
  value: ReactNode;
  small?: ReactNode;
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
      value: metrics.finishedToday,
      small: `${metrics.okToday} ✓ · ${metrics.failToday} ✗`,
      testId: "metric-finished",
    },
    {
      label: "Cost today",
      value: <span className="num">{money(metrics.costTodayUsd)}</span>,
      testId: "metric-cost",
    },
  ];

  return (
    <div className="health" data-testid="mission-metric-strip">
      {cells.map((cell) => (
        <div
          key={cell.testId}
          data-testid={cell.testId}
          className={cn("metric", cell.hot && "hot")}
        >
          <span className="k">{cell.label}</span>
          <span className="v">
            {cell.value}
            {cell.small && <small>{cell.small}</small>}
          </span>
        </div>
      ))}
    </div>
  );
}
