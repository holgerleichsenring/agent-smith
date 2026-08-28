// 2026-08-28-3793: the whole database, out and back in, from a browser.
//
// The download is the awkward half. The bearer token is injected by the single fetch
// helper, so a plain <a href> cannot carry it — the request has to be a fetch, and a fetch
// hands back a body the page then has to put somewhere. Where the browser offers a save
// dialog (showSaveFilePicker) the response is piped straight into the chosen file and the
// tab never holds it; where it does not, the body is read into a blob and the browser holds
// the whole archive in memory. That second path is NOT hidden: the caller is told which one
// ran and how many bytes arrived, so the surface can say so.

import { apiFetch, apiUrl, refused } from "@/lib/apiResponse";

/** One table's row count, as the manifest would carry it. */
export interface ArchivedTable {
  table: string;
  rows: number;
}

/** What an archive taken right now would carry. The byte size is not among it: a zip is
 *  written as it is produced, so nothing knows its size until it has been written. */
export interface ArchivePreview {
  schemaHead: string;
  provider: string;
  tables: ArchivedTable[];
  totalRows: number;
}

/** What a restore wrote, per table and in total. */
export interface ArchiveRestoreReport {
  schemaHead: string;
  tables: ArchivedTable[];
  totalRows: number;
}

/** How a download reached the disk — and whether this browser held it on the way. */
export interface ArchiveDownloadOutcome {
  fileName: string;
  bytes: number;
  /** True when the response was piped into a file the viewer chose, never buffered here. */
  streamedToDisk: boolean;
}

/**
 * A restore the server refused, carrying the rule's own sentence. Not an ApiResponseError:
 * a differing schema head or an installation that has already run something is a state the
 * operator can act on, and "HTTP 409" tells them none of it.
 */
export class ArchiveRefusedError extends Error {}

const PREVIEW_PATH = "/api/archive/preview";
const EXPORT_PATH = "/api/archive/export";
const IMPORT_PATH = "/api/archive/import";

export async function fetchArchivePreview(signal?: AbortSignal): Promise<ArchivePreview> {
  const res = await apiFetch(PREVIEW_PATH, { signal });
  if (!res.ok) throw await refused(res, PREVIEW_PATH);
  return (await res.json()) as ArchivePreview;
}

/** The archive, saved. Streams into a picked file where the browser has a save dialog, and
 *  falls back to a buffered blob where it does not — reporting which one happened. */
export async function downloadArchive(signal?: AbortSignal): Promise<ArchiveDownloadOutcome> {
  const res = await apiFetch(EXPORT_PATH, { signal });
  if (!res.ok) throw await refused(res, EXPORT_PATH);
  const fileName = archiveFileName();
  const handle = await pickSaveFile(fileName);
  if (handle && res.body) return await streamInto(handle, res.body, fileName);
  const blob = await res.blob();
  saveThroughALink(blob, fileName);
  return { fileName, bytes: blob.size, streamedToDisk: false };
}

/** Restore an archive into this installation. A refusal arrives as ArchiveRefusedError
 *  carrying the sentence the rule wrote; anything else fails as it always did. */
export async function restoreArchive(
  file: File,
  signal?: AbortSignal,
): Promise<ArchiveRestoreReport> {
  const res = await apiFetch(IMPORT_PATH, {
    method: "POST",
    headers: { "Content-Type": "application/zip" },
    body: file,
    signal,
  });
  if (res.status === 409) throw new ArchiveRefusedError(await refusalIn(res));
  if (!res.ok) throw await refused(res, IMPORT_PATH);
  return (await res.json()) as ArchiveRestoreReport;
}

/** The API origin the archive is served from — shown so an operator can reach it with a
 *  tool of their own when a browser is the wrong instrument for a multi-gigabyte file. */
export function archiveExportUrl(): string {
  return apiUrl(EXPORT_PATH);
}

async function refusalIn(res: Response): Promise<string> {
  try {
    const body = (await res.json()) as { refusal?: unknown };
    if (typeof body?.refusal === "string" && body.refusal.length > 0) return body.refusal;
  } catch (cause) {
    console.debug("A refused restore named no cause this client could read", cause);
  }
  return "The server refused the restore and named no cause.";
}

interface SaveFileHandle {
  createWritable(): Promise<WritableStream<Uint8Array>>;
}

type SaveFilePicker = (options: {
  suggestedName?: string;
  types?: { description: string; accept: Record<string, string[]> }[];
}) => Promise<SaveFileHandle>;

async function pickSaveFile(fileName: string): Promise<SaveFileHandle | null> {
  const picker = (window as unknown as { showSaveFilePicker?: SaveFilePicker }).showSaveFilePicker;
  if (typeof picker !== "function") return null;
  try {
    return await picker({
      suggestedName: fileName,
      types: [{ description: "Agent Smith data archive", accept: { "application/zip": [".zip"] } }],
    });
  } catch (cause) {
    // A cancelled dialog is the operator saying no — it is not a fallback to the buffered
    // path, and it is not an error either.
    console.debug("No file was chosen for the archive", cause);
    throw new DOMException("The download was cancelled.", "AbortError");
  }
}

async function streamInto(
  handle: SaveFileHandle,
  body: ReadableStream<Uint8Array>,
  fileName: string,
): Promise<ArchiveDownloadOutcome> {
  let bytes = 0;
  const counting = new TransformStream<Uint8Array, Uint8Array>({
    transform(chunk, controller) {
      bytes += chunk.byteLength;
      controller.enqueue(chunk);
    },
  });
  await body.pipeThrough(counting).pipeTo(await handle.createWritable());
  return { fileName, bytes, streamedToDisk: true };
}

function saveThroughALink(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = fileName;
  link.click();
  URL.revokeObjectURL(url);
}

function archiveFileName(): string {
  const stamp = new Date().toISOString().replace(/[-:]/g, "").replace(/\..+$/, "");
  return `agentsmith-archive-${stamp}Z.zip`;
}
