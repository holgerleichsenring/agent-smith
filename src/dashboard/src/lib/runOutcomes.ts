import { bucketRuns } from "@/components/jobs/mission/missionBuckets";
import { toNodeStatus } from "@/components/jobs/runStatus";
import type { RunSnapshot } from "@/types/hub-events";

// 2026-08-27-7463: what came back, counted off the SAME buckets the rail counts
// — bucketRuns is the one place a run is placed, so the Overview and the rail
// can never report a different number of running jobs. The finished bucket is
// then split into how those runs ended, which is the reading no page shows.

export interface RunOutcomes {
  total: number;
  needsYou: number;
  running: number;
  queued: number;
  finished: number;
  succeeded: number;
  failed: number;
  cancelled: number;
}

export function deriveRunOutcomes(runs: RunSnapshot[]): RunOutcomes {
  const buckets = bucketRuns(runs);
  let succeeded = 0;
  let failed = 0;
  let cancelled = 0;
  for (const run of buckets.finished) {
    const status = toNodeStatus(run.status);
    if (status === "ok") succeeded += 1;
    else if (status === "fail") failed += 1;
    else if (status === "cancel") cancelled += 1;
  }
  return {
    total: runs.length,
    needsYou: buckets.needsYou.length,
    running: buckets.running.length,
    queued: buckets.queued.length,
    finished: buckets.finished.length,
    succeeded,
    failed,
    cancelled,
  };
}
