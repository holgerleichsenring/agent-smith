// 2026-09-15-cb3e: the dialog surface's two calls — what is on this dialog id, and one
// message into it. Both carry the dialog id the BROWSER minted; the server decides whether
// this caller owns the conversation behind it.

import { apiFetch, getJson, refused } from "@/lib/apiResponse";
import type { SpecDialogView } from "@/types/spec-dialog";

const MESSAGES_PATH = "/api/spec-dialog/messages";

export async function fetchSpecDialog(
  dialogId: string,
  signal?: AbortSignal,
): Promise<SpecDialogView> {
  return getJson<SpecDialogView>(`/api/spec-dialog/${encodeURIComponent(dialogId)}`, signal);
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
