import { renderHook } from "@testing-library/react";
import { describe, it, expect, vi, afterEach } from "vitest";
import { useSubsystemActivity } from "../useSubsystemActivity";
import { SystemEventType, type SystemEvent } from "@/types/system-events";

// p0209b: per-subsystem freshness derivation. Time is pinned via fake timers so
// "now - latest" is deterministic across runs.

afterEach(() => {
  vi.useRealTimers();
});

function pollFinished(timestamp: string): SystemEvent {
  return {
    source: "tracker",
    type: SystemEventType.PollCycleFinished,
    timestamp,
    tracker: "sample",
    ticketsPolled: 49,
    matched: 0,
    spawned: 0,
    statusFiltered: 0,
    zeroMatched: 49,
    durationMs: 269,
  };
}

describe("useSubsystemActivity", () => {
  it("useSubsystemActivity_NewestEventPerSubsystem_SetsTailAndFreshness", () => {
    vi.useFakeTimers();
    const now = new Date("2026-06-03T19:09:30.000Z");
    vi.setSystemTime(now);

    const events: SystemEvent[] = [
      pollFinished("2026-06-03T19:08:30.000Z"),
      pollFinished("2026-06-03T19:09:28.000Z"), // newest = 2s ago
    ];

    const { result } = renderHook(() => useSubsystemActivity(events));
    const tracker = result.current.tracker;

    expect(tracker.live).toBe(true);
    expect(tracker.freshness).toBe("2s ago");
    expect(tracker.events).toHaveLength(2);
    // tail reflects the NEWEST event and its short time.
    expect(tracker.tail).not.toBeNull();
    expect(tracker.tail!.timestamp).toBe("19:09:28");
    expect(tracker.tail!.text).toContain("poll done");
  });

  it("useSubsystemActivity_NoRecentEvents_MarksIdle", () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-06-03T19:30:00.000Z"));

    // tracker last fired 10 minutes ago → outside the 120s live window.
    const events: SystemEvent[] = [pollFinished("2026-06-03T19:20:00.000Z")];

    const { result } = renderHook(() => useSubsystemActivity(events));

    expect(result.current.tracker.live).toBe(false);
    expect(result.current.tracker.freshness).toBe("10m ago");
    // webhooks never fired → idle with em-dash freshness and no tail.
    expect(result.current.webhooks.live).toBe(false);
    expect(result.current.webhooks.freshness).toBe("—");
    expect(result.current.webhooks.tail).toBeNull();
    expect(result.current.webhooks.events).toHaveLength(0);
  });
});
