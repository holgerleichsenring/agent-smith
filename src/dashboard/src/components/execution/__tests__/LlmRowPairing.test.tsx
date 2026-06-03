import { renderHook } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { EventType, type RunEvent, type RunSnapshot } from "@/types/hub-events";
import { useRunExecutionTree } from "@/hooks/useRunExecutionTree";
import { pairLlmCalls } from "@/hooks/execution-tree/llmPairing";

// p0203 (4) — LLM call pairing happens in the hook, role "unknown" gets a
// marker (rendering-only; producer fix is p0203a), per-step cost rollup.

const RUN_ID = "r-llm";
const start = "2026-06-01T13:00:00.000Z";
function ts(s: number) { return new Date(new Date(start).getTime() + s * 1000).toISOString(); }

const SNAPSHOT: RunSnapshot = {
  runId: RUN_ID, pipeline: "fix-bug", trigger: "test", repos: ["r"],
  status: "running", prUrl: null, summary: null, startedAt: start, finishedAt: null,
  sandboxes: 0, stepIndex: 1, stepName: "GenerateGoodCode", totalSteps: 1,
  lastEventType: "StepStarted", costUsd: 0, llmCalls: 0,
  ticketId: null, ticketTitle: null, agentName: null, cancelRequested: false,
};

describe("LLM row pairing (p0203)", () => {
  it("ExecutionTree_LlmStartedFinished_PairedIntoSingleRowWithRoleModelDurationTokensCost", () => {
    const events: RunEvent[] = [
      { type: EventType.LlmCallStarted, runId: RUN_ID, timestamp: ts(0),
        model: "claude-opus-4-7", role: "architect", promptHash: "h",
        phase: "Plan", repoName: null },
      { type: EventType.LlmCallFinished, runId: RUN_ID, timestamp: ts(1),
        model: "claude-opus-4-7", role: "architect",
        tokensIn: 100, tokensOut: 200, costUsd: 0.0123, durationMs: 1000,
        phase: "Plan", repoName: null },
    ];
    const result = pairLlmCalls(events);
    expect(result.pairs).toHaveLength(1);
    const pair = result.pairs[0];
    expect(pair.role).toBe("architect");
    expect(pair.roleIsUnknown).toBe(false);
    expect(pair.model).toBe("claude-opus-4-7");
    expect(pair.durationMs).toBe(1000);
    expect(pair.tokensIn).toBe(100);
    expect(pair.tokensOut).toBe(200);
    expect(pair.costUsd).toBe(0.0123);
    expect(result.totalCostUsd).toBe(0.0123);
  });

  it("ExecutionTree_LlmCachedRead_RendersCacheBadge", () => {
    const events: RunEvent[] = [
      { type: EventType.LlmCallStarted, runId: RUN_ID, timestamp: ts(0),
        model: "claude-sonnet-4-6", role: "reviewer", promptHash: "h",
        phase: null, repoName: null },
      { type: EventType.LlmCallFinished, runId: RUN_ID, timestamp: ts(0.5),
        model: "claude-sonnet-4-6", role: "reviewer",
        tokensIn: 50, tokensOut: 0, costUsd: 0, durationMs: 200,
        phase: null, repoName: null },
    ];
    const { pairs } = pairLlmCalls(events);
    expect(pairs[0].cacheHit).toBe(true);
  });

  it("ExecutionTree_LlmRoleUnknown_RendersAsUnknownWithMarker", () => {
    const events: RunEvent[] = [
      { type: EventType.LlmCallStarted, runId: RUN_ID, timestamp: ts(0),
        model: "claude-opus-4-7", role: "unknown", promptHash: "h",
        phase: null, repoName: null },
      { type: EventType.LlmCallFinished, runId: RUN_ID, timestamp: ts(1),
        model: "claude-opus-4-7", role: "unknown",
        tokensIn: 10, tokensOut: 20, costUsd: 0.01, durationMs: 1000,
        phase: null, repoName: null },
    ];
    const { pairs } = pairLlmCalls(events);
    expect(pairs).toHaveLength(1);
    expect(pairs[0].role).toBe("unknown");
    expect(pairs[0].roleIsUnknown).toBe(true);
  });

  it("ExecutionTree_LlmStartWithoutFinish_RoundTripsAsInFlightSingleton", () => {
    const events: RunEvent[] = [
      { type: EventType.LlmCallStarted, runId: RUN_ID, timestamp: ts(0),
        model: "claude-opus-4-7", role: "architect", promptHash: "h",
        phase: null, repoName: null },
    ];
    const { pairs } = pairLlmCalls(events);
    expect(pairs).toHaveLength(1);
    expect(pairs[0].finishedAt).toBeNull();
    expect(pairs[0].durationMs).toBeNull();
  });

  it("ExecutionTree_PerStepCostRollup_AppearsAsCostBadgeOnParent", () => {
    const events: RunEvent[] = [
      { type: EventType.StepStarted, runId: RUN_ID, timestamp: ts(0),
        stepIndex: 1, stepName: "GenerateGoodCode", totalSteps: 1, displayName: null },
      { type: EventType.LlmCallStarted, runId: RUN_ID, timestamp: ts(0.1),
        model: "claude-opus-4-7", role: "architect", promptHash: "h",
        phase: null, repoName: null },
      { type: EventType.LlmCallFinished, runId: RUN_ID, timestamp: ts(1),
        model: "claude-opus-4-7", role: "architect",
        tokensIn: 100, tokensOut: 200, costUsd: 0.05, durationMs: 900,
        phase: null, repoName: null },
      { type: EventType.LlmCallStarted, runId: RUN_ID, timestamp: ts(1.1),
        model: "claude-opus-4-7", role: "architect", promptHash: "h2",
        phase: null, repoName: null },
      { type: EventType.LlmCallFinished, runId: RUN_ID, timestamp: ts(2),
        model: "claude-opus-4-7", role: "architect",
        tokensIn: 50, tokensOut: 80, costUsd: 0.0075, durationMs: 900,
        phase: null, repoName: null },
      { type: EventType.StepFinished, runId: RUN_ID, timestamp: ts(2.1),
        stepIndex: 1, status: "success", durationMs: 2100, reason: "done" },
    ];
    const { result } = renderHook(() => useRunExecutionTree(events, SNAPSHOT));
    const node = result.current.nodes[0];
    expect(node.costBadge).toContain("$0.0575");
    expect(node.costBadge).toContain("2 LLM");
  });
});
