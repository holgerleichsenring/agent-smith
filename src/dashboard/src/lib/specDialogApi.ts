// 2026-09-15-cb3e: the dialog surface's calls — what is on this dialog id, one message into
// it, and the caller's conversations. The first two carry the dialog id the BROWSER minted;
// the server decides whether this caller owns the conversation behind it. The list carries
// no id at all: it answers for the signed-in principal.

import { apiFetch, getJson, refused } from "@/lib/apiResponse";
import type { FiledWork, SpecDialogSessionSummary, SpecDialogView } from "@/types/spec-dialog";

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

/** The caller's conversations, open and closed, most recently active first. */
export async function fetchSpecDialogConversations(
  signal?: AbortSignal,
): Promise<SpecDialogSessionSummary[]> {
  return getJson<SpecDialogSessionSummary[]>("/api/spec-dialog/conversations", signal);
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
 * One message into the dialog. The reply does NOT come back here — a design turn runs a
 * master and can take minutes, so the server accepts the message and answers on the hub.
 */
export async function postSpecDialogMessage(dialogId: string, text: string): Promise<void> {
  const res = await apiFetch(MESSAGES_PATH, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ dialogId, text }),
  });
  if (!res.ok) throw await refused(res, MESSAGES_PATH);
}
