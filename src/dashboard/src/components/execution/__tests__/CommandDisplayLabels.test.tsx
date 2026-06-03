import { renderHook } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { EventType, type RunEvent, type RunSnapshot } from "@/types/hub-events";
import { useRunExecutionTree } from "@/hooks/useRunExecutionTree";

// p0203 (2) — Step rows render the operator-facing DisplayName when the
// server emits one; fall back to the raw stepName for legacy producers.

const RUN_ID = "r-disp";
const start = "2026-06-01T12:00:00.000Z";

function ts(secOffset: number): string {
  return new Date(new Date(start).getTime() + secOffset * 1000).toISOString();
}

const SNAPSHOT: RunSnapshot = {
  runId: RUN_ID, pipeline: "fix-bug", trigger: "test", repos: ["repo-a"],
  status: "running", prUrl: null, summary: null, startedAt: start, finishedAt: null,
  sandboxes: 0, stepIndex: 1, stepName: "AnalyzeCodeCommand", totalSteps: 2,
  lastEventType: "StepStarted", costUsd: 0, llmCalls: 0,
  ticketId: null, ticketTitle: null, agentName: null, cancelRequested: false,
};

describe("Step display labels (p0203)", () => {
  it("ExecutionNode_StepName_UsesDisplayLabelFromMap", () => {
    const events: RunEvent[] = [
      {
        type: EventType.StepStarted, runId: RUN_ID, timestamp: ts(0),
        stepIndex: 1, stepName: "AnalyzeCodeCommand", totalSteps: 1,
        displayName: "Analyze codebase",
      },
    ];
    const { result } = renderHook(() => useRunExecutionTree(events, SNAPSHOT));
    expect(result.current.nodes[0].label).toBe("Analyze codebase");
  });

  it("ExecutionNode_StepName_FallsBackToCommandName_WhenDisplayLabelMissing", () => {
    const events: RunEvent[] = [
      {
        type: EventType.StepStarted, runId: RUN_ID, timestamp: ts(0),
        stepIndex: 1, stepName: "SomeLegacyCommand", totalSteps: 1,
        displayName: null,
      },
    ];
    const { result } = renderHook(() => useRunExecutionTree(events, SNAPSHOT));
    expect(result.current.nodes[0].label).toBe("SomeLegacyCommand");
  });
});
