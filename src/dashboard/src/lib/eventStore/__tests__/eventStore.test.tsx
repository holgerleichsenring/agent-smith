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

const runEvent = (): RunEvent => ({}) as RunEvent;

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

    act(() => {
      fake.emitSystem(webhook("/hooks/github"));
      fake.emitSystem(chat("#ops"));
      fake.emitSystem(webhook("/hooks/gitlab"));
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

    fake.emitRun("A", runEvent());
    fake.emitRun("A", runEvent());

    expect(a.getSnapshot()).toHaveLength(2);
    expect(b.getSnapshot()).toHaveLength(0);
  });
});
