import type { SeriesRunDetail, SeriesRunTicket } from "@/types/seriesRuns";
import { waitingPullRequests } from "./seriesNodeKind";

// 2026-11-23-0219: what a ticket's waiting pull requests say about their host — still a draft, a required build that has not
// started, CI that runs, CI that is red — read from the server's fields only, and whether its merge is one that completes
// once CI is green: a ready step of the merge order whose CI runs or has not started (red never merges).

export type SeriesCiWait = "red" | "not_started" | "pending";

export type SeriesBadge = "draft" | "ciRed" | "ciNotStarted" | "ciRunning";

const ORDER: SeriesCiWait[] = ["red", "not_started", "pending"];

/** The waiting pull requests' CI that holds a merge back: red, then not started, then running; null when none does. */
export function seriesCiWait(ticket: SeriesRunTicket): SeriesCiWait | null {
  const states = new Set(waitingPullRequests(ticket.pullRequests).map((pr) => pr.ci));
  return ORDER.find((state) => states.has(state)) ?? null;
}

/** The badges the Overview draws for a ticket, in a fixed order. */
export function seriesTicketBadges(ticket: SeriesRunTicket): SeriesBadge[] {
  const badges: SeriesBadge[] = [];
  if (waitingPullRequests(ticket.pullRequests).some((pr) => pr.isDraft)) badges.push("draft");
  const wait = seriesCiWait(ticket);
  if (wait === "red") badges.push("ciRed");
  if (wait === "not_started") badges.push("ciNotStarted");
  if (wait === "pending") badges.push("ciRunning");
  return badges;
}

/** A ready step whose CI runs or has not started: its Merge completes once CI is green. */
export function mergesWhenGreen(detail: SeriesRunDetail, ticket: SeriesRunTicket): boolean {
  if (ticket.landed) return false;
  const step = detail.mergeOrder.find((candidate) => candidate.ticketId === ticket.ticketId);
  const wait = seriesCiWait(ticket);
  return step?.wait === "ready" && (wait === "pending" || wait === "not_started");
}

/** What the newest merge run said about a waiting pull request it did not merge — the first one, refusals first. */
export function seriesMergeSaid(ticket: SeriesRunTicket) {
  const said = waitingPullRequests(ticket.pullRequests).map((pr) => pr.mergeSaid).filter((s) => !!s);
  return said.find((s) => s!.status === "completion_refused") ?? said[0] ?? null;
}
