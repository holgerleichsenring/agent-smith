// 2026-09-15-cb3e: the dialog surface's calls — what is on this dialog id, one message into
// it, and the caller's conversations. The first two carry the dialog id the BROWSER minted;
// the server decides whether this caller owns the conversation behind it. The list carries
// no id at all: it answers for the signed-in principal.

import { apiFetch, getJson, refused } from "@/lib/apiResponse";
import type {
  FiledWork,
  SpecDialogImage,
  SpecDialogSessionSummary,
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
): Promise<void> {
  const res = await apiFetch(MESSAGES_PATH, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ dialogId, text, project: project ?? null }),
  });
  if (!res.ok) throw await refused(res, MESSAGES_PATH);
}
