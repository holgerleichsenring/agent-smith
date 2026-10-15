// 2026-10-15-114fd: a person closes a ticket — an optional note, optionally its open pull requests and its tracker
// ticket — and reopens one they closed (runs.control). A 404 or 409 is a plain sentence the server wrote (no such
// project or ticket, a landed ticket, a live attempt, a merged pull request, a reopen that found no close) and is
// shown as is; any other non-2xx throws.

import { apiFetch, refused } from "@/lib/apiResponse";
import { ticketPath } from "@/lib/tickets/ticketAddress";

/** What a close did to one pull request: reason null when it closed, else a class (already-closed, refused, …). */
export interface TicketPrCloseOutcome {
  url: string;
  closed: boolean;
  reason: string | null;
}

export interface TicketTrackerCloseOutcome {
  closed: boolean;
  reason: string | null;
}

/** The 200 answer of a close (TicketCloseAnswer): one outcome per named pull request, the tracker's when asked. */
export interface TicketCloseAnswer {
  pullRequests: TicketPrCloseOutcome[];
  tracker: TicketTrackerCloseOutcome | null;
}

export interface TicketCloseRequest {
  note: string | null;
  closePullRequests: boolean;
  closeOnTracker: boolean;
}

const SENTENCE_STATUSES = new Set([404, 409]);

export type TicketCloseResult = { kind: "closed"; answer: TicketCloseAnswer } | { kind: "said"; text: string };
export type TicketReopenResult = { kind: "reopened" } | { kind: "said"; text: string };

export async function closeTicket(project: string, ticketId: string, request: TicketCloseRequest): Promise<TicketCloseResult> {
  const res = await post(`/api${ticketPath(project, ticketId)}/close`, request);
  if (res.ok) return { kind: "closed", answer: (await res.json()) as TicketCloseAnswer };
  return { kind: "said", text: await sentence(res, `/api${ticketPath(project, ticketId)}/close`) };
}

export async function reopenTicket(project: string, ticketId: string): Promise<TicketReopenResult> {
  const res = await post(`/api${ticketPath(project, ticketId)}/reopen`, null);
  if (res.ok) return { kind: "reopened" };
  return { kind: "said", text: await sentence(res, `/api${ticketPath(project, ticketId)}/reopen`) };
}

function post(path: string, body: unknown): Promise<Response> {
  return apiFetch(path, {
    method: "POST",
    headers: body === null ? undefined : { "Content-Type": "application/json" },
    body: body === null ? undefined : JSON.stringify(body),
  });
}

async function sentence(res: Response, path: string): Promise<string> {
  const text = SENTENCE_STATUSES.has(res.status) ? (await res.text()).trim() : "";
  if (text) return text;
  throw await refused(res, path);
}
