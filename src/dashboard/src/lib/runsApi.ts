// p0246f: the dashboard's READ client for runs. Run data lives in the DB
// system-of-record; the dashboard fetches the list/detail here on first paint
// and refetches when the SignalR "RunsChanged" nudge fires. Redis carries only
// transport (live events + the nudge), never the authoritative run snapshot —
// so the list survives a process restart AND a Redis flush.

import type { RunSnapshot } from "@/types/hub-events";

const API_BASE = process.env.NEXT_PUBLIC_API_BASE_URL ?? "";

export interface RunsResponse {
  active: RunSnapshot[];
  recent: RunSnapshot[];
}

export async function fetchRuns(signal?: AbortSignal): Promise<RunsResponse> {
  const res = await fetch(`${API_BASE}/api/runs`, { signal });
  if (!res.ok) throw new Error(`HTTP ${res.status}`);
  return (await res.json()) as RunsResponse;
}

export async function fetchRun(runId: string, signal?: AbortSignal): Promise<RunSnapshot | null> {
  const res = await fetch(`${API_BASE}/api/runs/${encodeURIComponent(runId)}`, { signal });
  if (res.status === 404) return null;
  if (!res.ok) throw new Error(`HTTP ${res.status}`);
  return (await res.json()) as RunSnapshot;
}
