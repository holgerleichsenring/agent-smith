"use client";

import { useT } from "@/hooks/useT";
import type { SeriesRunPullRequest } from "@/types/seriesRuns";

// 2026-11-23-8d16: the acts beside a merge — Merge (the series' one merge act, enabled only while it is the merge now;
// left out for a viewer without runs.control) and one Open pull request button per open pull request, always enabled, so
// the person can review ahead of the merge order.

interface Props {
  ticketId: string;
  pullRequests: SeriesRunPullRequest[];
  /** null: no Merge button (the viewer lacks runs.control). */
  merge: { ready: boolean; busy: string | null; onMerge: (ticketId: string) => void } | null;
  testId: string;
}

/** The pull requests that wait for a merge: open, or opened and not read by the host yet. */
export function waitingPullRequests(pullRequests: SeriesRunPullRequest[]): SeriesRunPullRequest[] {
  return pullRequests.filter((pr) => pr.state === "open" || pr.state === "unread");
}

export function SeriesMergeActs({ ticketId, pullRequests, merge, testId }: Props) {
  const t = useT();
  return (
    <div className="sr-acts">
      {merge && (
        <button type="button" className={merge.ready ? "btn sm primary" : "btn sm"} disabled={!merge.ready || merge.busy !== null}
          onClick={() => merge.onMerge(ticketId)} data-testid={`${testId}-button-${ticketId}`}>
          {merge.busy === ticketId ? t("series.merge.busy") : t("series.merge.button")}
        </button>
      )}
      {waitingPullRequests(pullRequests).map((pr) => (
        <a key={pr.repo} className="btn sm" href={pr.url} target="_blank" rel="noreferrer"
          data-testid={`${testId}-open-${ticketId}-${pr.repo}`}>
          {t("series.merge.openPr", { number: pr.number })}
        </a>
      ))}
    </div>
  );
}
