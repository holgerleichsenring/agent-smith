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
  /** 2026-11-23-933dc: the PoC's newest revision, which a blocked PoC's card links to; null before the first one. */
  latestRevision: number | null;
  /** 2026-11-23-c81ee: when the PoC expires and what may be done about it; null for a deleted or refused PoC or when its
   *  target is no longer configured. */
  lifetime?: PocLifetime | null;
}

/** 2026-11-23-c81ee: a PoC's lifetime as the server computed it. */
export interface PocLifetime {
  /** ISO timestamp. */
  expiresAt: string;
  /** Whole days, 0 once expired. */
  daysLeft: number;
  /** The reminder window is open and the PoC has not expired yet. */
  windowOpen: boolean;
  expired: boolean;
  /** ISO date an expired PoC that was stopped unseen is deleted unless extended; null otherwise. */
  deletesAt: string | null;
  extensions: number;
  /** pocs.lifetime.max_owner_extensions. */
  ownerAllowance: number;
  mayExtend: boolean;
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
  /** 2026-11-23-933dg: the PoC's variables, written by a poc-variables run before poc-deploy is queued; omitted when none.
   *  A failed variables run answers 422 with its line and queues nothing. */
  variables?: PocVariableInput[];
}

/** 2026-11-23-933dg: one variable as it is sent — a secret one is sealed, a plain one is committed in plain text. */
export interface PocVariableInput {
  name: string;
  secret: boolean;
  value: string;
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

// 2026-11-23-933dc: one revision of a PoC as the scan gate left it — what blocks, what is only a note, the agent's
// explanation, and whether the caller may overrule it. A foreign PoC or a caller who may not read its runs gets an
// empty 404, read here as "not found".

/** The states a revision carries. */
/** 2026-11-23-933dd: built — the image was built, scanned and pushed. */
export type PocRevisionState = "admitted" | "validated" | "refused" | "blocked" | "overruled" | "built";

export type PocFindingKind = "secret" | "vulnerability" | "note";

export interface PocScanFinding {
  id: string;
  kind: PocFindingKind | string;
  ruleId: string;
  file: string;
  line: number | null;
  title: string;
  detail: string;
  /** The line with the secret masked; never the value itself. */
  maskedSnippet: string | null;
  fix: string | null;
  aliases: string[];
}

export interface PocScanVerdict {
  blocks: PocScanFinding[];
  notes: PocScanFinding[];
  isBlocked: boolean;
  overrulable: boolean;
}

export interface PocRevisionView {
  pocId: number;
  name: string;
  revision: number;
  latestRevision: number;
  state: PocRevisionState | string;
  runId: string | null;
  commitSha: string | null;
  verdict: PocScanVerdict | null;
  /** The agent's explanation of a blocked verdict: untrusted text, rendered as text only. */
  explanation: string | null;
  mayOverrule: boolean;
  /** The server's sentence why the caller may not overrule; null when they may. */
  whyNot: string | null;
  /** 2026-11-23-933dd: the stage a blocked revision is blocked at; null when not blocked. */
  blockedStage?: "code" | "image" | string | null;
  /** 2026-11-23-933dd: the image scan's verdict — its blocks, or the notes of a pushed image. */
  imageVerdict?: PocScanVerdict | null;
  /** 2026-11-23-933dd: the pushed digest the registry confirmed. */
  digest?: string | null;
  /** 2026-11-23-933dd: the source repo host's pipeline run that builds the revision. */
  pipelineUrl?: string | null;
}


export interface PocOverruled {
  pocId: number;
  revision: number;
  runId: string;
}

export function pocRevisionPath(pocId: number, revision: number): string {
  return `${POCS_PATH}/${encodeURIComponent(String(pocId))}/revisions/${encodeURIComponent(String(revision))}`;
}

/** One revision, or null when the server answers it is not there (a foreign PoC answers the same empty 404). */
export async function getPocRevision(pocId: number, n: number, signal?: AbortSignal): Promise<PocRevisionView | null> {
  const path = pocRevisionPath(pocId, n);
  const res = await apiFetch(path, { signal });
  if (res.status === 404) return null;
  if (!res.ok) throw await refused(res, path);
  return readJson<PocRevisionView>(res, path);
}

/** Lift a revision's vulnerability block with a reason; the answer names the run that commits it. A refusal throws with
 *  the server's sentence as its reason. */
export async function overrulePocRevision(pocId: number, n: number, reason: string, signal?: AbortSignal): Promise<PocOverruled> {
  const path = `${pocRevisionPath(pocId, n)}/overrule`;
  const res = await apiFetch(path, {
    method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ reason }), signal,
  });
  if (!res.ok) throw await refusedAsSaid(res, path);
  return readJson<PocOverruled>(res, path);
}

// The upload routes answer a refusal as one plain sentence that names the file, the value and the config key; it is
// carried whole as the reason, not cut or parsed. A missing permission or sign-in stays the shared refusal.
async function refusedAsSaid(res: Response, path: string): Promise<Error> {
  if (res.status === 401 || res.status === 403) return refused(res, path);
  const text = (await res.text().catch(() => "")).trim();
  return new ApiResponseError(path, res.status, `HTTP ${res.status}${text ? ` — ${text}` : ""}`, text || null);
}

// 2026-11-23-933dd: POST /api/pocs/targets/{name}/install — a pocs.admin holder starts the run that writes the target's
// build pipeline into its source repo. The answer is how that run ended: the commit, or why nothing was written.
export interface PocTargetInstalled {
  target: string;
  succeeded: boolean;
  message: string;
  runId: string;
}

export async function installPocTarget(name: string, signal?: AbortSignal): Promise<PocTargetInstalled> {
  const path = `${POCS_PATH}/targets/${encodeURIComponent(name)}/install`;
  const res = await apiFetch(path, { method: "POST", signal });
  if (!res.ok) throw await refusedAsSaid(res, path);
  return readJson<PocTargetInstalled>(res, path);
}

// 2026-11-23-933dg: a PoC's variables. GET lists them — a secret one never with its value, a plain one with it. PUT sets
// and deletes them through a poc-variables run and answers that run's line; a refusal is a plain sentence (400, 404
// empty, 409 while another run of the PoC is live, 422, 503). A change applies at the PoC's next deploy.

export type PocVariableKind = "secret" | "plain";

export interface PocVariableView {
  name: string;
  kind: PocVariableKind;
  setBy: string;
  setAt: string;
  /** The value of a plain variable; always null for a secret one. */
  value: string | null;
}

export interface PocVariables {
  variables: PocVariableView[];
}

export interface PocVariablesChange {
  set: PocVariableInput[];
  delete: string[];
}

export interface PocVariablesWritten {
  runId: string;
  line: string;
  /** 2026-11-23-c81ed: the poc-apply run queued after the write, which applies the change at once; null when none was. */
  applyRunId?: string | null;
  /** 2026-11-23-c81ed: the server's sentence why the apply could not be queued (a live run, say); null when it was. */
  applyNote?: string | null;
}

export function pocVariablesPath(pocId: number): string {
  return `${POCS_PATH}/${encodeURIComponent(String(pocId))}/variables`;
}

export async function getPocVariables(pocId: number, signal?: AbortSignal): Promise<PocVariables> {
  const path = pocVariablesPath(pocId);
  const res = await apiFetch(path, { signal });
  if (!res.ok) throw await refusedAsSaid(res, path);
  return readJson<PocVariables>(res, path);
}

export async function putPocVariables(pocId: number, body: PocVariablesChange, signal?: AbortSignal): Promise<PocVariablesWritten> {
  const path = pocVariablesPath(pocId);
  const res = await apiFetch(path, {
    method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body), signal,
  });
  if (!res.ok) throw await refusedAsSaid(res, path);
  return readJson<PocVariablesWritten>(res, path);
}

// 2026-11-23-c81ec: GET /api/pocs/{pocId}/rollouts/{runId} — one poc-deploy run's rollout as the Poc and its revision
// recorded it. rolloutState is null while the run still follows Argo CD; url is set only once it settled healthy. A
// foreign PoC or run answers an empty 404, read here as "not found".

export type PocRolloutState = "healthy" | "failed";

export interface PocRolloutView {
  pocId: number;
  name: string;
  runId: string;
  revision: number | null;
  /** The Poc row's state (importing, ready, failed, stopped, …). */
  state: PocState | string;
  rolloutState: PocRolloutState | null;
  /** ISO timestamp of the reading that settled the rollout. */
  rolloutAt: string | null;
  /** https://<host>, only once the rollout settled healthy. */
  url: string | null;
  /** The source repo host's build pipeline run. */
  pipelineUrl: string | null;
  sourceCommitSha: string | null;
  gitopsCommitSha: string | null;
  gitopsCommitUrl: string | null;
}

export function pocRolloutPath(pocId: number, runId: string): string {
  return `${POCS_PATH}/${encodeURIComponent(String(pocId))}/rollouts/${encodeURIComponent(runId)}`;
}

export async function getPocRollout(pocId: number, runId: string, signal?: AbortSignal): Promise<PocRolloutView | null> {
  const path = pocRolloutPath(pocId, runId);
  const res = await apiFetch(path, { signal });
  if (res.status === 404) return null;
  if (!res.ok) throw await refused(res, path);
  return readJson<PocRolloutView>(res, path);
}

// 2026-11-23-c81ed: GET /api/pocs/{pocId} — one PoC as its detail page shows it: the desired state, a pending delete,
// the acts PocActPlan admits from its state for this caller, and the audit of its acts, oldest first. GET
// /api/pocs/{pocId}/revisions lists its revisions newest first. POST /api/pocs/{pocId}/acts/{act} starts the run that
// changes the desired state and commits it; a refusal is one plain sentence (400, 409 while a run is live or when the
// state does not admit the act), a foreign PoC an empty 404.

export type PocAct = "stop" | "start" | "rollback" | "apply" | "delete";

export interface PocDesired {
  replicas: number;
  revision: number | null;
  imageDigest: string | null;
  readinessPath: string | null;
}

export interface PocAuditEntry {
  /** ISO timestamp. */
  at: string;
  actor: string;
  /** imported, overruled, deployed, stopped, started, rolled-back, deleted, extended, expired. */
  act: string;
  reason: string | null;
  runId: string | null;
}

export interface PocDetail {
  id: number;
  name: string;
  project: string;
  target: string;
  host: string | null;
  url: string | null;
  state: PocState | string;
  ownerName: string;
  lastRunId: string | null;
  desired: PocDesired;
  deletePending: boolean;
  mayAct: boolean;
  /** What PocActPlan admits from this state; all false when the caller may not act. */
  acts: Record<PocAct, boolean>;
  readinessPathMaxChars: number;
  /** Oldest first. */
  audit: PocAuditEntry[];
  /** 2026-11-23-c81ee: see PocRow.lifetime. */
  lifetime?: PocLifetime | null;
}

export interface PocRevisionRow {
  number: number;
  state: PocRevisionState | string;
  digest: string | null;
  rolloutState: PocRolloutState | null;
  rolloutAt: string | null;
  runId: string | null;
  commitSha: string | null;
  mayRollBack: boolean;
  current: boolean;
}

export interface PocRevisionList {
  /** Newest first. */
  revisions: PocRevisionRow[];
}

export interface PocActBody {
  /** rollback: the revision to roll back to. */
  revision?: number;
  /** delete: the PoC's name, typed by the caller. */
  confirmName?: string;
  /** apply: omitted keeps the readiness; "tcp" is a TCP probe; a path starting with "/" an HTTP probe. */
  readiness?: string;
}

export interface PocActed {
  pocId: number;
  act: string;
  runId: string;
}

export function pocDetailPath(pocId: number): string {
  return `${POCS_PATH}/${encodeURIComponent(String(pocId))}`;
}

/** One PoC, or null on the empty 404 a foreign or missing PoC answers. */
export async function getPocDetail(pocId: number, signal?: AbortSignal): Promise<PocDetail | null> {
  const path = pocDetailPath(pocId);
  const res = await apiFetch(path, { signal });
  if (res.status === 404) return null;
  if (!res.ok) throw await refused(res, path);
  return readJson<PocDetail>(res, path);
}

/** A PoC's revisions newest first, or null on the empty 404. */
export async function getPocRevisions(pocId: number, signal?: AbortSignal): Promise<PocRevisionList | null> {
  const path = `${pocDetailPath(pocId)}/revisions`;
  const res = await apiFetch(path, { signal });
  if (res.status === 404) return null;
  if (!res.ok) throw await refused(res, path);
  return readJson<PocRevisionList>(res, path);
}

/** Start one act's run; a refusal throws with the server's sentence as its reason. */
export async function postPocAct(pocId: number, act: PocAct, body: PocActBody = {}, signal?: AbortSignal): Promise<PocActed> {
  const path = `${pocDetailPath(pocId)}/acts/${encodeURIComponent(act)}`;
  const res = await apiFetch(path, {
    method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body), signal,
  });
  if (!res.ok) throw await refusedAsSaid(res, path);
  return readJson<PocActed>(res, path);
}

// 2026-11-23-c81ee: the owner saw the reminder — POST /api/pocs/{pocId}/reminders/seen always answers 204; the server
// records it only for the owner inside the window. Extend starts the poc-extend run and answers the new expiry; a
// refusal is one plain sentence (409 while a run is live or the PoC is deleted, 422 when the run refused).

export interface PocExtended {
  pocId: number;
  runId: string;
  /** ISO timestamp. */
  expiresAt: string;
  extensions: number;
}

/** Tell the server the reminder was seen; it answers 204 whatever it recorded. */
export async function postPocReminderSeen(pocId: number, signal?: AbortSignal): Promise<void> {
  const path = `${pocDetailPath(pocId)}/reminders/seen`;
  const res = await apiFetch(path, { method: "POST", signal });
  if (!res.ok) throw await refused(res, path);
}

/** Extend the PoC's lifetime; a refusal throws with the server's sentence as its reason. */
export async function postPocExtend(pocId: number, signal?: AbortSignal): Promise<PocExtended> {
  const path = `${pocDetailPath(pocId)}/acts/extend`;
  const res = await apiFetch(path, { method: "POST", signal });
  if (!res.ok) throw await refusedAsSaid(res, path);
  return readJson<PocExtended>(res, path);
}
