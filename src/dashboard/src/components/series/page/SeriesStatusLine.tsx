"use client";

import { useT } from "@/hooks/useT";
import type { T, TextKey } from "@/lib/i18n/translate";
import type { SeriesProgressWord, SeriesRunDetail } from "@/types/seriesRuns";
import { seriesStateWord } from "../seriesNextStep";

// 2026-11-25-433fa: one line pinned at the top of the series chat — where the series stands without asking. It words what
// the server read from the one truth (counts, workingOn, nextMerge, progress, state) and derives nothing; the page re-reads
// the series on the project's ticket nudges, so the line moves in place.

/** The most ticket keys the line names before it counts the rest. */
export const STATUS_LINE_KEYS = 3;

const PROGRESS: Partial<Record<SeriesProgressWord, TextKey>> = {
  begun: "series.status.progress.begun", quarter: "series.status.progress.quarter", third: "series.status.progress.third",
  half: "series.status.progress.half", twoThirds: "series.status.progress.twoThirds",
  threeQuarters: "series.status.progress.threeQuarters", nearly: "series.status.progress.nearly",
};

export function SeriesStatusLine({ detail }: { detail: SeriesRunDetail }) {
  const t = useT();
  return (
    <p className="sr-status" role="status" aria-label={t("series.status.label")} title={t("series.conversation.sub")}
      data-testid="series-status-line">
      {statusParts(detail, t).join(" · ")}
    </p>
  );
}

/** The line's parts in order: the series' state when it is not simply running, merged of total, the tickets at work, the
 *  ones waiting on a person, the next merge and how far it is (none and all say nothing the count does not). */
export function statusParts(detail: SeriesRunDetail, t: T): string[] {
  const parts: string[] = [];
  if (detail.state !== "running") parts.push(seriesStateWord(detail.state, t));
  parts.push(t("series.status.merged", { merged: detail.counts.landed, total: detail.counts.total }));
  const working = detail.workingOn ?? [];
  if (working.length > 0) parts.push(t("series.status.working", { count: working.length, keys: keys(working, t) }));
  if (detail.counts.held > 0) parts.push(t("series.status.needsYou", { count: detail.counts.held }));
  if (detail.nextMerge) parts.push(t("series.status.next", { key: detail.nextMerge }));
  const progress = detail.progress ? PROGRESS[detail.progress] : undefined;
  if (progress) parts.push(t(progress));
  return parts;
}

function keys(working: string[], t: T): string {
  const named = working.slice(0, STATUS_LINE_KEYS).join(", ");
  const rest = working.length - STATUS_LINE_KEYS;
  return rest > 0 ? `${named} ${t("series.status.more", { count: rest })}` : named;
}
