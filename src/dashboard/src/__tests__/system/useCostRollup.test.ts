import { describe, it, expect } from "vitest";
import { deriveCostRollup } from "@/hooks/useCostRollup";
import type { OverviewSnapshot, RunSnapshot } from "@/types/hub-events";

const NOW = Date.parse("2026-05-27T12:00:00Z");

function snapshot(
  runId: string,
  startedAt: string,
  finishedAt: string | null,
  costUsd: number,
  llmCalls: number,
  status: "running" | "success" | "failed" = "success",
): RunSnapshot {
  return {
    runId,
    pipeline: "pipeline",
    trigger: "trigger",
    repos: [],
    status: finishedAt ? status : "running",
    prUrl: null,
    summary: null,
    startedAt,
    finishedAt,
    sandboxes: 0,
    stepIndex: 0,
    stepName: null,
    totalSteps: 0,
    lastEventType: null,
    costUsd,
    llmCalls,
  };
}

function overview(active: RunSnapshot[], recent: RunSnapshot[]): OverviewSnapshot {
  return { active, recent, systemActivity: null };
}

describe("deriveCostRollup", () => {
  it("sums costUsd from snapshots finished within today (24h)", () => {
    const ov = overview([], [
      snapshot("r1", "2026-05-27T10:00:00Z", "2026-05-27T10:30:00Z", 0.10, 1),
      snapshot("r2", "2026-05-27T09:00:00Z", "2026-05-27T11:00:00Z", 0.05, 1),
    ]);
    const cost = deriveCostRollup(ov, NOW);
    expect(cost.today).toBeCloseTo(0.15);
    expect(cost.week).toBeCloseTo(0.15);
    expect(cost.llmCalls).toBe(2);
  });

  it("excludes runs finished more than 7 days ago", () => {
    const ov = overview([], [
      snapshot("r1", "2026-05-18T11:00:00Z", "2026-05-19T11:00:00Z", 0.01, 1),
      snapshot("r2", "2026-05-27T10:00:00Z", "2026-05-27T11:00:00Z", 0.05, 2),
    ]);
    const cost = deriveCostRollup(ov, NOW);
    expect(cost.week).toBeCloseTo(0.05);
    expect(cost.llmCalls).toBe(2);
  });

  it("today is a subset of week", () => {
    const ov = overview([], [
      snapshot("r1", "2026-05-25T10:00:00Z", "2026-05-25T11:00:00Z", 0.20, 3),
      snapshot("r2", "2026-05-27T10:00:00Z", "2026-05-27T11:00:00Z", 0.05, 1),
    ]);
    const cost = deriveCostRollup(ov, NOW);
    expect(cost.today).toBeCloseTo(0.05);
    expect(cost.week).toBeCloseTo(0.25);
    expect(cost.llmCalls).toBe(4);
  });

  it("active in-flight runs are timed from startedAt", () => {
    const ov = overview(
      [snapshot("r-active", "2026-05-27T11:30:00Z", null, 0.07, 2, "running")],
      [],
    );
    const cost = deriveCostRollup(ov, NOW);
    expect(cost.today).toBeCloseTo(0.07);
    expect(cost.llmCalls).toBe(2);
  });

  it("null overview returns zeros", () => {
    const cost = deriveCostRollup(null, NOW);
    expect(cost).toEqual({ today: 0, week: 0, llmCalls: 0 });
  });
});
