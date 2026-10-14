import { describe, expect, it } from "vitest";
import { computeSegments } from "../computeSegments";
import { computeTotals, type AttemptMachine } from "../computeTotals";
import { costBuckets, costParts, costScale } from "../costScale";
import { attempt, compute, pass, passes, runDetail, segment, spawn } from "./detailFixtures";

// 2026-10-13-9a59s: the cost scale, the compute tiles and the wall-clock pieces — pure, over the server's shapes.

const NOW = Date.parse("2026-09-05T00:00:00Z");
const min = (n: number) => n * 60000;

describe("costScale", () => {
  it("TheLargestRunCap_IsTheFullWidth_AndASmallerCapIsMarkedOnItsRow", () => {
    const scale = costScale([{ cost: 1.9, cap: 15 }, { cost: 3, cap: 5 }]);
    expect(scale.full).toBe(15);
    expect(scale.capIsFull).toBe(true);
    expect(scale.rows[0]).toEqual({ costPct: (1.9 / 15) * 100, capPct: null, over: false });
    expect(scale.rows[1].capPct).toBeCloseTo((5 / 15) * 100);
    expect(scale.rows[1].over).toBe(false);
  });

  it("WithNoCapOnAnyAttempt_TheLargestCostIsTheFullWidth_AndACostOverItsCapPassesItsMarker", () => {
    const none = costScale([{ cost: 2, cap: null }, { cost: 8, cap: null }]);
    expect(none.full).toBe(8);
    expect(none.largestCap).toBeNull();
    expect(none.rows[1].costPct).toBe(100);
    const over = costScale([{ cost: 12, cap: 10 }, { cost: 3, cap: null }]);
    expect(over.full).toBe(12);
    expect(over.capIsFull).toBe(false);
    expect(over.rows[0].over).toBe(true);
    expect(over.rows[0].costPct).toBeGreaterThan(over.rows[0].capPct!);
  });

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

describe("computeSegments", () => {
  it("AParkSplitsTheWallClock_AndIsDrawnWithoutPods_AndACallTimeLargerThanItsSpanIsAFigureNotAShare", () => {
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
    const clock = computeSegments(parkedAttempt, record, NOW);
    expect(clock.pieces.map((p) => p.kind)).toEqual(["segment", "park", "segment"]);
    expect(clock.pieces.map((p) => Math.round(p.pct))).toEqual([20, 60, 20]);
    expect(clock.pieces.reduce((sum, p) => sum + p.pct, 0)).toBeCloseTo(100);
    expect(clock.figures[0]).toEqual({ launch: 1, wallMs: min(30), callMs: min(75) });
    const totals = computeTotals([machine({ attempt: parkedAttempt, compute: record })], NOW);
    expect(totals.podsHeldWhileWaiting).toBe(false);
    const held = { ...record, spawns: [spawn({ disposedAt: "2026-09-04T15:00:00Z", endReason: "exited" })] };
    expect(computeTotals([machine({ attempt: parkedAttempt, compute: held })], NOW).podsHeldWhileWaiting).toBe(true);
  });
});
