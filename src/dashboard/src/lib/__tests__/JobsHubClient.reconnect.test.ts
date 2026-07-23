import { describe, it, expect, beforeEach, vi } from "vitest";
import { HubConnectionState } from "@microsoft/signalr";

// p0366: a SignalR automatic-reconnect gets a NEW ConnectionId, so every
// server-side group membership is gone. These tests pin that JobsHubClient
// re-invokes every active subscription on reconnected — so the run-detail view
// (and every other group) rejoins instead of staying "Connected" yet silent.

interface FakeHubConnection {
  state: HubConnectionState;
  invocations: Array<{ method: string; args: unknown[] }>;
  reconnecting: (() => void) | null;
  reconnected: (() => void) | null;
  closed: (() => void) | null;
  on(name: string, handler: (...args: unknown[]) => void): void;
  onreconnecting(cb: () => void): void;
  onreconnected(cb: () => void): void;
  onclose(cb: () => void): void;
  start(): Promise<void>;
  stop(): Promise<void>;
  invoke(method: string, ...args: unknown[]): Promise<unknown>;
}

const hoisted = vi.hoisted(() => ({ built: [] as FakeHubConnection[] }));

vi.mock("@microsoft/signalr", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@microsoft/signalr")>();
  const State = actual.HubConnectionState;
  class FakeBuilder {
    withUrl(): this { return this; }
    withAutomaticReconnect(): this { return this; }
    configureLogging(): this { return this; }
    build(): FakeHubConnection {
      const invocations: Array<{ method: string; args: unknown[] }> = [];
      const conn: FakeHubConnection = {
        state: State.Disconnected,
        invocations,
        reconnecting: null,
        reconnected: null,
        closed: null,
        on: () => {},
        onreconnecting: (cb) => { conn.reconnecting = cb; },
        onreconnected: (cb) => { conn.reconnected = cb; },
        onclose: (cb) => { conn.closed = cb; },
        start: async () => { conn.state = State.Connected; },
        stop: async () => { conn.state = State.Disconnected; },
        invoke: async (method, ...args) => { invocations.push({ method, args }); },
      };
      hoisted.built.push(conn);
      return conn;
    }
  }
  return { ...actual, HubConnectionBuilder: FakeBuilder as unknown as typeof actual.HubConnectionBuilder };
});

// Import AFTER the mock is registered so the client builds the fake connection.
const { JobsHubClient } = await import("../JobsHubClient");

const flush = (): Promise<void> => new Promise((r) => setTimeout(r, 0));
const methodsOf = (conn: FakeHubConnection): string[] => conn.invocations.map((i) => i.method);

beforeEach(() => {
  hoisted.built.length = 0;
});

describe("JobsHubClient reconnect", () => {
  it("ReInvokesAllActiveSubscriptions_OnReconnected", async () => {
    const client = new JobsHubClient({ hubUrl: "/hub/jobs" });
    await client.subscribeOverview();
    await client.subscribeSystem();
    await client.subscribeRun("A");
    await client.subscribeRun("B");
    await client.expandSandbox("A", "repo1");

    const conn = hoisted.built[0];
    conn.invocations.length = 0; // ignore the initial joins; assert only the rejoin

    conn.reconnected!(); // fresh ConnectionId — every group membership dropped
    await flush();

    const methods = methodsOf(conn);
    expect(methods).toContain("SubscribeOverview");
    expect(methods).toContain("SubscribeSystem");
    expect(methods).toContain("ExpandSandbox");
    const runIds = conn.invocations.filter((i) => i.method === "SubscribeRun").map((i) => i.args[0]);
    expect(runIds).toEqual(expect.arrayContaining(["A", "B"]));
  });

  it("RunGroupRejoined_AfterTransientDrop", async () => {
    const client = new JobsHubClient({ hubUrl: "/hub/jobs" });
    await client.subscribeRun("run-42");
    const conn = hoisted.built[0];
    conn.invocations.length = 0;

    conn.reconnecting!(); // drop
    conn.reconnected!(); // recover with a new ConnectionId
    await flush();

    const runIds = conn.invocations.filter((i) => i.method === "SubscribeRun").map((i) => i.args[0]);
    expect(runIds).toContain("run-42");
  });

  it("DroppedSubscription_NotRejoined", async () => {
    const client = new JobsHubClient({ hubUrl: "/hub/jobs" });
    const cancelRun = await client.subscribeRun("A");
    await client.subscribeRun("B");
    await cancelRun(); // last subscriber for A leaves — its group is forgotten
    const conn = hoisted.built[0];
    conn.invocations.length = 0;

    conn.reconnected!();
    await flush();

    const runIds = conn.invocations.filter((i) => i.method === "SubscribeRun").map((i) => i.args[0]);
    expect(runIds).toContain("B");
    expect(runIds).not.toContain("A");
  });

  it("ReconnectingStateSurfaced_ThenConnectedAfterRejoin", async () => {
    const client = new JobsHubClient({ hubUrl: "/hub/jobs" });
    await client.subscribeRun("A");
    const conn = hoisted.built[0];

    const states: HubConnectionState[] = [];
    client.connectionState.add((s) => states.push(s));

    conn.reconnecting!();
    expect(states).toContain(HubConnectionState.Reconnecting);
    // Not silently Connected while the group is still un-rejoined.
    expect(states[states.length - 1]).toBe(HubConnectionState.Reconnecting);

    conn.reconnected!();
    await flush();
    expect(states[states.length - 1]).toBe(HubConnectionState.Connected);
  });

  it("MultipleTabs_EachClientRecoversItsOwnGroups", async () => {
    const tabA = new JobsHubClient({ hubUrl: "/hub/jobs" });
    const tabB = new JobsHubClient({ hubUrl: "/hub/jobs" });
    await tabA.subscribeRun("run-A");
    await tabB.subscribeRun("run-B");

    const connA = hoisted.built[0];
    const connB = hoisted.built[1];
    connA.invocations.length = 0;
    connB.invocations.length = 0;

    // Only tab A drops and recovers.
    connA.reconnected!();
    await flush();

    expect(methodsOf(connA).filter((m) => m === "SubscribeRun").length).toBe(1);
    expect(connA.invocations.find((i) => i.method === "SubscribeRun")?.args[0]).toBe("run-A");
    // Tab B was untouched — its subscription is per-client, not shared.
    expect(connB.invocations).toHaveLength(0);
  });
});
