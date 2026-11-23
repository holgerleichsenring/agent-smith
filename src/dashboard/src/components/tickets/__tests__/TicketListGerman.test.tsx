import { render, screen, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { TicketListView } from "@/types/tickets";
import { fakeHub, list, pr, ticket } from "./ticketFixtures";

// 2026-11-23-5669b: the ticket list in German — sections, tiles, pills and a row's activity read from the dictionary;
// the ticket's title is shown as the tracker wrote it.

const hoisted = vi.hoisted(() => ({ fetch: vi.fn() }));

vi.mock("@/hooks/useHoldsAdmin", () => ({ useHoldsAdmin: () => false }));
vi.mock("@/hooks/useProjectReach", () => ({ useProjectReach: () => "unknown" }));
vi.mock("next/navigation", () => ({ usePathname: () => "/tickets", useRouter: () => ({ push: vi.fn() }) }));
vi.mock("@/lib/ticketsApi", () => ({ fetchTickets: (...args: unknown[]) => hoisted.fetch(...args) }));

import TicketsPage from "@/app/tickets/page";
import { resetLanguage } from "@/lib/i18n/language";
import { TicketBucketFilterProvider } from "@/lib/TicketBucketFilter";
import { TicketListProvider } from "@/lib/tickets/TicketListProvider";

function renderTickets(all: TicketListView) {
  hoisted.fetch.mockResolvedValue(all);
  window.history.replaceState(null, "", "/tickets");
  return render(
    <TicketBucketFilterProvider>
      <TicketListProvider client={fakeHub().client}>
        <TicketsPage />
      </TicketListProvider>
    </TicketBucketFilterProvider>,
  );
}

const ago = (minutes: number) => new Date(Date.now() - minutes * 60000).toISOString();

beforeEach(() => resetLanguage("de"));
afterEach(() => resetLanguage());

describe("Ticket list in German", () => {
  it("TicketList_German_WordsSectionsTilesAndRows", async () => {
    const stuck = ticket({
      ticketId: "7", ticketKey: "GH-7", title: "Fix the login", bucket: "stuck", status: "failed", attemptCount: 2,
      since: ago(180), attempts: [
        { runId: "r-1", status: "failed", endedOnInfrastructure: false, handedBack: false },
        { runId: "r-2", status: "failed", endedOnInfrastructure: false, handedBack: false },
      ],
      pullRequests: [pr({ number: "809", state: "open" })],
    });
    renderTickets(list([stuck], { needsYou: 0, working: 0, stuck: 1 }));

    const section = await screen.findByTestId("ticket-section-stuck");
    expect(within(section).getByRole("heading")).toHaveTextContent("Hängt");
    expect(screen.getByTestId("ticket-section-needs-you")).toHaveTextContent("Nichts wartet auf dich.");
    expect(screen.getByTestId("ticket-metric-spent")).toHaveTextContent("Diesen Monat ausgegeben");
    expect(screen.getByTestId("ticket-pills")).toHaveTextContent("Alle");

    const row = screen.getByTestId("ticket-row-shop-7");
    expect(row).toHaveTextContent("Fix the login");
    expect(within(row).getByTestId("ticket-activity")).toHaveTextContent("2 Versuche fehlgeschlagen");
    expect(within(row).getByTestId("ticket-activity")).toHaveTextContent("PR #809 offen · kein Review");
    expect(row).toHaveTextContent("2 Versuche");
    expect(row).toHaveTextContent("3 Std.");
  });
});
