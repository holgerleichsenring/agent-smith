"use client";

import { useEffect, useRef, useState } from "react";
import { useBlobUrl } from "@/hooks/useBlobUrl";
import { fetchReferenceFileBlob, fetchReferenceFilePreview } from "@/lib/referenceFilesApi";
import type { SpecDialogFilePreview } from "@/types/spec-dialog";
import { sizeOf } from "./referenceSelection";

// 2026-10-09-86e1: one uploaded file, opened over the page rather than inside the 300 px pane —
// text as the monospace text it is (the server cuts it at 200 KB and says so), a picture as
// itself, anything else as a download. The modal dialog keeps focus inside it; Escape, the
// backdrop and Close leave it, and focus goes back to the control that opened it. Opened the
// way ConfirmDialog opens: rendered closed, then showModal, because showModal throws on an
// element that is already open.

export function UploadPreviewDialog({ dialogId, setId, path, onClose }: {
  dialogId: string;
  setId: string;
  path: string;
  onClose: () => void;
}) {
  const held = useRef<HTMLDialogElement | null>(null);
  const [preview, setPreview] = useState<SpecDialogFilePreview | null>(null);
  const [failed, setFailed] = useState<string | null>(null);

  useEffect(() => {
    const opener = document.activeElement as HTMLElement | null;
    const element = held.current;
    if (element && typeof element.showModal === "function") element.showModal();
    else element?.setAttribute("open", "");
    return () => {
      if (element && typeof element.close === "function") element.close();
      opener?.focus?.();
    };
  }, []);
  useEffect(() => {
    let live = true;
    fetchReferenceFilePreview(dialogId, setId, path)
      .then((read) => live && setPreview(read))
      .catch((thrown) => live && setFailed(thrown instanceof Error ? thrown.message : String(thrown)));
    return () => {
      live = false;
    };
  }, [dialogId, setId, path]);

  return (
    <dialog ref={held} className="d-preview" data-testid="dialog-upload-preview" aria-labelledby="dialog-upload-preview-path"
      onCancel={(event) => { event.preventDefault(); onClose(); }}
      onKeyDown={(event) => { if (event.key === "Escape") { event.preventDefault(); onClose(); } }}
      onClick={(event) => { if (event.target === held.current) onClose(); }}>
      <div className="d-preview-head">
        <p id="dialog-upload-preview-path" className="d-preview-t mono">{path}</p>
        {preview && <span className="ec-sub">{sizeOf(preview.bytes)}</span>}
        <button type="button" className="btn" data-testid="dialog-upload-preview-download"
          onClick={() => void download(dialogId, setId, path)}>Download</button>
        <button type="button" className="btn" data-testid="dialog-upload-preview-close" onClick={onClose}>Close</button>
      </div>
      <div className="d-preview-body">
        {failed && <p role="alert" className="d-upload-note bad">{`The file could not be opened: ${failed}`}</p>}
        {!failed && !preview && <p className="ec-sub" aria-busy="true">Opening…</p>}
        {preview && <Shown preview={preview} dialogId={dialogId} setId={setId} />}
      </div>
    </dialog>
  );
}

function Shown({ preview, dialogId, setId }: { preview: SpecDialogFilePreview; dialogId: string; setId: string }) {
  const picture = useBlobUrl(
    preview.kind === "image" ? () => fetchReferenceFileBlob(dialogId, setId, preview.path) : null,
    preview.kind === "image" ? `${setId}/${preview.path}` : null);
  if (preview.kind === "text")
    return (
      <>
        {preview.truncated && (
          <p className="ec-sub" data-testid="dialog-upload-preview-cut">
            {`Showing the first 200 KB of ${sizeOf(preview.bytes)}. Download the file for all of it.`}
          </p>
        )}
        <pre className="d-preview-text" data-testid="dialog-upload-preview-text" tabIndex={0}>{preview.text}</pre>
      </>
    );
  if (preview.kind === "image")
    return picture.url
      ? <img className="d-preview-img" src={picture.url} alt={preview.path} data-testid="dialog-upload-preview-image" />
      : <p className="ec-sub">{picture.failed ? "The picture could not be loaded." : "Loading the picture…"}</p>;
  return <p className="ec-sub" data-testid="dialog-upload-preview-binary">This file is neither text nor a picture the page can show. Download it to open it.</p>;
}

/** Saves the file under its own name, through the bearer token like every other read. */
async function download(dialogId: string, setId: string, path: string) {
  const url = URL.createObjectURL(await fetchReferenceFileBlob(dialogId, setId, path));
  const link = document.createElement("a");
  link.href = url;
  link.download = path.split("/").at(-1) ?? path;
  link.click();
  window.setTimeout(() => URL.revokeObjectURL(url), 1000);
}
