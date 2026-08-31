import { describe, it, expect, beforeEach, vi } from "vitest";
import { HubConnectionState } from "@microsoft/signalr";

// 2026-08-28-0f46: the first connection is the one nothing else retries.
// withAutomaticReconnect fires only after a connection that SUCCEEDED drops, so
// a refused negotiate is the end of the live channel for that tab — and the
// start promise, kept to deduplicate concurrent starts, hands that same refusal
// to every later subscribe. It reads exactly like a reconnect that never
// finishes, which is what the operator reported.

interface FakeHubConnection {
  state: HubConnectionState;
  invocations: Array<{ method: string; args: unknown[] }>;
  start(): Promise<void>;
  stop(): Promise<void>;
}

const hoisted = vi.hoisted(() => ({
  built: [] as FakeHubConnection[],
  refuse: 0,
  starts: 0,
  gate: null as (() => void) | null,
}));

vi.mock("@microsoft/signalr", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@microsoft/signalr")>();
  const State = actual.HubConnectionState;
  class FakeBuilder {
    withUrl(): this { return this; }
    withAutomaticReconnect(): this { return this; }
    configureLogging(): this { return this; }
    build(): FakeHubConnection {
      const invocations: Array<{ method: string; args: unknown[] }> = [];
      const conn = {
        state: State.Disconnected,
        invocations,
        on: () => {},
        onreconnecting: () => {},
        onreconnected: () => {},
        onclose: () => {},
        start: async () => {
          hoisted.starts += 1;
          if (hoisted.gate) await new Promise<void>((r) => { hoisted.gate = r; });
          if (hoisted.refuse > 0) {
            hoisted.refuse -= 1;
            throw new Error("the negotiate was refused");
          }
          conn.state = State.Connected;
        },
        stop: async () => { conn.state = State.Disconnected; },
        invoke: async (method: string, ...args: unknown[]) => { invocations.push({ method, args }); },
      };
      hoisted.built.push(conn as unknown as FakeHubConnection);
      return conn as unknown as FakeHubConnection;
    }
  }
  return { ...actual, HubConnectionBuilder: FakeBuilder as unknown as typeof actual.HubConnectionBuilder };
});

// Import AFTER the mock is registered so the client builds the fake connection.
const { JobsHubClient } = await import("../JobsHubClient");

beforeEach(() => {
  hoisted.built.length = 0;
  hoisted.refuse = 0;
  hoisted.starts = 0;
  hoisted.gate = null;
});

describe("JobsHubClient start", () => {
  it("Hub_AFirstConnectionThatFailed_IsRetriedByTheNextSubscribe", async () => {
    hoisted.refuse = 1;
    const client = new JobsHubClient({ hubUrl: "/hub/jobs" });

    await expect(client.subscribeOverview()).rejects.toThrow("refused");
    await client.subscribeOverview();

    expect(hoisted.built).toHaveLength(2);
    expect(client.state()).toBe(HubConnectionState.Connected);
    expect(hoisted.built[1].invocations.map((i) => i.method)).toEqual(["SubscribeOverview"]);
  });

  it("Hub_AFirstConnectionThatFailed_LeavesNoHalfOpenConnectionBehind", async () => {
    hoisted.refuse = 1;
    const client = new JobsHubClient({ hubUrl: "/hub/jobs" });

    await expect(client.subscribeOverview()).rejects.toThrow("refused");

    // A rejected start used to leave the Disconnected connection assigned, which
    // every later caller then read as "there is one".
    expect(client.state()).toBe(HubConnectionState.Disconnected);
  });

  it("Hub_AConcurrentStart_StillShareOneAttempt", async () => {
    hoisted.gate = () => {};
    const client = new JobsHubClient({ hubUrl: "/hub/jobs" });

    const both = Promise.all([client.subscribeOverview(), client.subscribeSystem()]);
    await Promise.resolve();
    hoisted.gate?.();
    await both;

    // Deduplicating concurrent starts is what the promise is FOR, and dropping
    // it when it settles must not cost that.
    expect(hoisted.starts).toBe(1);
    expect(hoisted.built).toHaveLength(1);
  });

  it("Hub_AConnectionThatDropped_IsReopenedAndRejoinsItsGroups", async () => {
    const client = new JobsHubClient({ hubUrl: "/hub/jobs" });
    await client.subscribeOverview();
    await client.subscribeRun("A");

    // Automatic reconnect gave up: the connection is closed and nothing else
    // will restart it. A resolved start promise used to be the answer forever.
    hoisted.built[0].state = HubConnectionState.Disconnected;
    await client.getTrail("A");

    expect(hoisted.built).toHaveLength(2);
    expect(hoisted.built[1].invocations.map((i) => i.method))
      .toEqual(["SubscribeOverview", "SubscribeRun", "GetTrail"]);
  });
});
