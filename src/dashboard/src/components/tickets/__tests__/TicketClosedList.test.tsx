import { act, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { TicketListView, TicketSummary } from "@/types/tickets";
import { fakeHub, list, pr, ticket } from "./ticketFixtures";

// 2026-10-15-114fd: the closed, failed and PR-closed buckets on the list and the rail, the closed row's words, and
// Close / Reopen beside the row link.

const hoisted = vi.hoisted(() => ({ fetch: vi.fn(), apiFetch: vi.fn(), fetchRun: vi.fn(), lacks: false }));

vi.mock("next/navigation", () => ({ usePathname: () => "/tickets", useRouter: () => ({ push: vi.fn() }) }));
vi.mock("@/lib/ticketsApi", () => ({ fetchTickets: (...args: unknown[]) => hoisted.fetch(...args) }));
vi.mock("@/lib/runsApi", () => ({ fetchRun: (...args: unknown[]) => hoisted.fetchRun(...args) }));
vi.mock("@/hooks/useLacksPermission", () => ({ useLacksPermission: () => hoisted.lacks }));
vi.mock("@/lib/apiResponse", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/lib/apiResponse")>()),
  apiFetch: (...args: unknown[]) => hoisted.apiFetch(...args),
}));

import TicketsPage from "@/app/tickets/page";
import { TicketBucketFilterProvider } from "@/lib/TicketBucketFilter";
import { TicketListProvider } from "@/lib/tickets/TicketListProvider";
import { WorkRailSection } from "@/components/shell/WorkRailSection";

function renderTickets(view: TicketListView, search = "") {
  hoisted.fetch.mockResolvedValue(view);
  window.history.replaceState(null, "", `/tickets${search}`);
  return render(
    <TicketBucketFilterProvider>
      <TicketListProvider client={fakeHub().client}>
        <nav data-testid="rail"><WorkRailSection /></nav>
        <TicketsPage />
      </TicketListProvider>
    </TicketBucketFilterProvider>,
  );
}

function reply(status: number, body: string): Response {
  return { ok: status >= 200 && status < 300, status, text: async () => body, json: async () => JSON.parse(body) } as unknown as Response;
}

const ago = (minutes: number) => new Date(Date.now() - minutes * 60000).toISOString();
const ended = (status: string) => [{ runId: "r-1", status, endedOnInfrastructure: false, handedBack: false }];

function closedByHand(over: Partial<TicketSummary> = {}): TicketSummary {
  return ticket({
    ticketId: "88", bucket: "closed", kind: "by-hand", status: "failed", lastOutcome: "failed", attempts: ended("failed"),
    closed: { at: ago(120), by: "Ada Lovelace", note: "superseded by #90", closedPullRequests: [], trackerClosed: false }, ...over,
  });
}

function closedOnTracker(): TicketSummary {
  return ticket({ ticketId: "89", bucket: "closed", kind: "tracker", status: "failed", lastOutcome: "failed", attempts: ended("failed"), closed: null });
}

beforeEach(() => {
  hoisted.fetch.mockReset();
  hoisted.apiFetch.mockReset();
  hoisted.fetchRun.mockReset().mockResolvedValue(null);
  hoisted.lacks = false;
});

describe("TicketList, closed", () => {
  it("WorkRailSection_ClosedFailedPrClosed_AreRailItemsWithTheirCounts", async () => {
    renderTickets(list([ticket()], { working: 1, prClosed: 2, failed: 3, idle: 4, closed: 5 }));
    await screen.findByTestId("ticket-list");

    const rail = screen.getByTestId("rail");
    expect(within(rail).getByTestId("app-rail-count-Tickets")).toHaveTextContent("15");
    for (const [label, icon, n] of [["PR closed", "⊘", "2"], ["Failed", "✕", "3"], ["Ended", "■", "4"], ["Closed", "◌", "5"]]) {
      expect(within(rail).getByTestId(`app-rail-item-${label}`)).toHaveTextContent(icon);
      expect(within(rail).getByTestId(`app-rail-count-${label}`)).toHaveTextContent(n);
    }
    expect(within(rail).getByTestId("app-rail-item-Closed")).toHaveAttribute("href", "/tickets?bucket=closed");
    const order = [...document.querySelectorAll("section[data-testid]")].map((s) => (s as HTMLElement).dataset.testid);
    expect(order).toEqual(["ticket-section-needs-you", "ticket-section-working", "ticket-section-pr-closed", "ticket-section-failed",
      "ticket-section-ended", "ticket-section-closed"]);
  });

  it("TicketActivity_ClosedByHand_SaysWhoWhenAndNote", async () => {
    renderTickets(list([closedByHand()], { closed: 1 }), "?bucket=closed");
    const row = await screen.findByTestId("ticket-row-shop-88");
    expect(within(row).getByTestId("ticket-activity")).toHaveTextContent("closed by Ada Lovelace 2h ago: superseded by #90");
  });

  it("TicketActivity_ClosedOnTheTracker_SaysClosedOnTheTracker", async () => {
    renderTickets(list([closedOnTracker()], { closed: 1 }), "?bucket=closed");
    const row = await screen.findByTestId("ticket-row-shop-89");
    expect(within(row).getByTestId("ticket-activity")).toHaveTextContent("closed on the tracker by a person");
    expect(screen.queryByTestId("ticket-reopen-89")).toBeNull();
    expect(screen.queryByTestId("ticket-close-89")).toBeNull();
  });

  it("TicketList_ClosedBucket_ShowsWhoWhenAndNoteAndReopenAndIsCollapsed", async () => {
    renderTickets(list([ticket({ bucket: "stuck", status: "failed" }), closedByHand()], { stuck: 1, closed: 1 }));
    const section = await screen.findByTestId("ticket-section-closed");
    expect(screen.getByTestId("ticket-section-closed-count")).toHaveTextContent("1");
    expect(within(section).queryByTestId("ticket-row-shop-88")).toBeNull();
    expect(within(section).getByTestId("ticket-closed-line")).toHaveTextContent("1 closed ticket");

    fireEvent.click(within(section).getByTestId("ticket-closed-show"));
    const row = within(section).getByTestId("ticket-row-shop-88");
    expect(row).toHaveTextContent("closed by Ada Lovelace 2h ago: superseded by #90");
    expect(within(row).getByTestId("ticket-dot").className).toBe("sd closed");
    expect(within(section).getByTestId("ticket-reopen-88")).toBeInTheDocument();
    expect(within(section).queryByTestId("ticket-close-88")).toBeNull();
  });

  it("TicketRow_CloseAct_IsNotInsideTheLink", async () => {
    renderTickets(list([ticket({ ticketId: "7", bucket: "failed", status: "failed", lastOutcome: "failed", attempts: ended("failed") })], { failed: 1 }));
    const link = await screen.findByTestId("ticket-row-shop-7");
    const close = screen.getByTestId("ticket-close-7");
    expect(link.tagName).toBe("A");
    expect(link.contains(close)).toBe(false);
    expect(close.closest(".trow-wrap")).toBe(link.closest(".trow-wrap"));
    expect(close.closest(".trow-act")).not.toBeNull();
  });

  it("TicketReopen_Click_PostsReopenAndRereads", async () => {
    renderTickets(list([closedByHand()], { closed: 1 }), "?bucket=closed");
    await screen.findByTestId("ticket-row-shop-88");
    const reads = hoisted.fetch.mock.calls.length;
    hoisted.apiFetch.mockResolvedValue(reply(200, JSON.stringify({ project: "shop", ticketId: "88", ticketKey: "github-88" })));

    await act(async () => fireEvent.click(screen.getByTestId("ticket-reopen-88")));

    expect(String(hoisted.apiFetch.mock.calls[0][0])).toBe("/api/tickets/shop/88/reopen");
    expect(hoisted.apiFetch.mock.calls[0][1]).toMatchObject({ method: "POST" });
    await waitFor(() => expect(hoisted.fetch.mock.calls.length).toBeGreaterThan(reads));
  });

  it("TicketReopen_404_ShowsTheServersSentence", async () => {
    renderTickets(list([closedByHand()], { closed: 1 }), "?bucket=closed");
    await screen.findByTestId("ticket-row-shop-88");
    hoisted.apiFetch.mockResolvedValue(reply(404, "88 is not closed."));

    await act(async () => fireEvent.click(screen.getByTestId("ticket-reopen-88")));

    expect(await screen.findByTestId("ticket-reopen-said-88")).toHaveTextContent("88 is not closed.");
  });

  it("TicketRow_ClosedRequiredTicket_ChipSaysClosed", async () => {
    const waiting = ticket({
      ticketId: "59", bucket: "blocked", status: "", newestRunId: "", attemptCount: 0, cost: 0, attempts: [], pullRequests: [pr()],
      waitsFor: [{ ticketId: "88", project: "shop", key: "#88", title: "#88", closed: true }, { ticketId: "41", project: "shop", key: "#41", title: "#41" }],
    });
    renderTickets(list([waiting], { blocked: 1 }));
    expect(await screen.findByTestId("ticket-norun")).toHaveTextContent("waits for #88 (closed), #41");
  });
});
