import type { TicketBucket, TicketCounts } from "@/types/tickets";

// 2026-10-13-9a59p: the list's sections in the order it shows them — who acts next, then the
// tickets no one has to act on. Ended is the server's idle: a cancelled attempt, a failed one
// nobody checked, a success without a pull request, pull requests that all closed unmerged. Done is
// a merged pull request with every other one ended.

export interface TicketSectionSpec {
  title: string;
  sub: string;
  /** The mock's .cnt colour, applied while the section holds any ticket. */
  tone?: "amber" | "red" | "green" | "person";
  empty: string;
  count: (counts: TicketCounts) => number;
}

export const TICKET_SECTIONS: Readonly<Record<TicketBucket, TicketSectionSpec>> = {
  "needs-you": {
    title: "Needs you",
    sub: "a person has to answer before the ticket moves",
    tone: "amber",
    empty: "Nothing waits on you.",
    count: (c) => c.needsYou,
  },
  working: {
    title: "Working",
    sub: "an attempt is running or waiting for capacity",
    empty: "No attempt is running or queued.",
    count: (c) => c.working,
  },
  "waiting-on-review": {
    title: "Waiting on review",
    sub: "agent-smith is done; a person owns the next move",
    empty: "No pull request waits on a review.",
    count: (c) => c.waitingOnReview,
  },
  stuck: {
    title: "Stuck",
    sub: "the last attempt failed and no one has acted on the ticket since",
    tone: "red",
    empty: "No ticket is stuck.",
    count: (c) => c.stuck,
  },
  // 2026-10-14-dbcen: filed tickets with no run on record (ticket-graph.html, list-buckets).
  blocked: {
    title: "Waits for another ticket",
    sub: "filed, held until what it requires is merged or marked done by hand",
    tone: "person",
    empty: "No filed ticket waits for another.",
    count: (c) => c.blocked ?? 0,
  },
  filed: {
    title: "Filed, no run on record",
    sub: "filed here, nothing it requires is open, no run recorded",
    empty: "No filed ticket is without a run.",
    count: (c) => c.filed ?? 0,
  },
  ended: {
    title: "Ended",
    sub: "no next step is running or waiting",
    empty: "No ticket has ended without a next step.",
    count: (c) => c.idle,
  },
  done: {
    title: "Done",
    sub: "a pull request merged, every one ended",
    tone: "green",
    empty: "No ticket is done yet.",
    count: (c) => c.done,
  },
};

/** Every ticket the counts cover: each ticket is in exactly one bucket. */
export function totalTickets(c: TicketCounts): number {
  return c.needsYou + c.working + c.waitingOnReview + c.stuck + (c.blocked ?? 0) + (c.filed ?? 0) + c.idle + c.done;
}
