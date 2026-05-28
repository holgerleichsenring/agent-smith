import { describe, it, expect } from "vitest";
import {
  filterSubAgentObservations,
  type SubAgentEventKind,
} from "../useSubAgentObservations";
import { EventType, type RunEvent } from "@/types/hub-events";

function obs(subAgentId: string, text: string): RunEvent {
  return {
    type: EventType.SubAgentObservation,
    runId: "r-1",
    timestamp: "2026-05-20T10:00:00Z",
    subAgentId,
    text,
  } as RunEvent;
}

function finding(subAgentId: string, title: string): RunEvent {
  return {
    type: EventType.SubAgentFinding,
    runId: "r-1",
    timestamp: "2026-05-20T10:00:01Z",
    subAgentId,
    severity: "high",
    title,
    detail: "detail",
  } as RunEvent;
}

function unrelated(): RunEvent {
  return {
    type: EventType.RunStarted,
    runId: "r-1",
    timestamp: "2026-05-20T10:00:00Z",
    trigger: "github",
    pipeline: "fix-bug",
    repos: [],
    startedAt: "2026-05-20T10:00:00Z",
  } as RunEvent;
}

describe("useSubAgentObservations (filter helper)", () => {
  it("useSubAgentObservations_FiltersStreamBySubAgentIdClientSide", () => {
    const events = [obs("sa-1", "alpha"), obs("sa-2", "beta"), unrelated()];
    const filtered = filterSubAgentObservations(events, "sa-1");
    expect(filtered).toHaveLength(1);
    expect(filtered[0]).toHaveProperty("text", "alpha");
  });

  it("useSubAgentObservations_PagesByKindFilter", () => {
    const events = [obs("sa-1", "alpha"), finding("sa-1", "the bug")];
    const kinds = new Set<SubAgentEventKind>(["finding"]);
    const filtered = filterSubAgentObservations(events, "sa-1", kinds);
    expect(filtered).toHaveLength(1);
    expect(filtered[0]).toHaveProperty("title", "the bug");
  });

  it("returns empty when subAgentId is null", () => {
    const events = [obs("sa-1", "alpha")];
    expect(filterSubAgentObservations(events, null)).toEqual([]);
  });
});
