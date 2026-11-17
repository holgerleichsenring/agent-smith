// 2026-11-17-dd05d: a person has the ticket's tracker text rendered again from its specs (runs.control) — a
// 'ticket-body' run. The server answers as a plain sentence: rewritten (200), or why not (404, 409); any other non-2xx
// throws.

import { apiFetch, refused } from "@/lib/apiResponse";
import { ticketPath } from "@/lib/tickets/ticketAddress";

export type TicketBodyResult = { rewritten: boolean; text: string };

const SENTENCE_STATUSES = new Set([404, 409]);

export async function overwriteTicketBody(project: string, ticketId: string): Promise<TicketBodyResult> {
  const path = `/api${ticketPath(project, ticketId)}/body`;
  const res = await apiFetch(path, { method: "POST" });
  if (res.ok) return { rewritten: true, text: (await res.text()).trim() };
  if (SENTENCE_STATUSES.has(res.status)) return { rewritten: false, text: (await res.text()).trim() };
  throw await refused(res, path);
}
