"use client";

import { useJobsHub } from "@/hooks/useJobsHub";
import { useCostRollup } from "@/hooks/useCostRollup";
import { costKpis } from "@/components/system/CostRollupCard";
import { activityKpis } from "@/components/system/TodayActivityCard";
import { PageHead } from "@/components/system/PageHead";
import { SystemMetricStrip } from "@/components/system/SystemMetricStrip";

// p0209c: the Cost / Today's-activity rollup views. Both reuse the existing
// rollup data sources (useCostRollup over the overview snapshot; the
// server-truth SystemActivitySnapshot) — restyle + relocate only, no new
// backend and no new aggregation. Prop-driven so it can be unit-tested with
// supplied values; RollupCardsView wires the live hooks.
// p0343d: parity re-dress — each rollup is a first-class page: .m-head title
// row + the mock's .health/.metric strip carrying the page's REAL numbers.
// The windows the data doesn't carry (30d, calls-today, avg-per-run) stay
// honestly omitted rather than faked.

export interface Kpi {
  label: string;
  value: string | number;
  testId?: string;
}

export type RollupView = "cost" | "today";

const META: Record<RollupView, { title: string; sub: string }> = {
  cost: {
    title: "Cost",
    sub: "LLM spend rolled up from the run ledger — today and the trailing 7 days.",
  },
  today: {
    title: "Today's activity",
    sub: "The last 24 hours of watch-loop work — server-truth counters, not client guesses.",
  },
};

export function RollupCards({ view, kpis }: { view: RollupView; kpis: Kpi[] }) {
  return (
    <section data-testid={`rollup-${view}`}>
      <PageHead title={META[view].title} sub={META[view].sub} />
      <SystemMetricStrip
        testId={`rollup-strip-${view}`}
        cells={kpis.map((kpi) => ({ label: kpi.label, value: kpi.value, testId: kpi.testId }))}
      />
    </section>
  );
}

export function RollupCardsView({ view }: { view: RollupView }) {
  const { overview, systemActivity } = useJobsHub();
  const cost = useCostRollup(overview);
  const kpis = view === "cost" ? costKpis(cost) : activityKpis(systemActivity);
  return <RollupCards view={view} kpis={kpis} />;
}
