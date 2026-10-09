// 2026-10-09-86e1: the files INSIDE a conversation's uploads — listed, previewed, fetched as
// bytes, removed one at a time — and the content hashes the conversation holds, which the
// selection card compares a pick against. Every call carries the dialog id; the server answers
// only for the conversation open on it that the caller may reach. Bytes are fetched through
// apiFetch, never by an <img src> or a link: with sign-in on the bearer token rides only there.

import { apiFetch, getJson, refused } from "@/lib/apiResponse";
import type { SpecDialogFilePreview, SpecDialogHeldContent, SpecDialogReferenceFile } from "@/types/spec-dialog";

function filesPath(dialogId: string, setId: string, suffix = "", path?: string): string {
  const query = `dialogId=${encodeURIComponent(dialogId)}${path === undefined ? "" : `&path=${encodeURIComponent(path)}`}`;
  return `/api/spec-dialog/references/${encodeURIComponent(setId)}/files${suffix}?${query}`;
}

export async function fetchReferenceFiles(dialogId: string, setId: string): Promise<SpecDialogReferenceFile[]> {
  return getJson<SpecDialogReferenceFile[]>(filesPath(dialogId, setId));
}

export async function fetchReferenceFilePreview(dialogId: string, setId: string, path: string): Promise<SpecDialogFilePreview> {
  return getJson<SpecDialogFilePreview>(filesPath(dialogId, setId, "/preview", path));
}

/** One file's bytes — an image to draw, or anything to save. */
export async function fetchReferenceFileBlob(dialogId: string, setId: string, path: string): Promise<Blob> {
  return fetchBlob(filesPath(dialogId, setId, "/content", path));
}

/** One file out of a set; the set's last file takes the set with it. Refused (409) like a set. */
export async function deleteReferenceFile(dialogId: string, setId: string, path: string): Promise<void> {
  const target = filesPath(dialogId, setId, "", path);
  const res = await apiFetch(target, { method: "DELETE" });
  if (!res.ok) throw await refused(res, target);
}

export async function fetchHeldContent(dialogId: string): Promise<SpecDialogHeldContent[]> {
  return getJson<SpecDialogHeldContent[]>(`/api/spec-dialog/references/hashes?dialogId=${encodeURIComponent(dialogId)}`);
}

/** Bytes behind the bearer token, for a blob URL. */
export async function fetchBlob(path: string): Promise<Blob> {
  const res = await apiFetch(path);
  if (!res.ok) throw await refused(res, path);
  return res.blob();
}
