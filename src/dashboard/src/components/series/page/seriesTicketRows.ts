import type { SeriesNodeKind, SeriesRunTicket } from "@/types/seriesRuns";

// 2026-11-27-ecf2a: the rail's ticket list — the meter's four buckets over the server's kinds, the Open / All / Merged
// filter, the row order (deepest first) and the tickets a waiting one waits on.

export type SeriesBucket = "merged" | "moving" | "you" | "waiting";

/** The meter's order, left to right. */
export const SERIES_BUCKETS: SeriesBucket[] = ["merged", "moving", "you", "waiting"];

const BUCKET: Record<SeriesNodeKind, SeriesBucket> = {
  merged: "merged",
  question: "you", approval: "you", draft: "you", failed: "you",
  now: "moving", needed: "moving", running: "moving", pr: "moving",
  waiting: "waiting",
};

export function seriesTicketBucket(kind: SeriesNodeKind): SeriesBucket {
  return BUCKET[kind] ?? "waiting";
}

export function seriesBucketCounts(tickets: SeriesRunTicket[]): Record<SeriesBucket, number> {
  const counts: Record<SeriesBucket, number> = { merged: 0, moving: 0, you: 0, waiting: 0 };
  for (const ticket of tickets) counts[seriesTicketBucket(ticket.kind)] += 1;
  return counts;
}

export type SeriesTicketFilter = "open" | "all" | "merged";

export const SERIES_TICKET_FILTERS: SeriesTicketFilter[] = ["open", "all", "merged"];

/** The rows the filter keeps, deepest first: the server sends the tickets in depth order, then series order. */
export function seriesTicketRows(tickets: SeriesRunTicket[], filter: SeriesTicketFilter): SeriesRunTicket[] {
  const kept = tickets.filter((t) => filter === "all" || (filter === "merged") === (t.kind === "merged"));
  return kept.slice().reverse();
}

/** The keys of the series' tickets a waiting ticket requires that have not merged yet; none for any other kind. */
export function seriesWaitsOn(ticket: SeriesRunTicket, tickets: SeriesRunTicket[]): string[] {
  if (ticket.kind !== "waiting") return [];
  const byId = new Map(tickets.map((t) => [t.ticketId, t]));
  return ticket.requires.map((id) => byId.get(id)).filter((t): t is SeriesRunTicket => !!t && t.kind !== "merged").map((t) => t.key);
}
