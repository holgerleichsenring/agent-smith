import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { RollupCards } from "../RollupCards";
import { costKpis } from "../CostRollupCard";
import { activityKpis } from "../TodayActivityCard";

// p0209c: RollupCards is the presentational .kcard grid; both views reuse the
// existing rollup data sources via the costKpis / activityKpis extractors. We
// prop-drive the grid with values built from those same extractors so the test
// asserts the relocated KPIs without touching the live hubs.

describe("RollupCards", () => {
  it("RollupCards_Cost_RendersWindowKpis", () => {
    const kpis = costKpis({ today: 2.44, week: 18.07, llmCalls: 1914 });
    render(<RollupCards view="cost" kpis={kpis} />);

    expect(screen.getByTestId("kcard-cost-today")).toHaveTextContent("$2.44");
    expect(screen.getByTestId("kcard-cost-week")).toHaveTextContent("$18.07");
    expect(screen.getByTestId("kcard-cost-calls-7d")).toHaveTextContent("1,914");
  });

  it("RollupCards_TodayActivity_RendersCounterKpis", () => {
    const kpis = activityKpis({
      ticketsScanned: 4838,
      ticketsTriggered: 3,
      ticketsSkipped: 4835,
      webhooksReceived: 0,
      webhooksActioned: 0,
      pollCyclesStarted: 104,
      pollCyclesFinished: 104,
      eventsPerSource: {},
    });
    render(<RollupCards view="today" kpis={kpis} />);

    expect(screen.getByTestId("kcard-tickets-scanned")).toHaveTextContent("4838");
    expect(screen.getByTestId("kcard-tickets-triggered")).toHaveTextContent("3");
    expect(screen.getByTestId("kcard-tickets-skipped")).toHaveTextContent("4835");
    expect(screen.getByTestId("kcard-poll-cycles")).toHaveTextContent("104");
    expect(screen.getByTestId("kcard-webhooks-received")).toHaveTextContent("0");
  });
});
