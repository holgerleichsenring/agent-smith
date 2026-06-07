import { describe, it, expect } from "vitest";
import { foldOverviewUpsert } from "@/lib/JobsHubClient";
import type { OverviewSnapshot, RunSnapshot } from "@/types/hub-events";

// p0239b ui-e2e (data layer): drive the overview projection from a RECORDED
// hub-event-style stream — the same JobUpserted snapshots the SignalR hub emits
// during a real run — and assert the dashboard's derived state for the
// live-update and failed-run views, deterministically, with no live server.
// The snapshots are typed against the GENERATED contract (RunSnapshot from
// types/hub-events.ts, kept in sync by the gen:hub-events drift check), so a
// contract change that drops/renames a field fails to compile here.
//
// NOTE: p0246's redis-thin-notify reshapes the hub payload into a nudge +
// DB-refetch contract; the full recorded UI fixtures (Jobs/Runs/Timeline +
// result.md/plan.md render against live SignalR) are regenerated against that
// new contract as part of p0246. This suite covers the contract-stable
// projection logic that survives that change.

function snapshot(runId: string, status: string, over: Partial<RunSnapshot> = {}): RunSnapshot {
  return {
    runId, pipeline: "fix-bug", trigger: "ticket", repos: ["primary"], status,
    prUrl: null, summary: null, startedAt: "t0", finishedAt: null, sandboxes: 1,
    stepIndex: 0, stepName: "LoadCatalog", totalSteps: 14, lastEventType: null, costUsd: 0,
    llmCalls: 0, ticketId: "42", ticketTitle: "Fix the bug", agentName: "claude", cancelRequested: false,
    ...over,
  } as RunSnapshot;
}

function replay(stream: RunSnapshot[]): OverviewSnapshot {
  return stream.reduce<OverviewSnapshot>(
    (acc, s) => foldOverviewUpsert(acc, s),
    { active: [], recent: [], systemActivity: null });
}

describe("RecordedRunStream (p0239b)", () => {
  it("LiveUpdate_RunningRunAdvancesThroughSteps_ReflectsLatestStep", () => {
    // A run progresses through its steps; each JobUpserted advances stepIndex.
    // The live view must always show the latest step in-place (no duplicates).
    const view = replay([
      snapshot("run-1", "running", { stepIndex: 0, stepName: "LoadCatalog" }),
      snapshot("run-1", "running", { stepIndex: 5, stepName: "AnalyzeCode" }),
      snapshot("run-1", "running", { stepIndex: 11, stepName: "AgenticMaster" }),
    ]);

    expect(view.active).toHaveLength(1);
    expect(view.active[0].stepIndex).toBe(11);
    expect(view.active[0].stepName).toBe("AgenticMaster");
    expect(view.recent).toHaveLength(0);
  });

  it("FailedRunView_TerminalFailure_MovesToRecentWithReason", () => {
    // The failed-run view: a run that ends failed leaves `active` and lands in
    // `recent` carrying its failed status + summary (the keystone reason).
    const view = replay([
      snapshot("run-1", "running", { stepIndex: 11, stepName: "AgenticMaster" }),
      snapshot("run-1", "failed", {
        stepIndex: 13, stepName: "CommitAndPR", finishedAt: "t1",
        summary: "This fix/feature run produced no code changes — Recorded as FAILED.",
      }),
    ]);

    expect(view.active).toHaveLength(0);
    expect(view.recent).toHaveLength(1);
    expect(view.recent[0].status).toBe("failed");
    expect(view.recent[0].summary).toContain("no code changes");
  });

  it("MultipleRuns_LiveAndTerminal_PartitionedCorrectly", () => {
    // Two concurrent runs: one still live, one finished green — the overview
    // partitions them into active vs recent (the Runs list's two sections).
    const view = replay([
      snapshot("run-1", "running", { stepIndex: 3 }),
      snapshot("run-2", "running", { stepIndex: 2 }),
      snapshot("run-2", "success", { stepIndex: 13, finishedAt: "t1", prUrl: "https://pr/2" }),
    ]);

    expect(view.active.map((r) => r.runId)).toEqual(["run-1"]);
    expect(view.recent.map((r) => r.runId)).toEqual(["run-2"]);
    expect(view.recent[0].prUrl).toBe("https://pr/2");
  });
});
