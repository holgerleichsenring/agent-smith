import type { SeriesRunTicket } from "@/types/seriesRuns";
import { waitingPullRequests } from "./seriesNodeKind";

// 2026-11-23-0219: what a ticket's waiting pull requests say about their host — a conflict, a required build
// that has not started, CI that runs, CI that is red — read from the server's fields only. 2026-11-25-9992a: the CI is the
// ticket's one CI (ticket.ci), aggregated on the server by the rule the hold and the master read. 2026-11-25-9992j: the
// graph's node marks read it.

export type SeriesBadge = "conflict" | "ciRed" | "ciNotStarted" | "ciRunning";

/** The host's words for a ticket, in a fixed order. */
export function seriesTicketBadges(ticket: SeriesRunTicket): SeriesBadge[] {
  const badges: SeriesBadge[] = [];
  // 2026-11-24-6811a: a conflict is said before the CI, which cannot merge it either way.
  if (waitingPullRequests(ticket.pullRequests).some((pr) => pr.hasConflicts)) badges.push("conflict");
  if (ticket.ci === "red") badges.push("ciRed");
  if (ticket.ci === "not_started") badges.push("ciNotStarted");
  if (ticket.ci === "pending") badges.push("ciRunning");
  return badges;
}
