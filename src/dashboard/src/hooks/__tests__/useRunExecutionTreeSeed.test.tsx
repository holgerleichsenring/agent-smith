import { renderHook } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { EventType, type RunEvent } from "@/types/hub-events";
import { useRunExecutionTree } from "../useRunExecutionTree";

// p0275: the step rail is seeded from RunStarted.plannedSteps (the pipeline's
// known step labels) so early steps stay visible even when their StepStarted
// event has been evicted from the 2000-event run buffer.
const RUN_ID = "r-seed";
const start = "2026-06-19T10:00:00.000Z";

describe("useRunExecutionTree planned-step seeding", () => {
  it("EarlySteps_SurviveWhenTheirStepStartedWasEvicted", () => {
    // Only the LAST step's events are present; the first two were evicted.
    const events: RunEvent[] = [
      {
        type: EventType.RunStarted, runId: RUN_ID, timestamp: start,
        trigger: "t", pipeline: "fix-bug", repos: ["r"], startedAt: start,
        agentName: null,
        plannedSteps: ["Load catalog", "Fetch ticket", "Check out source"],
      },
      {
        type: EventType.StepStarted, runId: RUN_ID, timestamp: start,
        stepIndex: 3, stepName: "Check out source", totalSteps: 3,
      },
      {
        type: EventType.StepFinished, runId: RUN_ID, timestamp: start,
        stepIndex: 3, status: "success", durationMs: 100, reason: null,
      },
    ];

    const { result } = renderHook(() => useRunExecutionTree(events, null));
    const nodes = result.current.nodes;

    expect(nodes.map((n) => n.label)).toEqual([
      "Load catalog", "Fetch ticket", "Check out source",
    ]);
    expect(nodes[0].status).toBe("wait"); // seeded, never started → pending
    expect(nodes[2].status).toBe("ok"); // real finished event attached to the seed
  });

  it("NoPlannedSteps_FallsBackToEventOnly", () => {
    const events: RunEvent[] = [
      {
        type: EventType.RunStarted, runId: RUN_ID, timestamp: start,
        trigger: "t", pipeline: "fix-bug", repos: ["r"], startedAt: start,
        agentName: null,
      },
      {
        type: EventType.StepStarted, runId: RUN_ID, timestamp: start,
        stepIndex: 1, stepName: "Only step", totalSteps: 1,
      },
    ];

    const { result } = renderHook(() => useRunExecutionTree(events, null));

    expect(result.current.nodes.map((n) => n.label)).toEqual(["Only step"]);
  });
});
