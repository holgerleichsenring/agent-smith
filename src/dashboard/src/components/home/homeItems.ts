import { TICKET_SECTIONS } from "@/components/tickets/ticketSections";
import { SERVER_BUCKET, TICKET_BUCKETS, type TicketSummary } from "@/types/tickets";

// 2026-11-17-d6fba: what the home page lists under its composer, from the one ticket-list read the sidebar counts from.
// Your move is the needs-you bucket — the same bucket the sidebar's count is — oldest first, at most three; recent
// tickets are the newest of the rest.

export const MOVES_SHOWN = 3;
export const RECENT_SHOWN = 8;

const since = (t: TicketSummary): number => (t.since ? Date.parse(t.since) || 0 : 0);

/** The tickets waiting on a person, longest waiting first, at most three. */
export function yourMoves(tickets: readonly TicketSummary[]): TicketSummary[] {
  return tickets.filter((t) => t.bucket === "needs_you").sort((l, r) => since(l) - since(r)).slice(0, MOVES_SHOWN);
}

/** The tickets that moved last and are not waiting on a person, newest first. */
export function recentTickets(tickets: readonly TicketSummary[]): TicketSummary[] {
  return tickets.filter((t) => t.bucket !== "needs_you").sort((l, r) => since(r) - since(l)).slice(0, RECENT_SHOWN);
}

/** What a waiting ticket asks of the operator, in one line. */
export function moveAsk(t: TicketSummary): string {
  if (t.kind === "handback") return t.handback?.text?.split(/\r?\n/)[0] ?? "The ticket came back to you.";
  return t.pendingQuestion?.question.text.split(/\r?\n/)[0] ?? "A run waits for your answer.";
}

/** The act a waiting ticket's row offers. */
export function moveAct(t: TicketSummary): string {
  return t.kind === "handback" ? "Reply" : "Answer";
}

/** "Nothing waits on you." / "One thing waits on you." / "4 things wait on you." */
export function waitingSentence(count: number): string {
  if (count <= 0) return "Nothing waits on you.";
  return count === 1 ? "One thing waits on you." : `${count} things wait on you.`;
}

/** The ticket's state as the ticket list's section names it: "Working", "In review", "Stuck". */
export function stateWords(t: TicketSummary): string {
  const bucket = TICKET_BUCKETS.find((b) => SERVER_BUCKET[b] === t.bucket);
  return bucket ? TICKET_SECTIONS[bucket].title : t.bucket.replace(/_/g, " ");
}
