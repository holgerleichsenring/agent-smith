import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { MetricStrip } from "../MetricStrip";
import type { MissionMetrics } from "../missionBuckets";

const base: MissionMetrics = {
  needsYou: 0,
  running: 0,
  queued: 0,
  finishedToday: 0,
  okToday: 0,
  failToday: 0,
  costTodayUsd: 0,
};

describe("MetricStrip", () => {
  it("MetricStrip_NeedsYouNonZero_RendersHotAmberCell", () => {
    render(<MetricStrip metrics={{ ...base, needsYou: 2 }} />);
    const cell = screen.getByTestId("metric-needs-you");
    expect(cell).toHaveTextContent("2");
    // p0343c: hot = the mock's .metric.hot amber wash
    expect(cell.className).toContain("hot");
  });

  it("MetricStrip_NeedsYouZero_IsNotHot", () => {
    render(<MetricStrip metrics={base} />);
    expect(screen.getByTestId("metric-needs-you").className).not.toContain("hot");
  });

  it("MetricStrip_CostToday_FormattedAsMoney", () => {
    render(<MetricStrip metrics={{ ...base, costTodayUsd: 3.5 }} />);
    expect(screen.getByTestId("metric-cost")).toHaveTextContent("$3.50");
  });

  it("MetricStrip_FinishedToday_ShowsOkFailSplit", () => {
    render(<MetricStrip metrics={{ ...base, finishedToday: 3, okToday: 2, failToday: 1 }} />);
    const cell = screen.getByTestId("metric-finished");
    expect(cell).toHaveTextContent("2 ✓");
    expect(cell).toHaveTextContent("1 ✗");
  });
});
