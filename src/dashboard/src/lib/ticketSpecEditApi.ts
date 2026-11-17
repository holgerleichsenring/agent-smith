// 2026-11-17-dd05c: a person edits one spec of a filed ticket (runs.control). The server answers every outcome as a
// plain sentence — what was recorded, or why nothing was (400 no yaml, 404 no project, 409 refused); any other non-2xx
// throws.

import { apiFetch, refused } from "@/lib/apiResponse";
import { ticketPath } from "@/lib/tickets/ticketAddress";

export type TicketSpecEditResult = { recorded: boolean; text: string };

const SENTENCE_STATUSES = new Set([400, 404, 409]);

export async function editTicketSpec(project: string, ticketId: string, specId: string, yaml: string): Promise<TicketSpecEditResult> {
  const path = `/api${ticketPath(project, ticketId)}/specs/${encodeURIComponent(specId)}`;
  const res = await apiFetch(path, { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ yaml }) });
  if (res.ok) return { recorded: true, text: (await res.text()).trim() };
  if (SENTENCE_STATUSES.has(res.status)) return { recorded: false, text: (await res.text()).trim() };
  throw await refused(res, path);
}
