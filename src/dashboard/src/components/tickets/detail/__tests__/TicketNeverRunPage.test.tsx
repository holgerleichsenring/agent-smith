import { act, fireEvent, render, screen, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fakeHub, ticket } from "../../__tests__/ticketFixtures";
import { conversation, defaults, detail, reviews, series, ticketText } from "./detailFixtures";
import type { RunEvent } from "@/types/hub-events";
import type { TicketDetail, TicketRequirement, TicketRequires } from "@/types/tickets";

// 2026-10-14-dbcen: a ticket with no run on record (ticket-graph.html, never-run-page) — its header, what it waits
// for with a done-by-hand act per unlanded ticket, what waits for it, the filed line by kind and the Work it out
// link, and no runway or tabs; a mark re-reads the page's requires and detail at once.

const NONE: RunEvent[] = [];
vi.mock("@/hooks/useRunEvents", () => ({ useRunEvents: () => NONE }));
vi.mock("next/navigation", () => ({
  useSearchParams: () => new URLSearchParams(""),
  useRouter: () => ({ replace: vi.fn(), push: vi.fn() }),
  usePathname: () => "/tickets/shop/59",
}));
const perm = vi.hoisted(() => ({ lacks: false }));
vi.mock("@/hooks/useLacksPermission", () => ({ useLacksPermission: () => perm.lacks }));
const mark = vi.hoisted(() => ({ fn: vi.fn() }));
vi.mock("@/lib/ticketGraphApi", () => ({ markLandedByHand: (...args: unknown[]) => mark.fn(...args) }));

const { TicketPageStore } = await import("@/lib/tickets/TicketPageStore");
const { TicketPage } = await import("../TicketPage");

const entry = (over: Partial<TicketRequirement>): TicketRequirement => ({
  ticketId: "58", key: "#58", title: "Orders: API", state: "ready", hasPage: true, ...over,
});

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

const settle = (ms = 0) => act(() => vi.advanceTimersByTimeAsync(ms));

function neverRun(over: Partial<TicketDetail["summary"]> = {}): TicketDetail {
  return detail([], {
    summary: ticket({
      ticketId: "59", ticketKey: "github-59", title: "Orders: list screen", bucket: "blocked", status: "", newestRunId: "",
      attemptCount: 0, cost: 0, attempts: [], ticketText: true, waitsFor: [], ...over,
    }),
  });
}

async function open() {
  const { hub, client } = fakeHub();
  render(<TicketPageStore project="shop" ticketId="59" client={client} api={api}><TicketPage /></TicketPageStore>);
  await settle();
  await settle();
  return hub;
}

beforeEach(() => {
  vi.useFakeTimers();
  perm.lacks = false;
  mark.fn.mockReset();
  for (const fn of Object.values(api)) fn.mockClear();
  world.detail = neverRun();
  world.requires = {
    requires: [entry({}), entry({ ticketId: "41", key: "#41", title: "#41", state: "idle", hasPage: false })],
    requiredBy: [entry({ ticketId: "60", key: "#60", title: "Orders: export", state: "blocked" })],
  };
});
afterEach(() => vi.useRealTimers());

describe("TicketPage, never run", () => {
  it("TicketPage_ATicketWithNoAttempt_ShowsHeaderWaitsForRequiredByTheFiledLineAndTheWorkItOutLink", async () => {
    await open();

    expect(screen.getByTestId("ticket-head")).toHaveTextContent("Orders: list screen");
    expect(screen.getByTestId("ticket-head")).toHaveTextContent("filed");
    expect(screen.getByTestId("ticket-head").querySelector(".ticon.blocked")).not.toBeNull();
    expect(screen.getByTestId("ticket-waits-for")).toHaveTextContent("#58Orders: API (not merged)");
    expect(screen.getByTestId("ticket-required-by")).toHaveTextContent("#60Orders: export (blocked)");
    expect(screen.getByTestId("ticket-filed-line")).toHaveTextContent("its specs are derived when its first run starts");
    expect(screen.getByTestId("ticket-settle-specs")).toHaveAttribute("href", "/spec-dialog?ticket=59&project=shop");
    expect(screen.queryByTestId("series-runway")).toBeNull();
    expect(document.querySelector(".tabs")).toBeNull();
    expect(screen.queryByTestId("ticket-continue")).toBeNull();
  });

  it("TicketPage_AFiledSpecWithNoRun_SaysOnlyFiledNoRunOnRecord", async () => {
    world.detail = neverRun({ ticketText: false, bucket: "filed" });
    await open();

    expect(screen.getByTestId("ticket-filed-line")).toHaveTextContent(/^Filed, no run on record\s*Settle/);
  });

  it("TicketWaitsFor_UnlandedRequirementWithoutAPage_CanBeMarkedDoneByHand", async () => {
    mark.fn.mockResolvedValue({ kind: "landed" });
    await open();

    const row = screen.getByTestId("ticket-waits-41");
    expect(within(row).queryByRole("link")).toBeNull();
    fireEvent.click(within(row).getByTestId("done-by-hand-41"));
    const dialog = screen.getByTestId("done-by-hand-dialog");
    expect(dialog).toHaveTextContent("Mark #41 done by hand");
    fireEvent.change(within(dialog).getByTestId("done-by-hand-note"), { target: { value: " merged in the auth repo " } });
    fireEvent.click(within(dialog).getByTestId("done-by-hand-send"));
    await settle();

    expect(mark.fn).toHaveBeenCalledWith("shop", "41", "merged in the auth repo");
    expect(screen.queryByTestId("done-by-hand-dialog")).toBeNull();
  });

  it("TicketWaitsFor_MarkedDoneByHand_ReReadsAndShowsTheLanding", async () => {
    mark.fn.mockResolvedValue({ kind: "landed" });
    await open();
    expect(api.requires).toHaveBeenCalledTimes(1);
    expect(api.detail).toHaveBeenCalledTimes(1);
    world.requires = {
      ...world.requires,
      requires: [entry({}), entry({
        ticketId: "41", key: "#41", title: "#41", state: "done-by-hand", hasPage: false, landedKind: "by-hand",
        landedBy: "Ada Lovelace", landedAt: new Date(Date.now() - 5 * 60000).toISOString(), note: "merged in the auth repo",
      })],
    };

    fireEvent.click(screen.getByTestId("done-by-hand-41"));
    fireEvent.change(screen.getByTestId("done-by-hand-note"), { target: { value: "merged in the auth repo" } });
    fireEvent.click(screen.getByTestId("done-by-hand-send"));
    await settle();
    await settle();

    expect(api.requires).toHaveBeenCalledTimes(2);
    expect(api.detail).toHaveBeenCalledTimes(2);
    expect(screen.getByTestId("ticket-waits-41")).toHaveTextContent("(done by hand)");
    expect(screen.getByTestId("ticket-landed-by-41")).toHaveTextContent("by Ada Lovelace, 5m ago — merged in the auth repo");
    expect(screen.queryByTestId("done-by-hand-41")).toBeNull();
  });

  it("DoneByHandAct_HiddenWithoutRunsControlAndOnALandedTicket", async () => {
    world.detail = neverRun({ bucket: "done" });
    world.requires = { requires: [entry({ landedKind: "merged", state: "merged" })], requiredBy: [] };
    await open();
    expect(screen.queryByTestId("done-by-hand-59")).toBeNull();
    expect(screen.queryByTestId("done-by-hand-58")).toBeNull();
  });

  it("DoneByHandAct_HiddenFromACallerKnownToLackRunsControl", async () => {
    perm.lacks = true;
    await open();
    expect(screen.queryByTestId("done-by-hand-59")).toBeNull();
    expect(screen.queryByTestId("done-by-hand-41")).toBeNull();
  });

  it("DoneByHandAct_AnEmptyNoteCannotBeSent_AndEveryRefusalIsTheServersSentence", async () => {
    const sentences = [
      "58 is already marked done by Ada Lovelace at 2026-10-13 10:00:00Z: shipped by hand.",
      "58 already counts as landed: merged on 2026-10-13 10:00:00Z.",
      "No ticket 58 on gh.",
      "The tracker gh could not be read (unreachable); nothing was recorded.",
    ];
    await open();
    fireEvent.click(screen.getByTestId("done-by-hand-59"));
    const send = screen.getByTestId("done-by-hand-send");
    expect(send).toBeDisabled();
    fireEvent.change(screen.getByTestId("done-by-hand-note"), { target: { value: "   " } });
    expect(send).toBeDisabled();
    fireEvent.change(screen.getByTestId("done-by-hand-note"), { target: { value: "shipped" } });
    for (const text of sentences) {
      mark.fn.mockResolvedValueOnce({ kind: "said", text });
      fireEvent.click(send);
      await settle();
      expect(screen.getByTestId("done-by-hand-said")).toHaveTextContent(text);
    }
    expect(screen.getByTestId("done-by-hand-dialog")).toBeInTheDocument();
  });
});
