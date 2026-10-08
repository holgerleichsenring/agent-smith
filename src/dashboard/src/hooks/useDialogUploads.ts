"use client";

import { useCallback, useState } from "react";
import { useConfirmDialog, type ConfirmDialogProps } from "@/components/dialog/ConfirmDialog";
import { ApiResponseError, refusalIn } from "@/lib/apiResponse";
import { deleteSpecDialogImage, deleteSpecDialogReference } from "@/lib/specDialogApi";
import type { SpecDialogImage, SpecDialogReferenceSet } from "@/types/spec-dialog";

// 2026-10-08-e8b9h: taking one upload back out of the conversation. The page asks first, in its
// own dialog, because a removal cannot be undone; then the view is read again and the transcript
// RESEEDED, so the chip of a removed set goes with it. A refusal — a turn running or waiting on an
// approval, an upload an approval cites — is said where the Remove was pressed.

export type DialogUpload =
  | { kind: "set"; set: SpecDialogReferenceSet }
  | { kind: "image"; image: SpecDialogImage };

export function useDialogUploads(
  dialogId: string | null,
  refresh: () => Promise<void>,
): { remove: (upload: DialogUpload) => Promise<void>; note: string | null; dialog: ConfirmDialogProps } {
  const { ask, dialog } = useConfirmDialog();
  const [note, setNote] = useState<string | null>(null);

  const remove = useCallback(
    async (upload: DialogUpload) => {
      if (!dialogId) return;
      if (!(await ask(removalQuestion(upload), { confirmLabel: "Remove", cancelLabel: "Keep" }))) return;
      setNote(null);
      try {
        if (upload.kind === "set") await deleteSpecDialogReference(dialogId, upload.set.setId);
        else await deleteSpecDialogImage(dialogId, upload.image.id);
      } catch (thrown) {
        setNote(reasonOf(thrown));
        return;
      }
      await refresh();
    },
    [dialogId, ask, refresh],
  );

  return { remove, note, dialog };
}

/** What the confirmation asks: the upload by its name, or an image — which has none — by when it came. */
export function removalQuestion(upload: DialogUpload): string {
  const what = upload.kind === "set"
    ? `Remove '${upload.set.name}'?`
    : `Remove the image attached ${momentOf(upload.image.at)}?`;
  return `${what}\n\nThe design partner no longer sees it from the next turn. This cannot be undone.`;
}

/** A moment as the panel states it, in UTC so every reader sees the same words. */
export function momentOf(at: string): string {
  return `${new Date(at).toISOString().slice(0, 16).replace("T", " ")} UTC`;
}

/** The server's own reason where it gave one — a 409 names the turn or the approval. */
function reasonOf(thrown: unknown): string {
  if (thrown instanceof ApiResponseError && thrown.reason) return thrown.reason;
  const refusal = refusalIn(thrown);
  if (refusal) return `The removal was refused: ${refusal.message}`;
  return `The removal failed: ${thrown instanceof Error ? thrown.message : String(thrown)}`;
}
