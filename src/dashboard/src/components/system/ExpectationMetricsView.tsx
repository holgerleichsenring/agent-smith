"use client";

import { useEffect, useState } from "react";
import {
  fetchExpectationMetrics,
  type ExpectationMetrics,
  type OutcomeCounts,
  type ProjectExpectationMetrics,
} from "@/lib/expectationsApi";
import { SystemMetricStrip, type MetricCell } from "@/components/system/SystemMetricStrip";

// p0329: the System → Expectations rollup — expectation-hit-rate and
// first-PR-acceptance per project, derived from production ratification
// outcomes (p0328). Honest empty-state: until a negotiated run records a
// ratification there is NO number to show, and the page says so instead of
// rendering zeros as if they were measurements.
// p0343d: parity re-dress — the page head moved to SystemView's .m-head; here
// the overall KPIs render as the mock's .health strip (overall rates are exact
// sums of the per-project counts, no new aggregation semantics; avg edit
// distance is per-project data and only surfaces in the strip when a single
// project reports one), and each project is an .ecard with its real rates.

export function ExpectationMetricsView() {
  const [data, setData] = useState<ExpectationMetrics | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    fetchExpectationMetrics(controller.signal)
      .then(setData)
      .catch((e: Error) => {
        if (e.name !== "AbortError") setError(e.message);
      });
    return () => controller.abort();
  }, []);

  return (
    <div data-testid="expectations-view">
      {error ? (
        <div className="stateline err" data-testid="expectations-error">
          Failed to load expectation metrics: {error}
        </div>
      ) : !data ? (
        <div className="stateline" data-testid="expectations-loading">
          Loading expectation metrics…
        </div>
      ) : data.total === 0 ? (
        <div className="empty" data-testid="expectations-empty">
          <div className="ei" aria-hidden>
            ✓
          </div>
          No ratification outcomes recorded yet. Expectation negotiation writes one
          outcome per fix-bug / add-feature run — metrics appear after the first
          negotiated run completes.
        </div>
      ) : (
        <>
          <SystemMetricStrip testId="expectations-kpis" cells={overallCells(data)} />
          <section>
            <div className="section-head">
              <h2>Per project</h2>
              <span className="cnt">{data.projects.length}</span>
              <span className="sh-sub">rates never render as 0% without a measurement</span>
            </div>
            <div style={{ height: 14 }} />
            <div className="list">
              {data.projects.map((p) => (
                <ProjectCard key={p.project} metrics={p} />
              ))}
            </div>
          </section>
        </>
      )}
    </div>
  );
}

const percent = (value: number | null): string =>
  value === null ? "—" : `${Math.round(value * 100)}%`;

// Overall rates from the exact per-project counts: hit rate = verbatim /
// human-ratified (total − unratified); first-PR acceptance = (verbatim +
// edited) / all negotiated. Both are the same definitions the backend applies
// per project — summed, not re-modeled.
function overallCells(data: ExpectationMetrics): MetricCell[] {
  const sum = data.projects.reduce<OutcomeCounts>(
    (acc, p) => ({
      total: acc.total + p.counts.total,
      verbatim: acc.verbatim + p.counts.verbatim,
      edited: acc.edited + p.counts.edited,
      rejected: acc.rejected + p.counts.rejected,
      unratified: acc.unratified + p.counts.unratified,
    }),
    { total: 0, verbatim: 0, edited: 0, rejected: 0, unratified: 0 },
  );
  const ratified = sum.total - sum.unratified;
  const withEditDistance = data.projects.filter((p) => p.averageEditDistance !== null);
  const editDistance =
    withEditDistance.length === 1 ? Math.round(withEditDistance[0].averageEditDistance!) : null;
  return [
    { label: "Negotiated", value: sum.total, testId: "exp-metric-negotiated" },
    {
      label: "Hit rate",
      value: ratified > 0 ? percent(sum.verbatim / ratified) : "—",
      small: `${sum.verbatim} verbatim`,
      testId: "exp-metric-hit-rate",
    },
    {
      label: "First-PR acceptance",
      value: sum.total > 0 ? percent((sum.verbatim + sum.edited) / sum.total) : "—",
      testId: "exp-metric-acceptance",
    },
    {
      label: "Avg edit distance",
      value: editDistance ?? "—",
      small: editDistance === null && withEditDistance.length > 1 ? "per project below" : undefined,
      testId: "exp-metric-edit-distance",
    },
  ];
}

function ProjectCard({ metrics }: { metrics: ProjectExpectationMetrics }) {
  const c = metrics.counts;
  return (
    <div className="ecard" data-testid={`expectations-project-${metrics.project}`}>
      <div className="ec-top">
        <div className="ec-ic" aria-hidden>
          ✓
        </div>
        <div style={{ minWidth: 0 }}>
          <div className="ec-name">{metrics.project}</div>
          <div className="ec-sub">
            {c.total} negotiated · {c.verbatim} verbatim · {c.edited} edited ·{" "}
            {c.rejected} rejected · {c.unratified} unratified
            {metrics.averageEditDistance !== null &&
              ` · avg edit distance ${Math.round(metrics.averageEditDistance)}`}
          </div>
        </div>
        <div className="ec-right">
          <span className="tybadge">
            hit rate{" "}
            <b data-testid={`expectations-hit-rate-${metrics.project}`}>
              {percent(metrics.expectationHitRate)}
            </b>
          </span>
          <span className="tybadge">
            first-PR{" "}
            <b data-testid={`expectations-acceptance-${metrics.project}`}>
              {percent(metrics.firstPrAcceptance)}
            </b>
          </span>
        </div>
      </div>
      {metrics.months.length > 0 && (
        <div className="ec-body">
          <span className="msub mono">
            {metrics.months
              .map((m) => `${m.month}: ${m.counts.verbatim + m.counts.edited}/${m.counts.total} accepted`)
              .join(" · ")}
          </span>
        </div>
      )}
    </div>
  );
}
