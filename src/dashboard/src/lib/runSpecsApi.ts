// p0466: the run's specs, read from the server's RunSpec projection. A spec row is
// one the producer wrote — its ordinal, its title, where it ended up — so a spec
// that has ENDED is still addressable. Before this the client could only group
// the rail by a prefix it parsed out of step names, which meant a finished spec
// had nothing to open. 2026-10-06-03c7g: served at /specs; was /phases.

import type { RunStepRow } from "@/lib/runStepsApi";
import { apiFetch, getJson, readJson } from "@/lib/apiResponse";

export interface RunSpecDecision {
  stepIndex: number | null;
  name: string;
  reason: string | null;
  category: string | null;
  recordedAt: string;
}

export interface RunSpecRow {
  specId: string;
  ordinal: number;
  title: string;
  /** "not_started" | "in_progress" | "done" | "failed" | "handed_back". */
  status: string;
  startedAt: string;
  endedAt: string | null;
  /** Why the standing is what it is — a failing command, or an entry note. */
  verdict: string | null;
  decisions: RunSpecDecision[];
  steps: RunStepRow[];
}

/** The spec row plus the body it executed. The record is served only per spec. */
export interface RunSpecDetail {
  spec: RunSpecRow;
  record: string | null;
}

export async function fetchRunSpecs(
  runId: string,
  signal?: AbortSignal,
): Promise<RunSpecRow[]> {
  const body = await getJson<{ specs?: RunSpecRow[] }>(
    `/api/runs/${encodeURIComponent(runId)}/specs`, signal);
  return body.specs ?? [];
}

export async function fetchRunSpec(
  runId: string,
  specId: string,
  signal?: AbortSignal,
): Promise<RunSpecDetail | null> {
  const path =
    `/api/runs/${encodeURIComponent(runId)}/specs/${encodeURIComponent(specId)}`;
  const res = await apiFetch(path, { signal });
  if (res.status === 404) return null;
  return readJson<RunSpecDetail>(res, path);
}
