import { fireEvent, render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { TicketListView } from "@/types/tickets";
import { list } from "@/components/tickets/__tests__/ticketFixtures";

// 2026-11-16-0440c: the top bar — the field goes to a page by name, ⌘K puts the cursor in it,
// and the bell opens what waits on you with a dot while anything does.

const hoisted = vi.hoisted(() => ({ push: vi.fn(), tickets: null as TicketListView | null }));
vi.mock("next/navigation", () => ({ useRouter: () => ({ push: hoisted.push }) }));
vi.mock("@/lib/tickets/TicketListProvider", () => ({ useTicketList: () => ({ list: hoisted.tickets }) }));

import { AppTopBar, placeForQuery, SEARCHABLE_PLACES } from "../AppTopBar";

beforeEach(() => {
  hoisted.push.mockReset();
  hoisted.tickets = list([], { needsYou: 2 });
});

describe("AppTopBar", () => {
  it("PlaceForQuery_ExactNameFirst_ThenAPrefix_CaseBlind", () => {
    expect(placeForQuery("delivery")).toBe("/delivery");
    expect(placeForQuery("  Pull ")).toBe("/pull-requests");
    expect(placeForQuery("Runs")).toBe("/");
    expect(placeForQuery("nothing by this name")).toBeNull();
    expect(placeForQuery("   ")).toBeNull();
  });

  it("SearchablePlaces_OnePathPerName", () => {
    const names = SEARCHABLE_PLACES.map((p) => p.name);
    expect(new Set(names).size).toBe(names.length);
    expect(names).toContain("Tickets");
  });

  it("Search_EnterGoesToTheNamedPage_AndClearsTheField", () => {
    render(<AppTopBar />);
    const field = screen.getByTestId("topbar-search");
    fireEvent.change(field, { target: { value: "Overview" } });
    fireEvent.submit(field.closest("form")!);
    expect(hoisted.push).toHaveBeenCalledWith("/overview");
    expect(field).toHaveValue("");
  });

  it("Search_TextNamingNoPage_StaysPut", () => {
    render(<AppTopBar />);
    const field = screen.getByTestId("topbar-search");
    fireEvent.change(field, { target: { value: "zzz" } });
    fireEvent.submit(field.closest("form")!);
    expect(hoisted.push).not.toHaveBeenCalled();
  });

  it("CommandK_FocusesTheField", () => {
    render(<AppTopBar />);
    fireEvent.keyDown(window, { key: "k", metaKey: true });
    expect(screen.getByTestId("topbar-search")).toHaveFocus();
  });

  it("Bell_OpensNeedsYou_WithADotWhileAnythingWaits", () => {
    const view = render(<AppTopBar />);
    expect(screen.getByTestId("topbar-bell")).toHaveAttribute("href", "/tickets?bucket=needs-you");
    expect(screen.getByTestId("topbar-bell")).toHaveAttribute("aria-label", "2 waiting on you");
    expect(screen.getByTestId("topbar-bell-dot")).toBeInTheDocument();
    view.unmount();

    hoisted.tickets = list([], { needsYou: 0 });
    render(<AppTopBar />);
    expect(screen.queryByTestId("topbar-bell-dot")).not.toBeInTheDocument();
  });
});
