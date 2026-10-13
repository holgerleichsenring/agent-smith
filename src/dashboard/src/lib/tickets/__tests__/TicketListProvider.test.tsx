import { act, render } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { HubConnectionState } from "@microsoft/signalr";
import { list } from "@/components/tickets/__tests__/ticketFixtures";

// 2026-10-13-9a59p: the ticket list is computed from up to 5000 runs per request, and RunsChanged
// fires on every step and LLM call. These pin the store's reads: a ticket nudge within 2 s, a
// RunsChanged burst within 2 s on a ticket route and 15 s elsewhere, one read for both when they
// meet, and nothing at all on a configuration route. The nudges arrive through the real
// JobsHubClient, by the hub message names the server sends.

type Handler = (...args: unknown[]) => void;

const hoisted = vi.hoisted(() => ({
  handlers: new Map<string, Handler>(),
  pathname: "/tickets",
  fetch: vi.fn(),
}));

vi.mock("@microsoft/signalr", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@microsoft/signalr")>();
  const State = actual.HubConnectionState;
  class FakeBuilder {
    withUrl(): this { return this; }
    withAutomaticReconnect(): this { return this; }
    configureLogging(): this { return this; }
    build() {
      const conn = {
        state: State.Disconnected,
        on: (name: string, handler: Handler) => hoisted.handlers.set(name, handler),
        onreconnecting: () => {},
        onreconnected: () => {},
        onclose: () => {},
        start: async () => { conn.state = State.Connected; },
        stop: async () => { conn.state = State.Disconnected; },
        invoke: async () => undefined,
      };
      return conn;
    }
  }
  return { ...actual, HubConnectionBuilder: FakeBuilder as unknown as typeof actual.HubConnectionBuilder };
});
vi.mock("next/navigation", () => ({ usePathname: () => hoisted.pathname }));
vi.mock("@/lib/ticketsApi", () => ({ fetchTickets: (...args: unknown[]) => hoisted.fetch(...args) }));

const { JobsHubClient } = await import("@/lib/JobsHubClient");
const { TicketListProvider } = await import("../TicketListProvider");

async function mounted(pathname: string) {
  hoisted.pathname = pathname;
  const client = new JobsHubClient({ hubUrl: "/hub/jobs" });
  const view = render(<TicketListProvider client={client}>{null}</TicketListProvider>);
  await act(() => vi.advanceTimersByTimeAsync(0));
  return { client, view };
}

const send = (message: string, ...args: unknown[]) => act(() => hoisted.handlers.get(message)!(...args));
const wait = (ms: number) => act(() => vi.advanceTimersByTimeAsync(ms));

beforeEach(() => {
  vi.useFakeTimers();
  hoisted.handlers.clear();
  hoisted.fetch.mockReset().mockResolvedValue(list([]));
});
afterEach(() => vi.useRealTimers());

describe("TicketListProvider", () => {
  it("TicketActivityChanged_RefetchesOnceWithinTwoSeconds_CoalescedWithRunsChanged", async () => {
    await mounted("/");
    expect(hoisted.fetch).toHaveBeenCalled();
    hoisted.fetch.mockClear();

    await send("RunsChanged", "r-1"); // elsewhere: may wait 15 s
    await send("TicketActivityChanged", "shop", "GH-101"); // pulls the read forward to 2 s
    await send("RunsChanged", "r-2");
    await wait(1999);
    expect(hoisted.fetch).not.toHaveBeenCalled();
    await wait(1);
    expect(hoisted.fetch).toHaveBeenCalledTimes(1);
    await wait(20000);
    expect(hoisted.fetch).toHaveBeenCalledTimes(1);
  });

  it("OnARunPage_ABurstOfRunsChanged_FetchesAtMostOncePerFifteenSeconds", async () => {
    await mounted("/jobs/r-1");
    hoisted.fetch.mockClear();

    for (let second = 0; second < 30; second++) {
      await send("RunsChanged", "r-1");
      await wait(1000);
    }
    expect(hoisted.fetch).toHaveBeenCalledTimes(2);
  });

  it("OnTheTicketList_RunsChanged_RefetchesWithinTwoSeconds", async () => {
    await mounted("/tickets");
    hoisted.fetch.mockClear();

    await send("RunsChanged", "r-1");
    await wait(2000);
    expect(hoisted.fetch).toHaveBeenCalledTimes(1);
  });

  it("OnAConfigurationRoute_FetchesNothing", async () => {
    await mounted("/config/projects");
    expect(hoisted.handlers.size).toBe(0);
    expect(hoisted.fetch).not.toHaveBeenCalled();
  });

  it("Reconnected_RereadsTheList", async () => {
    const { client } = await mounted("/tickets");
    hoisted.fetch.mockClear();

    await act(() => client.connectionState.emit(HubConnectionState.Connected));
    expect(hoisted.fetch).toHaveBeenCalledTimes(1);
  });
});
