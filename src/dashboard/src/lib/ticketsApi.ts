// 2026-10-13-9a59p: the dashboard's read of the ticket list — GET /api/tickets, needing runs.read.
// The unfiltered first page and the counts feed the rail and the list together; a bucket view asks
// the server for its bucket and pages it with the cursor the server returns.

import { getJson } from "@/lib/apiResponse";
import type { TicketListView, TicketServerBucket } from "@/types/tickets";

export interface TicketListQuery {
  bucket?: TicketServerBucket;
  cursor?: string | null;
  limit?: number;
}

export function ticketListPath(query: TicketListQuery = {}): string {
  const params = new URLSearchParams();
  if (query.bucket) params.set("bucket", query.bucket);
  if (query.cursor) params.set("cursor", query.cursor);
  if (query.limit) params.set("limit", String(query.limit));
  const search = params.toString();
  return search ? `/api/tickets?${search}` : "/api/tickets";
}

export async function fetchTickets(query: TicketListQuery = {}, signal?: AbortSignal): Promise<TicketListView> {
  return getJson<TicketListView>(ticketListPath(query), signal);
}
