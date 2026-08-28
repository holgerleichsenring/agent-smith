"use client";

import { useEffect, useRef, useState } from "react";
import {
  ArchiveRefusedError,
  downloadArchive,
  fetchArchivePreview,
  restoreArchive,
  type ArchiveDownloadOutcome,
  type ArchivePreview,
  type ArchiveRestoreReport,
} from "@/lib/archiveApi";
import { refusalIn } from "@/lib/apiResponse";
import { RefusalSurface } from "@/components/shell/RefusalSurface";

// 2026-08-28-3793: the whole database, out and back in, beside the versions and the
// database state — where an operator already goes to find out what this installation IS.
// The CLI verb stays the reliable path, because it works on an installation whose server
// will not start; this is the ordinary one, for an operator with a browser and no shell on
// the machine the database runs on.

const UNREDACTED =
  "The archive is UNREDACTED: it carries ticket text, prompts, artifacts and every "
  + "configuration secret in clear. Treat the file as the installation itself.";

const SIZE_IS_UNKNOWN_UNTIL_WRITTEN =
  "Its size is not known before it is written — the archive is produced as it streams, so "
  + "the byte count is stated once the file has arrived.";

export function DataArchiveView() {
  const [preview, setPreview] = useState<ArchivePreview | null>(null);
  const [error, setError] = useState<Error | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    fetchArchivePreview(controller.signal)
      .then(setPreview)
      .catch((e: Error) => {
        if (e.name !== "AbortError") setError(e);
      });
    return () => controller.abort();
  }, []);

  const refusal = refusalIn(error);

  return (
    <section data-testid="archive-view">
      <div className="section-head">
        <h2>Data archive</h2>
        {preview && <span className="cnt">{preview.totalRows.toLocaleString()} rows</span>}
        {preview && <span className="sh-sub">schema {preview.schemaHead}</span>}
      </div>
      <div style={{ height: 14 }} />
      {refusal ? (
        <RefusalSurface refusal={refusal} surface="the data archive" />
      ) : error ? (
        <div className="stateline err" data-testid="archive-error">
          Failed to read what an archive would carry: {error.message}
        </div>
      ) : !preview ? (
        <div className="stateline" data-testid="archive-loading">
          Reading what an archive would carry…
        </div>
      ) : (
        <>
          <p className="stateline" data-testid="archive-unredacted">
            {UNREDACTED} {SIZE_IS_UNKNOWN_UNTIL_WRITTEN}
          </p>
          <Tables preview={preview} />
          <Actions />
        </>
      )}
    </section>
  );
}

function Tables({ preview }: { preview: ArchivePreview }) {
  return (
    <div className="rows" data-testid="archive-tables">
      {preview.tables.map((table) => (
        <div className="lrow" key={table.table} data-testid={`archive-table-${table.table}`}>
          <span className="id">{table.table}</span>
          <span />
          <span className="meta">{table.rows.toLocaleString()} rows</span>
        </div>
      ))}
    </div>
  );
}

function Actions() {
  const [busy, setBusy] = useState<"download" | "restore" | null>(null);
  const [downloaded, setDownloaded] = useState<ArchiveDownloadOutcome | null>(null);
  const [restored, setRestored] = useState<ArchiveRestoreReport | null>(null);
  const [refused, setRefused] = useState<string | null>(null);
  const [failure, setFailure] = useState<Error | null>(null);
  const fileRef = useRef<HTMLInputElement>(null);

  const onDownload = async () => {
    setBusy("download");
    setFailure(null);
    try {
      setDownloaded(await downloadArchive());
    } catch (err) {
      if ((err as Error).name !== "AbortError") setFailure(err as Error);
    } finally {
      setBusy(null);
    }
  };

  const onFile = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.target.value = ""; // so the same file can be picked again after a refusal
    if (!file || !window.confirm(restoreWarning(file))) return;
    setBusy("restore");
    setRefused(null);
    setFailure(null);
    setRestored(null);
    try {
      setRestored(await restoreArchive(file));
    } catch (err) {
      if (err instanceof ArchiveRefusedError) setRefused(err.message);
      else setFailure(err as Error);
    } finally {
      setBusy(null);
    }
  };

  return (
    <div className="arch-actions" data-testid="archive-actions">
      <button
        type="button"
        className="btn"
        onClick={() => void onDownload()}
        disabled={busy !== null}
        data-testid="archive-download"
      >
        {busy === "download" ? "Writing…" : "Download archive ↧"}
      </button>
      <button
        type="button"
        className="btn"
        onClick={() => fileRef.current?.click()}
        disabled={busy !== null}
        data-testid="archive-restore"
      >
        {busy === "restore" ? "Restoring…" : "Restore from archive ↥"}
      </button>
      <input
        ref={fileRef}
        type="file"
        accept=".zip,application/zip"
        style={{ display: "none" }}
        onChange={(e) => void onFile(e)}
        data-testid="archive-restore-file"
      />
      {downloaded && <DownloadNote outcome={downloaded} />}
      {restored && (
        <p className="stateline" data-testid="archive-restored">
          Restored {restored.totalRows.toLocaleString()} rows across {restored.tables.length}{" "}
          tables, at schema {restored.schemaHead}. This installation now serves the
          configuration the archive carried — no restart.
        </p>
      )}
      {refused && (
        <p className="errline" data-testid="archive-refusal">
          {refused}
        </p>
      )}
      {failure && (
        <p className="errline" data-testid="archive-failure">
          {failure.message}
        </p>
      )}
    </div>
  );
}

function DownloadNote({ outcome }: { outcome: ArchiveDownloadOutcome }) {
  return (
    <p className="stateline" data-testid="archive-download-note">
      {outcome.fileName} — {megabytes(outcome.bytes)}.{" "}
      {outcome.streamedToDisk
        ? "Written straight into the file you chose, a chunk at a time."
        : "This browser offers no save dialog, so the whole archive was held in this tab "
          + "before it reached your downloads."}
    </p>
  );
}

function restoreWarning(file: File): string {
  return `Restore ${file.name} (${megabytes(file.size)})?\n\n`
    + "Every table in this installation is replaced by the archive's, and the restore is "
    + "refused outright if this installation has ever recorded a run.";
}

function megabytes(bytes: number): string {
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}
