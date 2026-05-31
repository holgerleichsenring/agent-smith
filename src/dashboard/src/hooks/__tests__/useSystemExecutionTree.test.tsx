import { renderHook } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { SystemEventType, type SystemEvent } from "@/types/system-events";
import { useSystemExecutionTree } from "../useSystemExecutionTree";

const NOW = "2026-05-30T16:00:00.000Z";

function recently(secAgo: number): string {
  return new Date(new Date(NOW).getTime() - secAgo * 1000).toISOString();
}

describe("useSystemExecutionTree", () => {
  it("useSystemExecutionTree_NoEvents_AllSubsystemsWaiting", () => {
    const { result } = renderHook(() => useSystemExecutionTree([]));
    const ids = result.current.nodes.map((n) => n.id);
    expect(ids).toEqual([
      "sys-tracker",
      "sys-webhook",
      "sys-chat",
      "sys-config",
      "sys-catalog",
    ]);
    for (const n of result.current.nodes) expect(n.status).toBe("wait");
  });

  it("useSystemExecutionTree_LatestPollEvent_FeedsTrackerTail", () => {
    const events: SystemEvent[] = [
      {
        type: SystemEventType.PollCycleFinished,
        source: "tracker:azuredevops",
        timestamp: recently(30),
        tracker: "azuredevops",
        ticketsPolled: 47,
        matched: 0,
        spawned: 0,
        statusFiltered: 0,
        zeroMatched: 47,
        durationMs: 250,
      },
    ];
    const { result } = renderHook(() => useSystemExecutionTree(events));
    const tracker = result.current.nodes.find((n) => n.id === "sys-tracker");
    expect(tracker?.tail?.text).toContain("poll done");
  });

  it("useSystemExecutionTree_StaleEvent_RendersWaitStatus", () => {
    const events: SystemEvent[] = [
      {
        type: SystemEventType.WebhookReceived,
        source: "webhook:github",
        timestamp: recently(60 * 60), // 1 hour ago
        eventType: "issues.opened",
        path: "/webhooks/github",
        actioned: true,
        skipReason: null,
      },
    ];
    const { result } = renderHook(() => useSystemExecutionTree(events));
    const webhook = result.current.nodes.find((n) => n.id === "sys-webhook");
    expect(webhook?.status).toBe("wait");
  });
});
