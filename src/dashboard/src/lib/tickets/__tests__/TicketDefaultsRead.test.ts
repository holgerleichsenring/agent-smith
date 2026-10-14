import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  attempt, conversation, defaultEntry, defaults, detail, passes, reviews, series,
} from "@/components/tickets/detail/__tests__/detailFixtures";
import { TicketPageReads, type TicketPageSnapshot } from "../TicketPageReads";

// 2026-10-13-8762q: the defaults are a ticket-level read with their own 2 s window — read at start, re-read on a
// TicketActivityChanged, a default taken in the live stream or this page's own verdict; never on a RunsChanged.

const api = {
  detail: vi.fn(() => Promise.resolve(detail([attempt({ runId: "r-1" })]))),
  series: vi.fn(() => Promise.resolve(series("r-1", []))),
  conversation: vi.fn(() => Promise.resolve(conversation([]))),
  reviews: vi.fn(() => Promise.resolve(reviews())),
  defaults: vi.fn(() => Promise.resolve(defaults([defaultEntry()]))),
  passes: vi.fn((runId: string) => Promise.resolve(passes(runId, {}))),
  run: vi.fn(() => Promise.reject(new Error("unused"))),
  compute: vi.fn(() => Promise.reject(new Error("unused"))),
};

let latest: TicketPageSnapshot | null = null;
let reads: TicketPageReads;
const settle = (ms: number) => vi.advanceTimersByTimeAsync(ms);

beforeEach(() => {
  vi.useFakeTimers();
  for (const fn of Object.values(api)) fn.mockClear();
  reads = new TicketPageReads("shop", "101", (s) => { latest = s; }, api);
  reads.start();
});
afterEach(() => {
  reads.dispose();
  vi.useRealTimers();
});

describe("TicketDefaultsRead", () => {
  it("TicketDefaultsRead_ReadAtStart_AndOnTicketChanged_NotOnRunsChanged", async () => {
    await settle(0);
    expect(api.defaults).toHaveBeenCalledTimes(1);
    expect(latest?.defaults.data?.pending).toBe(1);
    for (let i = 0; i < 4; i++) reads.runChanged("r-1");
    await settle(2000);
    expect(api.detail).toHaveBeenCalledTimes(2);
    expect(api.defaults).toHaveBeenCalledTimes(1);
    reads.ticketChanged();
    reads.defaultTaken();
    reads.defaultsChanged();
    await settle(2000);
    expect(api.defaults).toHaveBeenCalledTimes(2);
  });

  it("TicketDefaultsRead_DefaultTaken_RereadsOnlyTheDefaults", async () => {
    await settle(0);
    reads.defaultTaken();
    reads.defaultsChanged();
    await settle(2000);
    expect(api.defaults).toHaveBeenCalledTimes(2);
    expect(api.detail).toHaveBeenCalledTimes(1);
    expect(api.conversation).toHaveBeenCalledTimes(1);
    expect(api.reviews).toHaveBeenCalledTimes(1);
  });

  it("TicketDefaultsRead_DisposeOfThePageReads_CancelsItsRefetch", async () => {
    await settle(0);
    reads.defaultTaken();
    reads.dispose();
    await settle(5000);
    expect(api.defaults).toHaveBeenCalledTimes(1);
  });
});
