import { describe, it, expect } from "vitest";
import { foldOverviewUpsert } from "@/lib/JobsHubClient";
import type { OverviewSnapshot, RunSnapshot } from "@/types/hub-events";

function run(runId: string, status: string): RunSnapshot {
  return {
    runId, pipeline: "fix-bug", trigger: "ticket", repos: ["r"], status,
    prUrl: null, summary: null, startedAt: "t0", finishedAt: null, sandboxes: 1,
    stepIndex: 1, stepName: "s", totalSteps: 5, lastEventType: null, costUsd: 0,
    llmCalls: 0, ticketId: null, ticketTitle: null, agentName: null, cancelRequested: false,
  } as RunSnapshot;
}

const empty: OverviewSnapshot = { active: [], recent: [], systemActivity: null };

describe("foldOverviewUpsert (p0233)", () => {
  it("FoldUpsert_NewRunningJob_AppearsInActive", () => {
    // The bug: a job that arrived while the Runs list was unmounted never
    // showed until a full reload. Folding in the client makes a new job land
    // in `active` so the behavior-subject replay carries it to a remount.
    const next = foldOverviewUpsert(empty, run("run-1", "running"));
    expect(next.active.map((r) => r.runId)).toEqual(["run-1"]);
  });

  it("FoldUpsert_SameRunId_ReplacesInPlace_NoDuplicate", () => {
    const a = foldOverviewUpsert(empty, run("run-1", "running"));
    const b = foldOverviewUpsert(a, { ...run("run-1", "running"), stepIndex: 3 });
    expect(b.active).toHaveLength(1);
    expect(b.active[0].stepIndex).toBe(3);
  });

  it("FoldUpsert_TerminalRun_MovesFromActiveToRecent", () => {
    const a = foldOverviewUpsert(empty, run("run-1", "running"));
    const b = foldOverviewUpsert(a, run("run-1", "success"));
    expect(b.active).toHaveLength(0);
    expect(b.recent.map((r) => r.runId)).toEqual(["run-1"]);
  });

  it("FoldUpsert_NullCurrent_Seeds", () => {
    const next = foldOverviewUpsert(null, run("run-1", "running"));
    expect(next.active).toHaveLength(1);
  });
});
