import { describe, it, expect } from "vitest";
import {
  SystemEventType,
  type PollCycleFinishedEvent,
  type PollCycleStartedEvent,
  type SystemEvent,
} from "@/types/system-events";
import { deriveSystemStatus } from "@/hooks/useSystemStatus";

const NOW = Date.parse("2026-05-27T12:00:00Z");

function started(source: string, tracker: string, intervalSeconds: number, ts: string): PollCycleStartedEvent {
  return {
    type: SystemEventType.PollCycleStarted,
    source,
    tracker,
    intervalSeconds,
    timestamp: ts,
  };
}

function finished(source: string, tracker: string, ts: string): PollCycleFinishedEvent {
  return {
    type: SystemEventType.PollCycleFinished,
    source,
    tracker,
    ticketsPolled: 0,
    matched: 0,
    spawned: 0,
    statusFiltered: 0,
    zeroMatched: 0,
    durationMs: 100,
    timestamp: ts,
  };
}

describe("deriveSystemStatus", () => {
  it("no events returns empty", () => {
    const rows = deriveSystemStatus([], NOW);
    expect(rows).toEqual([]);
  });

  it("recent finish (within 1.5× interval) returns ok", () => {
    const events: SystemEvent[] = [
      started("tracker:jira/foo", "foo", 60, "2026-05-27T11:59:00Z"),
      finished("tracker:jira/foo", "foo", "2026-05-27T11:59:10Z"),
    ];
    const [row] = deriveSystemStatus(events, NOW);
    expect(row.status).toBe("ok");
    expect(row.tracker).toBe("foo");
  });

  it("overdue by 2× interval returns degraded", () => {
    const events: SystemEvent[] = [
      started("tracker:x/y", "y", 60, "2026-05-27T11:58:00Z"),
      finished("tracker:x/y", "y", "2026-05-27T11:58:10Z"),
    ];
    const [row] = deriveSystemStatus(events, NOW);
    expect(row.status).toBe("degraded");
  });

  it("overdue by >3× interval returns disconnected", () => {
    const events: SystemEvent[] = [
      started("tracker:x/y", "y", 60, "2026-05-27T11:50:00Z"),
      finished("tracker:x/y", "y", "2026-05-27T11:50:10Z"),
    ];
    const [row] = deriveSystemStatus(events, NOW);
    expect(row.status).toBe("disconnected");
  });

  it("multiple sources sorted by source name", () => {
    const events: SystemEvent[] = [
      started("tracker:b", "b", 60, "2026-05-27T11:59:30Z"),
      finished("tracker:b", "b", "2026-05-27T11:59:31Z"),
      started("tracker:a", "a", 60, "2026-05-27T11:59:30Z"),
      finished("tracker:a", "a", "2026-05-27T11:59:31Z"),
    ];
    const rows = deriveSystemStatus(events, NOW);
    expect(rows.map((r) => r.source)).toEqual(["tracker:a", "tracker:b"]);
  });
});
