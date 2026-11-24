import { describe, expect, it } from "vitest";
import type { RunSnapshot } from "@/types/hub-events";
import { splitSystemRuns } from "../systemRuns";

// 2026-11-24-35ff: the runs the server starts itself are hidden unless asked, and counted either way.

function run(runId: string, status: string, system?: boolean): RunSnapshot {
  return {
    runId, pipeline: system ? "series-run" : "code", trigger: "ticket", repos: [], status,
    prUrl: null, summary: null, startedAt: "2026-11-24T10:00:00Z", finishedAt: null,
    sandboxes: 0, stepIndex: 0, stepName: null, totalSteps: 0, lastEventType: null,
    costUsd: 0, llmCalls: 0, ticketId: null, ticketTitle: null, agentName: null, cancelRequested: false, system,
  };
}

describe("splitSystemRuns", () => {
  const runs = [run("w", "running"), run("s1", "success", true), run("s2", "failed", true), run("old", "failed")];

  it("Hidden_ShowsOnlyWorkRuns_AndCountsTheSystemRunsAndTheirFailures", () => {
    const split = splitSystemRuns(runs, false);
    expect(split.shown.map((r) => r.runId)).toEqual(["w", "old"]);
    expect(split.system).toBe(2);
    expect(split.systemFailed).toBe(1);
  });

  it("Shown_KeepsEveryRunInOrder", () => {
    expect(splitSystemRuns(runs, true).shown.map((r) => r.runId)).toEqual(["w", "s1", "s2", "old"]);
  });

  it("ARunWithoutTheFlag_IsWork", () => {
    const split = splitSystemRuns([run("legacy", "running")], false);
    expect(split.shown).toHaveLength(1);
    expect(split.system).toBe(0);
  });
});
