// p0423b: the STORY view's read surface — why did this run do that. Separate from the
// live surface on purpose: progress-watching and failure-diagnosis are different jobs, so
// the diagnosis payload is fetched only when an operator opens the story, never pushed.
//
// Every number here is a fold over the run's recorded trail (p0423). Nothing is counted
// while the run happens, so nothing here can drift from the events it describes.

import { getJson } from "@/lib/apiResponse";

/** The fold over a slice of the trail — the same shape for a run and for one phase. */
export interface RunCallStatistics {
  calls: number;
  failedCalls: number;
  totalDurationMs: number;
  totalPromptChars: number;
  largestPromptChars: number;
  totalResponseChars: number;
  smallestResponseChars: number;
  toolCalls: number;
  toolOutputChars: number;
  toolCharsNeverDelivered: number;
  retries: number;
}

/** One model call, in call order. The pair (promptChars, answerChars) IS the plot. */
export interface RunCallPoint {
  index: number;
  phaseId: string | null;
  stepIndex: number | null;
  role: string | null;
  model: string | null;
  promptChars: number;
  answerChars: number;
  durationMs: number;
  throttleWaitMs: number;
  outcome: string;
  attempt: number;
}

/** One command a phase ran, with the exit code it ended on. */
export interface RunCommandPoint {
  index: number;
  phaseId: string | null;
  stepIndex: number | null;
  repo: string;
  command: string;
  exitCode: number;
  durationMs: number;
  outputChars: number;
  deliveredChars: number;
  attempt: number;
}

export interface RunPhaseStatistics {
  phaseId: string | null;
  steps: number;
  durationMs: number;
  calls: RunCallStatistics;
  commands: number;
  failedCommands: number;
}

export interface RunStatistics {
  totals: RunCallStatistics;
  totalDurationMs: number;
  phases: RunPhaseStatistics[];
  calls: RunCallPoint[];
  commands: RunCommandPoint[];
  truncated: boolean;
  /** p0341h: what the run spent its time ON, at the two levels a reader asks about.
   *  Absent on payloads from servers that predate it — the panel then shows totals only. */
  work?: RunWorkBreakdown;
}

/** p0341h: one kind of work, folded — how often it ran and how long that took. */
export interface RunWorkKind {
  label: string;
  count: number;
  durationMs: number;
  failed: number;
}

export interface RunWorkBreakdown {
  pipeline: RunWorkKind[];
  sandbox: RunWorkKind[];
}

/** One entry of a recorded conversation, without its content — prompts reach megabytes. */
export interface RunTraceEntryHeader {
  sequence: number;
  label: string;
  chars: number;
}

export async function fetchRunStatistics(
  runId: string,
  signal?: AbortSignal,
): Promise<RunStatistics> {
  return getJson<RunStatistics>(`/api/runs/${encodeURIComponent(runId)}/statistics`, signal);
}

export async function fetchRunTrace(
  runId: string,
  signal?: AbortSignal,
): Promise<RunTraceEntryHeader[]> {
  const body = await getJson<{ entries?: RunTraceEntryHeader[] }>(
    `/api/runs/${encodeURIComponent(runId)}/trace`,
    signal,
  );
  return body.entries ?? [];
}

export async function fetchRunTraceEntry(
  runId: string,
  sequence: number,
  label: string,
  signal?: AbortSignal,
): Promise<string> {
  const body = await getJson<{ content?: string }>(
    `/api/runs/${encodeURIComponent(runId)}/trace/${sequence}/${encodeURIComponent(label)}`,
    signal,
  );
  return body.content ?? "";
}

