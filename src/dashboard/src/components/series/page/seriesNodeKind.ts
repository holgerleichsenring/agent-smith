import type { SeriesNodeKind, SeriesRunDetail, SeriesRunPullRequest } from "@/types/seriesRuns";

// 2026-11-23-f562: a ticket's ONE colour on the series graph. 2026-11-25-9992a: the server computes it (ticket.kind, from
// the hold's case, the merge order and the pull requests — the same state the master reads). 2026-11-25-9992j: the browser
// no longer counts it either; the graph draws it.

export type { SeriesNodeKind } from "@/types/seriesRuns";

/** The kinds in the legend's order. */
export const SERIES_NODE_KINDS: SeriesNodeKind[] = ["now", "needed", "question", "approval", "draft", "failed", "running", "pr", "waiting", "merged"];

/** The pull requests that wait for a merge: open, or opened and not read by the host yet. */
export function waitingPullRequests(pullRequests: SeriesRunPullRequest[]): SeriesRunPullRequest[] {
  return pullRequests.filter((pr) => pr.state === "open" || pr.state === "unread");
}

/** The tickets on a needed-merge chain: every one whose merge others start on. An edge between two of them is lit. */
export function seriesNeededChain(detail: SeriesRunDetail): Set<string> {
  return new Set(detail.mergeOrder.filter((step) => (step.neededBy ?? []).length > 0).map((step) => step.ticketId));
}
