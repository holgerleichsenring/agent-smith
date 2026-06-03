import { renderHook } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { EventType, type RunEvent, type RunSnapshot } from "@/types/hub-events";
import { useRunExecutionTree } from "@/hooks/useRunExecutionTree";

// p0203 (3) — Per-repo aggregation: multiple consecutive step buckets for
// the same multi-repo command collapse to one parent with N/M summary +
// failed-repo names visible without clicking.

const RUN_ID = "r-multi";
const start = "2026-06-01T12:00:00.000Z";

function ts(secOffset: number): string {
  return new Date(new Date(start).getTime() + secOffset * 1000).toISOString();
}

const SNAPSHOT: RunSnapshot = {
  runId: RUN_ID, pipeline: "fix-bug", trigger: "test",
  repos: ["repo-a", "repo-b", "repo-c"],
  status: "running", prUrl: null, summary: null, startedAt: start, finishedAt: null,
  sandboxes: 3, stepIndex: 3, stepName: "AnalyzeCodeCommand (repo-c)", totalSteps: 3,
  lastEventType: "StepStarted", costUsd: 0, llmCalls: 0,
  ticketId: null, ticketTitle: null, agentName: null, cancelRequested: false,
};

function startStep(index: number, repo: string, secOffset: number, display: string): RunEvent {
  return {
    type: EventType.StepStarted, runId: RUN_ID, timestamp: ts(secOffset),
    stepIndex: index, stepName: `AnalyzeCodeCommand (${repo})`, totalSteps: 5,
    displayName: `${display} (${repo})`,
  };
}

function finishStep(index: number, secOffset: number, status: string, reason: string | null): RunEvent {
  return {
    type: EventType.StepFinished, runId: RUN_ID, timestamp: ts(secOffset),
    stepIndex: index, status, durationMs: 1000, reason,
  };
}

describe("Per-repo aggregation (p0203)", () => {
  it("ExecutionTree_MultiRepoStep_ParentRowShowsNMRepoSummary", () => {
    const events: RunEvent[] = [
      startStep(1, "repo-a", 0, "Analyze codebase"),
      finishStep(1, 1, "success", "ok"),
      startStep(2, "repo-b", 1.1, "Analyze codebase"),
      finishStep(2, 2.1, "success", "ok"),
      startStep(3, "repo-c", 2.2, "Analyze codebase"),
      finishStep(3, 3.2, "success", "ok"),
    ];
    const { result } = renderHook(() => useRunExecutionTree(events, SNAPSHOT));
    expect(result.current.nodes).toHaveLength(1);
    const parent = result.current.nodes[0];
    expect(parent.label).toBe("Analyze codebase");
    expect(parent.repoSummary?.text).toBe("3/3 repos");
    expect(parent.repoSummary?.tone).toBe("ok");
    expect(parent.children).toHaveLength(3);
  });

  it("ExecutionTree_MultiRepoStep_FailedRepoNamesVisibleInParentWithoutExpanding", () => {
    const events: RunEvent[] = [
      startStep(1, "repo-a", 0, "Analyze codebase"),
      finishStep(1, 1, "success", "ok"),
      startStep(2, "repo-b", 1.1, "Analyze codebase"),
      finishStep(2, 2.1, "failed", "boom"),
      startStep(3, "repo-c", 2.2, "Analyze codebase"),
      finishStep(3, 3.2, "failed", "kaboom"),
    ];
    const { result } = renderHook(() => useRunExecutionTree(events, SNAPSHOT));
    expect(result.current.nodes).toHaveLength(1);
    const parent = result.current.nodes[0];
    expect(parent.repoSummary?.text).toContain("1/3 ok");
    expect(parent.repoSummary?.text).toContain("2/3 failed");
    expect(parent.repoSummary?.text).toContain("repo-b");
    expect(parent.repoSummary?.text).toContain("repo-c");
    expect(parent.repoSummary?.tone).toBe("fail");
  });

  it("ExecutionTree_MultiRepoStep_CollapsesPerRepoBlocksByDefault", () => {
    const events: RunEvent[] = [
      startStep(1, "repo-a", 0, "Analyze codebase"),
      finishStep(1, 1, "success", "ok"),
      startStep(2, "repo-b", 1.1, "Analyze codebase"),
      finishStep(2, 2.1, "success", "ok"),
    ];
    const { result } = renderHook(() => useRunExecutionTree(events, SNAPSHOT));
    const parent = result.current.nodes[0];
    expect(parent.children).toHaveLength(2);
    // Parent is expandable via children, body is undefined (no separate
    // expand body — clicking the parent surfaces the per-repo children).
    expect(parent.body).toBeUndefined();
    expect(parent.children![0].depth).toBe(1);
  });

  it("ExecutionTree_NonMultiRepoStep_NotCollapsed", () => {
    const events: RunEvent[] = [
      {
        type: EventType.StepStarted, runId: RUN_ID, timestamp: ts(0),
        stepIndex: 1, stepName: "FetchTicketCommand", totalSteps: 1,
        displayName: "Fetch ticket",
      },
      finishStep(1, 1, "success", "ok"),
    ];
    const { result } = renderHook(() => useRunExecutionTree(events, SNAPSHOT));
    expect(result.current.nodes).toHaveLength(1);
    expect(result.current.nodes[0].repoSummary).toBeFalsy();
  });
});
