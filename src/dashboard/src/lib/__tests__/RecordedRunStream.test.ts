import { describe, it, expect } from "vitest";
import { mergeNewestFirst } from "@/components/jobs/RunsList";
import { applySnapshotFilters } from "@/hooks/useJobsHub";
import type { RunSnapshot } from "@/types/hub-events";

// p0239b ui-e2e (data layer), regenerated for p0246f's nudge + DB-refetch
// contract. The dashboard no longer FOLDS a hub event stream — Redis is demoted
// to a "RunsChanged" nudge, and the dashboard refetches the authoritative run
// list from GET /api/runs (the DB system-of-record) on each nudge. So a recorded
// run is now a sequence of DB FRAMES (what /api/runs returns at successive
// nudges), and the dashboard's derived view is simply the LATEST frame run
// through the contract-stable projection the Runs list uses:
// applySnapshotFilters (zombie hide + cap) + mergeNewestFirst (active+recent →
// one newest-first list). The snapshots are typed against the GENERATED contract
// (RunSnapshot from types/hub-events.ts, kept in sync by gen:hub-events), so a
// field rename/drop fails to compile here.

function snapshot(runId: string, status: string, over: Partial<RunSnapshot> = {}): RunSnapshot {
  return {
    runId, pipeline: "fix-bug", trigger: "ticket", repos: ["primary"], status,
    prUrl: null, summary: null, startedAt: "t0", finishedAt: null, sandboxes: 1,
    stepIndex: 0, stepName: "LoadCatalog", totalSteps: 14, lastEventType: null, costUsd: 0,
    llmCalls: 0, ticketId: "42", ticketTitle: "Fix the bug", agentName: "claude", cancelRequested: false,
    ...over,
  } as RunSnapshot;
}

interface Frame {
  active: RunSnapshot[];
  recent: RunSnapshot[];
}

// The latest DB frame is the truth; render it the way the Runs list does.
function view(frame: Frame) {
  const filtered = applySnapshotFilters(
    { active: frame.active, recent: frame.recent, systemActivity: null }, false);
  return { ...filtered, merged: mergeNewestFirst(filtered.active, filtered.recent) };
}

describe("RecordedRunStream (p0239b → p0246f nudge+refetch)", () => {
  it("LiveUpdate_RunningRunAdvancesThroughSteps_ReflectsLatestStep", () => {
    // Each nudge triggers a refetch; the DB returns the run's CURRENT step. The
    // live view shows the latest step with no duplicate across refetches (the
    // frame is authoritative — there is no client-side accumulation).
    const frames: Frame[] = [
      { active: [snapshot("run-1", "running", { stepIndex: 0, stepName: "LoadCatalog" })], recent: [] },
      { active: [snapshot("run-1", "running", { stepIndex: 5, stepName: "AnalyzeCode" })], recent: [] },
      { active: [snapshot("run-1", "running", { stepIndex: 11, stepName: "AgenticMaster" })], recent: [] },
    ];

    const v = view(frames[frames.length - 1]);
    expect(v.active).toHaveLength(1);
    expect(v.active[0].stepIndex).toBe(11);
    expect(v.active[0].stepName).toBe("AgenticMaster");
    expect(v.recent).toHaveLength(0);
    expect(v.merged.map((r) => r.runId)).toEqual(["run-1"]);
  });

  it("FailedRunView_TerminalFailure_MovesToRecentWithReason", () => {
    // The failed-run view: the terminal frame has the run out of `active` and in
    // `recent` carrying its failed status + summary (the keystone reason).
    const frames: Frame[] = [
      { active: [snapshot("run-1", "running", { stepIndex: 11, stepName: "AgenticMaster" })], recent: [] },
      { active: [], recent: [snapshot("run-1", "failed", {
        stepIndex: 13, stepName: "CommitAndPR", finishedAt: "t1",
        summary: "This fix/feature run produced no code changes — Recorded as FAILED.",
      })] },
    ];

    const v = view(frames[frames.length - 1]);
    expect(v.active).toHaveLength(0);
    expect(v.recent).toHaveLength(1);
    expect(v.recent[0].status).toBe("failed");
    expect(v.recent[0].summary).toContain("no code changes");
  });

  it("MultipleRuns_LiveAndTerminal_PartitionedCorrectly", () => {
    // Two runs: one still live, one finished green — the DB frame partitions them
    // into active vs recent (the Runs list's two sections).
    const frame: Frame = {
      active: [snapshot("run-1", "running", { stepIndex: 3 })],
      recent: [snapshot("run-2", "success", { stepIndex: 13, finishedAt: "t1", prUrl: "https://pr/2" })],
    };

    const v = view(frame);
    expect(v.active.map((r) => r.runId)).toEqual(["run-1"]);
    expect(v.recent.map((r) => r.runId)).toEqual(["run-2"]);
    expect(v.recent[0].prUrl).toBe("https://pr/2");
  });
});
