import { describe, it, expect } from "vitest";
import { deriveTriggerLog } from "@/hooks/useTriggerLog";
import {
  SystemEventType,
  TicketSkipReason,
  type SystemEvent,
} from "@/types/system-events";

function skipped(ts: string, ticketId: string): SystemEvent {
  return {
    type: SystemEventType.TicketSkipped,
    source: "tracker:jira/foo",
    tracker: "foo",
    ticketId,
    reason: TicketSkipReason.ZeroMatch,
    detail: "no match",
    timestamp: ts,
  };
}

function triggered(ts: string, ticketId: string): SystemEvent {
  return {
    type: SystemEventType.TicketTriggered,
    source: "tracker:jira/foo",
    tracker: "foo",
    ticketId,
    project: "foo",
    pipeline: "fix-bug",
    outcome: "Claimed",
    timestamp: ts,
  };
}

describe("deriveTriggerLog", () => {
  it("returns chronologically newest-first", () => {
    const log = deriveTriggerLog([
      skipped("2026-05-27T10:00:00Z", "T-1"),
      triggered("2026-05-27T12:00:00Z", "T-2"),
      skipped("2026-05-27T11:00:00Z", "T-3"),
    ], 50);
    expect(log.map((r) => r.ticketId)).toEqual(["T-2", "T-3", "T-1"]);
  });

  it("limits to the requested page size (50 by default)", () => {
    const events: SystemEvent[] = [];
    for (let i = 0; i < 100; i++) {
      events.push(skipped(`2026-05-27T12:00:${String(i).padStart(2, "0")}Z`, `T-${i}`));
    }
    expect(deriveTriggerLog(events, 50)).toHaveLength(50);
  });

  it("triggered entries carry project + pipeline + outcome", () => {
    const log = deriveTriggerLog([triggered("2026-05-27T12:00:00Z", "T-1")], 50);
    expect(log[0]).toMatchObject({
      kind: "triggered",
      project: "foo",
      pipeline: "fix-bug",
      outcome: "Claimed",
    });
  });

  it("skipped entries carry reason + detail", () => {
    const log = deriveTriggerLog([skipped("2026-05-27T12:00:00Z", "T-1")], 50);
    expect(log[0]).toMatchObject({
      kind: "skipped",
      reason: TicketSkipReason.ZeroMatch,
      detail: "no match",
    });
  });
});
