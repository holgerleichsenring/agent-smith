import type { SeriesRunDetail, SeriesRunTicket } from "@/types/seriesRuns";

// 2026-11-23-31dcf: a ticket's frame in the Order and branches graph — merged, asking a question, the one to merge next
// (the merge order's ready step), a pull request waiting, never started; any other ticket (at work, stopped) is plain.

export type SeriesNodeFrame = "merged" | "question" | "next" | "waiting" | "notStarted" | "plain";

const WAITS = new Set(["open", "unread"]);

export function seriesNodeFrame(ticket: SeriesRunTicket, detail: SeriesRunDetail): SeriesNodeFrame {
  if (ticket.landed) return "merged";
  if (ticket.status === "waiting_for_input") return "question";
  if (detail.mergeOrder.some((step) => step.ticketId === ticket.ticketId && step.wait === "ready")) return "next";
  if (ticket.pullRequests.some((p) => WAITS.has(p.state))) return "waiting";
  if (ticket.status === null && ticket.pullRequests.length === 0) return "notStarted";
  return "plain";
}
