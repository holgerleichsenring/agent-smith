"use client";

import { useEffect, useRef, useState, type ReactNode } from "react";
import type { SpecDialogReferenceSet } from "@/types/spec-dialog";
import { sizeOf } from "./referenceSelection";
import { UploadFileTree } from "./UploadFileTree";
import { UploadPreviewDialog } from "./UploadPreviewDialog";

// 2026-10-09-86e1: one uploaded set in the Uploads tab — its name, how many files and how large,
// and, opened, every file in it. A row the transcript chip asked for opens itself, scrolls into
// view and takes focus, so the operator lands on the upload they pressed.

export function UploadSetRow({ set, dialogId, focused, actions, onRemoveFile }: {
  set: SpecDialogReferenceSet;
  dialogId: string | null;
  focused: boolean;
  /** The set's own Remove, or the word that says an approval cites it. */
  actions: ReactNode;
  onRemoveFile: (path: string) => void;
}) {
  const [open, setOpen] = useState(focused);
  const [previewing, setPreviewing] = useState<string | null>(null);
  const toggle = useRef<HTMLButtonElement>(null);
  useEffect(() => {
    if (!focused) return;
    setOpen(true);
    toggle.current?.scrollIntoView({ block: "nearest" });
    toggle.current?.focus();
  }, [focused]);

  return (
    <>
      <div className="d-upload-head">
        <button ref={toggle} type="button" className="d-upload-toggle" aria-expanded={open}
          data-testid={`dialog-upload-open-${set.setId}`} onClick={() => setOpen(!open)}>
          <span aria-hidden="true" className="d-tree-caret">{open ? "▾" : "▸"}</span>
          <span className="ec-name sans given">{set.name}</span>
          <span className="ec-sub">{set.files} {set.files === 1 ? "file" : "files"} · {sizeOf(set.bytes)}</span>
        </button>
        {actions}
      </div>
      {open && dialogId && (
        <UploadFileTree dialogId={dialogId} setId={set.setId} version={`${set.files}:${set.bytes}`}
          cited={!!set.cited} onOpen={setPreviewing} onRemove={onRemoveFile} />
      )}
      {previewing && dialogId && (
        <UploadPreviewDialog dialogId={dialogId} setId={set.setId} path={previewing} onClose={() => setPreviewing(null)} />
      )}
    </>
  );
}
