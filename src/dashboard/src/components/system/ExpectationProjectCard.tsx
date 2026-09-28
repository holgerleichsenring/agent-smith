import type { ProjectCriteria } from "@/lib/expectationsApi";
import { judgedSentence, overruleSentence, percentOrDash } from "@/lib/expectationTotals";

// One project's criteria as the parity mock's .ecard: its counts, its share, and the
// share per month of the runs' finish where months exist.

export function ExpectationProjectCard({ metrics }: { metrics: ProjectCriteria }) {
  const c = metrics.counts;
  const overrules = overruleSentence(c);
  return (
    <div className="ecard" data-testid={`expectations-project-${metrics.project}`}>
      <div className="ec-top">
        <div className="ec-ic" aria-hidden>
          ✓
        </div>
        <div style={{ minWidth: 0 }}>
          <div className="ec-name">{metrics.project}</div>
          <div className="ec-sub">
            {metrics.runs} runs · {judgedSentence(c)} · {c.unmet} unmet · {c.unproven} unproven
            {overrules !== null && ` · ${overrules}`}
          </div>
        </div>
        <div className="ec-right">
          <span className="tybadge">
            met{" "}
            <b data-testid={`expectations-share-${metrics.project}`}>{percentOrDash(c.share)}</b>
          </span>
        </div>
      </div>
      {metrics.months.length > 0 && <MonthTally metrics={metrics} />}
    </div>
  );
}

function MonthTally({ metrics }: { metrics: ProjectCriteria }) {
  return (
    <div className="ec-body">
      <span className="msub mono" data-testid={`expectations-months-${metrics.project}`}>
        {metrics.months
          .map((m) => `${m.month}: ${m.counts.met}/${m.counts.judged} met`)
          .join(" · ")}
      </span>
    </div>
  );
}
