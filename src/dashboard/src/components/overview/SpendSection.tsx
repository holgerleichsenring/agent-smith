"use client";

import type { CostRollup } from "@/hooks/useCostRollup";
import type { SpendSlice } from "@/hooks/useSpendBreakdown";
import { SectionHead } from "@/components/system/SectionHead";
import { SystemMetricStrip, type MetricCell } from "@/components/system/SystemMetricStrip";
import { SpendSlices } from "@/components/overview/SpendSlices";

// 2026-08-27-7463: the Overview's spend reading — the three figures the Cost
// rollup page read, and under them the breakdown of which work the trailing
// week's money went to. Both come from the run list the page already holds:
// the section takes values, mounts no hub of its own, and cannot therefore
// disagree with the sections beside it.

/** The three cost figures, in the cells and testids the Cost page established. */
export function costCells(cost: CostRollup): MetricCell[] {
  return [
    { label: "Today", value: `$${cost.today.toFixed(2)}`, testId: "kcard-cost-today" },
    { label: "7 days", value: `$${cost.week.toFixed(2)}`, testId: "kcard-cost-week" },
    {
      label: "LLM calls · 7d",
      value: cost.llmCalls.toLocaleString(),
      testId: "kcard-cost-calls-7d",
    },
  ];
}

export function SpendSection({
  cost,
  slices,
  ready,
}: {
  cost: CostRollup;
  slices: SpendSlice[];
  ready: boolean;
}) {
  return (
    <section data-testid="overview-spend">
      <SectionHead title="Spend" sub="LLM spend rolled up from the run ledger" />
      <div style={{ height: 14 }} />
      <SystemMetricStrip testId="overview-spend-strip" cells={costCells(cost)} />
      <section>
        <SectionHead
          title="Where the money went"
          count={ready ? slices.length : undefined}
          sub="trailing 7 days · sums to the 7-day figure above"
        />
        <div style={{ height: 14 }} />
        <SpendSlices slices={slices} ready={ready} />
      </section>
    </section>
  );
}
