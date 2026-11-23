"use client";

import { NoProjectReach } from "@/components/shell/NoProjectReach";
import { RefusalSurface } from "@/components/shell/RefusalSurface";
import { useTicketBucketFilter } from "@/lib/TicketBucketFilter";
import { useTicketList } from "@/lib/tickets/TicketListProvider";
import { useTicketBucketPage, type TicketBucketPage } from "@/lib/tickets/useTicketBucketPage";
import { SERVER_BUCKET, TICKET_BUCKETS, type TicketBucket, type TicketListView } from "@/types/tickets";
import { TicketMetricStrip } from "./TicketMetricStrip";
import { TicketSection } from "./TicketSection";
import { TICKET_SECTIONS, totalTickets } from "./ticketSections";
import { countsFor, isYourMove } from "./holds/holdWords";
import { useHoldsAdmin } from "@/hooks/useHoldsAdmin";

// 2026-10-13-9a59p: every ticket agent-smith has worked, grouped by who has to act next. The
// unfiltered view reads the shared first page the rail reads too; a bucket view pages its own.
// 2026-11-12-aedda: the unfiltered view pages on from the shared page with Load more.
// 2026-11-23-31dcg: Needs you is Your move — the tickets a person's hold lies on, and for an admin the admin holds too,
// which then leave Stuck; the counts are read the same way, so a section and its count agree.

export function TicketList() {
  const { filter } = useTicketBucketFilter();
  const shared = useTicketList();
  const bucket = filter === "all" ? null : filter;
  const admin = useHoldsAdmin();
  const page = useTicketBucketPage(bucket, admin);
  const read = shared.list ?? page.first;
  const list = read ? { ...read, counts: countsFor(read.counts, admin) } : null;
  const refusal = shared.refusal ?? page.refusal;

  if (!list && refusal) return <RefusalSurface refusal={refusal} surface="the ticket list" />;
  if (!list && shared.failed) return <div className="msub" data-testid="tickets-failed">The ticket list could not be read.</div>;
  if (!list) return <Loading />;
  if (totalTickets(list.counts) === 0) {
    return <NoProjectReach><div className="msub" data-testid="tickets-empty">No ticket has been worked yet. A ticket appears here with its first code run.</div></NoProjectReach>;
  }
  return (
    <div data-testid="ticket-list" data-bucket={filter}>
      <TicketMetricStrip counts={list.counts} />
      {list.counts.truncated && (
        <div className="msub note" data-testid="tickets-truncated">
          The list stopped at the server&apos;s read ceiling; older tickets are not counted.
        </div>
      )}
      {bucket ? <BucketView bucket={bucket} list={list} page={page} admin={admin} /> : <AllSections list={list} page={page} admin={admin} />}
    </div>
  );
}

function AllSections({ list, page, admin }: { list: TicketListView; page: TicketBucketPage; admin: boolean }) {
  const shown = TICKET_BUCKETS.filter((b) => b === "needs-you" || TICKET_SECTIONS[b].count(list.counts) > 0);
  return (
    <>
      {shown.map((b, i) => (
        <TicketSection
          key={b}
          bucket={b}
          counts={list.counts}
          rows={page.rows.filter((t) => (b === "needs-you" ? isYourMove(t, admin) : t.bucket === SERVER_BUCKET[b] && !isYourMove(t, admin)))}
          first={i === 0}
          chosen={false}
        />
      ))}
      <LoadMore page={page} />
    </>
  );
}

function BucketView({ bucket, list, page, admin }: { bucket: TicketBucket; list: TicketListView; page: TicketBucketPage; admin: boolean }) {
  // An admin's admin holds are Your move's; the Stuck view leaves them out, as its count does.
  const rows = bucket === "needs-you" ? page.rows : page.rows.filter((t) => !isYourMove(t, admin));
  return <TicketSection bucket={bucket} counts={list.counts} rows={rows} first chosen footer={<LoadMore page={page} />} />;
}

function LoadMore({ page }: { page: TicketBucketPage }) {
  if (!page.hasMore) return null;
  return (
    <button type="button" className="btn load-more" onClick={page.loadMore} disabled={page.loadingMore} data-testid="tickets-load-more">
      {page.loadingMore ? "Loading…" : "Load more"}
    </button>
  );
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
