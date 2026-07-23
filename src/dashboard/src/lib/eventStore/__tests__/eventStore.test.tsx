import { describe, it, expect } from "vitest";
import { renderHook, act } from "@testing-library/react";
import { createElement, type ReactNode } from "react";
import { EventStore } from "../eventStore";
import { EventStoreProvider } from "../EventStoreProvider";
import { useSubsystemEvents } from "@/hooks/useSubsystemEvents";
import { SystemEventType, type SystemEvent } from "@/types/system-events";
import type { RunEvent } from "@/types/hub-events";
import { createFakeSource, flush } from "./fakes";

function webhook(path: string): SystemEvent {
  return {
    source: "test",
    type: SystemEventType.WebhookReceived,
    timestamp: new Date().toISOString(),
    eventType: "push",
    path,
    actioned: true,
    skipReason: null,
  };
}

function chat(channel: string): SystemEvent {
  return {
    source: "test",
    type: SystemEventType.ChatMessageReceived,
    timestamp: new Date().toISOString(),
    channel,
    messageType: "message",
    actioned: true,
    skipReason: null,
  };
}

// p0366: the run scope now dedups by serialized event (idempotent reconnect
// replay), so distinct events must carry distinct content — real RunEvents
// always differ by at least their timestamp.
const runEvent = (seq: number): RunEvent => ({ timestamp: `t${seq}` }) as unknown as RunEvent;

describe("EventStore", () => {
  it("useSubsystemEvents_ShowsOnlyItsSubsystemScope", async () => {
    const fake = createFakeSource();
    const store = new EventStore(fake.source);
    const wrapper = ({ children }: { children: ReactNode }) =>
      createElement(EventStoreProvider, { store }, children);

    const { result } = renderHook(() => useSubsystemEvents("webhooks"), { wrapper });
    await act(async () => {
      await flush();
    });

    // p0355: notifications are coalesced (one render per burst), so the
    // re-render lands on the next tick — await it inside act.
    await act(async () => {
      fake.emitSystem(webhook("/hooks/github"));
      fake.emitSystem(chat("#ops"));
      fake.emitSystem(webhook("/hooks/gitlab"));
      await flush();
    });

    // Only the two webhook events — the chat event is out of scope.
    expect(result.current).toHaveLength(2);
    expect(result.current.every((e) => e.type === SystemEventType.WebhookReceived)).toBe(true);
  });

  it("EventStore_BacklogSurvivesRemount", async () => {
    const fake = createFakeSource();
    const store = new EventStore(fake.source);
    const scope = store.systemScope();

    const release = scope.acquire();
    await flush();
    fake.emitSystem(webhook("/a"));
    fake.emitSystem(webhook("/b"));
    expect(scope.getSnapshot()).toHaveLength(2);

    release(); // unmount: live subscription closes
    await flush();
    expect(fake.counts().systemCancels).toBe(1);

    scope.acquire(); // remount: backlog is still there, NOT wiped
    expect(scope.getSnapshot()).toHaveLength(2);
  });

  it("EventStore_ScopeChange_UnsubscribesPrevious_NoLeak", async () => {
    const fake = createFakeSource();
    const store = new EventStore(fake.source);

    const releaseA = store.runScope("A").acquire();
    await flush();
    expect(fake.counts().runSubs).toBe(1);
    expect(fake.runListenerCount()).toBe(1);

    releaseA(); // scope change away from A
    await flush();
    expect(fake.counts().runCancels).toBe(1);
    expect(fake.runListenerCount()).toBe(0); // no leaked listener

    store.runScope("B").acquire();
    await flush();
    expect(fake.counts().runSubs).toBe(2);
  });

  it("RunEvents_RemainPerRunScoped", async () => {
    const fake = createFakeSource();
    const store = new EventStore(fake.source);
    const a = store.runScope("A");
    const b = store.runScope("B");
    a.acquire();
    b.acquire();
    await flush();

    fake.emitRun("A", runEvent(1));
    fake.emitRun("A", runEvent(2));

    expect(a.getSnapshot()).toHaveLength(2);
    expect(b.getSnapshot()).toHaveLength(0);
  });

  it("RunScope_ReconnectReplayOverlappingLiveTail_NoDuplicates", async () => {
    // p0366: on reconnect JobsHubClient re-invokes SubscribeRun, whose server
    // replay re-emits the retained structural window — which overlaps the live
    // tail already in the backlog. The run scope's keyed dedup must collapse the
    // overlap and keep only the genuinely-new (missed-during-drop) event.
    const fake = createFakeSource();
    const store = new EventStore(fake.source);
    const scope = store.runScope("A");
    scope.acquire();
    await flush();

    fake.emitRun("A", runEvent(1));
    fake.emitRun("A", runEvent(2));
    fake.emitRun("A", runEvent(3));
    await flush();
    expect(scope.getSnapshot()).toHaveLength(3);

    // Reconnect replay: the retained window (1,2,3) PLUS an event that landed
    // while the client was out of the group (4).
    [1, 2, 3, 4].forEach((seq) => fake.emitRun("A", runEvent(seq)));
    await flush();

    const timestamps = scope.getSnapshot().map((e) => (e as { timestamp: string }).timestamp);
    expect(timestamps).toEqual(["t1", "t2", "t3", "t4"]);
  });
});
