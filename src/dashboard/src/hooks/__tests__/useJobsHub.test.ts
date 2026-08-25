import { describe, it, expect } from "vitest";
import { isPreSpawnZombie, applySnapshotFilters } from "../useJobsHub";
import type { RunSnapshot } from "@/types/hub-events";

// 2026-08-25-39ab: the zombie filter runs over EVERY snapshot on the home
// screen before anything is drawn, so a payload it cannot read is a blank page
// rather than a bad row. It used to reach straight through `repos.length` and
// `status.toLowerCase()`.

function snap(over: Partial<RunSnapshot> = {}): RunSnapshot {
  return {
    runId: "r1",
    pipeline: "fix-bug",
    trigger: "ticket",
    repos: [],
    status: "running",
    prUrl: null,
    summary: null,
    startedAt: "2026-08-25T10:00:00Z",
    finishedAt: null,
    sandboxes: 0,
    stepIndex: 0,
    stepName: null,
    totalSteps: 0,
    lastEventType: null,
    costUsd: 0,
    llmCalls: 0,
    ticketId: null,
    ticketTitle: null,
    agentName: null,
    cancelRequested: false,
    ...over,
  };
}

function without(fields: string[], over: Partial<RunSnapshot> = {}): RunSnapshot {
  const partial: Record<string, unknown> = { ...snap(over) };
  for (const field of fields) delete partial[field];
  return partial as RunSnapshot;
}

describe("isPreSpawnZombie", () => {
  it("isPreSpawnZombie_ARunningRunWithNothingYet_IsAZombie", () => {
    expect(isPreSpawnZombie(snap())).toBe(true);
  });

  it("isPreSpawnZombie_ARunWithRepos_IsNot", () => {
    expect(isPreSpawnZombie(snap({ repos: ["server"] }))).toBe(false);
  });

  it("Snapshot_MissingARequiredField_RendersWithoutThrowing", () => {
    // No repos field and no status field: read as absent, never dereferenced.
    expect(() => isPreSpawnZombie(without(["repos", "status"]))).not.toThrow();
    expect(isPreSpawnZombie(without(["status"]))).toBe(false);
    expect(isPreSpawnZombie(without(["repos"]))).toBe(true);
  });
});

describe("applySnapshotFilters", () => {
  it("applySnapshotFilters_ASnapshotWithoutItsFields_StillFilters", () => {
    const filtered = applySnapshotFilters(
      {
        active: [without(["repos", "status"], { runId: "a" }), snap({ runId: "b", repos: ["x"] })],
        recent: [],
        systemActivity: null,
      },
      false,
    );

    expect(filtered.active.map((r) => r.runId)).toEqual(["a", "b"]);
  });
});
