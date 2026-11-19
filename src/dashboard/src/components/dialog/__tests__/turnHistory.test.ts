import { describe, expect, it } from "vitest";
import { historyGap, historyOf, mergedSteps } from "../turnSteps";
import type { SpecDialogActivityPush, SpecDialogActivityStep } from "@/types/spec-dialog";

// 2026-11-19-687ec: a page that opens a running turn after more than fifty steps is served the server's in-memory
// tail only; the rest is read back from the turn's run.

const turn = "2026-11-19T10:00:00Z";

function push(seq: number): SpecDialogActivityPush {
  return { dialogId: "d-1", kind: "tool", name: "read_file", detail: `f${seq}`, at: turn, seq, turnStartedAt: turn };
}

function kept(seq: number, turnStartedAt = "2026-11-19T10:00:00.0000000+00:00"): SpecDialogActivityStep {
  return { kind: "tool", name: "read_file", detail: `f${seq}`, seq, timestamp: turn, turnStartedAt };
}

const range = (from: number, to: number) => Array.from({ length: to - from + 1 }, (_, i) => from + i);

describe("historyGap", () => {
  it("is false when nothing is held or the steps run from 1 without a gap", () => {
    expect(historyGap([], 0)).toBe(false);
    expect(historyGap(range(1, 40).map(push), 0)).toBe(false);
  });

  it("is true for a late reader's tail and for a reconnect's gap", () => {
    expect(historyGap(range(31, 80).map(push), 0)).toBe(true);
    expect(historyGap([...range(1, 20), ...range(90, 140)].map(push), 0)).toBe(true);
  });

  it("is false when the only gap lies at or below what history already returned", () => {
    expect(historyGap(range(2, 80).map(push), 80)).toBe(false);
  });
});

describe("historyOf", () => {
  it("keeps this turn's steps under the held turn string and drops another turn's", () => {
    const steps = historyOf([kept(1), kept(2, "2026-11-19T09:00:00Z")], "d-1", turn);

    expect(steps).toEqual([push(1)]);
  });

  it("a reader arriving after eighty steps sees the first step once the history is merged", () => {
    const tail = range(31, 80).map(push);

    const merged = mergedSteps(tail, historyOf(range(1, 80).map((seq) => kept(seq)), "d-1", turn));

    expect(merged[0].seq).toBe(1);
    expect(merged).toHaveLength(80);
  });
});
