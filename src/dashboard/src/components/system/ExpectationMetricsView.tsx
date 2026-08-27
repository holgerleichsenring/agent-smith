"use client";

import type { ExpectationMetrics } from "@/lib/expectationsApi";
import type { ExpectationRead } from "@/hooks/useExpectationMetrics";
import { SystemMetricStrip, type MetricCell } from "@/components/system/SystemMetricStrip";
import { SectionHead } from "@/components/system/SectionHead";
import { ExpectationProjectCard } from "@/components/system/ExpectationProjectCard";
import { percentOrDash, ratifiedCount, sumOutcomeCounts } from "@/lib/expectationTotals";
import { refusalIn } from "@/lib/apiResponse";
import { RefusalSurface } from "@/components/shell/RefusalSurface";

// p0329: expectation-hit-rate and first-PR-acceptance per project, derived from
// production ratification outcomes (p0328). Honest empty-state: until a
// negotiated run records a ratification there is NO number to show, and the
// view says so instead of rendering zeros as if they were measurements.
// p0343d: the overall KPIs render as the mock's .health strip (overall rates
// are exact sums of the per-project counts, no new aggregation semantics; avg
// edit distance is per-project data and only surfaces in the strip when a
// single project reports one).
// 2026-08-27-559e: it is the narrower of the Overview's two panels, and it
// takes the read rather than making it — the criteria card above it shows the
// same outcomes, and a read owned here would be a second request for a number
// the first already answered. Every state it can be in, empty and failed
// included, renders INSIDE the panel: an installation that has negotiated
// nothing loses a panel, not the bottom half of the page.

export function ExpectationMetricsView({ data, error }: ExpectationRead) {
  const refusal = refusalIn(error);
  return (
    <section className="ov-panel" data-testid="expectations-view">
      <SectionHead
        title="Criteria outcomes"
        sub="hit rate = drafts ratified verbatim; first-PR acceptance = PRs built on an accepted contract"
      />
      <div style={{ height: 14 }} />
      {refusal ? (
        <RefusalSurface refusal={refusal} surface="the expectation metrics" />
      ) : error ? (
        <div className="stateline err" data-testid="expectations-error">
          Failed to load expectation metrics: {error.message}
        </div>
      ) : !data ? (
        <div className="stateline" data-testid="expectations-loading">
          Loading expectation metrics…
        </div>
      ) : data.total === 0 ? (
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
      No ratification outcomes recorded yet. Expectation negotiation writes one outcome
      per fix-bug / add-feature run — metrics appear after the first negotiated run
      completes.
    </div>
  );
}

function PopulatedCriteria({ data }: { data: ExpectationMetrics }) {
  return (
    <>
      <SystemMetricStrip testId="expectations-kpis" cells={overallCells(data)} />
      <section>
        <SectionHead
          title="Per project"
          count={data.projects.length}
          sub="rates never render as 0% without a measurement"
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

// Overall rates from the exact per-project counts: hit rate = verbatim /
// human-ratified (total − unratified); first-PR acceptance = (verbatim +
// edited) / all negotiated. Both are the same definitions the backend applies
// per project — summed, not re-modeled.
function overallCells(data: ExpectationMetrics): MetricCell[] {
  const sum = sumOutcomeCounts(data.projects);
  const ratified = ratifiedCount(sum);
  const reporting = data.projects.filter((p) => p.averageEditDistance !== null);
  // Edit distance is per-project data: averaging averages would invent a
  // figure, so it surfaces here only when exactly one project reports one.
  const editDistance =
    reporting.length === 1 ? Math.round(reporting[0].averageEditDistance!) : null;
  return [
    { label: "Negotiated", value: sum.total, testId: "exp-metric-negotiated" },
    {
      label: "Hit rate",
      value: ratified > 0 ? percentOrDash(sum.verbatim / ratified) : "—",
      small: `${sum.verbatim} verbatim`,
      testId: "exp-metric-hit-rate",
    },
    {
      label: "First-PR acceptance",
      value: sum.total > 0 ? percentOrDash((sum.verbatim + sum.edited) / sum.total) : "—",
      testId: "exp-metric-acceptance",
    },
    {
      label: "Avg edit distance",
      value: editDistance ?? "—",
      small: editDistance === null && reporting.length > 1 ? "per project below" : undefined,
      testId: "exp-metric-edit-distance",
    },
  ];
}
