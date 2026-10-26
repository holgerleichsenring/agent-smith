// 2026-10-13-9a59p: the dashboard's read of the ticket list — GET /api/tickets, needing runs.read.
// The unfiltered first page and the counts feed the rail and the list together; a bucket view asks
// the server for its bucket and pages it with the cursor the server returns.

import { apiFetch, getJson, refused } from "@/lib/apiResponse";
import { ticketPath } from "@/lib/tickets/ticketAddress";
import type {
  DefaultVerdictAnswer,
  DefaultVerdictItem,
  RetryRefusal,
  RunCompute,
  RunPasses,
  TicketApprovalPreview,
  TicketConversation,
  TicketDefaults,
  TicketDetail,
  TicketListView,
  TicketReviews,
  TicketSeries,
  TicketServerBucket,
  TicketText,
} from "@/types/tickets";

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

// 2026-10-13-9a59q: the ticket page's reads, under /api at the page's own address.

export function fetchTicketDetail(project: string, ticketId: string, signal?: AbortSignal): Promise<TicketDetail> {
  return getJson<TicketDetail>(`/api${ticketPath(project, ticketId)}`, signal);
}

/** Without a run id the ticket's latest cut with specs; with one, that attempt's cut and gate. */
export function fetchTicketSeries(
  project: string, ticketId: string, runId: string | null, signal?: AbortSignal,
): Promise<TicketSeries> {
  const query = runId ? `?runId=${encodeURIComponent(runId)}` : "";
  return getJson<TicketSeries>(`/api${ticketPath(project, ticketId)}/series${query}`, signal);
}

export function fetchTicketConversation(
  project: string, ticketId: string, signal?: AbortSignal,
): Promise<TicketConversation> {
  return getJson<TicketConversation>(`/api${ticketPath(project, ticketId)}/conversation`, signal);
}

/** 2026-10-13-8762l: every attempt's review rounds and the approved set's conversation rounds. */
export function fetchTicketReviews(project: string, ticketId: string, signal?: AbortSignal): Promise<TicketReviews> {
  return getJson<TicketReviews>(`/api${ticketPath(project, ticketId)}/reviews`, signal);
}

/** 2026-10-13-8762q: the ticket's defaults, pending ones first. */
export function fetchTicketDefaults(project: string, ticketId: string, signal?: AbortSignal): Promise<TicketDefaults> {
  return getJson<TicketDefaults>(`/api${ticketPath(project, ticketId)}/defaults`, signal);
}

/** 2026-10-13-8762q: ratify or overrule several defaults in one act (runs.control). A 400 is an answer: its
 *  JSON-string body names the refused items. Every other non-2xx throws. */
export async function postDefaultVerdicts(
  project: string, ticketId: string, items: DefaultVerdictItem[],
): Promise<DefaultVerdictAnswer> {
  const path = `/api${ticketPath(project, ticketId)}/defaults/verdicts`;
  const res = await apiFetch(path, {
    method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(items),
  });
  if (res.status === 400) return { kind: "refused", text: String(await res.json()) };
  if (!res.ok) throw await refused(res, path);
  const body = (await res.json()) as Omit<Extract<DefaultVerdictAnswer, { kind: "applied" }>, "kind">;
  return { kind: "applied", applied: body.applied, alreadyDecided: body.alreadyDecided };
}

/** The ticket itself, read from its tracker (a minute's cache on the server), else an attempt's copy. */
export function fetchTicketText(project: string, ticketId: string, signal?: AbortSignal): Promise<TicketText> {
  return getJson<TicketText>(`/api${ticketPath(project, ticketId)}/text`, signal);
}

export function fetchRunPasses(runId: string, signal?: AbortSignal): Promise<RunPasses> {
  return getJson<RunPasses>(`/api/runs/${encodeURIComponent(runId)}/passes`, signal);
}

/** 2026-10-13-9a59s: every sandbox and launch segment of one run (9a59i, 9a59j); runs.read. */
export function fetchRunCompute(runId: string, signal?: AbortSignal): Promise<RunCompute> {
  return getJson<RunCompute>(`/api/runs/${encodeURIComponent(runId)}/compute`, signal);
}

// 2026-10-13-9a59r: what approving the open proposal would do; dialog.write. A 404 (no proposal open)
// throws like any other refusal — the caller asks only while the conversation shows one.
export function fetchApprovalPreview(
  project: string, ticketId: string, signal?: AbortSignal,
): Promise<TicketApprovalPreview> {
  return getJson<TicketApprovalPreview>(`/api${ticketPath(project, ticketId)}/approval-preview`, signal);
}

export type RetryAnswer = { kind: "requested" } | { kind: "refused"; refusal: RetryRefusal };

/** 2026-10-13-9a59r: Retry unchanged (9a59n, runs.control). A 409 is an answer, not a fault: its
 *  reason text is what the page shows. Every other non-2xx throws. */
/** 2026-10-26-bdd0f: how a derivation stores its specs. */
export type DeriveMode = "branch" | "preview";

export type DeriveAnswer = { kind: "started"; runId: string } | { kind: "refused"; text: string };

/** 2026-10-26-bdd0f: POST /api/tickets/{project}/{ticketId}/derive-specs; a refusal carries the server's sentence. */
export async function deriveSpecs(project: string, ticketId: string, mode: DeriveMode): Promise<DeriveAnswer> {
  const path = `/api${ticketPath(project, ticketId)}/derive-specs`;
  const res = await apiFetch(path, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ mode }),
  });
  if (res.ok) return { kind: "started", runId: ((await res.json()) as { runId: string }).runId };
  if (res.status === 409 || res.status === 503 || res.status === 400) return { kind: "refused", text: await res.text() };
  throw await refused(res, path);
}

export async function retryAttempt(runId: string): Promise<RetryAnswer> {
  const path = `/api/runs/${encodeURIComponent(runId)}/retry`;
  const res = await apiFetch(path, { method: "POST" });
  if (res.status === 409) return { kind: "refused", refusal: (await res.json()) as RetryRefusal };
  if (!res.ok) throw await refused(res, path);
  return { kind: "requested" };
}
