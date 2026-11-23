import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { SeriesOrderSection } from "../SeriesOrderSection";
import { seriesDetail, seriesTicket } from "./seriesFixtures";

// 2026-11-23-31dcf: the Order and branches section — every edge drawn by its kind, every ticket framed by its state, and
// the course from one ticket dims everything outside its up- and downstream until Show everything.

const detail = seriesDetail({
  tickets: [
    seriesTicket({ ticketId: "1", key: "#1", landed: true }),
    seriesTicket({ ticketId: "2", key: "#2", depth: 1, pullRequests: [{ repo: "server", number: "7", url: "u", state: "open", ci: null, ciFailure: null, ciLog: null, target: null }] }),
    seriesTicket({ ticketId: "3", key: "#3", depth: 1, repos: [], status: "waiting_for_input" }),
    seriesTicket({ ticketId: "4", key: "#4", depth: 2, repos: [] }),
    seriesTicket({ ticketId: "5", key: "#5", repos: [] }),
  ],
  mergeOrder: [{ ticketId: "2", key: "#2", depth: 1, wait: "ready", after: [] }],
  edges: [
    { ticketId: "2", requiredTicketId: "1", kind: "landed" },
    { ticketId: "3", requiredTicketId: "1", kind: "landed" },
    { ticketId: "4", requiredTicketId: "2", kind: "stacked" },
    { ticketId: "4", requiredTicketId: "3", kind: "unknown" },
    { ticketId: "4", requiredTicketId: "5", kind: "merge" },
  ],
});

describe("SeriesOrderSection", () => {
  it("SeriesOrderSection_DrawsEdgesByKind_AndTicketsByState", () => {
    render(<SeriesOrderSection detail={detail} />);

    expect(screen.getByTestId("series-edge-2-4")).toHaveClass("sr-edge", "stacked");
    expect(screen.getByTestId("series-edge-5-4")).toHaveClass("merge");
    expect(screen.getByTestId("series-edge-3-4")).toHaveClass("unknown");
    expect(screen.getByTestId("series-edge-1-2")).toHaveClass("landed");
    expect(screen.getByTestId("series-node-1")).toHaveClass("merged");
    expect(screen.getByTestId("series-node-2")).toHaveClass("next");
    expect(screen.getByTestId("series-node-3")).toHaveClass("question");
    expect(screen.getByTestId("series-node-4")).toHaveClass("notStarted");
    expect(screen.getByTestId("series-node-4")).toHaveAccessibleName("#4 Approval contracts · not started");
    expect(screen.getByTestId("series-lane-server")).toHaveTextContent("server");
    expect(screen.getByTestId("series-lane-none")).toHaveTextContent("No pull request yet");
  });

  it("SeriesOrderSection_CourseFromATicket_DimsEverythingOutsideItsUpAndDownstream", () => {
    render(<SeriesOrderSection detail={detail} />);

    fireEvent.click(screen.getByTestId("series-node-2"));

    expect(screen.getByTestId("series-order-from")).toHaveValue("2");
    for (const lit of ["1", "2", "4"]) expect(screen.getByTestId(`series-node-${lit}`)).not.toHaveClass("dim");
    for (const dim of ["3", "5"]) expect(screen.getByTestId(`series-node-${dim}`)).toHaveClass("dim");
    expect(screen.getByTestId("series-edge-2-4")).not.toHaveClass("dim");
    expect(screen.getByTestId("series-edge-3-4")).toHaveClass("dim");

    fireEvent.click(screen.getByTestId("series-order-reset"));

    expect(screen.getByTestId("series-node-3")).not.toHaveClass("dim");
    expect(screen.getByTestId("series-order-from")).toHaveValue("");

    fireEvent.click(screen.getByTestId("series-node-4"));
    fireEvent.click(screen.getByTestId("series-node-4"));

    expect(screen.getByTestId("series-node-1")).not.toHaveClass("dim");
    expect(screen.getByTestId("series-order-from")).toHaveValue("");
  });

  it("SeriesOrderSection_PickingATicketInTheSelect_ShowsItsCourse", () => {
    render(<SeriesOrderSection detail={detail} />);

    fireEvent.change(screen.getByTestId("series-order-from"), { target: { value: "5" } });

    expect(screen.getByTestId("series-node-5")).not.toHaveClass("dim");
    expect(screen.getByTestId("series-node-4")).not.toHaveClass("dim");
    expect(screen.getByTestId("series-node-2")).toHaveClass("dim");
  });
});
