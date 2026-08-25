// p0246f: the dashboard's READ client for runs. Run data lives in the DB
// system-of-record; the dashboard fetches the list/detail here on first paint
// and refetches when the SignalR "RunsChanged" nudge fires. Redis carries only
// transport (live events + the nudge), never the authoritative run snapshot —
// so the list survives a process restart AND a Redis flush.

import type { RunSnapshot } from "@/types/hub-events";
import { apiFetch, getJson, readJson } from "@/lib/apiResponse";

export interface RunsResponse {
  active: RunSnapshot[];
  recent: RunSnapshot[];
}

// 2026-08-25-39ab: a list the server answers without one of its halves is a list
// with that half empty, not a page that throws while reading `.length`.
export async function fetchRuns(signal?: AbortSignal): Promise<RunsResponse> {
  const body = await getJson<Partial<RunsResponse>>(`/api/runs`, signal);
  return { active: body.active ?? [], recent: body.recent ?? [] };
}

// p0355: page OLDER runs than a cursor for the runs-list "load more". Newest-
// first. The list endpoint's default window is capped; this walks back beyond it
// via a `before` timestamp cursor so every run is reachable. Accepts a flat
// array OR the {active,recent}/{recent} envelope so it degrades gracefully if
// the backing endpoint returns the list shape rather than a bare page.
export async function fetchRunsBefore(
  beforeIso: string,
  limit = 20,
  signal?: AbortSignal,
): Promise<RunSnapshot[]> {
  const params = new URLSearchParams({ before: beforeIso, limit: String(limit) });
  const data = await getJson<unknown>(`/api/runs?${params.toString()}`, signal);
  if (Array.isArray(data)) return data as RunSnapshot[];
  if (data && typeof data === "object") {
    const env = data as { recent?: RunSnapshot[] };
    if (Array.isArray(env.recent)) return env.recent;
  }
  return [];
}

export async function fetchRun(runId: string, signal?: AbortSignal): Promise<RunSnapshot | null> {
  const path = `/api/runs/${encodeURIComponent(runId)}`;
  const res = await apiFetch(path, { signal });
  if (res.status === 404) return null;
  return readJson<RunSnapshot>(res, path);
}
