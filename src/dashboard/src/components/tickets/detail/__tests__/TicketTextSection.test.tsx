import { act, fireEvent, render, screen, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fakeHub } from "../../__tests__/ticketFixtures";
import { attempt, conversation, detail, passes, series, ticketText } from "./detailFixtures";
import type { RunEvent } from "@/types/hub-events";
import type { TicketText } from "@/types/tickets";

// The ticket itself on the Summary tab: the description rendered in its tracker's format, the tracker's own
// criteria under their own label, twelve lines until "Show all", and a snapshot naming the attempt that read it.

const NONE: RunEvent[] = [];
vi.mock("@/hooks/useRunEvents", () => ({ useRunEvents: () => NONE }));
vi.mock("next/navigation", () => ({
  useSearchParams: () => new URLSearchParams(""),
  useRouter: () => ({ replace: vi.fn(), push: vi.fn() }),
  usePathname: () => "/tickets/shop/101",
}));

const { TicketPageStore } = await import("@/lib/tickets/TicketPageStore");
const { TicketSummaryTab } = await import("../TicketSummaryTab");

let text: TicketText;
const api = {
  detail: vi.fn(() => Promise.resolve(detail([attempt()]))),
  series: vi.fn((_p: string, _t: string, runId: string | null) => Promise.resolve(series(runId ?? "r-1", []))),
  conversation: vi.fn(() => Promise.resolve(conversation([]))),
  text: vi.fn(() => Promise.resolve(text)),
  passes: vi.fn((runId: string) => Promise.resolve(passes(runId, {}))),
  run: vi.fn(() => Promise.reject(new Error("unused"))),
  compute: vi.fn(() => Promise.reject(new Error("unused"))),
};

async function open() {
  render(<TicketPageStore project="shop" ticketId="101" client={fakeHub().client} api={api}><TicketSummaryTab /></TicketPageStore>);
  for (let i = 0; i < 3; i++) await act(() => vi.advanceTimersByTimeAsync(0));
  return screen.getByTestId("ticket-text");
}

function overflowing(scroll: number, client: number) {
  vi.spyOn(HTMLElement.prototype, "scrollHeight", "get").mockReturnValue(scroll);
  vi.spyOn(HTMLElement.prototype, "clientHeight", "get").mockReturnValue(client);
}

beforeEach(() => {
  vi.useFakeTimers();
  text = ticketText();
});
afterEach(() => {
  vi.useRealTimers();
  vi.restoreAllMocks();
});

describe("TicketTextSection", () => {
  it("ShowsTheDescriptionRendered_TheTrackersCriteriaUnderTheirOwnLabel_AndItsFields", async () => {
    text = ticketText({
      description: "<h2>Goal</h2><ul><li>Export the report</li></ul>", descriptionFormat: "html",
      acceptanceCriteria: "<p>One <b>CSV</b> file.</p>", criteriaFormat: "html",
      workItemType: "User Story", state: "Active", labels: ["export"], assignee: "Jane Operator",
    });
    const section = await open();
    const description = within(section).getByTestId("ticket-text-description");
    expect(within(description).getByRole("heading", { name: "Goal" })).toBeInTheDocument();
    expect(within(description).getByRole("listitem")).toHaveTextContent("Export the report");
    expect(description.textContent).not.toContain("<h2>");
    const criteria = within(section).getByTestId("ticket-text-criteria");
    expect(criteria).toHaveTextContent("Acceptance criteria (from the ticket)");
    expect(within(criteria).getByText("CSV").tagName).toBe("B");
    expect(within(section).getByTestId("ticket-text-type")).toHaveTextContent("User Story");
    expect(within(section).getByTestId("ticket-text-state")).toHaveTextContent("now: Active");
    expect(section).toHaveTextContent("assigned to Jane Operator");
  });

  it("ALongDescription_IsCollapsedUntilShowAll", async () => {
    overflowing(900, 300);
    const section = await open();
    const description = within(section).getByTestId("ticket-text-description");
    expect(description.className).toContain("clamp");
    fireEvent.click(within(section).getByRole("button", { name: "Show all" }));
    expect(description.className).not.toContain("clamp");
    expect(within(section).queryByRole("button", { name: "Show all" })).toBeNull();
  });

  it("AShortDescription_OffersNoShowAll", async () => {
    overflowing(100, 100);
    const section = await open();
    expect(within(section).queryByRole("button", { name: "Show all" })).toBeNull();
  });

  it("ASnapshot_NamesTheAttemptAndTimeItWasRead", async () => {
    text = ticketText({ source: "snapshot", state: null, runId: "r-1", attempt: 1, at: "2026-09-04T14:05:00Z" });
    const section = await open();
    expect(within(section).getByTestId("ticket-text-snapshot")).toHaveTextContent(/as attempt 1 read it, Sep 4 \d\d:05 — the tracker could not be read/);
    expect(within(section).queryByTestId("ticket-text-state")).toBeNull();
  });

  it("Unavailable_SaysWhyInWords", async () => {
    text = ticketText({ source: "unavailable", description: null, state: null, at: null, reason: "unauthorized" });
    const section = await open();
    expect(within(section).getByTestId("ticket-text-unavailable")).toHaveTextContent("the tracker refused this connection's credentials");
  });
});
