"use client";

import Link from "next/link";
import type { TicketSummary } from "@/types/tickets";
import { conversationHref } from "../TicketNeedsYou";

// 2026-10-14-dbcen: the filed line of a ticket with no run on record. A ticket from a list is a ticket text whose
// specs its first run derives; a filed spec or epic already has them, so it says only what is known. Either way
// the specs can be settled first in the ticket's own Work it out conversation.

export const FILED_TICKET_TEXT = "Filed, no run on record — its specs are derived when its first run starts";
export const FILED_PLAIN = "Filed, no run on record";

export function TicketNeverRun({ summary }: { summary: TicketSummary }) {
  return (
    <div className="filed-line" data-testid="ticket-filed-line">
      <span>{summary.ticketText ? FILED_TICKET_TEXT : FILED_PLAIN}</span>
      <Link href={conversationHref(summary)} data-testid="ticket-settle-specs">Settle its specs first in Work it out →</Link>
    </div>
  );
}
