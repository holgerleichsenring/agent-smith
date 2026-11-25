"use client";

import type { ReactNode } from "react";

import { cn } from "@/lib/utils";
import { useT } from "@/hooks/useT";
import type { RunSpend } from "@/lib/runSpendApi";
import type { MissionMetrics } from "./missionBuckets";

// p0343: the mission-control metric strip. Every value is derived client-side
// from the same run list the sections render, so the strip and the sections can
// never disagree. "Waiting for input" glows amber (mock .metric.hot) only when non-zero.
// p0343c (pixel identity): emits the runs-list.html .health/.metric DOM
// verbatim — 5 honest cells (the mock's capacity cell has no live data source
// on the overview payload and is omitted rather than faked).
// 2026-11-24-0fbfb: Cost today is the server's spend rollup since the viewer's local midnight, work and system apart,
// whatever the list shows; "—" until it answered.

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

export function MetricStrip({ metrics, spend }: { metrics: MissionMetrics; spend: RunSpend | null }) {
  const t = useT();
  const cells: Cell[] = [
    { label: t("runs.mission.needsYou"), value: metrics.needsYou, hot: metrics.needsYou > 0, testId: "metric-needs-you" },
    { label: t("runs.mission.running"), value: metrics.running, testId: "metric-running" },
    { label: t("runs.mission.queued"), value: metrics.queued, testId: "metric-queued" },
    {
      label: t("runs.mission.finishedToday"),
      value: metrics.finishedToday,
      small: `${metrics.okToday} ✓ · ${metrics.failToday} ✗`,
      testId: "metric-finished",
    },
    {
      label: t("runs.mission.costToday"),
      value: spend ? <span className="num">{money(spend.totalUsd)}</span> : "—",
      small: spend ? t("runs.mission.costSplit", { work: money(spend.work.costUsd), system: money(spend.system.costUsd) }) : undefined,
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
