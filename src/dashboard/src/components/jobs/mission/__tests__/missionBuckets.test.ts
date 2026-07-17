import { describe, it, expect } from "vitest";
import type { RunSnapshot } from "@/types/hub-events";
import { bucketRuns, deriveMetrics } from "../missionBuckets";

function snap(runId: string, status: string, over: Partial<RunSnapshot> = {}): RunSnapshot {
  return {
    runId,
    pipeline: "fix-bug",
    trigger: "ticket",
    repos: ["server"],
    status,
    prUrl: null,
    summary: null,
    startedAt: "2026-07-17T10:00:00Z",
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

describe("missionBuckets", () => {
  it("BucketRuns_ByStatus_AssignsEachToOneBucket", () => {
    const buckets = bucketRuns([
      snap("a", "waiting_for_input"),
      snap("b", "running"),
      snap("c", "queued"),
      snap("d", "success"),
      snap("e", "failed"),
      snap("f", "cancelled"),
    ]);
    expect(buckets.needsYou.map((r) => r.runId)).toEqual(["a"]);
    expect(buckets.running.map((r) => r.runId)).toEqual(["b"]);
    expect(buckets.queued.map((r) => r.runId)).toEqual(["c"]);
    expect(buckets.finished.map((r) => r.runId)).toEqual(["d", "e", "f"]);
  });

  it("DeriveMetrics_FinishedToday_SplitsOkFailAndSumsCost", () => {
    // Today's finished runs share `now`'s exact time-of-day so the same-day
    // check is timezone-robust; yesterday is exactly 24h earlier.
    const now = new Date("2026-07-17T12:00:00Z").getTime();
    const metrics = deriveMetrics(
      [
        snap("ok1", "success", { finishedAt: "2026-07-17T12:00:00Z", costUsd: 1.5 }),
        snap("fail1", "failed", { finishedAt: "2026-07-17T12:00:00Z", costUsd: 2.0 }),
        snap("yesterday", "success", { finishedAt: "2026-07-16T12:00:00Z", costUsd: 9.0 }),
        snap("running", "running"),
        snap("needs", "waiting_for_input"),
        snap("queued", "queued"),
      ],
      now,
    );
    expect(metrics.needsYou).toBe(1);
    expect(metrics.running).toBe(1);
    expect(metrics.queued).toBe(1);
    expect(metrics.finishedToday).toBe(2);
    expect(metrics.okToday).toBe(1);
    expect(metrics.failToday).toBe(1);
    expect(metrics.costTodayUsd).toBeCloseTo(3.5);
  });
});
