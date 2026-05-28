"use client";

import { useMemo } from "react";
import { EventType, type RunEvent } from "@/types/hub-events";
import { useRunEvents } from "./useRunEvents";

export type SubAgentEventKind =
  | "spawned"
  | "observation"
  | "finding"
  | "file_written"
  | "tool_call"
  | "completed";

// p0173f: filters the existing useRunEvents stream by SubAgentId
// client-side — no parallel WebSocket subscription per child. Returns
// the subset of L2SubAgent events (optionally narrowed by kind) in
// arrival order.
export function useSubAgentObservations(
  runId: string,
  subAgentId: string | null,
  kinds?: ReadonlySet<SubAgentEventKind>,
): RunEvent[] {
  const events = useRunEvents(runId);
  return useMemo(() => {
    if (!subAgentId) return [];
    return events.filter((e) => matchesSubAgent(e, subAgentId) && (!kinds || kinds.has(kindOf(e)!)));
  }, [events, subAgentId, kinds]);
}

// p0173f: testable seam — filter a caller-provided event array by the
// same rules. The hook composes this on top of useRunEvents.
export function filterSubAgentObservations(
  events: ReadonlyArray<RunEvent>,
  subAgentId: string | null,
  kinds?: ReadonlySet<SubAgentEventKind>,
): RunEvent[] {
  if (!subAgentId) return [];
  return events.filter((e) => matchesSubAgent(e, subAgentId) && (!kinds || kinds.has(kindOf(e)!)));
}

function matchesSubAgent(event: RunEvent, subAgentId: string): boolean {
  switch (event.type) {
    case EventType.SubAgentSpawned:
    case EventType.SubAgentObservation:
    case EventType.SubAgentFinding:
    case EventType.SubAgentFileWritten:
    case EventType.SubAgentToolCall:
    case EventType.SubAgentCompleted:
      // All L2SubAgent records carry subAgentId — narrowed by the switch above.
      return (event as { subAgentId: string }).subAgentId === subAgentId;
    default:
      return false;
  }
}

function kindOf(event: RunEvent): SubAgentEventKind | null {
  switch (event.type) {
    case EventType.SubAgentSpawned: return "spawned";
    case EventType.SubAgentObservation: return "observation";
    case EventType.SubAgentFinding: return "finding";
    case EventType.SubAgentFileWritten: return "file_written";
    case EventType.SubAgentToolCall: return "tool_call";
    case EventType.SubAgentCompleted: return "completed";
    default: return null;
  }
}
