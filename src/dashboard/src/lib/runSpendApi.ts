import { getJson } from "@/lib/apiResponse";

// 2026-11-24-0fbfb: what the caller's projects spent on runs since an instant, summed by the server over every run in
// the store (GET /api/runs/spend) — not over the runs a page has loaded, and whatever the system-runs toggle shows.
// The browser names the instant: the start of its own day, so "today" is the viewer's day and the zone never travels.

export interface RunSpendPart {
  costUsd: number;
  runs: number;
}

export interface RunSpendSlice {
  /** The repositories the runs worked on, sorted; empty for none. */
  repos: string[];
  pipeline: string;
  costUsd: number;
}

export interface RunSpend {
  from: string;
  totalUsd: number;
  work: RunSpendPart;
  system: RunSpendPart;
  llmCalls: number;
  slices: RunSpendSlice[];
}

export const WEEK_MS = 7 * 24 * 60 * 60 * 1000;

/** The instant the viewer's local day began, as ISO. */
export function localDayStart(nowMs: number): string {
  const now = new Date(nowMs);
  return new Date(now.getFullYear(), now.getMonth(), now.getDate()).toISOString();
}

/** The instant seven days before `nowMs`, as ISO. */
export function weekStart(nowMs: number): string {
  return new Date(nowMs - WEEK_MS).toISOString();
}

export function fetchRunSpend(fromIso: string, signal?: AbortSignal): Promise<RunSpend> {
  return getJson<RunSpend>(`/api/runs/spend?from=${encodeURIComponent(fromIso)}`, signal);
}
