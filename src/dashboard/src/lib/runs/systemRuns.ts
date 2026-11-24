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
