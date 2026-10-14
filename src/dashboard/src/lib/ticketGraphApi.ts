// 2026-10-14-dbcen: the requires graph, one ticket's requires (runs.read) and marking a ticket done by hand
// (runs.control). Every non-200 answer of the mark is a plain sentence the server wrote — a missing note, an
// unknown project or ticket, a landing that already stands, an unreachable tracker — shown as is; a non-2xx
// without a sentence throws.

import { apiFetch, getJson, refused } from "@/lib/apiResponse";
import { ticketPath } from "@/lib/tickets/ticketAddress";
import type { TicketGraph, TicketRequires } from "@/types/tickets";

export function fetchTicketGraph(project?: string | null, signal?: AbortSignal): Promise<TicketGraph> {
  const query = project ? `?project=${encodeURIComponent(project)}` : "";
  return getJson<TicketGraph>(`/api/tickets/graph${query}`, signal);
}

export function fetchTicketRequires(project: string, ticketId: string, signal?: AbortSignal): Promise<TicketRequires> {
  return getJson<TicketRequires>(`/api${ticketPath(project, ticketId)}/requires`, signal);
}

/** The answers that carry a sentence: a missing note, an unknown project or ticket, a landing that stands, the tracker. */
const SENTENCE_STATUSES = new Set([400, 404, 409, 503]);

export type LandedByHandAnswer = { kind: "landed" } | { kind: "said"; text: string };

export async function markLandedByHand(project: string, ticketId: string, note: string): Promise<LandedByHandAnswer> {
  const path = `/api${ticketPath(project, ticketId)}/landed-by-hand`;
  const res = await apiFetch(path, {
    method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ note }),
  });
  if (res.ok) return { kind: "landed" };
  const text = SENTENCE_STATUSES.has(res.status) ? (await res.text()).trim() : "";
  if (text) return { kind: "said", text };
  throw await refused(res, path);
}
