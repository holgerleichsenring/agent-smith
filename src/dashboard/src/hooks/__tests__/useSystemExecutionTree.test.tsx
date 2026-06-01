import { renderHook } from "@testing-library/react";
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { SystemEventType, type SystemEvent } from "@/types/system-events";
import { useSystemExecutionTree } from "../useSystemExecutionTree";

const NOW = "2026-05-30T16:00:00.000Z";

function recently(secAgo: number): string {
  return new Date(new Date(NOW).getTime() - secAgo * 1000).toISOString();
}

beforeEach(() => {
  vi.useFakeTimers();
  vi.setSystemTime(new Date(NOW));
});

afterEach(() => {
  vi.useRealTimers();
});

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

  it("useSystemExecutionTree_FreshnessBar_GrowsFromLeftWithRecency_p0190", () => {
    // Two events: one 30s old in tracker, one 280s old in config — window
    // auto-sizes to 280s. Fresh subsystem's bar should be near-full; the
    // older subsystem's bar should be much shorter. Both start at 0.
    const events: SystemEvent[] = [
      {
        type: SystemEventType.PollCycleFinished,
        source: "tracker:azuredevops",
        timestamp: recently(30),
        tracker: "azuredevops",
        ticketsPolled: 5,
        matched: 0,
        spawned: 0,
        statusFiltered: 0,
        zeroMatched: 5,
        durationMs: 100,
      },
      {
        type: SystemEventType.ConfigFileRead,
        source: "config:yaml",
        timestamp: recently(280),
        kind: "AgentSmithYml",
        path: "/app/config/agentsmith.yml",
        sizeBytes: 8000,
        runId: null,
      },
    ];
    const { result } = renderHook(() => useSystemExecutionTree(events));
    const tracker = result.current.nodes.find((n) => n.id === "sys-tracker")!;
    const config = result.current.nodes.find((n) => n.id === "sys-config")!;
    expect(tracker.startSeconds).toBe(0);
    expect(config.startSeconds).toBe(0);
    // 30s-old event in 280s window → bar fills (280 - 30)/280 ≈ 89%
    // 280s-old event in 280s window → bar fills ~0% (clamped to 0.5)
    const trackerFill = tracker.durationSeconds / tracker.totalSeconds;
    const configFill = config.durationSeconds / config.totalSeconds;
    expect(trackerFill).toBeGreaterThan(0.8);
    expect(configFill).toBeLessThan(0.05);
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
