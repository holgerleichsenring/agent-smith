"use client";

import type { RunStatistics } from "@/lib/runStoryApi";
import type { RunAcceptance } from "@/types/hub-events";
import { formatChars, formatMs } from "./callSeries";
import { WorkBreakdown } from "./WorkBreakdown";
import { useDateLocale, useT } from "@/hooks/useT";
import type { T } from "@/lib/i18n/translate";

// p0423b: what the ticket cost, read off the trail. Specs, criteria, duration per ticket,
// calls and their sizes — every one of them a fold over the recorded events, never a
// counter kept beside them. A counter and the events it counts are two answers to one
// question, and they drift.

export interface TicketStatisticsProps {
  statistics: RunStatistics;
  acceptance: RunAcceptance | null;
  /** p0341h: a running run has not been accounted for yet, which is not the same as having
   *  nothing to account for. Saying "none ratified" mid-run reads as a verdict. */
  running?: boolean;
}

// 2026-10-12-6bfad: an account nobody could take is no criterion; with nothing else to count
// it is what the figure says, rather than "none ratified".
function criteriaNote(t: T, counted: number, unmeasured: number, running?: boolean): string {
  if (counted > 0) return unmeasured > 0 ? t("runs.why.criteria.provenUnmeasured") : t("runs.why.criteria.proven");
  if (unmeasured > 0) return t("runs.why.criteria.notMeasured");
  return running ? t("runs.why.criteria.notYet") : t("runs.why.criteria.noneRatified");
}

export function TicketStatistics({ statistics, acceptance, running }: TicketStatisticsProps) {
  const t = useT();
  const locale = useDateLocale();
  const totals = statistics.totals;
  const criteria = acceptance?.criteria ?? [];
  const met = criteria.filter((c) => c.status === "met").length;
  const unmeasured = acceptance?.unmeasured?.length ?? 0;
  const work = statistics.work;

  return (
    <section className="card" data-testid="ticket-statistics">
      <div className="card-h">
        <h3>{t("runs.why.cost.title")}</h3>
        <span className="badge neu">{t("runs.why.cost.derived")}</span>
      </div>
      <div className="card-b">
        {work && (
          <>
            <WorkBreakdown
              title={t("runs.why.cost.pipeline")}
              subtitle={t("runs.why.cost.steps")}
              kinds={work.pipeline}
              testId="work-pipeline"
            />
            <WorkBreakdown
              title={t("runs.why.cost.sandbox")}
              subtitle={t("runs.why.cost.commands")}
              kinds={work.sandbox}
              testId="work-sandbox"
            />
          </>
        )}
        <div className="health health-plain">
          <Metric label={t("runs.why.cost.specs")} value={String(statistics.specs.length)} />
          <Metric
            label={t("runs.why.cost.criteria")}
            value={criteria.length > 0 ? `${met}/${criteria.length}` : "—"}
            note={criteriaNote(t, criteria.length, unmeasured, running)}
          />
          {/* p0341h: the sum of the steps is not the wall clock — sandboxes run in parallel,
              so on run a98c these were 39m against 65m of elapsed time. The run header owns
              the wall clock; this panel says exactly what it measured. */}
          <Metric label={t("runs.why.cost.timeInSteps")} value={formatMs(statistics.totalDurationMs)} />
          <Metric
            label={t("runs.why.cost.calls")}
            value={String(totals.calls)}
            note={totals.failedCalls > 0 ? t("runs.why.cost.didNotEndWell", { count: totals.failedCalls }) : undefined}
          />
          <Metric label={t("runs.why.cost.largestPrompt")} value={formatChars(totals.largestPromptChars, locale)} />
          <Metric label={t("runs.why.cost.timeInCalls")} value={formatMs(totals.totalDurationMs)} />
          {/* p0341h: shown only when it HAPPENED. A metric that can only ever read zero —
              because the events feeding it never reach the trail — teaches a reader to stop
              looking at the panel. A real cut is worth a row. */}
          {totals.toolCharsNeverDelivered > 0 && (
            <Metric
              label={t("runs.why.cost.neverDelivered")}
              value={formatChars(totals.toolCharsNeverDelivered, locale)}
              note={t("runs.why.cost.boundCut")}
            />
          )}
          <Metric label={t("runs.why.cost.retries")} value={String(totals.retries)} />
        </div>
        {statistics.truncated && (
          <p className="hint" data-testid="ticket-statistics-truncated">
            {t("runs.why.cost.truncated")}
          </p>
        )}
      </div>
    </section>
  );
}

function Metric({ label, value, note }: { label: string; value: string; note?: string }) {
  return (
    <div className="metric">
      <span className="k">{label}</span>
      <span className="v num">
        {value}
        {note && <small> {note}</small>}
      </span>
    </div>
  );
}
