import { act, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fakeHub, ticket } from "../../__tests__/ticketFixtures";
import { conversation, defaults, detail, reviews, series, ticketText } from "./detailFixtures";
import type { RunEvent } from "@/types/hub-events";
import type { TicketDetail, TicketRequires, TicketSummary } from "@/types/tickets";

// 2026-10-15-114fd: the ticket page's header on a closed ticket — the closed line follows the bucket, Reopen only for a
// close made here, Close ticket otherwise — and a required ticket that is closed reads "(closed)" in Waits for.

const NONE: RunEvent[] = [];
vi.mock("@/hooks/useRunEvents", () => ({ useRunEvents: () => NONE }));
vi.mock("next/navigation", () => ({
  useSearchParams: () => new URLSearchParams(""),
  useRouter: () => ({ replace: vi.fn(), push: vi.fn() }),
  usePathname: () => "/tickets/shop/59",
}));
vi.mock("@/hooks/useLacksPermission", () => ({ useLacksPermission: () => false }));

const { TicketPageStore } = await import("@/lib/tickets/TicketPageStore");
const { TicketPage } = await import("../TicketPage");

const world = { detail: null as unknown as TicketDetail, requires: { requires: [], requiredBy: [] } as TicketRequires };
const api = {
  detail: vi.fn(() => Promise.resolve(world.detail)),
  series: vi.fn(() => Promise.resolve(series("", [], { runId: null, gate: null, recorded: false }))),
  conversation: vi.fn(() => Promise.resolve(conversation([]))),
  text: vi.fn(() => Promise.resolve(ticketText())),
  passes: vi.fn(() => Promise.reject(new Error("unused"))),
  run: vi.fn(() => Promise.reject(new Error("unused"))),
  compute: vi.fn(() => Promise.reject(new Error("unused"))),
  reviews: vi.fn(() => Promise.resolve(reviews())),
  defaults: vi.fn(() => Promise.resolve(defaults())),
  requires: vi.fn(() => Promise.resolve(world.requires)),
};

const CLOSED = { at: new Date(Date.now() - 3 * 3600000).toISOString(), by: "Ada Lovelace", note: "superseded", closedPullRequests: [], trackerClosed: false };

function page(over: Partial<TicketSummary>): TicketDetail {
  return detail([], {
    summary: ticket({
      ticketId: "59", ticketKey: "github-59", title: "Orders: list screen", status: "", newestRunId: "", attemptCount: 0, cost: 0,
      attempts: [], ticketText: true, waitsFor: [], ...over,
    }),
  });
}

async function open() {
  render(<TicketPageStore project="shop" ticketId="59" client={fakeHub().client} api={api}><TicketPage /></TicketPageStore>);
  await act(() => vi.advanceTimersByTimeAsync(0));
  await act(() => vi.advanceTimersByTimeAsync(0));
}

beforeEach(() => {
  vi.useFakeTimers();
  for (const fn of Object.values(api)) fn.mockClear();
  world.requires = { requires: [], requiredBy: [] };
});
afterEach(() => vi.useRealTimers());

describe("TicketHeader, closed", () => {
  it("TicketHeader_ClosedByHand_ShowsTheLineAndReopen", async () => {
    world.detail = page({ bucket: "closed", kind: "by-hand", closed: CLOSED });
    await open();

    expect(screen.getByTestId("ticket-closed-line")).toHaveTextContent("Closed by Ada Lovelace 3h ago: superseded");
    expect(screen.getByTestId("ticket-head").querySelector(".ticon.closed")).not.toBeNull();
    expect(screen.getByTestId("ticket-reopen-59")).toBeInTheDocument();
    expect(screen.queryByTestId("ticket-close-59")).toBeNull();
  });

  it("TicketHeader_ClosedOnTheTracker_ShowsTheLineWithoutReopen", async () => {
    world.detail = page({ bucket: "closed", kind: "tracker", closed: null });
    await open();

    expect(screen.getByTestId("ticket-closed-line")).toHaveTextContent("Closed on the tracker by a person");
    expect(screen.queryByTestId("ticket-reopen-59")).toBeNull();
    expect(screen.queryByTestId("ticket-close-59")).toBeNull();
  });

  it("TicketHeader_MarkStoredButBucketNotClosed_ShowsNoClosedLine", async () => {
    world.detail = page({ bucket: "filed", kind: null, closed: CLOSED });
    await open();

    expect(screen.queryByTestId("ticket-closed-line")).toBeNull();
    expect(screen.queryByTestId("ticket-reopen-59")).toBeNull();
    expect(screen.getByTestId("ticket-close-59")).toHaveTextContent("Close ticket");
  });

  it("TicketWaitsFor_ClosedRequiredTicket_SaysClosed", async () => {
    world.detail = page({ bucket: "blocked" });
    world.requires = {
      requires: [
        { ticketId: "88", key: "#88", title: "Legacy import", state: "closed", hasPage: true },
        { ticketId: "41", key: "#41", title: "#41", state: "idle", hasPage: false },
      ],
      requiredBy: [],
    };
    await open();

    const waits = screen.getByTestId("ticket-waits-88");
    expect(waits).toHaveTextContent("#88Legacy import (closed)");
    expect(screen.getByTestId("ticket-waits-41")).toHaveTextContent("(not merged)");
  });
});
