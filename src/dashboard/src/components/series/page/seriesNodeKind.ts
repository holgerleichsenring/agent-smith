import type { SeriesNodeKind, SeriesRunDetail, SeriesRunPullRequest } from "@/types/seriesRuns";

// 2026-11-23-f562: a ticket's ONE colour on the series Overview. 2026-11-25-9992a: the server computes it (ticket.kind, from
// the hold's case, the merge order and the pull requests — the same state the master reads); the browser only counts it.

export type { SeriesNodeKind } from "@/types/seriesRuns";

/** The kinds in the legend's order. */
export const SERIES_NODE_KINDS: SeriesNodeKind[] = ["now", "needed", "question", "failed", "running", "pr", "waiting", "merged"];

/** The pull requests that wait for a merge: open, or opened and not read by the host yet. */
export function waitingPullRequests(pullRequests: SeriesRunPullRequest[]): SeriesRunPullRequest[] {
  return pullRequests.filter((pr) => pr.state === "open" || pr.state === "unread");
}

export interface SeriesOverviewCounts {
  kinds: Record<SeriesNodeKind, number>;
  /** The keys of the tickets the needed merges let start, in merge order. */
  neededFor: string[];
  /** The ones a person moves: the merge now, the questions and the failures. */
  yours: number;
  /** The ones nobody moves: at work, a pull request waiting, or waiting to start. */
  quiet: number;
}

/** Every count the Overview shows — the chips, the list's groups and the bar's — from the same kinds, so they agree.
 *  2026-11-23-8071: the merge now is the viewer's only when they may merge (runs.control); otherwise it is nobody's here. */
export function seriesOverviewCounts(detail: SeriesRunDetail, canMerge = true): SeriesOverviewCounts {
  const kinds = Object.fromEntries(SERIES_NODE_KINDS.map((kind) => [kind, 0])) as Record<SeriesNodeKind, number>;
  detail.tickets.forEach((ticket) => { kinds[ticket.kind] += 1; });
  const needed = new Set(detail.tickets.filter((t) => t.kind === "needed").map((t) => t.ticketId));
  const neededFor = [...new Set(detail.mergeOrder.filter((s) => needed.has(s.ticketId)).flatMap((s) => s.neededBy ?? []))];
  return { kinds, neededFor, yours: (canMerge ? kinds.now : 0) + kinds.question + kinds.failed, quiet: kinds.running + kinds.pr + kinds.waiting };
}

/** The tickets on a needed-merge chain: every one whose merge others start on. An edge between two of them is lit. */
export function seriesNeededChain(detail: SeriesRunDetail): Set<string> {
  return new Set(detail.mergeOrder.filter((step) => (step.neededBy ?? []).length > 0).map((step) => step.ticketId));
}
