"use client";

import { useCallback, useState } from "react";
import { useConfirmDialog, type ConfirmDialogProps } from "@/components/dialog/ConfirmDialog";
import { ApiResponseError, refusalIn } from "@/lib/apiResponse";
import type { T } from "@/lib/i18n/translate";
import { useT } from "./useT";
import { deleteReferenceFile } from "@/lib/referenceFilesApi";
import { deleteSpecDialogImage, deleteSpecDialogReference } from "@/lib/specDialogApi";
import type { SpecDialogImage, SpecDialogReferenceSet } from "@/types/spec-dialog";

// 2026-10-08-e8b9h: taking one upload back out of the conversation. The page asks first, in its
// own dialog, because a removal cannot be undone; then the view is read again and the transcript
// RESEEDED, so the chip of a removed set goes with it. A refusal — a turn running or waiting on an
// approval, an upload an approval cites — is said where the Remove was pressed.
// 2026-10-09-86e1: and one FILE of a set, asked and refused the same way; the set's last file
// takes the set with it, which the question says.

export type DialogUpload =
  | { kind: "set"; set: SpecDialogReferenceSet }
  | { kind: "image"; image: SpecDialogImage };

export function useDialogUploads(
  dialogId: string | null,
  refresh: () => Promise<void>,
): {
  remove: (upload: DialogUpload) => Promise<void>;
  removeFile: (set: SpecDialogReferenceSet, path: string) => Promise<void>;
  note: string | null;
  dialog: ConfirmDialogProps;
} {
  const t = useT();
  const { ask, dialog } = useConfirmDialog();
  const [note, setNote] = useState<string | null>(null);

  const confirmed = useCallback(
    async (question: string, act: (dialog: string) => Promise<void>) => {
      if (!dialogId) return;
      if (!(await ask(question, { confirmLabel: t("dialog.uploads.confirmRemove"), cancelLabel: t("dialog.uploads.keep") }))) return;
      setNote(null);
      try {
        await act(dialogId);
      } catch (thrown) {
        setNote(reasonOf(t, thrown));
        return;
      }
      await refresh();
    },
    [dialogId, ask, refresh, t],
  );
  const remove = useCallback(
    (upload: DialogUpload) => confirmed(removalQuestion(t, upload), (dialog) => upload.kind === "set"
      ? deleteSpecDialogReference(dialog, upload.set.setId)
      : deleteSpecDialogImage(dialog, upload.image.id)),
    [confirmed, t],
  );
  const removeFile = useCallback(
    (set: SpecDialogReferenceSet, path: string) =>
      confirmed(fileRemovalQuestion(t, set, path), (dialog) => deleteReferenceFile(dialog, set.setId, path)),
    [confirmed, t],
  );

  return { remove, removeFile, note, dialog };
}

/** What the confirmation asks: the upload by its name, or an image — which has none — by when it came. */
export function removalQuestion(t: T, upload: DialogUpload): string {
  const what = upload.kind === "set"
    ? t("dialog.uploads.removeSet", { name: upload.set.name })
    : t("dialog.uploads.removeImage", { when: momentOf(upload.image.at) });
  return `${what}\n\n${t("dialog.uploads.removeTail")}`;
}

/** What the confirmation asks before one file goes; the last file of a set takes the set. */
export function fileRemovalQuestion(t: T, set: SpecDialogReferenceSet, path: string): string {
  const what = set.files <= 1
    ? t("dialog.uploads.removeOnly", { path, name: set.name })
    : t("dialog.uploads.removeFrom", { path, name: set.name });
  return `${what}\n\n${t("dialog.uploads.removeTail")}`;
}

/** A moment as the panel states it, in UTC so every reader sees the same words. */
export function momentOf(at: string): string {
  return `${new Date(at).toISOString().slice(0, 16).replace("T", " ")} UTC`;
}

/** The server's own reason where it gave one — a 409 names the turn or the approval. */
function reasonOf(t: T, thrown: unknown): string {
  if (thrown instanceof ApiResponseError && thrown.reason) return thrown.reason;
  const refusal = refusalIn(thrown);
  if (refusal) return t("dialog.uploads.removalRefused", { reason: refusal.message });
  return t("dialog.uploads.removalFailed", { reason: thrown instanceof Error ? thrown.message : String(thrown) });
}
