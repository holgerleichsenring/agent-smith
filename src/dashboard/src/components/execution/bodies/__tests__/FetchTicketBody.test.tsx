import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { FetchTicketBody } from "../FetchTicketBody";
import { EventType, type RunEvent } from "@/types/hub-events";

const RUN_ID = "r1";

function ticketEvent(overrides: Partial<Extract<RunEvent, { type: EventType.TicketFetched }>> = {}) {
  return {
    runId: RUN_ID,
    type: EventType.TicketFetched as const,
    timestamp: "2026-05-31T20:13:44.000Z",
    ticketId: "18794",
    title: "Fix refresh-token expiry",
    description: "The endpoint accepts expired tokens.",
    state: "Active",
    labels: ["AuthPort", "bug"],
    attachmentCount: 2,
    source: "azuredevops",
    ...overrides,
  };
}

describe("FetchTicketBody", () => {
  it("FetchTicketBody_RendersTicketIdTitleStateDescription", () => {
    render(<FetchTicketBody events={[ticketEvent()]} />);
    expect(screen.getByTestId("fetch-ticket-body-id")).toHaveTextContent("#18794");
    expect(screen.getByTestId("fetch-ticket-body-title")).toHaveTextContent("Fix refresh-token expiry");
    expect(screen.getByTestId("fetch-ticket-body-state")).toHaveTextContent("Active");
    expect(screen.getByTestId("fetch-ticket-body-description")).toHaveTextContent(
      "The endpoint accepts expired tokens.",
    );
  });

  it("FetchTicketBody_AttachmentCountChip_RendersCount", () => {
    render(<FetchTicketBody events={[ticketEvent({ attachmentCount: 3 })]} />);
    expect(screen.getByTestId("fetch-ticket-body-attachments")).toHaveTextContent("3 attachments");
  });

  it("FetchTicketBody_NoTicketEvents_ShowsWaitingPlaceholder", () => {
    render(<FetchTicketBody events={[]} />);
    expect(screen.getByText(/waiting for ticket fetch/i)).toBeInTheDocument();
  });

  it("FetchTicketBody_PicksLatestTicketEvent_WhenMultiplePresent", () => {
    const events: RunEvent[] = [
      ticketEvent({ timestamp: "2026-05-31T20:13:00.000Z", title: "Old" }),
      ticketEvent({ timestamp: "2026-05-31T20:14:00.000Z", title: "New" }),
    ];
    render(<FetchTicketBody events={events} />);
    expect(screen.getByTestId("fetch-ticket-body-title")).toHaveTextContent("New");
  });

  it("FetchTicketBody_TruncatesLongDescription_To320Chars", () => {
    const longDescription = "a".repeat(500);
    render(<FetchTicketBody events={[ticketEvent({ description: longDescription })]} />);
    const desc = screen.getByTestId("fetch-ticket-body-description").textContent ?? "";
    expect(desc.length).toBeLessThanOrEqual(320);
    expect(desc.endsWith("…")).toBe(true);
  });
});
