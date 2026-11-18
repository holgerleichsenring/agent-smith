import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { TicketSeriesSpec } from "@/types/tickets";

const post = vi.fn();
vi.mock("@/lib/ticketsApi", () => ({ postFollowUp: (...args: unknown[]) => post(...args) }));
vi.mock("@/lib/tickets/TicketPageStore", () => ({ useTicketDetail: () => ({ project: "shop", ticketId: "7" }) }));

import { MasterReportText, TicketFollowUps } from "../TicketFollowUps";

// 2026-11-18-c8e2d: the follow-ups the masters proposed, and filing one as a ticket.

const spec = (followUps: TicketSeriesSpec["followUps"], report: string | null = null): TicketSeriesSpec => ({
  specId: "s2", ordinal: 2, fileStem: null, goal: "g", repos: [], steps: [], yaml: "", state: "done",
  executedBy: "r-1", executedByAttempt: 1, done: [], unmeasured: [], followUps, report,
});

describe("TicketFollowUps", () => {
  it("TicketFollowUps_NoneProposed_RendersNothing", () => {
    const { container } = render(<TicketFollowUps specs={[spec(null)]} />);
    expect(container).toBeEmptyDOMElement();
  });

  it("TicketFollowUps_FileAsATicket_PostsTheRunSpecAndPlace_AndSaysFiled", async () => {
    post.mockResolvedValue({ ticketId: "8", key: "#8", url: "https://t/8" });
    render(<TicketFollowUps specs={[spec([{ goal: "Exports stream CSV", why: "paging first", runId: "r-1", index: 0 }])]} />);

    expect(screen.getByTestId("acceptance-follow-up")).toHaveTextContent("Exports stream CSV");
    fireEvent.click(screen.getByTestId("follow-up-file"));

    await waitFor(() => expect(screen.getByTestId("follow-up-filed")).toHaveTextContent("filed as #8"));
    expect(post).toHaveBeenCalledWith("shop", "7", { runId: "r-1", specId: "s2", index: 0 });
  });

  it("MasterReportText_RendersTheSpecsReportAsMarkdown_NothingWithoutOne", () => {
    const { container, rerender } = render(<MasterReportText spec={spec(null)} />);
    expect(container).toBeEmptyDOMElement();
    rerender(<MasterReportText spec={spec(null, "## Delivered\n\n- the 404 handler")} />);
    const report = screen.getByTestId("master-report-s2");
    expect(report.querySelector("li")).toHaveTextContent("the 404 handler");
    expect(report).toHaveTextContent("Delivered");
    expect(report).not.toHaveTextContent("##");
  });
});
