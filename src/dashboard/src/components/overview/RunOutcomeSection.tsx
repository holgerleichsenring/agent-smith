"use client";

import type { RunOutcomes } from "@/lib/runOutcomes";
import { SectionHead } from "@/components/system/SectionHead";
import { SystemMetricStrip, type MetricCell } from "@/components/system/SystemMetricStrip";

// 2026-08-27-7463: what came back — the buckets the rail counts, and the split
// of how the finished ones ended. The reading no page carried before, which is
// the room the deleted today rollup paid for.

export function outcomeCells(outcomes: RunOutcomes): MetricCell[] {
  return [
    { label: "Runs", value: outcomes.total, testId: "kcard-runs-total" },
    {
      label: "Needs you",
      value: outcomes.needsYou,
      hot: outcomes.needsYou > 0,
      testId: "kcard-runs-needs-you",
    },
    { label: "Running", value: outcomes.running, testId: "kcard-runs-running" },
    { label: "Queued", value: outcomes.queued, testId: "kcard-runs-queued" },
    { label: "Succeeded", value: outcomes.succeeded, testId: "kcard-runs-succeeded" },
    { label: "Failed", value: outcomes.failed, testId: "kcard-runs-failed" },
    { label: "Cancelled", value: outcomes.cancelled, testId: "kcard-runs-cancelled" },
  ];
}

export function RunOutcomeSection({
  outcomes,
  ready,
}: {
  outcomes: RunOutcomes;
  ready: boolean;
}) {
  return (
    <section data-testid="overview-runs">
      <SectionHead
        title="Runs by outcome"
        count={ready ? outcomes.total : undefined}
        sub="the same buckets the rail counts, with the finished ones split by how they ended"
      />
      <div style={{ height: 14 }} />
      {ready ? (
        <SystemMetricStrip testId="overview-runs-strip" cells={outcomeCells(outcomes)} />
      ) : (
        <div className="stateline" data-testid="overview-runs-loading">
          Reading the run list…
        </div>
      )}
    </section>
  );
}
