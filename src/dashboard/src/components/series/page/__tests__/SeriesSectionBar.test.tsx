import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { SeriesSectionBar } from "../SeriesSectionBar";
import { seriesSectionOf, seriesSections } from "../seriesPageSections";
import { seriesDetail } from "./seriesFixtures";

// 2026-11-23-31dcd: the series bar — back to the list, name, state with the question, counts, the sections with their
// counters, and the facts at its foot.

describe("SeriesSectionBar", () => {
  it("SeriesSectionBar_RendersNameStateCountersAndFacts", () => {
    render(<SeriesSectionBar detail={seriesDetail({ question: "Keep it?" })} current={seriesSectionOf("tickets")} onSelect={() => {}} />);

    expect(screen.getByTestId("series-back")).toHaveAttribute("href", "/series");
    expect(screen.getByTestId("series-head")).toHaveTextContent("series · 3 tickets");
    expect(screen.getByTestId("series-head")).toHaveTextContent("Approval workflow");
    expect(screen.getByTestId("series-state-word")).toHaveTextContent("running · 1 question for you");
    expect(screen.getByTestId("series-counts")).toHaveTextContent("1 of 3 merged · 1 PRs waiting · 1 not started");
    expect(screen.getByTestId("series-section-tickets")).toHaveTextContent("Tickets3");
    expect(screen.getByTestId("series-section-tickets")).toHaveAttribute("aria-current", "page");
    expect(screen.getByTestId("series-repos")).toHaveTextContent("server · worker");
    expect(screen.getByTestId("series-conversation-link")).toHaveAttribute("href", "/spec-dialog?open=conv-1&on=d-thread1");
    expect(screen.getByTestId("series-facts")).toHaveTextContent("$41.20 so far");
  });

  it("SeriesSectionBar_ASection_IsSelectedByItsId", () => {
    const onSelect = vi.fn();
    render(<SeriesSectionBar detail={seriesDetail()} current={seriesSections[0]} onSelect={onSelect} />);

    fireEvent.click(screen.getByTestId("series-section-talk"));

    expect(onSelect).toHaveBeenCalledWith("talk");
  });
});
