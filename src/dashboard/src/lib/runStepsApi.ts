// p0388b: the run detail's full pipeline reads BOUNDED queries against the DB
// projections instead of folding the replayed event log client-side. The rail is
// one row per step, a step's body is one clamped page fetched on selection, and
// the decisions list is the latest N. What the client holds is O(visible), never
// O(runtime) — so a 4-hour run costs the browser the same as a 4-minute one.

import type { RunEvent } from "@/types/hub-events";

const API_BASE = process.env.NEXT_PUBLIC_API_BASE_URL ?? "";

export interface RunStepRow {
  stepIndex: number;
  stepName: string;
  displayName: string | null;
  commandName: string | null;
  status: string;
  durationSeconds: number | null;
  resultMessage: string | null;
  llmCalls: number;
  costUsd: number;
  sandboxCommands: number;
  subAgents: number;
}

export interface RunStepEventPage {
  events: RunEvent[];
  nextSeq: number;
  hasMore: boolean;
}

export interface RunDecisionRow {
  stepIndex: number | null;
  name: string;
  reason: string | null;
  recordedAt: string;
}

export async function fetchRunSteps(runId: string, signal?: AbortSignal): Promise<RunStepRow[]> {
  const res = await fetch(`${API_BASE}/api/runs/${encodeURIComponent(runId)}/steps`, { signal });
  if (!res.ok) throw new Error(`HTTP ${res.status}`);
  const body = (await res.json()) as { steps?: RunStepRow[] };
  return body.steps ?? [];
}

export async function fetchRunStepEvents(
  runId: string,
  stepIndex: number,
  sinceSeq: number,
  signal?: AbortSignal,
): Promise<RunStepEventPage> {
  const params = new URLSearchParams({ sinceSeq: String(sinceSeq) });
  const res = await fetch(
    `${API_BASE}/api/runs/${encodeURIComponent(runId)}/steps/${stepIndex}/events?${params}`,
    { signal },
  );
  if (!res.ok) throw new Error(`HTTP ${res.status}`);
  const body = (await res.json()) as Partial<RunStepEventPage>;
  return { events: body.events ?? [], nextSeq: body.nextSeq ?? sinceSeq, hasMore: body.hasMore ?? false };
}

export async function fetchRunDecisions(
  runId: string,
  signal?: AbortSignal,
): Promise<RunDecisionRow[]> {
  const res = await fetch(`${API_BASE}/api/runs/${encodeURIComponent(runId)}/decisions`, { signal });
  if (!res.ok) throw new Error(`HTTP ${res.status}`);
  const body = (await res.json()) as { decisions?: RunDecisionRow[] };
  return body.decisions ?? [];
}
