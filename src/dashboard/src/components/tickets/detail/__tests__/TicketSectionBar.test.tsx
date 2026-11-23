import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { TicketDetail } from "@/types/tickets";
import { ticket } from "@/components/tickets/__tests__/ticketFixtures";

// 2026-11-23-31dcc: the ticket bar's facts link the series that holds the ticket — also once it ended — and say
// nothing of a series for a ticket outside every series.

vi.mock("@/lib/tickets/TicketPageStore", () => ({
  useTicketSeries: () => ({ data: null }),
  useTicketDefaults: () => ({ data: null }),
  useTicketConversation: () => ({ data: null }),
}));

import { TicketSectionBar } from "../TicketSectionBar";
import { ticketSections } from "../ticketPageSections";

function detail(over: Partial<TicketDetail> = {}): TicketDetail {
  return { summary: ticket(), branch: "agentsmith/42", attempts: [], otherRuns: [], ...over };
}

function renderBar(d: TicketDetail) {
  return render(<TicketSectionBar detail={d} current={ticketSections[0]} onSelect={() => {}} />);
}

describe("TicketSectionBar", () => {
  it("TicketSectionBar_ATicketInASeries_LinksTheSeriesInItsFacts", () => {
    renderBar(detail({ seriesRun: { id: "sr-1", name: "Approval workflow" } }));

    const link = screen.getByTestId("ticket-series-run");
    expect(link).toHaveTextContent("Part of series Approval workflow");
    expect(link).toHaveAttribute("href", "/series/sr-1");
  });

  it("TicketSectionBar_ATicketOutsideEverySeries_ShowsNoSeries", () => {
    renderBar(detail({ seriesRun: null }));

    expect(screen.queryByTestId("ticket-series-run")).toBeNull();
  });
});
