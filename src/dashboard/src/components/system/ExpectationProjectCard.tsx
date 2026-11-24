"use client";

import type { ProjectCriteria } from "@/lib/expectationsApi";
import { judgedSentence, overruleSentence, percentOrDash } from "@/lib/expectationTotals";
import { useT } from "@/hooks/useT";

// One project's criteria as the parity mock's .ecard: its counts, its share, and the
// share per month of the runs' finish where months exist.

export function ExpectationProjectCard({ metrics }: { metrics: ProjectCriteria }) {
  const t = useT();
  const c = metrics.counts;
  const overrules = overruleSentence(c, t);
  return (
    <div className="ecard" data-testid={`expectations-project-${metrics.project}`}>
      <div className="ec-top">
        <div className="ec-ic" aria-hidden>
          ✓
        </div>
        <div style={{ minWidth: 0 }}>
          <div className="ec-name">{metrics.project}</div>
          <div className="ec-sub">
            {t("system.criteria.projectRuns", { count: metrics.runs })} · {judgedSentence(c, t)} ·{" "}
            {t("system.criteria.unmetCount", { count: c.unmet })} · {t("system.criteria.unprovenCount", { count: c.unproven })}
            {overrules !== null && ` · ${overrules}`}
          </div>
        </div>
        <div className="ec-right">
          <span className="tybadge">
            {t("system.criteria.metBadge")}{" "}
            <b data-testid={`expectations-share-${metrics.project}`}>{percentOrDash(c.share)}</b>
          </span>
        </div>
      </div>
      {metrics.months.length > 0 && <MonthTally metrics={metrics} />}
    </div>
  );
}

function MonthTally({ metrics }: { metrics: ProjectCriteria }) {
  const t = useT();
  return (
    <div className="ec-body">
      <span className="msub mono" data-testid={`expectations-months-${metrics.project}`}>
        {metrics.months
          .map((m) => t("system.criteria.month", { month: m.month, met: m.counts.met, judged: m.counts.judged }))
          .join(" · ")}
      </span>
    </div>
  );
}
