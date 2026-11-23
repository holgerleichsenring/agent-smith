"use client";

import Link from "next/link";
import { useLacksPermission } from "@/hooks/useLacksPermission";
import { useT } from "@/hooks/useT";
import { ticketPath } from "@/lib/tickets/ticketAddress";
import type { SeriesRunDetail, SeriesRunMergeStep, SeriesRunPullRequest } from "@/types/seriesRuns";
import { RUNS_CONTROL } from "../../tickets/detail/RetryUnchanged";
import { holdSentence, namesOf } from "../../tickets/holds/holdWords";
import { SeriesMergeActs, waitingPullRequests } from "./SeriesMergeActs";
import { SeriesMergeOutcome } from "./SeriesMergeOutcome";
import { mergesNow } from "./seriesStepLines";
import { useSeriesMerge, type SeriesMerge } from "./useSeriesMerge";

// 2026-11-23-31dce: the series' open pull requests in merge order — per ticket each repo's pull request with its link and
// CI (a red one with its failing check and log), why it waits, and on the ready row a Merge button (runs.control) that
// starts a merge run. Only the ready row's button is enabled; the others say what comes first. What the run did, or why it
// refused, stays on the row; the series is read again afterwards. 2026-11-23-849e: a ready step is the one to merge only while
// its ticket's hold is that merge (mergesNow), the rule Your move and the series list read.
// 2026-11-23-8d16: the merge act is useSeriesMerge (Your next step starts the same one); every row has an Open pull request
// button per open pull request; a row others start on is marked with the tickets that wait for it (the server's neededBy);
// a draft says so.

export function SeriesMergeNextSection({ detail, reload, merge: shared }: { detail: SeriesRunDetail; reload: () => void; merge?: SeriesMerge }) {
  const t = useT();
  const lacks = useLacksPermission(RUNS_CONTROL, detail.project);
  const own = useSeriesMerge(detail.id, reload);
  const merge = shared ?? own; // the page's shared act; its own when drawn alone
  if (detail.mergeOrder.length === 0) return <p className="msub" data-testid="series-merge-empty">{t("series.merge.empty")}</p>;
  const { outcome } = merge;
  const shown = outcome && detail.mergeOrder.some((step) => step.ticketId === outcome.ticketId) ? null : outcome;
  return (
    <>
      <p className="msub">{t("series.merge.sub")}</p>
      {shown && <SeriesMergeOutcome outcome={shown} />}
      <ol className="sr-queue" data-testid="series-merge-order">
        {detail.mergeOrder.map((step) => (
          <MergeRow key={step.ticketId} detail={detail} step={step} lacks={lacks} merge={merge} />
        ))}
      </ol>
    </>
  );
}

function MergeRow({ detail, step, lacks, merge }: { detail: SeriesRunDetail; step: SeriesRunMergeStep; lacks: boolean; merge: SeriesMerge }) {
  const t = useT();
  const ticket = detail.tickets.find((candidate) => candidate.ticketId === step.ticketId);
  // 2026-11-23-849e: ready only while the ticket's hold is the merge — a running or red CI says its hold's sentence instead.
  const ready = mergesNow(detail, step);
  const waiting = waitingPullRequests(ticket?.pullRequests ?? []);
  const draft = waiting.some((pr) => pr.isDraft);
  const why = ready ? [t("series.merge.ready"), ticket?.hold?.approves ? t("hold.merge.approves") : null,
    draft ? t("series.merge.draftFirst") : null].filter(Boolean).join(" ")
    : step.wait === "ready" && ticket?.hold ? holdSentence(ticket.hold, t)
    : step.wait === "retarget" ? t("series.merge.retarget") : t("series.merge.after", { keys: step.after.join(", ") });
  const needed = step.neededBy ?? [];
  const tone = [ready ? "now" : "later", needed.length > 0 ? "needed" : ""].filter(Boolean).join(" ");
  const outcome = merge.outcome?.ticketId === step.ticketId ? merge.outcome : null;
  return (
    <li className={tone} data-testid={`series-merge-${step.ticketId}`} data-wait={step.wait}>
      <div className="sr-queue-main">
        <div><Link className="mono" href={ticketPath(detail.project, step.ticketId)}>{step.key}</Link> {ticket?.title}</div>
        {waiting.map((pr) => <PullRequestLine key={pr.repo} ticketId={step.ticketId} pr={pr} />)}
        {needed.length > 0 && (
          <div className="sr-queue-need" data-testid={`series-merge-needed-${step.ticketId}`}>
            {t("series.merge.needed", { names: namesOf(needed, t), count: needed.length })}
          </div>
        )}
        <div className="sr-queue-why" data-testid={`series-merge-why-${step.ticketId}`}>{why}</div>
        {outcome && <SeriesMergeOutcome outcome={outcome} />}
      </div>
      <SeriesMergeActs ticketId={step.ticketId} pullRequests={ticket?.pullRequests ?? []} testId="series-merge"
        merge={lacks ? null : { ready, busy: merge.busy, onMerge: (id) => void merge.merge(id) }} />
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
      {pr.isDraft && <span className="sr-state need" data-testid={`series-merge-draft-${ticketId}-${pr.repo}`}>{t("series.merge.draft")}</span>}
      <span className={`sr-state ${ci.tone}`}>{ci.text}</span>
      {pr.ci === "red" && pr.ciFailure && <span className="sr-queue-fail">{pr.ciFailure}</span>}
      {pr.ci === "red" && pr.ciLog && <a href={pr.ciLog} target="_blank" rel="noreferrer">{t("series.merge.log")}</a>}
    </div>
  );
}
