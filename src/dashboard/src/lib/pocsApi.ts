// 2026-11-23-933da: GET /api/pocs — the PoCs the caller may see, whether they may import, and the targets. The status
// IS the feature switch: 200 = on and readable, 404 = pocs.enabled is off, 403 = the caller lacks pocs.read. It is
// asked with or without a token, so an installation without sign-in learns the switch too.

import { ApiResponseError, apiFetch, readJson, refused } from "@/lib/apiResponse";

export const POCS_PATH = "/api/pocs";

/** The states a PoC row carries; later specs write them, this one only reads them. */
export type PocState = "importing" | "refused" | "blocked" | "ready" | "failed" | "stopped" | "deleted";

export interface PocRow {
  id: number;
  name: string;
  project: string;
  target: string;
  /** The address from the target's domain pattern; null while the target names none. */
  host: string | null;
  state: string;
  ownerName: string;
  lastRunId: string | null;
}

export interface PocTarget {
  name: string;
  sourceRepo: string;
  root: string;
}

export interface PocList {
  pocs: PocRow[];
  mayImport: boolean;
  targets: PocTarget[];
}

/** What the read said: the list, the feature off, or no permission to see it. */
export type PocsRead = { status: "on"; list: PocList } | { status: "off" } | { status: "forbidden" };

export async function fetchPocs(signal?: AbortSignal): Promise<PocsRead> {
  const res = await apiFetch(POCS_PATH, { signal });
  if (res.status === 404) return { status: "off" };
  if (res.status === 403 || res.status === 401) return { status: "forbidden" };
  if (!res.ok) throw await refused(res, POCS_PATH);
  return { status: "on", list: await readJson<PocList>(res, POCS_PATH) };
}

// 2026-11-23-933db: importing a ZIP is two calls. The upload sends the archive; the server checks what the ZIP declares
// and answers with the checks passed, a proposed name and, when the ZIP looks like one of the caller's PoCs, how it
// compares. The import then names the choice and queues the run that checks the archive again.

export const POC_UPLOADS_PATH = "/api/pocs/uploads";

/** The keys of the checks an upload passed, in display order. */
export type PocCheckKey = "paths" | "kinds" | "bounds" | "forbidden" | "dockerfile" | "expose";

/** How the upload compares with the latest checked revision of the PoC it looks like. */
export interface PocUploadMatch {
  pocId: number;
  pocName: string;
  revision: number | null;
  added: number;
  changed: number;
  removed: number;
  unchanged: number;
  ratioPercent: number;
  portChanged: boolean;
  entrypointChanged: boolean;
  previousPort: number | null;
  /** "ask": the ZIP differs enough that replacing needs a confirmation. */
  verdict: "same" | "ask";
}

export interface PocUpload {
  uploadId: string;
  fileName: string;
  target: string;
  port: number;
  entryCount: number;
  unpackedBytes: number;
  proposedName: string;
  checks: string[];
  match: PocUploadMatch | null;
}

export type PocImportChoice = "new" | "replace";

export interface PocImportBody {
  choice: PocImportChoice;
  target?: string;
  /** For a new PoC. */
  name?: string;
  /** For a replace. */
  pocId?: number;
  /** Replace although the upload's verdict was "ask". */
  confirmed?: boolean;
}

export interface PocImported {
  pocId: number;
  revision: number;
  runId: string;
}

/** One .zip, and the target when several are configured. A refusal throws with the server's sentence as its reason. */
export async function uploadPocZip(file: File, target?: string, signal?: AbortSignal): Promise<PocUpload> {
  const form = new FormData();
  form.append("file", file, file.name);
  if (target) form.append("target", target);
  const res = await apiFetch(POC_UPLOADS_PATH, { method: "POST", body: form, signal });
  if (!res.ok) throw await refusedAsSaid(res, POC_UPLOADS_PATH);
  return readJson<PocUpload>(res, POC_UPLOADS_PATH);
}

/** Import an upload as a new PoC or as the next revision of one; the answer names the run that checks it. */
export async function importPocUpload(uploadId: string, body: PocImportBody, signal?: AbortSignal): Promise<PocImported> {
  const path = `${POC_UPLOADS_PATH}/${encodeURIComponent(uploadId)}/import`;
  const res = await apiFetch(path, {
    method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body), signal,
  });
  if (!res.ok) throw await refusedAsSaid(res, path);
  return readJson<PocImported>(res, path);
}

// The upload routes answer a refusal as one plain sentence that names the file, the value and the config key; it is
// carried whole as the reason, not cut or parsed. A missing permission or sign-in stays the shared refusal.
async function refusedAsSaid(res: Response, path: string): Promise<Error> {
  if (res.status === 401 || res.status === 403) return refused(res, path);
  const text = (await res.text().catch(() => "")).trim();
  return new ApiResponseError(path, res.status, `HTTP ${res.status}${text ? ` — ${text}` : ""}`, text || null);
}
