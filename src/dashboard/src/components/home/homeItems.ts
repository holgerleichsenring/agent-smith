import { TICKET_SECTIONS } from "@/components/tickets/ticketSections";
import { isYourMove } from "@/components/tickets/holds/holdWords";
import { SERVER_BUCKET, TICKET_BUCKETS, type TicketSummary } from "@/types/tickets";

// 2026-11-17-d6fba: what the home page lists under its composer, from the one ticket-list read the sidebar counts from.
// Your move oldest first, at most three; recent tickets are the newest of the rest. 2026-11-23-31dcg: Your move is the
// tickets a person's hold lies on — and an admin's for an admin — as the server read them; what each row says and
// offers is its hold's (holdWords), not a who-acts rule of its own.

export const MOVES_SHOWN = 3;
export const RECENT_SHOWN = 8;

const since = (t: TicketSummary): number => (t.since ? Date.parse(t.since) || 0 : 0);

/** The tickets waiting on the viewer, longest waiting first, at most three. */
export function yourMoves(tickets: readonly TicketSummary[], admin = false): TicketSummary[] {
  return tickets.filter((t) => isYourMove(t, admin)).sort((l, r) => since(l) - since(r)).slice(0, MOVES_SHOWN);
}

/** The tickets that moved last and are not waiting on the viewer, newest first. */
export function recentTickets(tickets: readonly TicketSummary[], admin = false): TicketSummary[] {
  return tickets.filter((t) => !isYourMove(t, admin)).sort((l, r) => since(r) - since(l)).slice(0, RECENT_SHOWN);
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
