"use client";

import Link from "next/link";
import { useLacksPermission } from "@/hooks/useLacksPermission";
import { useT } from "@/hooks/useT";
import { seriesRunPath } from "@/lib/seriesRunAddress";
import type { SeriesRunDetail } from "@/types/seriesRuns";
import { RUNS_CONTROL } from "../../tickets/detail/RetryUnchanged";
import { SeriesMergeActs } from "./SeriesMergeActs";
import { SeriesMergeOutcome } from "./SeriesMergeOutcome";
import { mergesNow, seriesMergeLine, seriesStepLines, type SeriesStepLine } from "./seriesStepLines";
import { useSeriesMerge, type SeriesMerge } from "./useSeriesMerge";

// 2026-11-23-849e: the block at the top of every series section — what the person does next, from the holds
// (seriesStepLines), and whether merging is what moves the rest. Nothing to do says so.
// 2026-11-23-8d16: the merge's line runs the same merge as its Merge next row (useSeriesMerge), enabled exactly when that
// row's is, with an Open pull request button per open pull request beside it.

interface Props {
  detail: SeriesRunDetail;
  reload?: () => void;
  /** The page's one merge act, shared with Merge next; the block's own when drawn alone. */
  merge?: SeriesMerge;
}

export function SeriesYourStep({ detail, reload = () => {}, merge: shared }: Props) {
  const t = useT();
  const lacks = useLacksPermission(RUNS_CONTROL, detail.project);
  const own = useSeriesMerge(detail.id, reload);
  const merge = shared ?? own;
  const lines = seriesStepLines(detail, seriesRunPath(detail.id), t);
  const merges = seriesMergeLine(detail, t);
  return (
    <section className="sr-you" data-testid="series-your-step" aria-label={t("series.you.title")}>
      <div className="sr-you-title">{t("series.you.title")}</div>
      {lines.length === 0 ? <div className="sr-you-line" data-testid="series-your-step-nothing">{t("series.you.nothing")}</div>
        : lines.map((line) => <StepLine key={line.ticketId} detail={detail} line={line} lacks={lacks} merge={merge} />)}
      {merges && <div className="sr-you-merges" data-testid="series-your-step-merges">{merges}</div>}
      {merge.outcome && !lines.some((line) => line.ticketId === merge.outcome?.ticketId)
        && <SeriesMergeOutcome outcome={merge.outcome} />}
    </section>
  );
}

function StepLine({ detail, line, lacks, merge }: { detail: SeriesRunDetail; line: SeriesStepLine; lacks: boolean; merge: SeriesMerge }) {
  const ticket = detail.tickets.find((candidate) => candidate.ticketId === line.ticketId);
  const step = detail.mergeOrder.find((candidate) => candidate.ticketId === line.ticketId);
  return (
    <div className="sr-you-line" data-testid={`series-your-step-${line.ticketId}`}>
      <span>{line.text}</span>
      {line.merge
        ? <SeriesMergeActs ticketId={line.ticketId} pullRequests={ticket?.pullRequests ?? []} testId="series-your-step"
          merge={lacks ? null : { ready: !!step && mergesNow(detail, step), busy: merge.busy, onMerge: (id) => void merge.merge(id) }} />
        : line.act.external
          ? <a className="btn sm primary" href={line.act.href} target="_blank" rel="noreferrer">{line.act.label}</a>
          : <Link className="btn sm primary" href={line.act.href}>{line.act.label}</Link>}
      {merge.outcome?.ticketId === line.ticketId && <div className="sr-you-said"><SeriesMergeOutcome outcome={merge.outcome} /></div>}
    </div>
  );
}
