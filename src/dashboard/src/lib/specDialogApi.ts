// 2026-09-15-cb3e: the dialog surface's calls — what is on this dialog id, one message into
// it, and the caller's conversations.
// 2026-09-22-2a86: and continuing one of those conversations here, which is a route of its own
// rather than a command the page types at its own server. The first two carry the dialog id the BROWSER minted;
// the server decides whether this caller owns the conversation behind it. The list carries
// no id at all: it answers for the signed-in principal.

import { apiFetch, getJson, refused } from "@/lib/apiResponse";
import type {
  FiledWork,
  SpecDialogConversationPage,
  SpecDialogImage,
  SpecDialogView,
} from "@/types/spec-dialog";

const MESSAGES_PATH = "/api/spec-dialog/messages";

export async function fetchSpecDialog(
  dialogId: string,
  signal?: AbortSignal,
): Promise<SpecDialogView> {
  return getJson<SpecDialogView>(`/api/spec-dialog/${encodeURIComponent(dialogId)}`, signal);
}

/**
 * 2026-09-17-042ej: the work this conversation filed, as its runs now stand. A read of its
 * own, because the dialog view is refetched after every reply and run lookups there would be
 * paid on each one. The server checks the same ownership the dialog read does.
 */
export async function fetchFiledWork(
  dialogId: string,
  signal?: AbortSignal,
): Promise<FiledWork> {
  return getJson<FiledWork>(
    `/api/spec-dialog/${encodeURIComponent(dialogId)}/filed-work`,
    signal,
  );
}

/**
 * The caller's conversations, open and closed, most recently active first, with how many they
 * hold in all. 2026-09-21-f237b: the limit is the caller's — the panel reads the server's own
 * default, the conversations page asks for the ceiling — and the server clamps it either way.
 */
export async function fetchSpecDialogConversations(
  limit?: number,
  signal?: AbortSignal,
): Promise<SpecDialogConversationPage> {
  const path = limit === undefined
    ? "/api/spec-dialog/conversations"
    : `/api/spec-dialog/conversations?limit=${encodeURIComponent(String(limit))}`;
  return getJson<SpecDialogConversationPage>(path, signal);
}

/**
 * 2026-09-18-7a05: one conversation the caller owns, deleted. Addressed by its SESSION id — a
 * dialog id is only the tab it was last on. The server answers the same for a conversation that
 * is not yours, is not there, or is already gone, so nothing here learns which ids exist; a
 * conversation with a turn running is the one refusal it makes.
 */
export async function deleteSpecDialogConversation(sessionId: string): Promise<void> {
  const path = `/api/spec-dialog/conversations/${encodeURIComponent(sessionId)}`;
  const res = await apiFetch(path, { method: "DELETE" });
  if (!res.ok) throw await refused(res, path);
}

/**
 * 2026-09-22-2a86: a conversation the caller owns, continued on the dialog id this tab holds.
 * The page used to post "/spec resume <id>" as message text and let the server parse it back;
 * this is the same act as a route. Addressed by the conversation's SESSION id, because a
 * dialog id is only the tab it was last on. The server checks BOTH owners — the conversation
 * being moved and the dialog it is moved onto, which the move closes whatever is open on.
 */
export async function resumeSpecDialogConversation(
  sessionId: string,
  dialogId: string,
): Promise<void> {
  const path = `/api/spec-dialog/conversations/${encodeURIComponent(sessionId)}/resume`;
  const res = await apiFetch(path, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ dialogId }),
  });
  if (!res.ok) throw await refused(res, path);
}

/**
 * 2026-09-20-3af8: one image into the conversation on this dialog id. The body IS the image —
 * nothing is bound from it, so an over-size upload is refused on its declared length before the
 * bytes are read — and the project rides the query because an upload may be the FIRST thing on
 * a dialog id: the server opens the conversation rather than losing the opening screenshot.
 */
export async function uploadSpecDialogImage(
  dialogId: string,
  project: string,
  file: File,
): Promise<SpecDialogImage> {
  const path = `/api/spec-dialog/images?dialogId=${encodeURIComponent(dialogId)}`
    + `&project=${encodeURIComponent(project)}`;
  const res = await apiFetch(path, {
    method: "POST",
    headers: { "Content-Type": file.type || "application/octet-stream" },
    body: file,
  });
  if (!res.ok) throw await refused(res, path);
  return (await res.json()) as SpecDialogImage;
}

/** Where the transcript reads one stored image from. */
export function specDialogImageUrl(imageId: number): string {
  return `/api/spec-dialog/images/${imageId}`;
}

/**
 * One message into the dialog. The reply does NOT come back here — a design turn runs a
 * master and can take minutes, so the server accepts the message and answers on the hub.
 *
 * 2026-09-20-4b0aa: the project rides along for the same reason the upload's does. A message may
 * be the FIRST thing on a dialog id, and the server opens the conversation and routes the message
 * in one ordered act — which two posts, each only accepted, could never order between themselves.
 */
export async function postSpecDialogMessage(
  dialogId: string,
  text: string,
  project?: string,
  /** 2026-09-25-8e51b: the ticket this conversation is to belong to, when the page was opened on
   *  one. It rides with the project for the same reason: the first message is what OPENS the
   *  conversation, and a conversation that missed its binding can never be given one. */
  ticketId?: string,
): Promise<void> {
  const res = await apiFetch(MESSAGES_PATH, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      dialogId,
      text,
      project: project ?? null,
      ticketId: ticketId ?? null,
    }),
  });
  if (!res.ok) throw await refused(res, MESSAGES_PATH);
}

/** 2026-09-25-8e51b: which conversation a ticket has, and the dialog a running one is on.
 *  A page cannot work either out: it opens a conversation by SESSION id, and only the DIALOG id
 *  sends it to a live conversation instead of queueing a resume the server refuses mid-turn. */
export async function readTicketConversation(
  project: string,
  ticketId: string,
): Promise<TicketConversationRead | null> {
  const path = `/api/spec-dialog/tickets/${encodeURIComponent(project)}/${encodeURIComponent(ticketId)}`;
  const res = await apiFetch(path);
  if (res.status === 404) return null;
  if (!res.ok) throw await refused(res, path);
  return (await res.json()) as TicketConversationRead;
}

/** 2026-09-25-8e51a: the ticket alone — every configured tracker is asked for it, and the
 *  projects routed to the one that has it are matched from its own labels. */
export async function readTicketProject(ticketId: string): Promise<TicketProjectRead | null> {
  const path = `/api/spec-dialog/tickets/${encodeURIComponent(ticketId)}`;
  const res = await apiFetch(path);
  if (res.status === 404) return null;
  if (!res.ok) throw await refused(res, path);
  return (await res.json()) as TicketProjectRead;
}

export interface TicketProjectRead extends TicketConversationRead {
  tracker: string;
  /** The projects this ticket's own routing names. One is chosen; none or several are asked about. */
  projects: string[];
  /** Projects routed by area path, repository or address — which a ticket read by id cannot
   *  carry, so they are unanswerable rather than unmatched. Shown as a reason, not a fault. */
  unanswerable: string[];
}

export interface TicketConversationRead {
  ticketId: string;
  title: string;
  /** Null when no conversation has been opened for this ticket yet. */
  sessionId: string | null;
  /** Null when the conversation exists but is closed. */
  openDialogId: string | null;
}
