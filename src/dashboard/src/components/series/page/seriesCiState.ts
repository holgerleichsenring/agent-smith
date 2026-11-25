import type { SeriesRunDetail, SeriesRunTicket } from "@/types/seriesRuns";
import { waitingPullRequests } from "./seriesNodeKind";

// 2026-11-23-0219: what a ticket's waiting pull requests say about their host — still a draft, a required build that has not
// started, CI that runs, CI that is red — read from the server's fields only, and whether its merge is one that completes
// once CI is green: a ready step of the merge order whose CI runs or has not started (red never merges). 2026-11-25-9992a:
// the CI is the ticket's one CI (ticket.ci), aggregated on the server by the rule the hold and the master read.

export type SeriesBadge = "draft" | "conflict" | "ciRed" | "ciNotStarted" | "ciRunning";

/** The badges the Overview draws for a ticket, in a fixed order. */
export function seriesTicketBadges(ticket: SeriesRunTicket): SeriesBadge[] {
  const badges: SeriesBadge[] = [];
  if (waitingPullRequests(ticket.pullRequests).some((pr) => pr.isDraft)) badges.push("draft");
  // 2026-11-24-6811a: a conflict is said before the CI, which cannot merge it either way.
  if (waitingPullRequests(ticket.pullRequests).some((pr) => pr.hasConflicts)) badges.push("conflict");
  if (ticket.ci === "red") badges.push("ciRed");
  if (ticket.ci === "not_started") badges.push("ciNotStarted");
  if (ticket.ci === "pending") badges.push("ciRunning");
  return badges;
}

/** A ready step whose CI runs or has not started: its Merge completes once CI is green. */
export function mergesWhenGreen(detail: SeriesRunDetail, ticket: SeriesRunTicket): boolean {
  if (ticket.landed) return false;
  const step = detail.mergeOrder.find((candidate) => candidate.ticketId === ticket.ticketId);
  return step?.wait === "ready" && (ticket.ci === "pending" || ticket.ci === "not_started");
}

/** What the newest merge run said about a waiting pull request it did not merge — the first one, refusals first. */
export function seriesMergeSaid(ticket: SeriesRunTicket) {
  const said = waitingPullRequests(ticket.pullRequests).map((pr) => pr.mergeSaid).filter((s) => !!s);
  return said.find((s) => s!.status === "completion_refused") ?? said[0] ?? null;
}
