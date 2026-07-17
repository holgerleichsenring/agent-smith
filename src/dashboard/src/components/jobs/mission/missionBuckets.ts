import type { RunSnapshot } from "@/types/hub-events";
import { toNodeStatus } from "@/components/jobs/runStatus";

// p0343: state-ranked mission control. Tickets are worked as jobs; the home
// screen ranks them by what needs the operator — Needs-you first, then Running,
// Queued, Finished. Bucketing + metrics are pure (no hooks) so they're unit-
// testable and the same derivation drives both the sections and the metric
// strip. A run lands in exactly ONE bucket by its NodeStatus.

export interface MissionBuckets {
  needsYou: RunSnapshot[];
  running: RunSnapshot[];
  queued: RunSnapshot[];
  finished: RunSnapshot[];
}

export function bucketRuns(runs: RunSnapshot[]): MissionBuckets {
  const buckets: MissionBuckets = { needsYou: [], running: [], queued: [], finished: [] };
  for (const run of runs) {
    switch (toNodeStatus(run.status)) {
      case "input":
        buckets.needsYou.push(run);
        break;
      case "queued":
        buckets.queued.push(run);
        break;
      case "ok":
      case "fail":
      case "cancel":
        buckets.finished.push(run);
        break;
      // "run" and the neutral "wait" are both in flight — a live-but-
      // unclassified run is running, not idle.
      default:
        buckets.running.push(run);
        break;
    }
  }
  return buckets;
}

export interface MissionMetrics {
  needsYou: number;
  running: number;
  queued: number;
  finishedToday: number;
  okToday: number;
  failToday: number;
  costTodayUsd: number;
}

function isSameDay(iso: string | null, now: number): boolean {
  if (!iso) return false;
  const then = new Date(iso);
  const today = new Date(now);
  return (
    then.getFullYear() === today.getFullYear() &&
    then.getMonth() === today.getMonth() &&
    then.getDate() === today.getDate()
  );
}

// Every metric is derived from the same run list the sections render — no
// separate server rollup, so the strip can never disagree with the sections
// below it. `now` is injectable for deterministic tests.
export function deriveMetrics(runs: RunSnapshot[], now: number = Date.now()): MissionMetrics {
  const buckets = bucketRuns(runs);
  const finishedToday = buckets.finished.filter((run) => isSameDay(run.finishedAt, now));

  let okToday = 0;
  let failToday = 0;
  let costTodayUsd = 0;
  for (const run of finishedToday) {
    const status = toNodeStatus(run.status);
    if (status === "ok") okToday += 1;
    else if (status === "fail") failToday += 1;
    costTodayUsd += run.costUsd ?? 0;
  }

  return {
    needsYou: buckets.needsYou.length,
    running: buckets.running.length,
    queued: buckets.queued.length,
    finishedToday: finishedToday.length,
    okToday,
    failToday,
    costTodayUsd,
  };
}
