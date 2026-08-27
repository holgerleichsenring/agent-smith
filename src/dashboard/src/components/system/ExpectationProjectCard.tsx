import type { ProjectExpectationMetrics } from "@/lib/expectationsApi";
import { percentOrDash } from "@/lib/expectationTotals";

// p0343d: one project's ratification record as the parity mock's .ecard — its
// counts, its two rates, and the per-month accepted tally where months exist.
// 2026-08-27-559e: its own file, so the criteria panel's view holds the states
// it can be in and nothing else.

export function ExpectationProjectCard({ metrics }: { metrics: ProjectExpectationMetrics }) {
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
            {c.total} negotiated · {c.verbatim} verbatim · {c.edited} edited · {c.rejected}{" "}
            rejected · {c.unratified} unratified
            {metrics.averageEditDistance !== null &&
              ` · avg edit distance ${Math.round(metrics.averageEditDistance)}`}
          </div>
        </div>
        <div className="ec-right">
          <span className="tybadge">
            hit rate{" "}
            <b data-testid={`expectations-hit-rate-${metrics.project}`}>
              {percentOrDash(metrics.expectationHitRate)}
            </b>
          </span>
          <span className="tybadge">
            first-PR{" "}
            <b data-testid={`expectations-acceptance-${metrics.project}`}>
              {percentOrDash(metrics.firstPrAcceptance)}
            </b>
          </span>
        </div>
      </div>
      {metrics.months.length > 0 && <MonthTally metrics={metrics} />}
    </div>
  );
}

function MonthTally({ metrics }: { metrics: ProjectExpectationMetrics }) {
  return (
    <div className="ec-body">
      <span className="msub mono">
        {metrics.months
          .map((m) => `${m.month}: ${m.counts.verbatim + m.counts.edited}/${m.counts.total} accepted`)
          .join(" · ")}
      </span>
    </div>
  );
}
