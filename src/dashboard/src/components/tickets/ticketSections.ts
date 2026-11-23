import type { TextKey } from "@/lib/i18n/translate";
import type { TicketBucket, TicketCounts } from "@/types/tickets";

// 2026-10-13-9a59p: the list's sections in the order it shows them — who acts next, then the
// tickets no one has to act on. Ended is the server's idle: a cancelled attempt or a success without a
// pull request. Done is a merged pull request with every other one ended. 2026-10-15-114fd: PR closed
// and Failed split out of Ended; Closed — a person stopped work on it — is last and grey.
// 2026-11-23-d60d: no In review section — a pull request that can merge or whose CI failed is Needs you, one that waits
// (CI running, behind another pull request) is Working.

/** 2026-11-23-5669b: title, sub and empty are dictionary keys; the reader looks them up in the chosen language. */
export interface TicketSectionSpec {
  title: TextKey;
  sub: TextKey;
  /** The mock's .cnt colour, applied while the section holds any ticket. */
  tone?: "amber" | "red" | "green" | "person";
  empty: TextKey;
  count: (counts: TicketCounts) => number;
}

export const TICKET_SECTIONS: Readonly<Record<TicketBucket, TicketSectionSpec>> = {
  "needs-you": {
    title: "tickets.section.needsYou.title",
    sub: "tickets.section.needsYou.sub",
    tone: "amber",
    empty: "tickets.section.needsYou.empty",
    count: (c) => c.needsYou,
  },
  working: {
    title: "tickets.section.working.title",
    sub: "tickets.section.working.sub",
    empty: "tickets.section.working.empty",
    count: (c) => c.working,
  },
  stuck: {
    title: "tickets.section.stuck.title",
    sub: "tickets.section.stuck.sub",
    tone: "red",
    empty: "tickets.section.stuck.empty",
    count: (c) => c.stuck,
  },
  // 2026-10-14-dbcen: filed tickets with no run on record (ticket-graph.html, list-buckets).
  blocked: {
    title: "tickets.section.blocked.title",
    sub: "tickets.section.blocked.sub",
    tone: "person",
    empty: "tickets.section.blocked.empty",
    count: (c) => c.blocked ?? 0,
  },
  filed: {
    title: "tickets.section.filed.title",
    sub: "tickets.section.filed.sub",
    empty: "tickets.section.filed.empty",
    count: (c) => c.filed ?? 0,
  },
  "pr-closed": {
    title: "tickets.section.prClosed.title",
    sub: "tickets.section.prClosed.sub",
    tone: "red",
    empty: "tickets.section.prClosed.empty",
    count: (c) => c.prClosed ?? 0,
  },
  failed: {
    title: "tickets.section.failed.title",
    sub: "tickets.section.failed.sub",
    tone: "red",
    empty: "tickets.section.failed.empty",
    count: (c) => c.failed ?? 0,
  },
  ended: {
    title: "tickets.section.ended.title",
    sub: "tickets.section.ended.sub",
    empty: "tickets.section.ended.empty",
    count: (c) => c.idle,
  },
  done: {
    title: "tickets.section.done.title",
    sub: "tickets.section.done.sub",
    tone: "green",
    empty: "tickets.section.done.empty",
    count: (c) => c.done,
  },
  closed: {
    title: "tickets.section.closed.title",
    sub: "tickets.section.closed.sub",
    empty: "tickets.section.closed.empty",
    count: (c) => c.closed ?? 0,
  },
};

/** Every ticket the counts cover: each ticket is in exactly one bucket. */
export function totalTickets(c: TicketCounts): number {
  return c.needsYou + c.working + c.stuck + (c.blocked ?? 0) + (c.filed ?? 0)
    + (c.prClosed ?? 0) + (c.failed ?? 0) + c.idle + c.done + (c.closed ?? 0);
}
