"use client";

import Link from "next/link";
import { useT } from "@/hooks/useT";
import type { TicketSummary } from "@/types/tickets";
import { conversationHref } from "../TicketNeedsYou";

// 2026-10-14-dbcen: the filed line of a ticket with no run on record. A ticket from a list is a ticket text whose
// specs its first run derives; a filed spec or epic already has them, so it says only what is known. Either way
// the specs can be settled first in the ticket's own Work it out conversation.

export function TicketNeverRun({ summary }: { summary: TicketSummary }) {
  const t = useT();
  return (
    <div className="filed-line" data-testid="ticket-filed-line">
      <span>{summary.ticketText ? t("tickets.neverRun.text") : t("tickets.neverRun.plain")}</span>
      <Link href={conversationHref(summary)} data-testid="ticket-settle-specs">{t("tickets.neverRun.settle")}</Link>
    </div>
  );
}
