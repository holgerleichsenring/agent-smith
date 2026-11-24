"use client";

import { useRef, useState } from "react";
import { useT } from "@/hooks/useT";
import { sizeOf } from "@/components/dialog/referenceSelection";
import type { PocTarget, PocUpload } from "@/lib/pocsApi";
import { cn } from "@/lib/utils";

// 2026-11-23-933db: where the ZIP goes in — one file, dropped or picked. The target is asked only when several exist;
// with one the server takes it. Once uploaded the zone shows the file and offers to choose another.

export function DropZone({ targets, target, onTarget, upload, busy, onFile, onAnother }: {
  targets: PocTarget[];
  target: string;
  onTarget: (target: string) => void;
  upload: PocUpload | null;
  busy: boolean;
  onFile: (file: File) => void;
  onAnother: () => void;
}) {
  const t = useT();
  const input = useRef<HTMLInputElement>(null);
  const [over, setOver] = useState(false);
  const [note, setNote] = useState<string | null>(null);
  const needsTarget = targets.length > 1 && !target;
  const take = (files: FileList | null) => {
    if (!files || files.length === 0) return;
    if (files.length > 1) {
      setNote(t("pocs.import.drop.oneFile"));
      return;
    }
    setNote(null);
    onFile(files[0]);
  };

  if (upload) {
    return (
      <div className="poc-drop done" data-testid="poc-import-file">
        <div className="poc-zip" aria-hidden="true">ZIP</div>
        <div className="poc-drop-text">
          <div className="poc-file-name">{upload.fileName}</div>
          <div className="poc-meta">
            {t("pocs.import.drop.meta", { files: t("common.files", { count: upload.entryCount }), size: sizeOf(upload.unpackedBytes) })}
          </div>
        </div>
        <button type="button" className="btn" onClick={onAnother} data-testid="poc-import-another">
          {t("pocs.import.drop.another")}
        </button>
      </div>
    );
  }

  return (
    <>
      {targets.length > 1 && (
        <div className="poc-field">
          <label htmlFor="poc-import-target">{t("pocs.import.target.label")}</label>
          <select id="poc-import-target" value={target} onChange={(e) => onTarget(e.target.value)} data-testid="poc-import-target">
            <option value="">{t("pocs.import.target.pick")}</option>
            {targets.map((each) => <option key={each.name} value={each.name}>{each.name}</option>)}
          </select>
        </div>
      )}
      <div className={cn("poc-drop", over && "over")} data-testid="poc-import-drop" aria-busy={busy}
        onDragOver={(e) => {
          e.preventDefault();
          if (!busy && !needsTarget) setOver(true);
        }}
        onDragLeave={() => setOver(false)}
        onDrop={(e) => {
          e.preventDefault();
          setOver(false);
          if (!busy && !needsTarget) take(e.dataTransfer.files);
        }}>
        <div className="poc-zip" aria-hidden="true">ZIP</div>
        <div className="poc-drop-text">
          <div className="poc-file-name">{busy ? t("pocs.import.drop.uploading") : t("pocs.import.drop.title")}</div>
          <div className="poc-meta">{needsTarget ? t("pocs.import.target.first") : t("pocs.import.drop.body")}</div>
          {note && <div className="poc-meta" role="status" data-testid="poc-import-drop-note">{note}</div>}
        </div>
        <button type="button" className="btn" disabled={busy || needsTarget} onClick={() => input.current?.click()}
          data-testid="poc-import-pick">
          {t("pocs.import.drop.pick")}
        </button>
        <input ref={input} type="file" accept=".zip,application/zip" hidden data-testid="poc-import-input"
          onChange={(e) => {
            take(e.target.files);
            e.target.value = "";
          }} />
      </div>
    </>
  );
}
