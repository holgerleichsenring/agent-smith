import { describe, it, expect } from "vitest";
import { deriveCostRollup } from "@/hooks/useCostRollup";
import { EventType, type RunEvent } from "@/types/hub-events";

const NOW = Date.parse("2026-05-27T12:00:00Z");

function llmFinished(ts: string, cost: number): RunEvent {
  return {
    type: EventType.LlmCallFinished,
    runId: "r-1",
    timestamp: ts,
    model: "claude",
    role: "planner",
    tokensIn: 1000,
    tokensOut: 500,
    costUsd: cost,
    durationMs: 1200,
  };
}

describe("deriveCostRollup", () => {
  it("sums LlmCallFinished cost within today (24h)", () => {
    const events: RunEvent[] = [
      llmFinished("2026-05-27T11:00:00Z", 0.05),
      llmFinished("2026-05-27T10:00:00Z", 0.10),
    ];
    const cost = deriveCostRollup(events, NOW);
    expect(cost.today).toBeCloseTo(0.15);
    expect(cost.week).toBeCloseTo(0.15);
    expect(cost.llmCalls).toBe(2);
  });

  it("excludes older-than-7-days events", () => {
    const events: RunEvent[] = [
      llmFinished("2026-05-19T11:00:00Z", 0.01), // 8 days ago
      llmFinished("2026-05-27T11:00:00Z", 0.05),
    ];
    const cost = deriveCostRollup(events, NOW);
    expect(cost.week).toBeCloseTo(0.05);
    expect(cost.llmCalls).toBe(1);
  });

  it("today is a subset of week", () => {
    const events: RunEvent[] = [
      llmFinished("2026-05-25T11:00:00Z", 0.20), // 2 days ago, in week, not today
      llmFinished("2026-05-27T11:00:00Z", 0.05),
    ];
    const cost = deriveCostRollup(events, NOW);
    expect(cost.today).toBeCloseTo(0.05);
    expect(cost.week).toBeCloseTo(0.25);
  });

  it("empty events returns zeros", () => {
    const cost = deriveCostRollup([], NOW);
    expect(cost).toEqual({ today: 0, week: 0, llmCalls: 0 });
  });
});
