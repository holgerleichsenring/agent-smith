import { describe, expect, it } from "vitest";
import type { RunSnapshot } from "@/types/hub-events";
import { detailLines, elapsedWords, metaParts, spanWords, stateWords } from "../runMetaWords";

// 2026-11-17-6d9ea: the run page's one meta line and its details.

function snap(over: Partial<RunSnapshot> = {}): RunSnapshot {
  return {
    runId: "r1", pipeline: "fix-bug", trigger: "ticket", repos: [], status: "running", prUrl: null, summary: null,
    startedAt: "2026-10-13T06:00:00Z", finishedAt: null, sandboxes: 1, stepIndex: 4, stepName: null, totalSteps: 12,
    lastEventType: null, costUsd: 4.5, llmCalls: 12, ticketId: null, ticketTitle: null, agentName: "openai/gpt",
    cancelRequested: false, ...over,
  };
}

const NOW = Date.parse("2026-10-13T07:18:00Z");
const text = (s: RunSnapshot, steps = 36) => metaParts(s, steps, NOW).map((p) => p.text).join(" · ");

describe("runMetaWords", () => {
  it("metaParts_LiveRun_SaysStepOfTotalAndTimeSoFar", () => {
    expect(text(snap({ budgetCapUsd: 30, workShape: "mixed",
      liveCompute: { pods: [1, 2, 3].map((n) => ({ repo: `r${n}`, image: "i", mem: "1Gi", endReason: null, exitCode: null, oomKilled: null })), totalMem: "3Gi" } })))
      .toBe("running · step 4 of 12 · 1h 18m so far · $4.50 of $30.00 · 3 pods · 3Gi · shape mixed");
  });

  it("metaParts_FinishedRun_SaysStepsReachedAndTheSpan", () => {
    expect(text(snap({ status: "success", finishedAt: "2026-10-13T07:36:00Z" }))).toBe("done · 36 steps · 1h 36m · $4.50");
  });

  it("metaParts_CostNearAndOverTheCap_CarriesTheTone", () => {
    expect(metaParts(snap({ budgetCapUsd: 5 }), 1, NOW).find((p) => p.key === "cost")?.tone).toBe("near");
    expect(metaParts(snap({ budgetCapUsd: 4 }), 1, NOW).find((p) => p.key === "cost")?.tone).toBe("over");
    expect(metaParts(snap({ budgetCapUsd: 50 }), 1, NOW).find((p) => p.key === "cost")?.tone).toBeUndefined();
  });

  it("stateWords_EveryState", () => {
    expect(stateWords(snap({ status: "queued", queuePosition: 2 }))).toEqual({ text: "queued, place 2", tone: "run" });
    expect(stateWords(snap({ status: "waiting_for_input" }))).toEqual({ text: "needs you", tone: "amber" });
    expect(stateWords(snap({ status: "waiting_for_input", queuePosition: 3 }))).toEqual({ text: "resuming, place 3", tone: "run" });
    expect(stateWords(snap({ status: "shortfall" })).tone).toBe("amber");
    expect(stateWords(snap({ status: "failed" }))).toEqual({ text: "failed", tone: "bad" });
    expect(stateWords(snap({ status: "cancelled" })).tone).toBe("quiet");
  });

  it("spanWords_And_elapsedWords", () => {
    expect(spanWords(43)).toBe("43s");
    expect(spanWords(725)).toBe("12m 05s");
    expect(spanWords(0)).toBeNull();
    expect(elapsedWords(snap(), NOW)).toBe("1h 18m so far");
  });

  it("detailLines_SayEverythingTheMetaLineLeavesOut", () => {
    const lines = detailLines(snap({ budgetCapUsd: 5, budgetTier: "medium", reservedGiMinutes: 6.2, workShape: "mixed", workShapeReason: "two kinds of work",
      timeSplit: { modelMs: 360000, sandboxMs: 120000, scaffoldingMs: 60000, throttleMs: 0 } }), ["server", "client"], { commands: 7, lastCommand: "dotnet test" });
    expect(lines).toEqual(expect.arrayContaining([
      "model openai/gpt, 12 model calls", "repositories server, client", "pipeline fix-bug", "run r1",
      "time 6m model, 2m sandbox, 1m scaffolding", "90% of the budget spent, tier medium", "reserved 6.2 Gi·min",
      "shape mixed: two kinds of work", "sandbox 7 commands, last dotnet test",
    ]));
    expect(lines[0]).toMatch(/^started /);
    expect(detailLines(snap({ status: "success" }), [], { commands: 7 })).not.toContain("sandbox 7 commands");
  });
});
