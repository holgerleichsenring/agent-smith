"use client";

import type { CriteriaMet } from "@/lib/expectationsApi";
import type { ExpectationRead } from "@/hooks/useExpectationMetrics";
import { SystemMetricStrip, type MetricCell } from "@/components/system/SystemMetricStrip";
import { SectionHead } from "@/components/system/SectionHead";
import { ExpectationProjectCard } from "@/components/system/ExpectationProjectCard";
import { percentOrDash } from "@/lib/expectationTotals";
import { refusalIn } from "@/lib/apiResponse";
import { RefusalSurface } from "@/components/shell/RefusalSurface";
import { NoProjectReach } from "@/components/shell/NoProjectReach";
import { useT } from "@/hooks/useT";
import type { T } from "@/lib/i18n/translate";

// The criteria finished coding runs were judged on, overall and per project, with the
// operator's overrules applied. It takes the read rather than making it — the Criteria
// met card above it shows the same counts. Every state it can be in, empty and failed
// included, renders INSIDE the panel: an installation with no judged run loses a panel,
// not the bottom half of the page.

export function ExpectationMetricsView({ data, error }: ExpectationRead) {
  const t = useT();
  const refusal = refusalIn(error);
  return (
    <section className="ov-panel" data-testid="expectations-view">
      <SectionHead
        title={t("system.criteria.title")}
        sub={t("system.criteria.sub")}
      />
      <div style={{ height: 14 }} />
      {refusal ? (
        <RefusalSurface refusal={refusal} surface={t("system.criteria.surface")} />
      ) : error ? (
        <div className="stateline err" data-testid="expectations-error">
          {t("system.criteria.failed", { message: error.message })}
        </div>
      ) : !data ? (
        <div className="stateline" data-testid="expectations-loading">
          {t("system.criteria.loading")}
        </div>
      ) : data.runs === 0 ? (
        <NoProjectReach><EmptyCriteria /></NoProjectReach>
      ) : (
        <PopulatedCriteria data={data} />
      )}
    </section>
  );
}

function EmptyCriteria() {
  const t = useT();
  return (
    <div className="empty" data-testid="expectations-empty">
      <div className="ei" aria-hidden>
        ✓
      </div>
      {t("system.criteria.empty")}
    </div>
  );
}

function PopulatedCriteria({ data }: { data: CriteriaMet }) {
  const t = useT();
  return (
    <>
      <SystemMetricStrip testId="expectations-kpis" cells={overallCells(data, t)} />
      <section>
        <SectionHead
          title={t("system.criteria.perProject")}
          count={data.projects.length}
          sub={t("system.criteria.perProjectSub")}
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
function overallCells({ runs, counts }: CriteriaMet, t: T): MetricCell[] {
  return [
    { label: t("system.criteria.runs"), value: runs, testId: "exp-metric-runs" },
    {
      label: t("system.criteria.met"),
      value: percentOrDash(counts.share),
      small: t("system.criteria.metOf", { met: counts.met, judged: counts.judged }),
      testId: "exp-metric-share",
    },
    {
      label: t("system.criteria.unproven"),
      value: counts.unproven,
      small: t("system.criteria.unmetCount", { count: counts.unmet }),
      testId: "exp-metric-unproven",
    },
    {
      label: t("system.criteria.overruled"),
      value: counts.overruled,
      small: counts.staleOverrules > 0 ? t("system.criteria.staleCount", { count: counts.staleOverrules }) : undefined,
      hot: counts.staleOverrules > 0,
      testId: "exp-metric-overruled",
    },
  ];
}
