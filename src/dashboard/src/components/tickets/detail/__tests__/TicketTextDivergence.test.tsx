import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fakeHub } from "../../__tests__/ticketFixtures";
import { attempt, conversation, defaults, detail, reviews, series, ticketText } from "./detailFixtures";
import type { RunEvent } from "@/types/hub-events";
import type { TicketText } from "@/types/tickets";

// 2026-11-17-dd05d: the tracker text no longer states the approved specs — What happened says which goals, and offers
// editing the specs or overwriting the tracker from them; a focus re-reads the text past the server's cache.

const NONE: RunEvent[] = [];
vi.mock("@/hooks/useRunEvents", () => ({ useRunEvents: () => NONE }));
vi.mock("next/navigation", () => ({
  useSearchParams: () => new URLSearchParams(""),
  useRouter: () => ({ replace: vi.fn(), push: vi.fn() }),
  usePathname: () => "/tickets/shop/101",
}));
const body = vi.hoisted(() => ({ overwriteTicketBody: vi.fn() }));
vi.mock("@/lib/ticketBodyApi", () => body);

const { TicketPageStore } = await import("@/lib/tickets/TicketPageStore");
const { TicketPage } = await import("../TicketPage");

const world = { text: ticketText() as TicketText };
const api = {
  detail: vi.fn(() => Promise.resolve(detail([attempt({})]))),
  series: vi.fn(() => Promise.resolve(series("r-1", []))),
  conversation: vi.fn(() => Promise.resolve(conversation([]))),
  text: vi.fn((...args: [string, string, AbortSignal?, boolean?]) => Promise.resolve(args && world.text)),
  passes: vi.fn(() => Promise.reject(new Error("unused"))),
  run: vi.fn(() => Promise.reject(new Error("unused"))),
  compute: vi.fn(() => Promise.reject(new Error("unused"))),
  reviews: vi.fn(() => Promise.resolve(reviews())),
  defaults: vi.fn(() => Promise.resolve(defaults())),
  requires: vi.fn(() => Promise.resolve({ requires: [], requiredBy: [] })),
};

const settle = async () => {
  for (let i = 0; i < 4; i++) await act(() => vi.advanceTimersByTimeAsync(0));
};

async function show() {
  const { client } = fakeHub();
  render(<TicketPageStore project="shop" ticketId="101" client={client} api={api}><TicketPage /></TicketPageStore>);
  await settle();
}

beforeEach(() => {
  vi.useFakeTimers();
  world.text = { ...ticketText(), divergence: { specs: 2, unsaid: ["Retry with backoff"] } };
  api.text.mockClear();
  body.overwriteTicketBody.mockReset();
});
afterEach(() => {
  cleanup();
  vi.useRealTimers();
});

describe("TicketTextDivergence", () => {
  it("Divergence_NamesTheUnsaidGoals_AndOffersBothActs", async () => {
    await show();

    expect(screen.getByTestId("ticket-divergence")).toHaveTextContent("no longer states 1 of its 2 spec goals: “Retry with backoff”");
    expect(screen.getByTestId("divergence-edit")).toBeInTheDocument();
    expect(screen.getByTestId("divergence-overwrite")).toBeInTheDocument();
  });

  it("Overwrite_PostsAndShowsTheServersSentence", async () => {
    body.overwriteTicketBody.mockResolvedValue({ rewritten: false, text: "Ticket 101 is claimed by run r-9." });
    await show();

    fireEvent.click(screen.getByTestId("divergence-overwrite"));
    await settle();

    expect(body.overwriteTicketBody).toHaveBeenCalledWith("shop", "101");
    expect(screen.getByTestId("ticket-divergence")).toHaveTextContent("claimed by run r-9");
  });

  it("NoDivergence_DrawsNothing", async () => {
    world.text = { ...ticketText(), divergence: null };
    await show();

    expect(screen.queryByTestId("ticket-divergence")).toBeNull();
  });

  it("WindowFocus_ReadsTheTextFresh", async () => {
    await show();
    api.text.mockClear();

    act(() => { window.dispatchEvent(new Event("focus")); });
    await settle();

    expect(api.text).toHaveBeenCalledWith("shop", "101", expect.anything(), true);
  });
});
