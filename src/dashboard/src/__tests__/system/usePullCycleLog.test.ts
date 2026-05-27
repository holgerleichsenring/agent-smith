import { describe, it, expect } from "vitest";
import { derivePullCycleLog } from "@/hooks/usePullCycleLog";
import {
  SystemEventType,
  TicketSkipReason,
  type SystemEvent,
} from "@/types/system-events";

function started(source: string, tracker: string, ts: string, intervalSeconds = 60): SystemEvent {
  return {
    type: SystemEventType.PollCycleStarted,
    source,
    tracker,
    intervalSeconds,
    timestamp: ts,
  };
}

function finished(source: string, tracker: string, ts: string, ticketsPolled = 0): SystemEvent {
  return {
    type: SystemEventType.PollCycleFinished,
    source,
    tracker,
    ticketsPolled,
    matched: 0,
    spawned: 0,
    statusFiltered: 0,
    zeroMatched: 0,
    durationMs: 100,
    timestamp: ts,
  };
}

function scanned(source: string, tracker: string, ticketId: string, ts: string): SystemEvent {
  return {
    type: SystemEventType.TicketScanned,
    source,
    tracker,
    ticketId,
    labels: [],
    timestamp: ts,
  };
}

function skipped(
  source: string,
  tracker: string,
  ticketId: string,
  reason: TicketSkipReason,
  ts: string,
): SystemEvent {
  return {
    type: SystemEventType.TicketSkipped,
    source,
    tracker,
    ticketId,
    reason,
    detail: "test",
    timestamp: ts,
  };
}

function triggered(source: string, tracker: string, ticketId: string, ts: string): SystemEvent {
  return {
    type: SystemEventType.TicketTriggered,
    source,
    tracker,
    ticketId,
    project: "p",
    pipeline: "fix-bug",
    outcome: "Claimed",
    timestamp: ts,
  };
}

describe("derivePullCycleLog", () => {
  it("pairs a started + finished into one cycle", () => {
    const cycles = derivePullCycleLog(
      [
        started("tracker:a", "a", "2026-05-27T12:00:00Z"),
        finished("tracker:a", "a", "2026-05-27T12:00:05Z", 3),
      ],
      50,
    );
    expect(cycles).toHaveLength(1);
    expect(cycles[0].source).toBe("tracker:a");
    expect(cycles[0].durationMs).toBe(5000);
    expect(cycles[0].ticketsPolled).toBe(3);
  });

  it("in-flight cycle (no Finished yet) has null finishedAt", () => {
    const cycles = derivePullCycleLog(
      [started("tracker:a", "a", "2026-05-27T12:00:00Z")],
      50,
    );
    expect(cycles).toHaveLength(1);
    expect(cycles[0].finishedAt).toBeNull();
    expect(cycles[0].durationMs).toBeNull();
  });

  it("two concurrent sources pair correctly by source", () => {
    const cycles = derivePullCycleLog(
      [
        started("tracker:a", "a", "2026-05-27T12:00:00Z"),
        started("tracker:b", "b", "2026-05-27T12:00:01Z"),
        finished("tracker:b", "b", "2026-05-27T12:00:03Z"),
        finished("tracker:a", "a", "2026-05-27T12:00:05Z"),
      ],
      50,
    );
    expect(cycles).toHaveLength(2);
    const a = cycles.find((c) => c.source === "tracker:a")!;
    const b = cycles.find((c) => c.source === "tracker:b")!;
    expect(a.durationMs).toBe(5000);
    expect(b.durationMs).toBe(2000);
  });

  it("groups skipped events into a reason histogram", () => {
    const cycles = derivePullCycleLog(
      [
        started("tracker:a", "a", "2026-05-27T12:00:00Z"),
        skipped("tracker:a", "a", "T-1", TicketSkipReason.ZeroMatch, "2026-05-27T12:00:01Z"),
        skipped("tracker:a", "a", "T-2", TicketSkipReason.ZeroMatch, "2026-05-27T12:00:02Z"),
        skipped("tracker:a", "a", "T-3", TicketSkipReason.StatusFilter, "2026-05-27T12:00:03Z"),
        finished("tracker:a", "a", "2026-05-27T12:00:04Z", 3),
      ],
      50,
    );
    expect(cycles[0].skippedTotal).toBe(3);
    expect(cycles[0].skippedByReason[TicketSkipReason.ZeroMatch]).toBe(2);
    expect(cycles[0].skippedByReason[TicketSkipReason.StatusFilter]).toBe(1);
  });

  it("counts triggered events into the cycle", () => {
    const cycles = derivePullCycleLog(
      [
        started("tracker:a", "a", "2026-05-27T12:00:00Z"),
        triggered("tracker:a", "a", "T-1", "2026-05-27T12:00:01Z"),
        triggered("tracker:a", "a", "T-2", "2026-05-27T12:00:02Z"),
        finished("tracker:a", "a", "2026-05-27T12:00:03Z", 5),
      ],
      50,
    );
    expect(cycles[0].triggered).toBe(2);
    expect(cycles[0].triggeredEntries.map((t) => t.ticketId)).toEqual(["T-1", "T-2"]);
  });

  it("does not bleed events from one cycle into the next on the same source", () => {
    const cycles = derivePullCycleLog(
      [
        started("tracker:a", "a", "2026-05-27T12:00:00Z"),
        scanned("tracker:a", "a", "T-1", "2026-05-27T12:00:01Z"),
        finished("tracker:a", "a", "2026-05-27T12:00:02Z", 1),
        started("tracker:a", "a", "2026-05-27T12:01:00Z"),
        scanned("tracker:a", "a", "T-2", "2026-05-27T12:01:01Z"),
        finished("tracker:a", "a", "2026-05-27T12:01:02Z", 1),
      ],
      50,
    );
    expect(cycles).toHaveLength(2);
    // newest-first
    expect(cycles[0].scannedTicketIds).toEqual(["T-2"]);
    expect(cycles[1].scannedTicketIds).toEqual(["T-1"]);
  });

  it("newest cycle is first in the result", () => {
    const cycles = derivePullCycleLog(
      [
        started("tracker:a", "a", "2026-05-27T12:00:00Z"),
        finished("tracker:a", "a", "2026-05-27T12:00:05Z"),
        started("tracker:a", "a", "2026-05-27T12:05:00Z"),
        finished("tracker:a", "a", "2026-05-27T12:05:05Z"),
      ],
      50,
    );
    expect(cycles[0].startedAt).toBe("2026-05-27T12:05:00Z");
    expect(cycles[1].startedAt).toBe("2026-05-27T12:00:00Z");
  });

  it("respects the limit parameter", () => {
    const events: SystemEvent[] = [];
    for (let i = 0; i < 10; i++) {
      events.push(started("tracker:a", "a", `2026-05-27T12:${String(i).padStart(2, "0")}:00Z`));
      events.push(finished("tracker:a", "a", `2026-05-27T12:${String(i).padStart(2, "0")}:05Z`));
    }
    expect(derivePullCycleLog(events, 5)).toHaveLength(5);
  });
});
