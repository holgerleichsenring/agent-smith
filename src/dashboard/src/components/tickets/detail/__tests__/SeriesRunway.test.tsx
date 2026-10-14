import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { TicketAttempt, TicketSeriesBudget } from "@/types/tickets";
import { SeriesRunway } from "../SeriesRunway";
import { GO_ON } from "../seriesWords";
import { attempt, budget } from "./detailFixtures";

// 2026-10-13-8762w: the series strip says what the server decided — spend and run time against the caps, the
// chain with its automatic starts, and whether the next attempt starts by itself or why it holds.

const chain: TicketAttempt[] = [
  attempt({ number: 1, runId: "r-1" }),
  attempt({ number: 2, runId: "r-2", cause: { channel: "ticket_comment", actor: "m.reviewer" } }),
  attempt({ number: 3, runId: "r-3", cause: { channel: "series", actor: "agent-smith" } }),
  attempt({ number: 4, runId: "r-4", cause: { channel: "series", actor: "agent-smith" } }),
];

function show(over: Partial<TicketSeriesBudget> = {}, onDefaults = vi.fn()) {
  render(<SeriesRunway budget={budget({ chainFrom: 2, ...over })} attempts={chain} onDefaults={onDefaults} />);
  return onDefaults;
}

const next = () => screen.getByTestId("series-next");

describe("SeriesRunway", () => {
  it("SeriesRunway_Start_ShowsSpendRunTimeAndStartsByItself", () => {
    show();
    expect(screen.getByTestId("series-spend")).toHaveTextContent("$42.50 of $150.00");
    expect(screen.getByTestId("series-time")).toHaveTextContent("3.3 h of 12 h run time");
    expect(next()).toHaveTextContent("the next attempt starts by itself");
    expect(screen.getByTestId("series-runway")).toHaveAttribute("data-next", "start");
  });

  it("SeriesRunway_HeldOnBudget_NamesTheReasonAndHowToGoOn_WithoutRetry", () => {
    show({ next: "hold", heldBecause: "budget-usd", spentUsd: 151 });
    expect(next()).toHaveTextContent("held — the chain of attempts has spent the series budget");
    expect(screen.getByTestId("series-go-on")).toHaveTextContent(GO_ON);
    expect(screen.getByTestId("series-runway").textContent).not.toMatch(/retry/i);
  });

  it("SeriesRunway_HeldOnDefault_PointsAtTheDefaultsTab", () => {
    const onDefaults = show({ next: "hold", heldBecause: "default-pending" });
    expect(next()).toHaveTextContent("held — a default waits for a person");
    const go = screen.getByTestId("series-go-defaults");
    expect(go).toHaveTextContent("Ratify it in the Defaults tab.");
    expect(go).toHaveAttribute("aria-controls", "tab-defaults");
    fireEvent.click(go);
    expect(onDefaults).toHaveBeenCalledTimes(1);
  });

  it("SeriesRunway_HeldRefusedOrClosed_SaysSo", () => {
    show({ next: "hold", heldBecause: "refused" });
    expect(next()).toHaveTextContent("held — the last automatic start was refused");
    expect(next()).toHaveTextContent(GO_ON);
  });

  it("SeriesRunway_HeldClosed_SaysAPersonClosedTheTicket", () => {
    show({ next: "hold", heldBecause: "closed" });
    expect(next()).toHaveTextContent("held — a person closed the ticket");
  });

  it("SeriesRunway_HeldMisconfigured_PointsAtTheSeriesSettings", () => {
    show({ next: "hold", heldBecause: "misconfigured" });
    expect(screen.getByTestId("series-go-settings")).toHaveAttribute("href", "/config/settings/series");
  });

  it("SeriesRunway_Complete_SaysTheSeriesIsComplete", () => {
    show({ next: "complete" });
    expect(next()).toHaveTextContent("the series is complete");
    expect(screen.queryByTestId("series-go-on")).toBeNull();
  });

  it("SeriesRunway_Chain_MarksAutomaticAttempts", () => {
    show();
    const strip = screen.getByTestId("series-chain");
    expect(strip).toHaveTextContent("A2 → autoA3 → autoA4");
    expect(screen.queryByTestId("series-chain-a1")).toBeNull();
    expect(strip.querySelectorAll('[data-auto="true"]')).toHaveLength(2);
  });

  it("SeriesRunway_BarAtCap_IsHot", () => {
    show({ spentUsd: 150, elapsedHours: 4 });
    expect(screen.getByTestId("series-spend")).toHaveAttribute("data-hot", "true");
    expect(screen.getByTestId("series-spend")).toHaveClass("hot");
    expect(screen.getByTestId("series-time")).toHaveAttribute("data-hot", "false");
  });

  it("SeriesRunway_NoBudgetOrOff_RendersNothing", () => {
    const { container } = render(<SeriesRunway budget={null} attempts={chain} onDefaults={vi.fn()} />);
    expect(container).toBeEmptyDOMElement();
    render(<SeriesRunway budget={budget({ next: "hold", heldBecause: "off" })} attempts={chain} onDefaults={vi.fn()} />);
    expect(screen.queryByTestId("series-runway")).toBeNull();
  });
});
