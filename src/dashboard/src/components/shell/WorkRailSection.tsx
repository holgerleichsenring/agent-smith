"use client";

import { usePathname } from "next/navigation";
import { TICKETS_PATH, ticketBucketHref, useTicketBucketFilter, type TicketBucketFilter } from "@/lib/TicketBucketFilter";
import { useTicketList } from "@/lib/tickets/TicketListProvider";
import { TICKET_SECTIONS, totalTickets } from "@/components/tickets/ticketSections";
import { TICKET_BUCKETS } from "@/types/tickets";
import { AppRailItem } from "./AppRailItem";

// 2026-10-13-9a59p: the rail's WORK section — the ticket list and its buckets, each with the
// count the server computed over every ticket, read from the same shared response the list
// renders, so the rail and the sections cannot disagree. A bucket is marked only on /tickets.

const ICON: Record<TicketBucketFilter, string> = {
  all: "◧",
  "needs-you": "?",
  working: "▶",
  "waiting-on-review": "◔",
  stuck: "!",
  blocked: "⧗",
  filed: "○",
  "pr-closed": "⊘",
  failed: "✕",
  ended: "■",
  done: "✓",
  closed: "◌",
};

export function WorkRailSection() {
  const pathname = usePathname();
  const { filter, select } = useTicketBucketFilter();
  const counts = useTicketList().list?.counts;
  const isChosen = (bucket: TicketBucketFilter) => pathname === TICKETS_PATH && filter === bucket;
  const choose = (bucket: TicketBucketFilter) => (event: React.MouseEvent) => {
    if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
    event.preventDefault();
    select(bucket);
  };
  return (
    <>
      <div className="nav-h" data-testid="app-rail-section-Work">
        Work
      </div>
      <AppRailItem
        label="Tickets"
        href={ticketBucketHref("all")}
        icon={ICON.all}
        active={isChosen("all")}
        count={counts ? totalTickets(counts) : undefined}
        onClick={choose("all")}
      />
      {TICKET_BUCKETS.map((bucket) => {
        const count = counts ? TICKET_SECTIONS[bucket].count(counts) : undefined;
        return (
          <AppRailItem
            key={bucket}
            label={TICKET_SECTIONS[bucket].title}
            href={ticketBucketHref(bucket)}
            icon={ICON[bucket]}
            active={isChosen(bucket)}
            count={count}
            hot={bucket === "needs-you" && (count ?? 0) > 0}
            onClick={choose(bucket)}
          />
        );
      })}
    </>
  );
}
