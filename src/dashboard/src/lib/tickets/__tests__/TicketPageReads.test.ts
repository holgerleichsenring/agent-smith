import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { attempt, conversation, detail, passes, reviews, series } from "@/components/tickets/detail/__tests__/detailFixtures";
import { TicketPageReads, type TicketPageSnapshot } from "../TicketPageReads";

// 2026-10-13-8762l: the review rounds are a ticket-level read with their own 2 s window — re-read on a
// TicketActivityChanged with everything else, and alone on a new review round.

const api = {
  detail: vi.fn(() => Promise.resolve(detail([attempt()]))),
  series: vi.fn(() => Promise.resolve(series("r-1", []))),
  conversation: vi.fn(() => Promise.resolve(conversation([]))),
  reviews: vi.fn(() => Promise.resolve(reviews())),
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

describe("TicketPageReads", () => {
  it("TicketPageReads_TicketChanged_RereadsReviews", async () => {
    await settle(0);
    expect(api.reviews).toHaveBeenCalledTimes(1);
    expect(latest?.reviews.data?.attempts).toEqual([]);
    reads.ticketChanged();
    reads.ticketChanged();
    await settle(2000);
    expect(api.reviews).toHaveBeenCalledTimes(2);
    expect(api.detail).toHaveBeenCalledTimes(2);
  });

  it("TicketPageReads_ReviewRound_RereadsOnlyReviews", async () => {
    await settle(0);
    for (let i = 0; i < 3; i++) reads.reviewRound();
    await settle(1999);
    expect(api.reviews).toHaveBeenCalledTimes(1);
    await settle(1);
    expect(api.reviews).toHaveBeenCalledTimes(2);
    expect(api.detail).toHaveBeenCalledTimes(1);
    expect(api.series).toHaveBeenCalledTimes(1);
    expect(api.conversation).toHaveBeenCalledTimes(1);
  });

  it("TicketPageReads_FailedReviewsReread_KeepsTheRoundsItShowed", async () => {
    await settle(0);
    api.reviews.mockImplementationOnce(() => Promise.reject(new Error("down")));
    reads.reviewRound();
    await settle(2000);
    expect(latest?.reviews.failed).toBe(true);
    expect(latest?.reviews.data).not.toBeNull();
  });
});
