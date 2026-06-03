"use client";

import { useCostRollup, type CostRollup } from "@/hooks/useCostRollup";
import type { OverviewSnapshot } from "@/types/hub-events";
import type { Kpi } from "@/components/system/RollupCards";

interface Props {
  overview: OverviewSnapshot | null;
}

// p0209c: the rollup card grid (RollupCards) reuses the same cost values this
// card renders, re-presented as the mockup's .kcard grid. Today / 7d / LLM
// calls (7d) come straight from useCostRollup — no new backend, no new
// aggregation; the windows the data doesn't carry (30d, calls-today,
// avg-per-run) are honestly omitted rather than faked.
export function costKpis(cost: CostRollup): Kpi[] {
  return [
    { label: "Today", value: `$${cost.today.toFixed(2)}`, testId: "kcard-cost-today" },
    { label: "7 days", value: `$${cost.week.toFixed(2)}`, testId: "kcard-cost-week" },
    { label: "LLM calls · 7d", value: cost.llmCalls.toLocaleString(), testId: "kcard-cost-calls-7d" },
  ];
}

export function CostRollupCard({ overview }: Props) {
  const cost = useCostRollup(overview);

  return (
    <section
      className="rounded-lg border border-stone-200 bg-white p-4"
      data-testid="cost-rollup-card"
    >
      <h2 className="mb-3 text-sm font-medium text-stone-700">LLM cost</h2>
      <dl className="grid grid-cols-3 gap-4 text-sm">
        <div data-testid="cost-today">
          <dt className="text-xs text-stone-500">Today</dt>
          <dd className="text-2xl font-medium tabular-nums text-stone-800">
            ${cost.today.toFixed(2)}
          </dd>
        </div>
        <div data-testid="cost-week">
          <dt className="text-xs text-stone-500">7 days</dt>
          <dd className="text-2xl font-medium tabular-nums text-stone-800">
            ${cost.week.toFixed(2)}
          </dd>
        </div>
        <div data-testid="cost-llm-calls">
          <dt className="text-xs text-stone-500">LLM calls (7d)</dt>
          <dd className="text-2xl font-medium tabular-nums text-stone-800">
            {cost.llmCalls}
          </dd>
        </div>
      </dl>
    </section>
  );
}
