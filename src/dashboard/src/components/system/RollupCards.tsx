"use client";

import { useJobsHub } from "@/hooks/useJobsHub";
import { useCostRollup } from "@/hooks/useCostRollup";
import { costKpis } from "@/components/system/CostRollupCard";
import { activityKpis } from "@/components/system/TodayActivityCard";

// p0209c: the Cost / Today's-activity rollup views as the mockup's .kcard KPI
// grid (a faint label + a big value, three-up). Both views reuse the existing
// rollup data sources (useCostRollup over the overview snapshot; the
// server-truth SystemActivitySnapshot) — restyle + relocate only, no new
// backend and no new aggregation. The grid is prop-driven so it can be
// unit-tested with supplied values; RollupCardsView wires the live hooks.

export interface Kpi {
  label: string;
  value: string | number;
  testId?: string;
}

export type RollupView = "cost" | "today";

const HEADINGS: Record<RollupView, string> = {
  cost: "LLM cost",
  today: "Today's activity",
};

export function RollupCards({ view, kpis }: { view: RollupView; kpis: Kpi[] }) {
  return (
    <section data-testid={`rollup-${view}`} className="px-7 py-6">
      <h2 className="mb-4 text-sm font-medium text-stone-700">{HEADINGS[view]}</h2>
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {kpis.map((kpi) => (
          <div
            key={kpi.label}
            data-testid={kpi.testId}
            className="rounded-lg border border-stone-200 px-5 py-[18px]"
          >
            <div className="mb-[7px] dsh-body text-stone-500">{kpi.label}</div>
            <div className="dsh-h1 font-bold tracking-tight tabular-nums text-stone-800">
              {kpi.value}
            </div>
          </div>
        ))}
      </div>
    </section>
  );
}

export function RollupCardsView({ view }: { view: RollupView }) {
  const { overview, systemActivity } = useJobsHub();
  const cost = useCostRollup(overview);
  const kpis = view === "cost" ? costKpis(cost) : activityKpis(systemActivity);
  return <RollupCards view={view} kpis={kpis} />;
}
