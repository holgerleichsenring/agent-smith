import { describe, it, expect } from "vitest";
import type { RunSnapshot } from "@/types/hub-events";
import { bucketRuns } from "../missionBuckets";

// 2026-09-22-7c41c: a parked run whose RELAUNCH is under way is no longer asking the operator
// anything — its answer is in and it is waiting for a slot. The server says so by carrying the
// place the capacity queue holds for it (0 = under way, its entry already taken by the
// launcher). The bucketing must NAME the destination: the switch's default arm is running, so
// merely dropping the run out of the attention arm would paint it as executing.

function snap(runId: string, over: Partial<RunSnapshot> = {}): RunSnapshot {
  return {
    runId,
    pipeline: "fix-bug",
    trigger: "ticket",
    repos: ["server"],
    status: "waiting_for_input",
    prUrl: null,
    summary: null,
    startedAt: "2026-09-22T10:00:00Z",
    finishedAt: null,
    sandboxes: 1,
    stepIndex: 1,
    stepName: null,
    totalSteps: 5,
    lastEventType: null,
    costUsd: 0,
    llmCalls: 0,
    ticketId: null,
    ticketTitle: null,
    agentName: null,
    cancelRequested: false,
    ...over,
  };
}

describe("missionBuckets · a parked run whose relaunch is under way", () => {
  it("MissionBuckets_AWaitingRunWithAQueuePlace_IsInTheQueuedBucket", () => {
    const buckets = bucketRuns([snap("a", { queuePosition: 2 })]);

    expect(buckets.queued.map((r) => r.runId)).toEqual(["a"]);
    expect(buckets.needsYou).toHaveLength(0);
  });

  it("MissionBuckets_AWaitingRunWithAQueuePlace_IsNotInRunning", () => {
    // The destination has to be named. Dropped out of the attention arm and no further,
    // the default arm below would have called a run that is executing nothing "running".
    const buckets = bucketRuns([snap("a", { queuePosition: 0 })]);

    expect(buckets.running).toHaveLength(0);
    expect(buckets.queued.map((r) => r.runId)).toEqual(["a"]);
  });

  it("MissionBuckets_AWaitingRunWithNoQueuePlace_IsStillInNeedsYou", () => {
    const buckets = bucketRuns([snap("a"), snap("b", { queuePosition: null })]);

    expect(buckets.needsYou.map((r) => r.runId)).toEqual(["a", "b"]);
    expect(buckets.queued).toHaveLength(0);
  });
});
