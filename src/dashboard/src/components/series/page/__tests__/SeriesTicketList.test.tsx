import { fireEvent, render, screen, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { SeriesTicketList } from "../SeriesTicketList";
import { overviewDetail } from "./seriesFixtures";

// 2026-11-27-ecf2a: the rail's ticket list — the meter, the counts, the filters, the rows and what pointing and clicking report.

describe("SeriesTicketList", () => {
  it("SeriesTicketList_ShowsTheMeterCountsAndOneRowPerTicket", () => {
    render(<SeriesTicketList detail={overviewDetail()} hovered={null} onHover={vi.fn()} onOpen={vi.fn()} />);

    expect(screen.getByTestId("series-rail-counts")).toHaveTextContent("1 of 9 merged · 2 need you · 1 waiting");
    expect(screen.getByTestId("series-meter-merged").style.width).toBe(`${100 / 9}%`);
    expect(screen.getAllByTestId(/^series-row-\d+$/)).toHaveLength(9);
    expect(screen.getByTestId("series-row-state-62")).toHaveTextContent("waits on #51 and #55");
    expect(screen.getByTestId("series-row-state-61")).toHaveTextContent("question / plan");
    expect(screen.queryByTestId("series-row-state-48")).toBeNull();
    expect(within(screen.getByTestId("series-row-48")).getByText("#48")).toBeInTheDocument();
  });

  it("SeriesTicketList_Filters_SplitMergedFromOpen", () => {
    render(<SeriesTicketList detail={overviewDetail()} hovered={null} onHover={vi.fn()} onOpen={vi.fn()} />);

    fireEvent.click(screen.getByTestId("series-filter-merged"));
    expect(screen.getAllByTestId(/^series-row-\d+$/).map((r) => r.dataset.testid)).toEqual(["series-row-48"]);
    expect(screen.getByTestId("series-filter-merged")).toHaveAttribute("aria-pressed", "true");
    fireEvent.click(screen.getByTestId("series-filter-open"));
    expect(screen.queryByTestId("series-row-48")).toBeNull();
    expect(screen.getAllByTestId(/^series-row-\d+$/)).toHaveLength(8);
  });

  it("SeriesTicketList_PointingAndClicking_ReportTheTicket", () => {
    const onHover = vi.fn();
    const onOpen = vi.fn();
    render(<SeriesTicketList detail={overviewDetail()} hovered="50" onHover={onHover} onOpen={onOpen} />);

    expect(screen.getByTestId("series-row-50")).toHaveClass("on");
    fireEvent.mouseEnter(screen.getByTestId("series-row-56"));
    expect(onHover).toHaveBeenLastCalledWith("56");
    fireEvent.mouseLeave(screen.getByRole("list"));
    expect(onHover).toHaveBeenLastCalledWith(null);
    fireEvent.click(screen.getByTestId("series-row-56"));
    expect(onOpen).toHaveBeenCalledWith("56", screen.getByTestId("series-row-56"));
  });
});
