// p0466: the run's phases, read from the server's RunPhase projection. A phase
// is a row the producer wrote — its ordinal, its title, where it ended up — so a
// phase that has ENDED is still addressable. Before this the client could only
// group the rail by a prefix it parsed out of step names, which meant a finished
// phase had nothing to open.

import type { RunStepRow } from "@/lib/runStepsApi";

const API_BASE = process.env.NEXT_PUBLIC_API_BASE_URL ?? "";

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
  const res = await fetch(`${API_BASE}/api/runs/${encodeURIComponent(runId)}/phases`, { signal });
  if (!res.ok) throw new Error(`HTTP ${res.status}`);
  const body = (await res.json()) as { phases?: RunPhaseRow[] };
  return body.phases ?? [];
}

export async function fetchRunPhase(
  runId: string,
  phaseId: string,
  signal?: AbortSignal,
): Promise<RunPhaseDetail | null> {
  const res = await fetch(
    `${API_BASE}/api/runs/${encodeURIComponent(runId)}/phases/${encodeURIComponent(phaseId)}`,
    { signal },
  );
  if (res.status === 404) return null;
  if (!res.ok) throw new Error(`HTTP ${res.status}`);
  return (await res.json()) as RunPhaseDetail;
}
