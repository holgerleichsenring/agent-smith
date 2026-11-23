"use client";

import Link from "next/link";
import { useT } from "@/hooks/useT";
import { runPath } from "@/lib/runPath";
import { useTicketSeries } from "@/lib/tickets/TicketPageStore";
import type { TicketSeriesCriterion, TicketSeriesUnattributed } from "@/types/tickets";
import { CriterionRejection } from "./CriterionRejection";
import { MasterReportText, TicketFollowUps } from "./TicketFollowUps";
import { acceptanceTally, criterionRow, isRunRecord, overruleWords, rejectionOf, RUN_RECORD_LINE, verdictRow } from "./specWords";

// 2026-10-13-9a59r: acceptance PER SPEC — each spec's done list (a run's acceptance contract is the current
// spec's), every criterion with its mark and the attempt that verified it, "verify pending" or "not started";
// an operator judgement shows as the overruled disposition beside the machine's. Overruling stays in the
// run viewer: nothing here records one. A run-record series says a spec whose run judged nothing recorded no
// verdict, and lists the run criteria it could not tie to one spec.
// 2026-10-16-86e6e: a criterion the gate did not accept says why, with the run where it can be overruled.
// 2026-11-17-8d1cd: plain lines (TicketRail mock) — the criterion, the reason its verdict was given, and "overrule" as a
// text link to the run that judged it; recording the overrule stays on the run page.

export function TicketAcceptanceTab() {
  const t = useT();
  const series = useTicketSeries();
  const tally = acceptanceTally(series.data);
  const specs = [...(series.data?.specs ?? [])].sort((l, r) => l.ordinal - r.ordinal);
  const unrecorded = tally && tally.unrecorded > 0
    ? ` · ${t("tickets.acceptance.unrecorded", { count: tally.unrecorded })}` : "";
  return (
    <>
      <div className="section-head first">
        <h2>{t("tickets.acceptance.title")}</h2>
        {tally && <span className="cnt" data-testid="acceptance-tally">{t("tickets.acceptance.verified", { verified: tally.verified, total: tally.total })}{unrecorded}</span>}
        <span className="sh-sub">{t("tickets.acceptance.sub")}</span>
      </div>
      {isRunRecord(series.data) && specs.length > 0 && <div className="msub" data-testid="acceptance-run-record">{t(RUN_RECORD_LINE)}</div>}
      {specs.length === 0 && <div className="msub">{series.loading ? t("tickets.specs.reading") : t("tickets.specs.none")}</div>}
      <div className="acc-list">
        {specs.map((spec) => {
          const standing = spec.state === "in_progress" ? ` · ${t("tickets.spec.state.in_progress")}`
            : spec.state === "not_started" ? ` · ${t("tickets.spec.state.not_started")}` : "";
          return (
            <div key={spec.specId} className="doc" data-testid={`acceptance-spec-${spec.specId}`}>
              <h5>{t("tickets.acceptance.spec", { n: spec.ordinal })}{spec.repos.length > 0 ? ` · ${spec.repos.join(", ")}` : ""}{standing}</h5>
              <div className="crit">
                {spec.done.map((criterion) => (
                  <CriterionLine key={criterion.text} criterion={criterion} row={criterionRow(t, criterion, spec)} judgedBy={spec.judgedBy} />
                ))}
              </div>
              <MasterReportText spec={spec} />
            </div>
          );
        })}
        {(series.data?.unattributedCriteria ?? []).map((run) => <RunCriteria key={run.runId} run={run} />)}
        <TicketFollowUps specs={specs} />
      </div>
    </>
  );
}

function RunCriteria({ run }: { run: TicketSeriesUnattributed }) {
  const t = useT();
  const by = `A${run.attempt}`;
  return (
    <div className="doc" data-testid={`acceptance-run-${run.runId}`}>
      <h5>{t("tickets.acceptance.runCriteria", { n: run.attempt })}</h5>
      <div className="crit">
        {run.criteria.map((criterion) => (
          <CriterionLine key={criterion.text} criterion={criterion} row={verdictRow(t, criterion, by, "")} judgedBy={run.runId} />
        ))}
        {run.unmeasured.map((account) => (
          <div key={account.repositories} className="ci" data-testid="acceptance-unmeasured">
            <span className="m q">·</span>
            <span>{t("tickets.acceptance.unmeasured", { repositories: account.repositories })}{account.reason ? ` — ${account.reason}` : ""}</span>
            <span className="cw">{by}</span>
          </div>
        ))}
      </div>
    </div>
  );
}

interface LineProps {
  criterion: TicketSeriesCriterion;
  row: { mark: string; glyph: string; who: string };
  judgedBy: string | null | undefined;
}

function CriterionLine({ criterion, row, judgedBy }: LineProps) {
  const t = useT();
  const overruled = overruleWords(t, criterion);
  const rejected = rejectionOf(criterion) !== null;
  return (
    <div className="ci" data-testid="acceptance-criterion">
      <span className={`m ${row.mark}`}>{row.glyph}</span>
      <span>
        {criterion.text}
        {criterion.note && !rejected && <span className="tk-crit-why" data-testid="acceptance-reason">{criterion.note}</span>}
        {overruled && <span className="dim-line" data-testid="acceptance-overruled"> · {overruled}</span>}
        <CriterionRejection criterion={criterion} judgedBy={judgedBy} />
      </span>
      <span className="cw">
        {row.who}
        {judgedBy && criterion.verdict && !overruled && !rejected && (
          <Link className="tk-overrule" href={runPath(judgedBy)} data-testid="acceptance-overrule">{t("tickets.acceptance.overrule")}</Link>
        )}
      </span>
    </div>
  );
}
