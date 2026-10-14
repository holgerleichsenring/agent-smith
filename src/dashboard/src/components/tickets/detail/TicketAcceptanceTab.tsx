"use client";

import { useTicketSeries } from "@/lib/tickets/TicketPageStore";
import type { TicketSeriesCriterion, TicketSeriesUnattributed } from "@/types/tickets";
import { acceptanceTally, criterionRow, isRunRecord, overruleWords, RUN_RECORD_LINE, verdictRow } from "./specWords";

// 2026-10-13-9a59r: acceptance PER SPEC — each spec's done list (a run's acceptance contract is the current
// spec's), every criterion with its mark and the attempt that verified it, "verify pending" or "not started";
// an operator judgement shows as the overruled disposition beside the machine's. Overruling stays in the
// run viewer: nothing here records one. A run-record series says a spec whose run judged nothing recorded no
// verdict, and lists the run criteria it could not tie to one spec.

export function TicketAcceptanceTab() {
  const series = useTicketSeries();
  const tally = acceptanceTally(series.data);
  const specs = [...(series.data?.specs ?? [])].sort((l, r) => l.ordinal - r.ordinal);
  const unrecorded = tally && tally.unrecorded > 0
    ? ` · ${tally.unrecorded} spec${tally.unrecorded === 1 ? "" : "s"} without a recorded verdict` : "";
  return (
    <>
      <div className="section-head first">
        <h2>Acceptance</h2>
        {tally && <span className="cnt" data-testid="acceptance-tally">{tally.verified} of {tally.total} verified{unrecorded}</span>}
        <span className="sh-sub">each spec&apos;s done criteria · the attempt that verified them</span>
      </div>
      {isRunRecord(series.data) && specs.length > 0 && <div className="msub" data-testid="acceptance-run-record">{RUN_RECORD_LINE}</div>}
      {specs.length === 0 && <div className="msub">{series.loading ? "Reading the series…" : "No spec has been cut for this ticket."}</div>}
      <div className="acc-list">
        {specs.map((spec) => {
          const standing = spec.state === "in_progress" ? " · working" : spec.state === "not_started" ? " · not started" : "";
          return (
            <div key={spec.specId} className="doc" data-testid={`acceptance-spec-${spec.specId}`}>
              <h5>Spec {spec.ordinal}{spec.repos.length > 0 ? ` · ${spec.repos.join(", ")}` : ""}{standing}</h5>
              <div className="crit">
                {spec.done.map((criterion) => (
                  <CriterionLine key={criterion.text} criterion={criterion} row={criterionRow(criterion, spec)} />
                ))}
              </div>
            </div>
          );
        })}
        {(series.data?.unattributedCriteria ?? []).map((run) => <RunCriteria key={run.runId} run={run} />)}
      </div>
    </>
  );
}

function RunCriteria({ run }: { run: TicketSeriesUnattributed }) {
  const by = `A${run.attempt}`;
  return (
    <div className="doc" data-testid={`acceptance-run-${run.runId}`}>
      <h5>Run criteria, not tied to a spec · attempt {run.attempt}</h5>
      <div className="crit">
        {run.criteria.map((criterion) => (
          <CriterionLine key={criterion.text} criterion={criterion} row={verdictRow(criterion, by, "no verdict recorded")} />
        ))}
        {run.unmeasured.map((account) => (
          <div key={account.repositories} className="ci" data-testid="acceptance-unmeasured">
            <span className="m q">·</span>
            <span>{account.repositories} could not be measured{account.reason ? ` — ${account.reason}` : ""}</span>
            <span className="cw">{by}</span>
          </div>
        ))}
      </div>
    </div>
  );
}

function CriterionLine({ criterion, row }: { criterion: TicketSeriesCriterion; row: { mark: string; glyph: string; who: string } }) {
  const overruled = overruleWords(criterion);
  return (
    <div className="ci" data-testid="acceptance-criterion">
      <span className={`m ${row.mark}`}>{row.glyph}</span>
      <span>
        {criterion.text}
        {overruled && <span className="dim-line" data-testid="acceptance-overruled"> · {overruled}</span>}
      </span>
      <span className="cw">{row.who}</span>
    </div>
  );
}
