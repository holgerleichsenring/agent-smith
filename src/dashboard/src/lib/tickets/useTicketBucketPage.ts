"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { refusalIn, type ApiRefusal } from "@/lib/apiResponse";
import { fetchTickets } from "@/lib/ticketsApi";
import { SERVER_BUCKET, type TicketBucket, type TicketListView, type TicketSummary } from "@/types/tickets";
import { useTicketList } from "./TicketListProvider";

// 2026-10-13-9a59p: a bucket view reads its own pages — the shared first page holds every bucket,
// so a section filtered from it would show fewer rows than its count. The first page is reread
// whenever the shared list lands (the shared store already coalesced the nudges); pages loaded
// past it are kept and paged on with the server's cursor.

export interface TicketBucketPage {
  first: TicketListView | null;
  rows: TicketSummary[];
  hasMore: boolean;
  loadingMore: boolean;
  refusal: ApiRefusal | null;
  loadMore: () => void;
}

export function useTicketBucketPage(bucket: TicketBucket | null): TicketBucketPage {
  const { version } = useTicketList();
  const [first, setFirst] = useState<TicketListView | null>(null);
  const [older, setOlder] = useState<{ rows: TicketSummary[]; next: string | null } | null>(null);
  const [loadingMore, setLoadingMore] = useState(false);
  const [refusal, setRefusal] = useState<ApiRefusal | null>(null);

  useEffect(() => {
    setFirst(null);
    setOlder(null);
  }, [bucket]);

  useEffect(() => {
    if (bucket === null) return;
    const ctrl = new AbortController();
    fetchTickets({ bucket: SERVER_BUCKET[bucket] }, ctrl.signal)
      .then((page) => {
        setFirst(page);
        setRefusal(null);
      })
      .catch((thrown: unknown) => {
        if (!ctrl.signal.aborted) setRefusal(refusalIn(thrown));
      });
    return () => ctrl.abort();
  }, [bucket, version]);

  const next = older ? older.next : first?.nextCursor ?? null;
  const loadMore = useCallback(() => {
    if (bucket === null || next === null || loadingMore) return;
    setLoadingMore(true);
    fetchTickets({ bucket: SERVER_BUCKET[bucket], cursor: next })
      .then((page) => setOlder((prev) => ({ rows: [...(prev?.rows ?? []), ...page.tickets], next: page.nextCursor })))
      .catch(() => setOlder((prev) => ({ rows: prev?.rows ?? [], next: null })))
      .finally(() => setLoadingMore(false));
  }, [bucket, next, loadingMore]);

  const rows = useMemo(() => unique([...(first?.tickets ?? []), ...(older?.rows ?? [])]), [first, older]);
  return { first, rows, hasMore: next !== null, loadingMore, refusal, loadMore };
}

function unique(rows: TicketSummary[]): TicketSummary[] {
  const seen = new Set<string>();
  return rows.filter((row) => {
    const key = `${row.project}\u0000${row.ticketId}`;
    if (seen.has(key)) return false;
    seen.add(key);
    return true;
  });
}
