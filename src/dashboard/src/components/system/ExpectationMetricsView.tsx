"use client";

import type { CriteriaMet } from "@/lib/expectationsApi";
import type { ExpectationRead } from "@/hooks/useExpectationMetrics";
import { SystemMetricStrip, type MetricCell } from "@/components/system/SystemMetricStrip";
import { SectionHead } from "@/components/system/SectionHead";
import { ExpectationProjectCard } from "@/components/system/ExpectationProjectCard";
import { percentOrDash } from "@/lib/expectationTotals";
import { refusalIn } from "@/lib/apiResponse";
import { RefusalSurface } from "@/components/shell/RefusalSurface";

// The criteria finished coding runs were judged on, overall and per project, with the
// operator's overrules applied. It takes the read rather than making it — the Criteria
// met card above it shows the same counts. Every state it can be in, empty and failed
// included, renders INSIDE the panel: an installation with no judged run loses a panel,
// not the bottom half of the page.

export function ExpectationMetricsView({ data, error }: ExpectationRead) {
  const refusal = refusalIn(error);
  return (
    <section className="ov-panel" data-testid="expectations-view">
      <SectionHead
        title="Criteria outcomes"
        sub="met of met, unmet and unproven; not applicable counts in neither"
      />
      <div style={{ height: 14 }} />
      {refusal ? (
        <RefusalSurface refusal={refusal} surface="the criteria outcomes" />
      ) : error ? (
        <div className="stateline err" data-testid="expectations-error">
          Failed to load criteria outcomes: {error.message}
        </div>
      ) : !data ? (
        <div className="stateline" data-testid="expectations-loading">
          Loading criteria outcomes…
        </div>
      ) : data.runs === 0 ? (
        <EmptyCriteria />
      ) : (
        <PopulatedCriteria data={data} />
      )}
    </section>
  );
}

function EmptyCriteria() {
  return (
    <div className="empty" data-testid="expectations-empty">
      <div className="ei" aria-hidden>
        ✓
      </div>
      No coding run has been judged on its criteria yet. Outcomes appear once a code run finishes
      with an acceptance account.
    </div>
  );
}

function PopulatedCriteria({ data }: { data: CriteriaMet }) {
  return (
    <>
      <SystemMetricStrip testId="expectations-kpis" cells={overallCells(data)} />
      <section>
        <SectionHead
          title="Per project"
          count={data.projects.length}
          sub="a share never renders as 0% without a measurement"
        />
        <div style={{ height: 14 }} />
        <div className="list">
          {data.projects.map((p) => (
            <ExpectationProjectCard key={p.project} metrics={p} />
          ))}
        </div>
      </section>
    </>
  );
}

// The overall counts come from the server, summed over the same runs as the projects.
function overallCells({ runs, counts }: CriteriaMet): MetricCell[] {
  return [
    { label: "Runs", value: runs, testId: "exp-metric-runs" },
    {
      label: "Met",
      value: percentOrDash(counts.share),
      small: `${counts.met} of ${counts.judged}`,
      testId: "exp-metric-share",
    },
    {
      label: "Unproven",
      value: counts.unproven,
      small: `${counts.unmet} unmet`,
      testId: "exp-metric-unproven",
    },
    {
      label: "Overruled",
      value: counts.overruled,
      small: counts.staleOverrules > 0 ? `${counts.staleOverrules} stale` : undefined,
      hot: counts.staleOverrules > 0,
      testId: "exp-metric-overruled",
    },
  ];
}
