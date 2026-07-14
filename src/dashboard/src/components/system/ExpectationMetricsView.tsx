"use client";

import { useEffect, useState } from "react";
import {
  fetchExpectationMetrics,
  type ExpectationMetrics,
  type ProjectExpectationMetrics,
} from "@/lib/expectationsApi";
import { Card } from "@/components/ui/Card";
import { SectionLabel } from "@/components/ui/SectionLabel";

// p0329: the System → Expectations rollup — expectation-hit-rate and
// first-PR-acceptance per project, derived from production ratification
// outcomes (p0328). Honest empty-state: until a negotiated run records a
// ratification there is NO number to show, and the page says so instead of
// rendering zeros as if they were measurements.

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
    <div className="flex h-full flex-col overflow-y-auto" data-testid="expectations-view">
      <div className="content-shell pb-0">
        <SectionLabel>Expectations</SectionLabel>
        <p className="mt-1 dsh-body text-stone-500">
          How often the drafted expectation hit the mark: hit rate is the share of
          drafts humans ratified verbatim; first-PR acceptance is the share of runs
          whose PR was built against a human-accepted contract.
        </p>
      </div>

      {error ? (
        <div className="content-shell dsh-body text-rose-700" data-testid="expectations-error">
          Failed to load expectation metrics: {error}
        </div>
      ) : !data ? (
        <div className="content-shell dsh-body text-stone-400" data-testid="expectations-loading">
          Loading expectation metrics…
        </div>
      ) : data.total === 0 ? (
        <div className="content-shell pt-4 dsh-body text-stone-500" data-testid="expectations-empty">
          No ratification outcomes recorded yet. Expectation negotiation writes one
          outcome per fix-bug / add-feature run — metrics appear after the first
          negotiated run completes.
        </div>
      ) : (
        <div className="content-shell space-y-3 pt-5">
          {data.projects.map((p) => (
            <ProjectCard key={p.project} metrics={p} />
          ))}
        </div>
      )}
    </div>
  );
}

const percent = (value: number | null): string =>
  value === null ? "—" : `${Math.round(value * 100)}%`;

function ProjectCard({ metrics }: { metrics: ProjectExpectationMetrics }) {
  const c = metrics.counts;
  return (
    <Card className="px-4 py-3" data-testid={`expectations-project-${metrics.project}`}>
      <div className="flex items-baseline gap-4">
        <span className="dsh-body font-semibold text-stone-800">{metrics.project}</span>
        <span className="ml-auto dsh-body text-stone-500">
          hit rate{" "}
          <span
            className="font-bold tabular-nums text-stone-800"
            data-testid={`expectations-hit-rate-${metrics.project}`}
          >
            {percent(metrics.expectationHitRate)}
          </span>
        </span>
        <span className="dsh-body text-stone-500">
          first-PR acceptance{" "}
          <span
            className="font-bold tabular-nums text-stone-800"
            data-testid={`expectations-acceptance-${metrics.project}`}
          >
            {percent(metrics.firstPrAcceptance)}
          </span>
        </span>
      </div>
      <p className="mt-1.5 dsh-mono text-stone-500">
        {c.total} negotiated · {c.verbatim} verbatim · {c.edited} edited ·{" "}
        {c.rejected} rejected · {c.unratified} unratified
        {metrics.averageEditDistance !== null &&
          ` · avg edit distance ${Math.round(metrics.averageEditDistance)}`}
      </p>
      {metrics.months.length > 0 && (
        <p className="mt-1 dsh-mono text-stone-400">
          {metrics.months
            .map((m) => `${m.month}: ${m.counts.verbatim + m.counts.edited}/${m.counts.total} accepted`)
            .join(" · ")}
        </p>
      )}
    </Card>
  );
}
