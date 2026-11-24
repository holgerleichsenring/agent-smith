"use client";

import { useT } from "@/hooks/useT";
import type { T } from "@/lib/i18n/translate";
import { StatePills, type StatePill, type StateTone } from "@/components/ui/StatePills";
import { ticketBucketHref, useTicketBucketFilter, type TicketBucketFilter } from "@/lib/TicketBucketFilter";
import { useTicketList } from "@/lib/tickets/TicketListProvider";
import type { TicketBucket, TicketCounts } from "@/types/tickets";
import { TICKET_SECTIONS, totalTickets } from "./ticketSections";

// 2026-11-16-0440b: the ticket list's states as pills, with the counts the server computed over
// every ticket, read from the same shared response the list renders. The pills replace the rail's
// bucket items, so every bucket stays reachable: the ones a person checks daily always show, the
// rest while they hold a ticket or are the one chosen.

const DAILY: readonly TicketBucket[] = ["needs-you", "working", "stuck", "blocked", "done", "closed"];
const OCCASIONAL: readonly TicketBucket[] = ["filed", "pr-closed", "failed", "ended"];

const TONE: Partial<Record<TicketBucket, StateTone>> = {
  "needs-you": "needs",
  working: "proven",
  stuck: "stopped",
  "pr-closed": "stopped",
  failed: "stopped",
};

export function ticketPills(t: T, counts: TicketCounts | undefined, filter: TicketBucketFilter): StatePill<TicketBucketFilter>[] {
  const pill = (bucket: TicketBucket): StatePill<TicketBucketFilter> => ({
    key: bucket,
    label: t(TICKET_SECTIONS[bucket].title),
    href: ticketBucketHref(bucket),
    count: counts ? TICKET_SECTIONS[bucket].count(counts) : undefined,
    tone: TONE[bucket],
  });
  const occasional = OCCASIONAL.filter((b) => b === filter || (counts ? TICKET_SECTIONS[b].count(counts) > 0 : false));
  return [
    { key: "all", label: t("tickets.pills.all"), href: ticketBucketHref("all"), count: counts ? totalTickets(counts) : undefined },
    ...DAILY.map(pill),
    ...occasional.map(pill),
  ];
}

export function TicketStatePills() {
  const t = useT();
  const { filter, select } = useTicketBucketFilter();
  const counts = useTicketList().list?.counts;
  return (
    <StatePills label={t("tickets.pills.label")} pills={ticketPills(t, counts, filter)} selected={filter} onSelect={select} testId="ticket-pills" />
  );
}
