import { describe, it, expect, beforeEach, vi } from "vitest";
import { HubConnectionState } from "@microsoft/signalr";

// 2026-08-25-2de1: the hub is the second and last outgoing point, and it takes a
// token FACTORY rather than a token — SignalR asks again on every reconnect, and
// a token frozen at connect time is a connection that reconnects forever an hour
// later without ever saying why. p0503c reads it off the query string, which is
// what this factory produces.

const auth = vi.hoisted(() => ({ token: null as string | null }));

vi.mock("@/lib/auth/session", () => ({
  currentAccessToken: async () => auth.token,
}));

interface WithUrlCall {
  url: string;
  options?: { accessTokenFactory?: () => Promise<string> };
}

const hoisted = vi.hoisted(() => ({ withUrlCalls: [] as WithUrlCall[] }));

vi.mock("@microsoft/signalr", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@microsoft/signalr")>();
  const State = actual.HubConnectionState;
  class FakeBuilder {
    withUrl(url: string, options?: WithUrlCall["options"]): this {
      hoisted.withUrlCalls.push({ url, options });
      return this;
    }
    withAutomaticReconnect(): this { return this; }
    configureLogging(): this { return this; }
    build() {
      return {
        state: State.Disconnected,
        on: () => {},
        onreconnecting: () => {},
        onreconnected: () => {},
        onclose: () => {},
        start: async () => {},
        stop: async () => {},
        invoke: async () => {},
      };
    }
  }
  return {
    ...actual,
    HubConnectionBuilder: FakeBuilder as unknown as typeof actual.HubConnectionBuilder,
  };
});

const { JobsHubClient, getJobsHubClient, hubAccessToken, __resetJobsHubClientForTests } =
  await import("../JobsHubClient");

/** The factory the connection was built with, if it was given one. */
function factoryFromLastConnect(): (() => Promise<string>) | undefined {
  return hoisted.withUrlCalls.at(-1)?.options?.accessTokenFactory;
}

beforeEach(() => {
  hoisted.withUrlCalls.length = 0;
  auth.token = null;
  __resetJobsHubClientForTests();
});

describe("JobsHubClient handshake token", () => {
  it("Hub_NoTokenFactory_TheHandshakeIsTheOneItAlwaysMade", async () => {
    await new JobsHubClient({ hubUrl: "/hub/jobs" }).subscribeOverview();

    expect(hoisted.withUrlCalls).toEqual([{ url: "/hub/jobs", options: undefined }]);
  });

  it("Hub_StoreHoldsAToken_TheFactoryReturnsItOnEveryCall", async () => {
    auth.token = "at-1";
    await getJobsHubClient("/hub/jobs").subscribeOverview();

    const factory = factoryFromLastConnect();

    expect(await factory?.()).toBe("at-1");
    // Asked AGAIN — SignalR re-invokes this on every reconnect.
    expect(await factory?.()).toBe("at-1");
  });

  it("Hub_TokenRenewed_TheNextConnectUsesTheNewOne", async () => {
    auth.token = "at-1";
    await getJobsHubClient("/hub/jobs").subscribeOverview();
    const factory = factoryFromLastConnect();
    expect(await factory?.()).toBe("at-1");

    auth.token = "at-2";

    expect(await factory?.()).toBe("at-2");
  });

  it("Hub_StateIsReportedThroughTheFakeTransport", () => {
    expect(new JobsHubClient({ hubUrl: "/hub/jobs" }).state()).toBe(
      HubConnectionState.Disconnected,
    );
  });
});

describe("hubAccessToken", () => {
  it("HubToken_NoneHeld_IsTheEmptyStringSignalROmits", async () => {
    expect(await hubAccessToken()).toBe("");
  });

  it("HubToken_OneHeld_IsTheTokenItself", async () => {
    auth.token = "at-1";

    expect(await hubAccessToken()).toBe("at-1");
  });
});
