// 2026-09-15-cb3e: the dialog surface's calls — what is on this dialog id, one message into
// it, and the caller's conversations. The first two carry the dialog id the BROWSER minted;
// the server decides whether this caller owns the conversation behind it. The list carries
// no id at all: it answers for the signed-in principal.

import { apiFetch, getJson, refused } from "@/lib/apiResponse";
import type { SpecDialogSessionSummary, SpecDialogView } from "@/types/spec-dialog";

const MESSAGES_PATH = "/api/spec-dialog/messages";

export async function fetchSpecDialog(
  dialogId: string,
  signal?: AbortSignal,
): Promise<SpecDialogView> {
  return getJson<SpecDialogView>(`/api/spec-dialog/${encodeURIComponent(dialogId)}`, signal);
}

/** The caller's conversations, open and closed, most recently active first. */
export async function fetchSpecDialogConversations(
  signal?: AbortSignal,
): Promise<SpecDialogSessionSummary[]> {
  return getJson<SpecDialogSessionSummary[]>("/api/spec-dialog/conversations", signal);
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
