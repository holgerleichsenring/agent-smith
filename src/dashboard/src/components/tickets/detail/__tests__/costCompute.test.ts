import { describe, expect, it } from "vitest";
import { computeTotals, type AttemptMachine } from "../computeTotals";
import { costBuckets, costParts } from "../costScale";
import { attempt, compute, pass, passes, runDetail, segment, spawn } from "./detailFixtures";

// 2026-10-13-9a59s: the cost split and the compute figures — pure, over the server's shapes.

const NOW = Date.parse("2026-09-05T00:00:00Z");
const min = (n: number) => n * 60000;

describe("costBuckets", () => {
  it("CarriedSpecs_AreWrittenAtZero_AndAddNothingToTheBar", () => {
    const read = passes("r-3", { s3: [pass({ costUsd: 1.2 }), pass({ ordinal: 2, passInSpec: 2, costUsd: 0.5 })] }, 0.9);
    const buckets = costBuckets(read, { s1: 1, s2: 2, s3: 3 });
    expect(buckets.map((b) => b.label)).toEqual(["derive", "S3"]);
    expect(buckets.reduce((sum, b) => sum + b.usd, 0)).toBeCloseTo(2.6);
    expect(costParts(buckets, ["S1", "S2"], false)).toBe("derive $0.90 · S3 $1.70 (2 passes) · S1, S2 carried $0");
  });
});

function machine(over: Partial<AttemptMachine> & Pick<AttemptMachine, "attempt">): AttemptMachine {
  return { compute: null, run: null, next: null, ...over };
}

describe("computeTotals", () => {
  it("TheHandbackWait_RunsFromItsAskedAtToTheNextAttemptsCauseAt", () => {
    const first = attempt({ status: "failed", handedBack: true, startedAt: "2026-09-04T09:00:00Z", finishedAt: "2026-09-04T09:13:00Z" });
    const handback = segment({
      startedAt: "2026-09-04T09:00:00Z", endedAt: "2026-09-04T09:13:00Z", endStatus: "waiting_for_input", modelMs: min(5),
      park: { kind: "handback", askedAt: "2026-09-04T09:12:00Z", questionId: null, answeredAt: null, answeredBy: null, resumedAt: null },
    });
    const next = attempt({ number: 2, runId: "r-2", cause: { channel: "ticket_comment", actor: "h", at: "2026-09-04T10:46:00Z" } });
    const totals = computeTotals([machine({ attempt: first, compute: compute("r-1", { spawns: [], segments: [handback] }), next })], NOW);
    expect(totals.waits).toHaveLength(1);
    expect(totals.waits[0]).toMatchObject({ kind: "handback", open: false });
    expect(totals.waitingMs).toBe(min(94));
  });

  it("ASpawnWithNoObservedDispose_CountsToTheEndOfItsSegment_AndTheRunEndFigureIsKeptBesideIt", () => {
    const ended = attempt({ startedAt: "2026-09-04T14:00:00Z", finishedAt: "2026-09-04T15:00:00Z" });
    const record = compute("r-1", {
      segments: [segment({ startedAt: "2026-09-04T14:00:00Z", endedAt: "2026-09-04T14:20:00Z", endStatus: "queued" }),
        segment({ startedAt: "2026-09-04T14:40:00Z", endedAt: "2026-09-04T15:00:00Z" })],
      spawns: [spawn({ spawnedAt: "2026-09-04T14:00:00Z", disposedAt: null, endReason: "unobserved" })],
    });
    const totals = computeTotals([machine({ attempt: ended, compute: record, run: runDetail("r-1") })], NOW);
    expect(totals.podMinutes).toBe(20);
    expect(totals.podMinutesToRunEnd).toBe(60);
    expect(totals.unobserved).toBe(1);
  });

  it("ASegmentEndedQueued_AndAReleasedAtRequeuePod_AddNothingToWaitingOnYou", () => {
    const ended = attempt({ startedAt: "2026-09-04T14:00:00Z", finishedAt: "2026-09-04T15:00:00Z" });
    const record = compute("r-1", {
      segments: [segment({ startedAt: "2026-09-04T14:00:00Z", endedAt: "2026-09-04T14:10:00Z", endStatus: "queued" }),
        segment({ startedAt: "2026-09-04T14:30:00Z", endedAt: "2026-09-04T15:00:00Z" })],
      spawns: [spawn({ spawnedAt: "2026-09-04T14:01:00Z", disposedAt: "2026-09-04T14:10:00Z", endReason: "released-at-requeue" })],
    });
    const totals = computeTotals([machine({ attempt: ended, compute: record })], NOW);
    expect(totals.waitingMs).toBe(0);
    expect(totals.waits).toEqual([]);
    expect(totals.capacityMs).toBe(min(20));
  });

  it("MemoryReserved_LeavesOutLiveAttempts", () => {
    const totals = computeTotals([
      machine({ attempt: attempt(), run: runDetail("r-1", { reservedGiMinutes: 240 }) }),
      machine({ attempt: attempt({ runId: "r-2", status: "running", finishedAt: null }), run: runDetail("r-2", { reservedGiMinutes: 999 }) }),
    ], NOW);
    expect(totals.reservedGiHours).toBe(4);
  });
});

describe("computeTotals, parks", () => {
  it("APark_HoldsNoPods_UnlessAPodOutlivedIt", () => {
    const parkedAttempt = attempt({ startedAt: "2026-09-04T14:00:00Z", finishedAt: "2026-09-04T16:30:00Z" });
    const record = compute("r-1", {
      segments: [
        segment({
          startedAt: "2026-09-04T14:00:00Z", endedAt: "2026-09-04T14:30:00Z", endStatus: "waiting_for_input", modelMs: min(75),
          park: { kind: "question", askedAt: "2026-09-04T14:29:00Z", questionId: "1", answeredAt: "2026-09-04T16:00:00Z", answeredBy: "h", resumedAt: "2026-09-04T16:00:00Z" },
        }),
        segment({ startedAt: "2026-09-04T16:00:00Z", endedAt: "2026-09-04T16:30:00Z", modelMs: min(10) }),
      ],
    });
    const totals = computeTotals([machine({ attempt: parkedAttempt, compute: record })], NOW);
    expect(totals.podsHeldWhileWaiting).toBe(false);
    const held = { ...record, spawns: [spawn({ disposedAt: "2026-09-04T15:00:00Z", endReason: "exited" })] };
    expect(computeTotals([machine({ attempt: parkedAttempt, compute: held })], NOW).podsHeldWhileWaiting).toBe(true);
  });
});
