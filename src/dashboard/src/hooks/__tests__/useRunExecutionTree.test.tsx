import { renderHook } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { EventType, type RunEvent, type RunSnapshot } from "@/types/hub-events";
import { useRunExecutionTree } from "../useRunExecutionTree";

const RUN_ID = "r1";
const start = "2026-05-30T16:00:00.000Z";

function ts(secOffset: number): string {
  return new Date(new Date(start).getTime() + secOffset * 1000).toISOString();
}

const RUN_STARTED: RunEvent = {
  type: EventType.RunStarted,
  runId: RUN_ID,
  timestamp: start,
  trigger: "test",
  pipeline: "fix-bug",
  repos: ["repo-a"],
  startedAt: start,
  agentName: null,
};

const SNAPSHOT: RunSnapshot = {
  runId: RUN_ID,
  pipeline: "fix-bug",
  trigger: "test",
  repos: ["repo-a"],
  status: "running",
  prUrl: null,
  summary: null,
  startedAt: start,
  finishedAt: null,
  sandboxes: 1,
  stepIndex: 1,
  stepName: "FetchTicket",
  totalSteps: 5,
  lastEventType: "StepStarted",
  costUsd: 0,
  llmCalls: 0,
  ticketId: null,
  ticketTitle: null,
  agentName: null,
  cancelRequested: false,
};

describe("useRunExecutionTree", () => {
  it("useRunExecutionTree_EmptyEvents_ReturnsEmptyTree", () => {
    const { result } = renderHook(() => useRunExecutionTree([], null));
    expect(result.current.nodes).toEqual([]);
  });

  it("useRunExecutionTree_ComposesStepsAndSubAgents_FromTypedStream", () => {
    const events: RunEvent[] = [
      RUN_STARTED,
      {
        type: EventType.StepStarted,
        runId: RUN_ID,
        timestamp: ts(0.1),
        stepIndex: 1,
        stepName: "Fetching ticket",
        totalSteps: 5,
      },
      {
        type: EventType.StepFinished,
        runId: RUN_ID,
        timestamp: ts(0.5),
        stepIndex: 1,
        status: "success",
        durationMs: 400,
        reason: null,
      },
      {
        type: EventType.StepStarted,
        runId: RUN_ID,
        timestamp: ts(1),
        stepIndex: 2,
        stepName: "Analyzing codebase",
        totalSteps: 5,
      },
      {
        type: EventType.SubAgentSpawned,
        runId: RUN_ID,
        timestamp: ts(1.2),
        subAgentId: "sa-1",
        name: "Reviewer",
        activity: "review the changes",
        parentSubAgentId: null,
        inheritedContextHash: "h",
      },
      {
        type: EventType.SubAgentFinding,
        runId: RUN_ID,
        timestamp: ts(2),
        subAgentId: "sa-1",
        severity: "high",
        title: "missing input validation",
        detail: "endpoint accepts unbounded payload",
      },
      {
        type: EventType.SubAgentCompleted,
        runId: RUN_ID,
        timestamp: ts(3),
        subAgentId: "sa-1",
        status: "success",
        observationsCount: 0,
        findingsCount: 1,
        filesWrittenCount: 0,
        toolCalls: 0,
        costUsd: 0,
      },
      {
        type: EventType.StepFinished,
        runId: RUN_ID,
        timestamp: ts(3.1),
        stepIndex: 2,
        status: "success",
        durationMs: 2100,
        reason: null,
      },
    ];
    const { result } = renderHook(() => useRunExecutionTree(events, SNAPSHOT));
    const nodes = result.current.nodes;
    expect(nodes.map((n) => n.label)).toEqual([
      "Fetching ticket",
      "Analyzing codebase",
    ]);
    expect(nodes[0].status).toBe("ok");
    expect(nodes[1].status).toBe("ok");
    // Sub-agent nested under step 2 (the active step at spawn time)
    expect(nodes[1].children).toHaveLength(1);
    expect(nodes[1].children![0].label).toBe("sub-agent: Reviewer");
    expect(nodes[1].children![0].depth).toBe(1);
    expect(nodes[1].children![0].status).toBe("ok");
  });

  it("useRunExecutionTree_StepFailureMapsToFailStatus", () => {
    const events: RunEvent[] = [
      RUN_STARTED,
      {
        type: EventType.StepStarted,
        runId: RUN_ID,
        timestamp: ts(0),
        stepIndex: 1,
        stepName: "Awaiting approval",
        totalSteps: 1,
      },
      {
        type: EventType.StepFinished,
        runId: RUN_ID,
        timestamp: ts(0.1),
        stepIndex: 1,
        status: "failed",
        durationMs: 100,
        reason: "Key 'Plan' not found in pipeline context.",
      },
    ];
    const { result } = renderHook(() => useRunExecutionTree(events, SNAPSHOT));
    expect(result.current.nodes[0].status).toBe("fail");
  });

  it("RunViewer_PendingStep_HasNoDuration", () => {
    // Only step 1 runs; steps 2 + 3 are seeded from plannedSteps and never
    // start. A pending (status "wait") step must carry NO duration — not the
    // whole run's elapsed.
    const events: RunEvent[] = [
      { ...RUN_STARTED, plannedSteps: ["Fetch ticket", "Analyze", "Build"] },
      {
        type: EventType.StepStarted,
        runId: RUN_ID,
        timestamp: ts(0),
        stepIndex: 1,
        stepName: "Fetch ticket",
        totalSteps: 3,
        displayName: null,
      },
      {
        type: EventType.StepFinished,
        runId: RUN_ID,
        timestamp: ts(0.4),
        stepIndex: 1,
        status: "success",
        durationMs: 400,
        reason: null,
      },
    ];
    const { result } = renderHook(() => useRunExecutionTree(events, SNAPSHOT));
    const nodes = result.current.nodes;
    const done = nodes.find((n) => n.label === "Fetch ticket")!;
    const pendingA = nodes.find((n) => n.label === "Analyze")!;
    const pendingB = nodes.find((n) => n.label === "Build")!;
    // The finished step keeps a real duration…
    expect(done.status).toBe("ok");
    expect(done.durationLabel).not.toBe("");
    // …the never-run steps show none.
    expect(pendingA.status).toBe("wait");
    expect(pendingA.durationLabel).toBe("");
    expect(pendingA.durationSeconds).toBe(0);
    expect(pendingB.durationLabel).toBe("");
  });

  it("useRunExecutionTree_SandboxEventsAttachToActiveStep_p0189", () => {
    const events: RunEvent[] = [
      RUN_STARTED,
      {
        type: EventType.StepStarted,
        runId: RUN_ID,
        timestamp: ts(0),
        stepIndex: 1,
        stepName: "Test",
        totalSteps: 1,
      },
      {
        type: EventType.SandboxCommand,
        runId: RUN_ID,
        timestamp: ts(0.1),
        repo: "repo-a",
        command: "npx jest",
        argsLength: 80,
        summary: "npx jest --runInBand",
      },
      {
        type: EventType.SandboxResult,
        runId: RUN_ID,
        timestamp: ts(5),
        repo: "repo-a",
        command: "npx jest",
        exitCode: 0,
        durationMs: 4900,
      },
      {
        type: EventType.SandboxCommand,
        runId: RUN_ID,
        timestamp: ts(0.2),
        repo: "repo-b",
        command: "dotnet test",
        argsLength: 40,
        summary: null,
      },
      {
        type: EventType.StepFinished,
        runId: RUN_ID,
        timestamp: ts(5.1),
        stepIndex: 1,
        status: "success",
        durationMs: 5100,
        reason: null,
      },
    ];
    const { result } = renderHook(() => useRunExecutionTree(events, SNAPSHOT, RUN_ID));
    // Body must be non-null when a step issued sandbox commands so the
    // node becomes expandable and surfaces stdout/stderr.
    expect(result.current.nodes[0].body).not.toBeNull();
  });

  it("useRunExecutionTree_TailReflectsLatestStepEvent", () => {
    const events: RunEvent[] = [
      RUN_STARTED,
      {
        type: EventType.StepStarted,
        runId: RUN_ID,
        timestamp: ts(0),
        stepIndex: 1,
        stepName: "Loading context",
        totalSteps: 1,
      },
      {
        type: EventType.L1StepDetail,
        runId: RUN_ID,
        timestamp: ts(0.5),
        stepIndex: 1,
        origin: "loader",
        detail: "merged 6 context.yaml",
      },
    ];
    const { result } = renderHook(() => useRunExecutionTree(events, SNAPSHOT));
    expect(result.current.nodes[0].tail?.text).toBe("merged 6 context.yaml");
  });
});
