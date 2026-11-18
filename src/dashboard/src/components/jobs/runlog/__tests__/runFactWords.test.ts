import { describe, expect, it } from "vitest";
import type { RunSnapshot } from "@/types/hub-events";
import { budgetShare, computeOf, timeSplitWords } from "../runFactWords";

// 2026-11-17-d6fbb: the graph view's side-rail facts, said in words for the run log.

function snap(over: Partial<RunSnapshot> = {}): RunSnapshot {
  return {
    runId: "r1", pipeline: "fix-bug", trigger: "ticket", repos: [], status: "running", prUrl: null, summary: null,
    startedAt: "2026-10-13T06:05:27Z", finishedAt: null, sandboxes: 1, stepIndex: 4, stepName: null, totalSteps: 12,
    lastEventType: null, costUsd: 4.5, llmCalls: 0, ticketId: null, ticketTitle: null, agentName: null,
    cancelRequested: false, ...over,
  };
}

describe("runFactWords", () => {
  it("budgetShare_NearAndOverTheCap_ChangeTone", () => {
    expect(budgetShare(snap({ budgetCapUsd: 5 }))).toEqual({ percent: 90, tone: "near" });
    expect(budgetShare(snap({ budgetCapUsd: 4 }))).toEqual({ percent: 100, tone: "over" });
    expect(budgetShare(snap({ budgetCapUsd: 50 }))).toEqual({ percent: 9, tone: "ok" });
    expect(budgetShare(snap())).toBeNull();
  });

  it("computeOf_LivePodsWin_ElseTheReservationWhileRunning_ElseNothing", () => {
    const live = computeOf(snap({ liveCompute: { pods: [{ repo: "api", image: "node:20", mem: "2Gi", endReason: null, exitCode: null, oomKilled: null }], totalMem: "2Gi" } }));
    expect(live?.words).toBe("1 pod, 2Gi");
    expect(live?.pods).toEqual([{ name: "api", image: "node:20", resources: "2Gi" }]);
    const footprint = { pods: [{ repo: "api", contexts: [], image: "node:20", cpuLimit: "1", memLimit: "2Gi" }],
      totalCpuLimit: "1", totalMemLimit: "2Gi", dropped: [], reason: "", reserved: true };
    expect(computeOf(snap({ footprint }))?.words).toBe("compute calculating");
    expect(computeOf(snap({ footprint, status: "success" }))).toBeNull();
    expect(computeOf(snap())).toBeNull();
  });

  it("timeSplitWords_FourWaySplit_OrTheModelOnlySplit", () => {
    expect(timeSplitWords(snap({ timeSplit: { modelMs: 360000, sandboxMs: 120000, scaffoldingMs: 60000, throttleMs: 30000 } })))
      .toBe("6m model, 2m sandbox, 1m scaffolding, 30s throttled");
    expect(timeSplitWords(snap({ llmDurationMs: 45000 }))).toBe("45s model");
    expect(timeSplitWords(snap())).toBeNull();
  });

});
