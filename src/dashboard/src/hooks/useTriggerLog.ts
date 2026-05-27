import { useMemo } from "react";
import {
  SystemEventType,
  TicketSkipReason,
  type SystemEvent,
  type TicketSkippedEvent,
  type TicketTriggeredEvent,
} from "@/types/system-events";

// p0173d: chronological log of recent trigger decisions — the merged
// stream of TicketSkipped + TicketTriggered events with metadata for
// the dashboard's TriggerLog table. Operator answers "why didn't this
// ticket trigger" without leaving the dashboard.

export type TriggerLogEntry =
  | {
      kind: "skipped";
      timestamp: string;
      source: string;
      tracker: string;
      ticketId: string;
      reason: TicketSkipReason;
      detail: string;
    }
  | {
      kind: "triggered";
      timestamp: string;
      source: string;
      tracker: string;
      ticketId: string;
      project: string;
      pipeline: string;
      outcome: string;
    };

export function useTriggerLog(
  events: readonly SystemEvent[],
  limit = 50,
): TriggerLogEntry[] {
  return useMemo(() => deriveTriggerLog(events, limit), [events, limit]);
}

export function deriveTriggerLog(
  events: readonly SystemEvent[],
  limit: number,
): TriggerLogEntry[] {
  const entries: TriggerLogEntry[] = [];
  for (const e of events) {
    if (e.type === SystemEventType.TicketSkipped) {
      const ev = e as TicketSkippedEvent;
      entries.push({
        kind: "skipped",
        timestamp: ev.timestamp,
        source: ev.source,
        tracker: ev.tracker,
        ticketId: ev.ticketId,
        reason: ev.reason,
        detail: ev.detail,
      });
    } else if (e.type === SystemEventType.TicketTriggered) {
      const ev = e as TicketTriggeredEvent;
      entries.push({
        kind: "triggered",
        timestamp: ev.timestamp,
        source: ev.source,
        tracker: ev.tracker,
        ticketId: ev.ticketId,
        project: ev.project,
        pipeline: ev.pipeline,
        outcome: ev.outcome,
      });
    }
  }
  entries.sort((a, b) => Date.parse(b.timestamp) - Date.parse(a.timestamp));
  return entries.slice(0, limit);
}

export function skipReasonLabel(reason: TicketSkipReason): string {
  switch (reason) {
    case TicketSkipReason.ZeroMatch:
      return "no project matched";
    case TicketSkipReason.StatusFilter:
      return "status filter";
    case TicketSkipReason.ClaimBlocked:
      return "claim blocked";
    case TicketSkipReason.DuplicateInFlight:
      return "duplicate in-flight";
    case TicketSkipReason.NotAnIssue:
      return "PR, not issue";
    default:
      return "skipped";
  }
}
