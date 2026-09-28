import type { RunSnapshot } from "@/types/hub-events";

// The live overview arrives as two lists — the active runs and the recent ones — and a
// run can sit in both while it finishes. Every reader wants ONE list: newest-first by
// startedAt, with the live snapshot winning on id over a stale recent duplicate.
export function mergeNewestFirst(active: RunSnapshot[], recent: RunSnapshot[]): RunSnapshot[] {
  const byId = new Map<string, RunSnapshot>();
  // active first so a still-running snapshot wins over a stale recent dup.
  for (const r of [...active, ...recent]) {
    if (!byId.has(r.runId)) byId.set(r.runId, r);
  }
  return [...byId.values()].sort(
    (a, b) => new Date(b.startedAt).getTime() - new Date(a.startedAt).getTime(),
  );
}
