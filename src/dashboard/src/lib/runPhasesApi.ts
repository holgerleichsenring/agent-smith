// p0466: the run's phases, read from the server's RunPhase projection. A phase
// is a row the producer wrote — its ordinal, its title, where it ended up — so a
// phase that has ENDED is still addressable. Before this the client could only
// group the rail by a prefix it parsed out of step names, which meant a finished
// phase had nothing to open.

import type { RunStepRow } from "@/lib/runStepsApi";
import { apiFetch, getJson, readJson } from "@/lib/apiResponse";

export interface RunPhaseDecision {
  stepIndex: number | null;
  name: string;
  reason: string | null;
  category: string | null;
  recordedAt: string;
}

export interface RunPhaseRow {
  phaseId: string;
  ordinal: number;
  title: string;
  /** "not_started" | "in_progress" | "done" | "failed". */
  status: string;
  startedAt: string;
  endedAt: string | null;
  /** Why the standing is what it is — a failing command, or an entry note. */
  verdict: string | null;
  decisions: RunPhaseDecision[];
  steps: RunStepRow[];
}

/** The phase plus the spec it executed. The record is served only per phase. */
export interface RunPhaseDetail {
  phase: RunPhaseRow;
  record: string | null;
}

export async function fetchRunPhases(
  runId: string,
  signal?: AbortSignal,
): Promise<RunPhaseRow[]> {
  const body = await getJson<{ phases?: RunPhaseRow[] }>(
    `/api/runs/${encodeURIComponent(runId)}/phases`, signal);
  return body.phases ?? [];
}

export async function fetchRunPhase(
  runId: string,
  phaseId: string,
  signal?: AbortSignal,
): Promise<RunPhaseDetail | null> {
  const path =
    `/api/runs/${encodeURIComponent(runId)}/phases/${encodeURIComponent(phaseId)}`;
  const res = await apiFetch(path, { signal });
  if (res.status === 404) return null;
  return readJson<RunPhaseDetail>(res, path);
}
