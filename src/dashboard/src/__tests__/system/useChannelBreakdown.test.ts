import { describe, it, expect } from "vitest";
import { deriveChannelBreakdown } from "@/hooks/useChannelBreakdown";
import { SystemEventType, type SystemEvent } from "@/types/system-events";

function evt(source: string): SystemEvent {
  return {
    source,
    type: SystemEventType.PollCycleStarted,
    timestamp: "2026-05-27T12:00:00Z",
  } as SystemEvent;
}

describe("deriveChannelBreakdown", () => {
  it("three sources returns three rows sorted by count desc then name", () => {
    const rows = deriveChannelBreakdown([
      evt("tracker:jira/a"),
      evt("webhook:github"),
      evt("tracker:jira/a"),
      evt("tracker:jira/a"),
      evt("webhook:github"),
      evt("chat:slack"),
    ]);
    expect(rows).toEqual([
      { source: "tracker:jira/a", count: 3 },
      { source: "webhook:github", count: 2 },
      { source: "chat:slack", count: 1 },
    ]);
  });

  it("empty events returns empty", () => {
    expect(deriveChannelBreakdown([])).toEqual([]);
  });
});
