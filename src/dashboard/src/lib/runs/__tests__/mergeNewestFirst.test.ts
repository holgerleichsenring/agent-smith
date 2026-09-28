import { describe, it, expect } from "vitest";
import type { RunSnapshot } from "@/types/hub-events";
import { mergeNewestFirst } from "../mergeNewestFirst";

function snap(runId: string, status: string, startedAt: string): RunSnapshot {
  return {
    runId,
    pipeline: "code",
    trigger: "ticket",
    repos: ["server"],
    status,
    prUrl: null,
    summary: null,
    startedAt,
    finishedAt: status === "running" ? null : startedAt,
    sandboxes: 1,
    stepIndex: 1,
    stepName: null,
    totalSteps: 5,
    lastEventType: null,
    costUsd: 0,
    llmCalls: 0,
    ticketId: null,
    ticketTitle: null,
    agentName: null,
    cancelRequested: false,
  };
}

describe("mergeNewestFirst", () => {
  it("mergeNewestFirst_LiveWinsOnId_NewestFirst", () => {
    const merged = mergeNewestFirst(
      [snap("active-old", "running", "2026-06-03T10:00:00Z")],
      [
        snap("recent-newest", "success", "2026-06-03T12:00:00Z"),
        snap("active-old", "success", "2026-06-03T10:00:00Z"),
        snap("recent-oldest", "failed", "2026-06-03T08:00:00Z"),
      ],
    );

    expect(merged.map((r) => r.runId)).toEqual(["recent-newest", "active-old", "recent-oldest"]);
    expect(merged.find((r) => r.runId === "active-old")?.status).toBe("running");
  });

  it("mergeNewestFirst_NothingEitherSide_Empty", () => {
    expect(mergeNewestFirst([], [])).toEqual([]);
  });
});
