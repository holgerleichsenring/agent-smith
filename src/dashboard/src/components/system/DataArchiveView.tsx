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
import { ConfirmDialog, useConfirmDialog } from "@/components/dialog/ConfirmDialog";
import { useDateLocale, useT } from "@/hooks/useT";
import type { T } from "@/lib/i18n/translate";

// 2026-08-28-3793: the whole database, out and back in, beside the versions and the
// database state — where an operator already goes to find out what this installation IS.
// The CLI verb stays the reliable path, because it works on an installation whose server
// will not start; this is the ordinary one, for an operator with a browser and no shell on
// the machine the database runs on.

export function DataArchiveView() {
  const t = useT();
  const locale = useDateLocale();
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
        <h2>{t("system.archive.title")}</h2>
        {preview && <span className="cnt">{t("system.archive.rows", { rows: preview.totalRows.toLocaleString(locale) })}</span>}
        {preview && <span className="sh-sub">{t("system.archive.schema", { schema: preview.schemaHead })}</span>}
      </div>
      <div style={{ height: 14 }} />
      {refusal ? (
        <RefusalSurface refusal={refusal} surface={t("system.archive.surface")} />
      ) : error ? (
        <div className="stateline err" data-testid="archive-error">
          {t("system.archive.failed", { message: error.message })}
        </div>
      ) : !preview ? (
        <div className="stateline" data-testid="archive-loading">
          {t("system.archive.loading")}
        </div>
      ) : (
        <>
          <p className="stateline" data-testid="archive-unredacted">
            {t("system.archive.unredacted")} {t("system.archive.sizeUnknown")}
          </p>
          <Tables preview={preview} />
          <Actions />
        </>
      )}
    </section>
  );
}

function Tables({ preview }: { preview: ArchivePreview }) {
  const t = useT();
  const locale = useDateLocale();
  return (
    <div className="rows" data-testid="archive-tables">
      {preview.tables.map((table) => (
        <div className="lrow" key={table.table} data-testid={`archive-table-${table.table}`}>
          <span className="id">{table.table}</span>
          <span />
          <span className="meta">{t("system.archive.rows", { rows: table.rows.toLocaleString(locale) })}</span>
        </div>
      ))}
    </div>
  );
}

function Actions() {
  const t = useT();
  const locale = useDateLocale();
  const [busy, setBusy] = useState<"download" | "restore" | null>(null);
  const [downloaded, setDownloaded] = useState<ArchiveDownloadOutcome | null>(null);
  const [restored, setRestored] = useState<ArchiveRestoreReport | null>(null);
  const [refused, setRefused] = useState<string | null>(null);
  const [failure, setFailure] = useState<Error | null>(null);
  const fileRef = useRef<HTMLInputElement>(null);
  // 2026-10-08-e8b9i: the restore warning is asked in the page's own dialog, not the browser's.
  const confirmation = useConfirmDialog();

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
    if (!file) return;
    if (!(await confirmation.ask(restoreWarning(file, t), { confirmLabel: t("system.archive.restore"), cancelLabel: t("common.cancel") }))) return;
    setBusy("restore");
    setRefused(null);
    setFailure(null);
    setRestored(null);
    try {
      setRestored(await restoreArchive(file));
    } catch (err) {
      if (err instanceof ArchiveRefusedError) setRefused(err.message || t("system.archive.refusedNoCause"));
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
        {busy === "download" ? t("system.archive.writing") : t("system.archive.download")}
      </button>
      <button
        type="button"
        className="btn"
        onClick={() => fileRef.current?.click()}
        disabled={busy !== null}
        data-testid="archive-restore"
      >
        {busy === "restore" ? t("system.archive.restoring") : t("system.archive.restoreFrom")}
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
          {t("system.archive.restored", {
            rows: restored.totalRows.toLocaleString(locale),
            tables: restored.tables.length,
            schema: restored.schemaHead,
          })}
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
      <ConfirmDialog {...confirmation.dialog} />
    </div>
  );
}

function DownloadNote({ outcome }: { outcome: ArchiveDownloadOutcome }) {
  const t = useT();
  return (
    <p className="stateline" data-testid="archive-download-note">
      {outcome.fileName} — {megabytes(outcome.bytes)}.{" "}
      {outcome.streamedToDisk
        ? t("system.archive.streamed")
        : t("system.archive.held")}
    </p>
  );
}

function restoreWarning(file: File, t: T): string {
  return t("system.archive.restoreWarning", { file: file.name, size: megabytes(file.size) });
}

function megabytes(bytes: number): string {
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}
