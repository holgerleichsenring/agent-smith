"use client";

import { RefusalSurface } from "@/components/shell/RefusalSurface";
import { useTicketBucketFilter } from "@/lib/TicketBucketFilter";
import { useTicketList } from "@/lib/tickets/TicketListProvider";
import { useTicketBucketPage, type TicketBucketPage } from "@/lib/tickets/useTicketBucketPage";
import { SERVER_BUCKET, TICKET_BUCKETS, type TicketBucket, type TicketListView } from "@/types/tickets";
import { TicketMetricStrip } from "./TicketMetricStrip";
import { TicketSection } from "./TicketSection";
import { TICKET_SECTIONS, totalTickets } from "./ticketSections";

// 2026-10-13-9a59p: every ticket agent-smith has worked, grouped by who has to act next. The
// unfiltered view reads the shared first page the rail reads too; a bucket view pages its own.

export function TicketList() {
  const { filter } = useTicketBucketFilter();
  const shared = useTicketList();
  const bucket = filter === "all" ? null : filter;
  const page = useTicketBucketPage(bucket);
  const list = shared.list ?? page.first;
  const refusal = shared.refusal ?? page.refusal;

  if (!list && refusal) return <RefusalSurface refusal={refusal} surface="the ticket list" />;
  if (!list && shared.failed) return <div className="msub" data-testid="tickets-failed">The ticket list could not be read.</div>;
  if (!list) return <Loading />;
  if (totalTickets(list.counts) === 0) {
    return <div className="msub" data-testid="tickets-empty">No ticket has been worked yet. A ticket appears here with its first code run.</div>;
  }
  return (
    <div data-testid="ticket-list" data-bucket={filter}>
      <TicketMetricStrip counts={list.counts} />
      {list.counts.truncated && (
        <div className="msub note" data-testid="tickets-truncated">
          The list stopped at the server&apos;s read ceiling; older tickets are not counted.
        </div>
      )}
      {bucket ? <BucketView bucket={bucket} list={list} page={page} /> : <AllSections list={list} />}
    </div>
  );
}

function AllSections({ list }: { list: TicketListView }) {
  const shown = TICKET_BUCKETS.filter((b) => b === "needs-you" || TICKET_SECTIONS[b].count(list.counts) > 0);
  return (
    <>
      {shown.map((b, i) => (
        <TicketSection
          key={b}
          bucket={b}
          counts={list.counts}
          rows={list.tickets.filter((t) => t.bucket === SERVER_BUCKET[b])}
          first={i === 0}
          chosen={false}
        />
      ))}
    </>
  );
}

function BucketView({ bucket, list, page }: { bucket: TicketBucket; list: TicketListView; page: TicketBucketPage }) {
  const more = page.hasMore ? (
    <button type="button" className="btn load-more" onClick={page.loadMore} disabled={page.loadingMore} data-testid="tickets-load-more">
      {page.loadingMore ? "Loading…" : "Load more"}
    </button>
  ) : null;
  return <TicketSection bucket={bucket} counts={list.counts} rows={page.rows} first chosen footer={more} />;
}

function Loading() {
  return (
    <div className="health" data-testid="tickets-loading">
      {[0, 1, 2, 3, 4, 5].map((i) => (
        <div key={i} className="metric h-16 animate-pulse" />
      ))}
    </div>
  );
}
