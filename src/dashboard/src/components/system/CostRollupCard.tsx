"use client";

import { useCostRollup } from "@/hooks/useCostRollup";
import type { RunEvent } from "@/types/hub-events";

interface Props {
  events: readonly RunEvent[];
}

export function CostRollupCard({ events }: Props) {
  const cost = useCostRollup(events);

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
