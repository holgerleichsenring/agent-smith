import { describe, it, expect } from "vitest";
import { deriveRunOutcomes } from "@/lib/runOutcomes";
import { bucketRuns } from "@/components/jobs/mission/missionBuckets";
import type { RunSnapshot } from "@/types/hub-events";

// 2026-08-27-7463: the Overview's run counts come off the SAME bucketing the rail
// counts, so the page and the rail cannot report different numbers. What is new
// is the split of the finished bucket by how those runs ended.

const run = (runId: string, status: string | null): RunSnapshot =>
  ({
    runId,
    pipeline: "fix-bug",
    trigger: "ticket",
    repos: ["server"],
    status,
    startedAt: "2026-08-27T10:00:00Z",
    finishedAt: null,
    costUsd: 0,
    llmCalls: 0,
  }) as unknown as RunSnapshot;

const RUNS = [
  run("a", "running"),
  run("b", "waiting_for_input"),
  run("c", "queued"),
  run("d", "success"),
  run("e", "success"),
  run("f", "failed"),
  run("g", "cancelled"),
  run("h", null),
];

describe("deriveRunOutcomes", () => {
  it("RunOutcomes_TheBuckets_AreTheOnesTheRailCounts", () => {
    const buckets = bucketRuns(RUNS);
    const outcomes = deriveRunOutcomes(RUNS);
    expect(outcomes.needsYou).toBe(buckets.needsYou.length);
    expect(outcomes.running).toBe(buckets.running.length);
    expect(outcomes.queued).toBe(buckets.queued.length);
    expect(outcomes.finished).toBe(buckets.finished.length);
    expect(outcomes.total).toBe(RUNS.length);
  });

  it("RunOutcomes_TheFinishedBucket_IsSplitByHowTheRunsEnded", () => {
    const outcomes = deriveRunOutcomes(RUNS);
    expect(outcomes.succeeded).toBe(2);
    expect(outcomes.failed).toBe(1);
    expect(outcomes.cancelled).toBe(1);
    // The split accounts for the whole bucket and nothing else.
    expect(outcomes.succeeded + outcomes.failed + outcomes.cancelled).toBe(outcomes.finished);
  });

  it("RunOutcomes_ARunWithNoStatus_IsInFlightNotFinished", () => {
    // A snapshot the server answered without a status is a run whose state is
    // not known — counting it as an outcome would invent one.
    const outcomes = deriveRunOutcomes([run("h", null)]);
    expect(outcomes.running).toBe(1);
    expect(outcomes.finished).toBe(0);
  });

  it("RunOutcomes_NoRuns_IsAllZeros", () => {
    const outcomes = deriveRunOutcomes([]);
    expect(outcomes).toEqual({
      total: 0,
      needsYou: 0,
      running: 0,
      queued: 0,
      finished: 0,
      succeeded: 0,
      failed: 0,
      cancelled: 0,
    });
  });
});
