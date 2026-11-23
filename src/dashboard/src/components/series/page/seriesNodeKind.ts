import type { SeriesRunDetail, SeriesRunMergeStep, SeriesRunPullRequest, SeriesRunTicket } from "@/types/seriesRuns";
import { isAsked } from "../../tickets/holds/holdRoutes";

// 2026-11-23-f562: a ticket's ONE colour on the series Overview, read from what the server sent — landed, its hold, its
// merge step and the step's neededBy — never re-derived from attempts or pull requests in the browser: merged; the merge
// now (a ready step whose hold is the series' merge); a question or plan gap; a failure or anything else a person decides;
// at work; a merge others start on (8d16's neededBy); a pull request that waits; and the rest waits or never started.

export type SeriesNodeKind = "now" | "needed" | "question" | "failed" | "running" | "pr" | "waiting" | "merged";

/** The kinds in the legend's order. */
export const SERIES_NODE_KINDS: SeriesNodeKind[] = ["now", "needed", "question", "failed", "running", "pr", "waiting", "merged"];

const AT_WORK = new Set(["working", "model_wait", "queued", "resuming", "restarting"]);

/** A step of the merge order is the person's merge now only while its ticket's hold is that merge (849e). */
export function mergesNow(detail: SeriesRunDetail, step: SeriesRunMergeStep): boolean {
  return step.wait === "ready" && detail.tickets.find((t) => t.ticketId === step.ticketId)?.hold?.case === "merge_next";
}

/** The pull requests that wait for a merge: open, or opened and not read by the host yet. */
export function waitingPullRequests(pullRequests: SeriesRunPullRequest[]): SeriesRunPullRequest[] {
  return pullRequests.filter((pr) => pr.state === "open" || pr.state === "unread");
}

export function seriesNodeKind(ticket: SeriesRunTicket, detail: SeriesRunDetail): SeriesNodeKind {
  if (ticket.landed) return "merged";
  const step = detail.mergeOrder.find((candidate) => candidate.ticketId === ticket.ticketId);
  const hold = ticket.hold;
  if (step && mergesNow(detail, step)) return "now";
  if (isAsked(hold) || hold?.addressee === "plan") return "question";
  if (hold?.addressee === "decide" || hold?.addressee === "admin") return "failed";
  if (hold && AT_WORK.has(hold.case)) return "running";
  if ((step?.neededBy ?? []).length > 0) return "needed";
  return waitingPullRequests(ticket.pullRequests).length > 0 ? "pr" : "waiting";
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

/** Every count the Overview shows — the chips, the list's groups and the bar's — from the same kinds, so they agree. */
export function seriesOverviewCounts(detail: SeriesRunDetail): SeriesOverviewCounts {
  const kinds = Object.fromEntries(SERIES_NODE_KINDS.map((kind) => [kind, 0])) as Record<SeriesNodeKind, number>;
  detail.tickets.forEach((ticket) => { kinds[seriesNodeKind(ticket, detail)] += 1; });
  const needed = new Set(detail.tickets.filter((t) => seriesNodeKind(t, detail) === "needed").map((t) => t.ticketId));
  const neededFor = [...new Set(detail.mergeOrder.filter((s) => needed.has(s.ticketId)).flatMap((s) => s.neededBy ?? []))];
  return { kinds, neededFor, yours: kinds.now + kinds.question + kinds.failed, quiet: kinds.running + kinds.pr + kinds.waiting };
}

/** The tickets on a needed-merge chain: every one whose merge others start on. An edge between two of them is lit. */
export function seriesNeededChain(detail: SeriesRunDetail): Set<string> {
  return new Set(detail.mergeOrder.filter((step) => (step.neededBy ?? []).length > 0).map((step) => step.ticketId));
}
