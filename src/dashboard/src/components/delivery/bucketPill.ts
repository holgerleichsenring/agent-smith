import { SERVER_BUCKET, type TicketBucket, type TicketServerBucket } from "@/types/tickets";
import { TICKET_SECTIONS } from "@/components/tickets/ticketSections";

// 2026-10-25-0099c: a ticket's outcome is the bucket the ticket list shows it in, named as the Tickets page names
// its sections, so a row and the page it opens say the same.

const URL_BUCKET = Object.fromEntries(
  (Object.entries(SERVER_BUCKET) as [TicketBucket, TicketServerBucket][]).map(([url, server]) => [server, url]),
) as Record<TicketServerBucket, TicketBucket>;

export function bucketLabel(bucket: TicketServerBucket | null): string {
  if (bucket === null) return "-";
  return TICKET_SECTIONS[URL_BUCKET[bucket]]?.title ?? bucket;
}

/** The pill's tone: the section's own colour, else plain. */
export function bucketTone(bucket: TicketServerBucket | null): string {
  if (bucket === "done") return "ok";
  const tone = bucket === null ? undefined : TICKET_SECTIONS[URL_BUCKET[bucket]]?.tone;
  return tone === "red" ? "bad" : tone === "amber" ? "run" : tone === "person" ? "person" : "plain";
}
