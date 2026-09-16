import { describe, it, expect, beforeEach, vi } from "vitest";
import { HubConnectionState } from "@microsoft/signalr";

// 2026-09-15-cb3e: the spec-dialog group is OWNED, so its subscribe is the first invoke a
// caller can expect the server to REFUSE. The join bookkeeping counted the subscriber
// before the invoke and never unwound it, so the refusal left the count at one: the next
// mount skipped the invoke, resolved, and rendered a live-looking conversation that could
// never receive a reply — on the surface that files tickets.

interface FakeHubConnection {
  state: HubConnectionState;
  invocations: Array<{ method: string; args: unknown[] }>;
  refuse: boolean;
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
        refuse: false,
        on: () => {},
        onreconnecting: () => {},
        onreconnected: () => {},
        onclose: () => {},
        start: async () => { conn.state = State.Connected; },
        stop: async () => { conn.state = State.Disconnected; },
        invoke: async (method, ...args) => {
          invocations.push({ method, args });
          if (conn.refuse) throw new Error("not the owner of this dialog");
        },
      };
      hoisted.built.push(conn);
      return conn;
    }
  }
  return {
    ...actual,
    HubConnectionBuilder: FakeBuilder as unknown as typeof actual.HubConnectionBuilder,
  };
});

const { JobsHubClient } = await import("../JobsHubClient");

beforeEach(() => {
  hoisted.built.length = 0;
});

describe("JobsHubClient refusal", () => {
  it("RefusedSubscribe_Rethrows_SoTheCallerCanSaySo", async () => {
    const client = new JobsHubClient({ hubUrl: "/hub/jobs" });
    await client.subscribeOverview();
    hoisted.built[0].refuse = true;

    await expect(client.subscribeSpecDialog("d-foreign")).rejects.toThrow("not the owner");
  });

  it("RefusedSubscribe_LeavesTheGroupJoinable_SoTheNextMountInvokesAgain", async () => {
    const client = new JobsHubClient({ hubUrl: "/hub/jobs" });
    await client.subscribeOverview();
    const conn = hoisted.built[0];
    conn.refuse = true;
    await client.subscribeSpecDialog("d-1").catch(() => {});

    conn.refuse = false;
    conn.invocations.length = 0;
    await client.subscribeSpecDialog("d-1");

    expect(conn.invocations.map((i) => i.method)).toContain("SubscribeSpecDialog");
  });
});
