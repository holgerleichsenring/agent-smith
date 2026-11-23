"use client";

import Link from "next/link";
import { useState } from "react";
import { useLacksPermission } from "@/hooks/useLacksPermission";
import { useT } from "@/hooks/useT";
import { mergeSeriesTicket, type SeriesMergeResult } from "@/lib/seriesRunsApi";
import { ticketPath } from "@/lib/tickets/ticketAddress";
import type { SeriesRunDetail, SeriesRunMergeStep, SeriesRunPullRequest } from "@/types/seriesRuns";
import { RUNS_CONTROL } from "../../tickets/detail/RetryUnchanged";

// 2026-11-23-31dce: the series' open pull requests in merge order — per ticket each repo's pull request with its link and
// CI (a red one with its failing check and log), why it waits, and on the ready row a Merge button (runs.control) that
// starts a merge run. Only the ready row's button is enabled; the others say what comes first. What the run did, or why it
// refused, stays on the row; the series is read again afterwards.

type Outcome = { ticketId: string; result: SeriesMergeResult } | { ticketId: string; failed: string };

export function SeriesMergeNextSection({ detail, reload }: { detail: SeriesRunDetail; reload: () => void }) {
  const t = useT();
  const lacks = useLacksPermission(RUNS_CONTROL, detail.project);
  const [busy, setBusy] = useState<string | null>(null);
  const [outcome, setOutcome] = useState<Outcome | null>(null);
  const merge = async (ticketId: string) => {
    setBusy(ticketId);
    setOutcome(null);
    try {
      setOutcome({ ticketId, result: await mergeSeriesTicket(detail.id, ticketId) });
    } catch (error: unknown) {
      setOutcome({ ticketId, failed: error instanceof Error ? error.message : String(error) });
    } finally {
      setBusy(null);
      reload();
    }
  };
  if (detail.mergeOrder.length === 0) return <p className="msub" data-testid="series-merge-empty">{t("series.merge.empty")}</p>;
  const shown = outcome && detail.mergeOrder.some((step) => step.ticketId === outcome.ticketId) ? null : outcome;
  return (
    <>
      <p className="msub">{t("series.merge.sub")}</p>
      {shown && <MergeOutcome outcome={shown} />}
      <ol className="sr-queue" data-testid="series-merge-order">
        {detail.mergeOrder.map((step) => (
          <MergeRow key={step.ticketId} detail={detail} step={step} lacks={lacks} busy={busy} onMerge={merge}
            outcome={outcome?.ticketId === step.ticketId ? outcome : null} />
        ))}
      </ol>
    </>
  );
}

interface RowProps {
  detail: SeriesRunDetail;
  step: SeriesRunMergeStep;
  lacks: boolean;
  busy: string | null;
  onMerge: (ticketId: string) => void;
  outcome: Outcome | null;
}

function MergeRow({ detail, step, lacks, busy, onMerge, outcome }: RowProps) {
  const t = useT();
  const ticket = detail.tickets.find((candidate) => candidate.ticketId === step.ticketId);
  const ready = step.wait === "ready";
  const why = ready ? t("series.merge.ready")
    : step.wait === "retarget" ? t("series.merge.retarget") : t("series.merge.after", { keys: step.after.join(", ") });
  const waiting = (ticket?.pullRequests ?? []).filter((pr) => pr.state === "open" || pr.state === "unread");
  return (
    <li className={ready ? "now" : "later"} data-testid={`series-merge-${step.ticketId}`} data-wait={step.wait}>
      <div className="sr-queue-main">
        <div><Link className="mono" href={ticketPath(detail.project, step.ticketId)}>{step.key}</Link> {ticket?.title}</div>
        {waiting.map((pr) => <PullRequestLine key={pr.repo} ticketId={step.ticketId} pr={pr} />)}
        <div className="sr-queue-why" data-testid={`series-merge-why-${step.ticketId}`}>{why}</div>
        {outcome && <MergeOutcome outcome={outcome} />}
      </div>
      {!lacks && (
        <button type="button" className={ready ? "btn sm primary" : "btn sm"} disabled={!ready || busy !== null}
          onClick={() => onMerge(step.ticketId)} data-testid={`series-merge-button-${step.ticketId}`}>
          {busy === step.ticketId ? t("series.merge.busy") : t("series.merge.button")}
        </button>
      )}
    </li>
  );
}

function PullRequestLine({ ticketId, pr }: { ticketId: string; pr: SeriesRunPullRequest }) {
  const t = useT();
  const ci = pr.ci === "green" ? { tone: "ok", text: t("series.merge.ci.green") }
    : pr.ci === "red" ? { tone: "bad", text: t("series.merge.ci.red") }
    : pr.ci === "pending" ? { tone: "run", text: t("series.merge.ci.pending") }
    : pr.ci === "none" ? { tone: "", text: t("series.merge.ci.none") } : { tone: "", text: t("series.merge.ci.unread") };
  return (
    <div className="sr-queue-pr" data-testid={`series-merge-pr-${ticketId}-${pr.repo}`}>
      <span>{pr.repo}</span>
      <a href={pr.url} target="_blank" rel="noreferrer">{t("series.merge.pr", { number: pr.number })}</a>
      <span className={`sr-state ${ci.tone}`}>{ci.text}</span>
      {pr.ci === "red" && pr.ciFailure && <span className="sr-queue-fail">{pr.ciFailure}</span>}
      {pr.ci === "red" && pr.ciLog && <a href={pr.ciLog} target="_blank" rel="noreferrer">{t("series.merge.log")}</a>}
    </div>
  );
}

function MergeOutcome({ outcome }: { outcome: Outcome }) {
  const t = useT();
  if ("failed" in outcome)
    return <div className="sr-queue-said bad" role="status">{t("series.merge.failed", { message: outcome.failed })}</div>;
  if (outcome.result.kind === "said")
    return <div className="sr-queue-said bad" role="status" data-testid="series-merge-said">{outcome.result.text}</div>;
  return (
    <div className="sr-queue-said" role="status" data-testid="series-merge-outcome">
      {outcome.result.answer.pullRequests.map((pr) => (
        <div key={pr.repo} data-outcome={pr.outcome}>
          <span className={`sr-state ${pr.outcome === "merged" ? "ok" : pr.outcome === "armed" ? "run" : "bad"}`}>
            {t(`series.merge.outcome.${pr.outcome}`)}
          </span>{" "}{pr.outcome === "merged" ? pr.repo : pr.text}
        </div>
      ))}
    </div>
  );
}
