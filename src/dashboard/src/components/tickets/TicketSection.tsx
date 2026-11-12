"use client";

import type { ReactNode } from "react";
import Link from "next/link";
import { cn } from "@/lib/utils";
import { RenderBoundary } from "@/components/shell/RenderBoundary";
import { ticketBucketHref, useTicketBucketFilter } from "@/lib/TicketBucketFilter";
import type { TicketBucket, TicketCounts, TicketSummary } from "@/types/tickets";
import { TicketNeedsYou } from "./TicketNeedsYou";
import { TicketRow } from "./TicketRow";
import { TICKET_SECTIONS } from "./ticketSections";

// 2026-10-13-9a59p: one bucket of the ticket list — the mock's slim .section-head with its count
// from the server's counts, and the rows beneath. A section that holds more tickets than were
// loaded ends with "Show all {n}" into its bucket view. 2026-11-12-aedda: every section draws its
// rows — Done and Closed were collapsed behind a button, which hid most tickets of a list whose
// work is largely finished; this month's merges stay in the metric strip.

interface Props {
  bucket: TicketBucket;
  counts: TicketCounts;
  rows: TicketSummary[];
  first: boolean;
  /** The bucket's own view: no Show-all link. */
  chosen: boolean;
  footer?: ReactNode;
}

export function TicketSection({ bucket, counts, rows, first, chosen, footer }: Props) {
  const spec = TICKET_SECTIONS[bucket];
  const count = spec.count(counts);
  return (
    <section data-testid={`ticket-section-${bucket}`}>
      <div className={cn("section-head", first && "first")}>
        <h2>{spec.title}</h2>
        <span className={cn("cnt", count > 0 && spec.tone)} data-testid={`ticket-section-${bucket}-count`}>
          {count}
        </span>
        <span className="sh-sub">{spec.sub}</span>
      </div>
      {rows.length === 0 ? <div className="msub">{spec.empty}</div> : <Rows bucket={bucket} rows={rows} />}
      {!chosen && count > rows.length && <ShowAll bucket={bucket} count={count} />}
      {footer}
    </section>
  );
}

function Rows({ bucket, rows }: { bucket: TicketBucket; rows: TicketSummary[] }) {
  return (
    <div className="rows">
      {rows.map((t) => (
        <RenderBoundary key={`${t.project}/${t.ticketId}`} surface={`ticket ${t.ticketId}`}>
          {bucket === "needs-you" ? <TicketNeedsYou ticket={t} /> : <TicketRow ticket={t} />}
        </RenderBoundary>
      ))}
    </div>
  );
}

function ShowAll({ bucket, count }: { bucket: TicketBucket; count: number }) {
  const { select } = useTicketBucketFilter();
  return (
    <Link
      className="show-all"
      href={ticketBucketHref(bucket)}
      data-testid={`ticket-section-${bucket}-show-all`}
      onClick={(event) => {
        if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
        event.preventDefault();
        select(bucket);
      }}
    >
      Show all {count} ›
    </Link>
  );
}
