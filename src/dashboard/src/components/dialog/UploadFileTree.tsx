"use client";

import { useEffect, useMemo, useState } from "react";
import { fetchReferenceFiles } from "@/lib/referenceFilesApi";
import type { SpecDialogReferenceFile } from "@/types/spec-dialog";
import { sizeOf } from "./referenceSelection";
import { initiallyOpen, treeOf, type UploadFolder } from "./uploadTree";

// 2026-10-09-86e1: the files of one upload, read when the row is opened — a tree of folders
// the operator opens and closes, each file opened in a preview by its name and, unless an
// approval cites the upload, removed on its own. `version` changes when the set does, which is
// what reads it again after a removal.

export function UploadFileTree({
  dialogId,
  setId,
  version,
  cited,
  onOpen,
  onRemove,
}: {
  dialogId: string;
  setId: string;
  version: string;
  cited: boolean;
  onOpen: (path: string) => void;
  onRemove: (path: string) => void;
}) {
  const [files, setFiles] = useState<SpecDialogReferenceFile[] | null>(null);
  const [failed, setFailed] = useState<string | null>(null);
  useEffect(() => {
    let live = true;
    fetchReferenceFiles(dialogId, setId)
      .then((read) => live && setFiles(read))
      .catch((thrown) => live && setFailed(thrown instanceof Error ? thrown.message : String(thrown)));
    return () => {
      live = false;
    };
  }, [dialogId, setId, version]);
  const root = useMemo(() => (files ? treeOf(files) : null), [files]);
  const [open, setOpen] = useState<Set<string> | null>(null);
  const shown = open ?? (root ? initiallyOpen(root) : new Set<string>());

  if (failed) return <p className="ec-sub d-tree-note" role="alert">{`The files could not be read: ${failed}`}</p>;
  if (!root) return <p className="ec-sub d-tree-note" aria-busy="true">Reading the files…</p>;
  const toggle = (path: string) => {
    const next = new Set(shown);
    if (next.has(path)) next.delete(path);
    else next.add(path);
    setOpen(next);
  };
  return (
    <ul className="d-tree" data-testid={`dialog-upload-files-${setId}`} aria-label="Files in this upload">
      <FolderItems folder={root} open={shown} onToggle={toggle} cited={cited} onOpen={onOpen} onRemove={onRemove} />
    </ul>
  );
}

function FolderItems({ folder, open, onToggle, cited, onOpen, onRemove }: {
  folder: UploadFolder;
  open: Set<string>;
  onToggle: (path: string) => void;
  cited: boolean;
  onOpen: (path: string) => void;
  onRemove: (path: string) => void;
}) {
  return (
    <>
      {folder.folders.map((child) => (
        <li key={child.path} className="d-tree-folder">
          <button type="button" className="d-tree-toggle" aria-expanded={open.has(child.path)}
            data-testid={`dialog-upload-folder-${child.path}`} onClick={() => onToggle(child.path)}>
            <span aria-hidden="true" className="d-tree-caret">{open.has(child.path) ? "▾" : "▸"}</span>
            <span className="mono">{`${child.name}/`}</span>
            <span className="d-tree-size">{child.count === 1 ? "1 file" : `${child.count} files`}</span>
          </button>
          {open.has(child.path) && (
            <ul className="d-tree">
              <FolderItems folder={child} open={open} onToggle={onToggle} cited={cited} onOpen={onOpen} onRemove={onRemove} />
            </ul>
          )}
        </li>
      ))}
      {folder.files.map((file) => (
        <li key={file.path} className="d-tree-file" data-testid={`dialog-upload-file-${file.path}`}>
          <button type="button" className="d-tree-open mono" title={`Open ${file.path}`} onClick={() => onOpen(file.path)}>
            {file.name}
          </button>
          <span className="d-tree-size">{sizeOf(file.bytes)}</span>
          {!cited && (
            <button type="button" className="d-tree-remove" aria-label={`Remove ${file.path}`} title="Remove this file"
              data-testid={`dialog-upload-file-remove-${file.path}`} onClick={() => onRemove(file.path)}>
              ×
            </button>
          )}
        </li>
      ))}
    </>
  );
}
