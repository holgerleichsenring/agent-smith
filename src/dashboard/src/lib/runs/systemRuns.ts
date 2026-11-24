import type { RunSnapshot } from "@/types/hub-events";
import { toNodeStatus } from "@/components/jobs/runStatus";

// 2026-11-24-35ff: the runs the server starts itself (series beats, a landing's follow-ups, sweeps) carry `system` from the
// pipeline's declared origin on the server. The list hides them unless the viewer asks, and says how many it hides and how
// many of those failed, so a failure is never hidden silently.

export interface SystemRunSplit {
  /** The runs the list renders. */
  shown: RunSnapshot[];
  /** Every system run in the list, shown or not. */
  system: number;
  /** The system runs among them that failed. */
  systemFailed: number;
}

export function splitSystemRuns(runs: RunSnapshot[], showSystem: boolean): SystemRunSplit {
  const system = runs.filter((run) => run.system === true);
  return {
    shown: showSystem ? runs : runs.filter((run) => run.system !== true),
    system: system.length,
    systemFailed: system.filter((run) => toNodeStatus(run.status) === "fail").length,
  };
}

// 2026-11-24-6357: the server sends a window of work runs and a window of system runs. Load more starts from the oldest
// run of what it pages through: with system runs hidden, the oldest finished work run; shown, the newer of the two windows'
// ends, so no run between them is skipped. Each page then moves the cursor to its own oldest run (see MissionControl).
export function loadMoreCursor(runs: RunSnapshot[], showSystem: boolean): string | null {
  const finished = runs.filter((run) => run.finishedAt != null);
  const work = oldestStart(finished.filter((run) => run.system !== true));
  const system = oldestStart(finished.filter((run) => run.system === true));
  if (!showSystem) return work ?? system;
  if (work === null) return system;
  if (system === null) return work;
  return work > system ? work : system;
}

export function oldestStart(runs: RunSnapshot[]): string | null {
  return runs.reduce<string | null>((min, run) => (min === null || run.startedAt < min ? run.startedAt : min), null);
}
