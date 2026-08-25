// p0388b: the run detail's full pipeline reads BOUNDED queries against the DB
// projections instead of folding the replayed event log client-side. The rail is
// one row per step, a step's body is one clamped page fetched on selection, and
// the decisions list is the latest N. What the client holds is O(visible), never
// O(runtime) — so a 4-hour run costs the browser the same as a 4-minute one.

import type { RunEvent, RunTimeSplit } from "@/types/hub-events";
import { getJson } from "@/lib/apiResponse";

export interface RunStepRow {
  stepIndex: number;
  stepName: string;
  displayName: string | null;
  commandName: string | null;
  /** p0395: the spliced phase (p0393a) this step belongs to, split off the step
   *  names server-side. Absent on payloads from servers that predate it — the
   *  rail then splits the known "p<id>: " prefix off the labels itself. */
  phaseId?: string | null;
  /** p0398: the command's static display class — "milestone" | "gate" |
   *  "internal". Absent on payloads from servers that predate it; the rail
   *  then treats every row as a milestone (nothing condenses, nothing hides). */
  stepClass?: string | null;
  /** p0398: whether a gate has something to say (failed/parked, or a summary
   *  that is not one of its known no-op sentences). Decided server-side in the
   *  read path; the UI only renders. */
  hasFinding?: boolean;
  /** p0405: true for a step the run has ANNOUNCED but not reached. Everything
   *  below is null on such a row — an unreached step has no status, no cost and
   *  no duration, and the server composes the whole ordered sequence. */
  planned?: boolean;
  status: string | null;
  durationSeconds: number | null;
  resultMessage: string | null;
  llmCalls: number | null;
  costUsd: number | null;
  sandboxCommands: number | null;
  subAgents: number | null;
  /** p0404: where this step's wall-clock went. Absent on payloads from servers
   *  that predate it — the pane then shows duration and cost as before. */
  time?: RunTimeSplit | null;
}

/** p0388d: a step's page read from the newest end backwards. `oldestSeq` is the
 *  cursor "load older" walks with; `hasOlder` is how the view knows it is not
 *  showing the whole step. */
export interface RunStepEventPage {
  events: RunEvent[];
  oldestSeq: number;
  newestSeq: number;
  hasOlder: boolean;
}

/** p0388d: what the live poll gets back — the rows written after the caller's
 *  newest cursor. `hasNewer` means the delta outgrew one page, so the caller
 *  reads on immediately instead of trailing the step by a page per tick. */
export interface RunStepEventDelta {
  events: RunEvent[];
  newestSeq: number;
  hasNewer: boolean;
}

export interface RunDecisionRow {
  stepIndex: number | null;
  name: string;
  reason: string | null;
  /** p0388c: the producer's classification. Null on rows written before it was
   *  projected — those render without the category segment. */
  category: string | null;
  recordedAt: string;
}

export async function fetchRunSteps(runId: string, signal?: AbortSignal): Promise<RunStepRow[]> {
  const body = await getJson<{ steps?: RunStepRow[] }>(
    `/api/runs/${encodeURIComponent(runId)}/steps`, signal);
  return body.steps ?? [];
}

/**
 * p0388d: the step's page anchored at its NEWEST row — what opening a step
 * returns. `beforeSeq` walks the same read backwards into the step's history,
 * each page contiguous with the last.
 */
export async function fetchRunStepEventPage(
  runId: string,
  stepIndex: number,
  beforeSeq: number | null,
  signal?: AbortSignal,
): Promise<RunStepEventPage> {
  const body = await getStepEvents(
    runId, stepIndex, beforeSeq === null ? undefined : { beforeSeq: String(beforeSeq) }, signal);
  return {
    events: body.events ?? [],
    oldestSeq: body.oldestSeq ?? beforeSeq ?? 0,
    newestSeq: body.newestSeq ?? 0,
    hasOlder: body.hasOlder ?? false,
  };
}

/** p0388d: the forward delta an open step polls while the run is live. */
export async function fetchRunStepEventDelta(
  runId: string,
  stepIndex: number,
  sinceSeq: number,
  signal?: AbortSignal,
): Promise<RunStepEventDelta> {
  const body = await getStepEvents(runId, stepIndex, { sinceSeq: String(sinceSeq) }, signal);
  return {
    events: body.events ?? [],
    newestSeq: body.newestSeq ?? sinceSeq,
    hasNewer: body.hasNewer ?? false,
  };
}

type StepEventsBody = Partial<RunStepEventPage> & Partial<RunStepEventDelta>;

async function getStepEvents(
  runId: string,
  stepIndex: number,
  query: Record<string, string> | undefined,
  signal?: AbortSignal,
): Promise<StepEventsBody> {
  const params = new URLSearchParams(query ?? {}).toString();
  return getJson<StepEventsBody>(
    `/api/runs/${encodeURIComponent(runId)}/steps/${stepIndex}/events${params ? `?${params}` : ""}`,
    signal,
  );
}

export async function fetchRunDecisions(
  runId: string,
  signal?: AbortSignal,
): Promise<RunDecisionRow[]> {
  const body = await getJson<{ decisions?: RunDecisionRow[] }>(
    `/api/runs/${encodeURIComponent(runId)}/decisions`, signal);
  return body.decisions ?? [];
}
