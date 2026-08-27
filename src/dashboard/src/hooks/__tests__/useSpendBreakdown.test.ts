import { describe, it, expect } from "vitest";
import { deriveSpendBreakdown } from "@/hooks/useSpendBreakdown";
import { deriveCostRollup } from "@/hooks/useCostRollup";
import type { OverviewSnapshot, RunSnapshot } from "@/types/hub-events";

// 2026-08-27-7463: the spend breakdown is a GROUPING of the run list the headline
// already sums, so the two must agree by construction. These pin the window, the
// grouping key and the sum.

const NOW = Date.parse("2026-08-27T12:00:00Z");
const HOUR_MS = 60 * 60 * 1000;
const DAY_MS = 24 * HOUR_MS;

function run(over: Partial<RunSnapshot> = {}): RunSnapshot {
  return {
    runId: "r",
    pipeline: "fix-bug",
    trigger: "ticket",
    repos: ["server"],
    status: "success",
    prUrl: null,
    summary: null,
    startedAt: new Date(NOW - HOUR_MS).toISOString(),
    finishedAt: new Date(NOW - HOUR_MS).toISOString(),
    sandboxes: 1,
    stepIndex: 1,
    stepName: null,
    totalSteps: 1,
    lastEventType: null,
    costUsd: 1,
    llmCalls: 1,
    ticketId: null,
    ticketTitle: null,
    agentName: null,
    cancelRequested: false,
    ...over,
  };
}

const snapshot = (runs: RunSnapshot[]): OverviewSnapshot => ({
  active: [],
  recent: runs,
  systemActivity: null,
});

describe("deriveSpendBreakdown", () => {
  it("SpendBreakdown_RunsOfTheSameWorkAndPipeline_AreOneSlice", () => {
    const slices = deriveSpendBreakdown(
      snapshot([run({ runId: "a", costUsd: 2 }), run({ runId: "b", costUsd: 3 })]),
      NOW,
    );
    expect(slices).toHaveLength(1);
    expect(slices[0].work).toBe("server");
    expect(slices[0].pipeline).toBe("fix-bug");
    expect(slices[0].amountUsd).toBe(5);
    expect(slices[0].share).toBe(1);
  });

  it("SpendBreakdown_ADifferentPipelineOnTheSameRepo_IsItsOwnSlice", () => {
    const slices = deriveSpendBreakdown(
      snapshot([
        run({ runId: "a", costUsd: 3 }),
        run({ runId: "b", costUsd: 1, pipeline: "add-feature" }),
      ]),
      NOW,
    );
    expect(slices.map((s) => s.pipeline)).toEqual(["fix-bug", "add-feature"]);
    expect(slices[0].share).toBeCloseTo(0.75, 10);
  });

  it("SpendBreakdown_AMultiRepoRun_IsOneSliceUnderItsWholeRepoSet", () => {
    // Booking it to each repo would count one run's money twice, and the
    // breakdown would stop adding up to the headline.
    const slices = deriveSpendBreakdown(
      snapshot([run({ runId: "a", costUsd: 4, repos: ["web", "server"] })]),
      NOW,
    );
    expect(slices).toHaveLength(1);
    expect(slices[0].work).toBe("server + web");
    expect(slices[0].amountUsd).toBe(4);
  });

  it("SpendBreakdown_ARunWithNoRepos_IsBookedAsUnattributed", () => {
    const slices = deriveSpendBreakdown(snapshot([run({ costUsd: 2, repos: null })]), NOW);
    expect(slices[0].work).toBe("unattributed");
  });

  it("SpendBreakdown_TheSlices_SumToTheHeadlineSevenDayFigure", () => {
    const overview = snapshot([
      run({ runId: "a", costUsd: 2.5 }),
      run({ runId: "b", costUsd: 1.25, repos: ["web"], pipeline: "add-feature" }),
      run({
        runId: "c",
        costUsd: 0.75,
        startedAt: new Date(NOW - 3 * DAY_MS).toISOString(),
        finishedAt: new Date(NOW - 3 * DAY_MS).toISOString(),
      }),
      // Outside the window on both sides of the ledger.
      run({
        runId: "old",
        costUsd: 99,
        startedAt: new Date(NOW - 30 * DAY_MS).toISOString(),
        finishedAt: new Date(NOW - 30 * DAY_MS).toISOString(),
      }),
    ]);
    const summed = deriveSpendBreakdown(overview, NOW).reduce((t, s) => t + s.amountUsd, 0);
    expect(summed).toBeCloseTo(deriveCostRollup(overview, NOW).week, 10);
  });

  it("SpendBreakdown_ARunThatCostNothing_IsNotASlice", () => {
    const slices = deriveSpendBreakdown(
      snapshot([run({ runId: "free", costUsd: 0, repos: ["docs"] }), run({ costUsd: 1 })]),
      NOW,
    );
    expect(slices.map((s) => s.work)).toEqual(["server"]);
  });

  it("SpendBreakdown_NoRunListYet_IsNoSlices", () => {
    expect(deriveSpendBreakdown(null, NOW)).toEqual([]);
  });
});
