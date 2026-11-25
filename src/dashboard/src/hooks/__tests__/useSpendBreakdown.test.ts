import { describe, it, expect } from "vitest";
import { deriveSpendBreakdown } from "@/hooks/useSpendBreakdown";
import type { RunSpend, RunSpendSlice } from "@/lib/runSpendApi";

// 2026-08-27-7463: the spend breakdown is a GROUPING of the read the headline sums, so the two agree by construction.
// 2026-11-24-0fbfb: that read is the server's week rollup; these pin the grouping key, the ranking and the sum.

function slice(over: Partial<RunSpendSlice> = {}): RunSpendSlice {
  return { repos: ["server"], pipeline: "fix-bug", costUsd: 1, ...over };
}

function week(slices: RunSpendSlice[]): RunSpend {
  const totalUsd = slices.reduce((t, s) => t + s.costUsd, 0);
  return { from: "", totalUsd, work: { costUsd: totalUsd, runs: slices.length }, system: { costUsd: 0, runs: 0 }, llmCalls: 0, slices };
}

describe("deriveSpendBreakdown", () => {
  it("SpendBreakdown_ASliceOfTheRollup_IsOneSlice", () => {
    const slices = deriveSpendBreakdown(week([slice({ costUsd: 5 })]));
    expect(slices).toHaveLength(1);
    expect(slices[0].work).toBe("server");
    expect(slices[0].pipeline).toBe("fix-bug");
    expect(slices[0].amountUsd).toBe(5);
    expect(slices[0].share).toBe(1);
  });

  it("SpendBreakdown_ADifferentPipelineOnTheSameRepo_IsItsOwnSlice", () => {
    const slices = deriveSpendBreakdown(week([slice({ costUsd: 3 }), slice({ costUsd: 1, pipeline: "add-feature" })]));
    expect(slices.map((s) => s.pipeline)).toEqual(["fix-bug", "add-feature"]);
    expect(slices[0].share).toBeCloseTo(0.75, 10);
  });

  it("SpendBreakdown_AMultiRepoSlice_NamesItsWholeRepoSet", () => {
    const slices = deriveSpendBreakdown(week([slice({ costUsd: 4, repos: ["web", "server"] })]));
    expect(slices).toHaveLength(1);
    expect(slices[0].work).toBe("server + web");
  });

  it("SpendBreakdown_NoRepos_IsBookedAsUnattributed", () => {
    expect(deriveSpendBreakdown(week([slice({ costUsd: 2, repos: [] })]))[0].work).toBe("unattributed");
  });

  it("SpendBreakdown_TheSlices_SumToTheWeeksTotal", () => {
    const read = week([slice({ costUsd: 2.5 }), slice({ costUsd: 1.25, repos: ["web"], pipeline: "add-feature" }), slice({ costUsd: 0.75, pipeline: "series-run", repos: [] })]);
    const summed = deriveSpendBreakdown(read).reduce((t, s) => t + s.amountUsd, 0);
    expect(summed).toBeCloseTo(read.totalUsd, 10);
  });

  it("SpendBreakdown_ASliceThatCostNothing_IsNotASlice", () => {
    const slices = deriveSpendBreakdown(week([slice({ costUsd: 0, repos: ["docs"] }), slice({ costUsd: 1 })]));
    expect(slices.map((s) => s.work)).toEqual(["server"]);
  });

  it("SpendBreakdown_NoReadYet_IsNoSlices", () => {
    expect(deriveSpendBreakdown(null)).toEqual([]);
  });
});
