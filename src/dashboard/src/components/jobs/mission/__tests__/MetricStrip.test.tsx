import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { MetricStrip } from "../MetricStrip";
import type { MissionMetrics } from "../missionBuckets";
import type { RunSpend } from "@/lib/runSpendApi";

const base: MissionMetrics = {
  needsYou: 0,
  running: 0,
  queued: 0,
  finishedToday: 0,
  okToday: 0,
  failToday: 0,
};

const spend: RunSpend = {
  from: "", totalUsd: 12.65, work: { costUsd: 10.65, runs: 5 }, system: { costUsd: 2, runs: 206 }, llmCalls: 0, slices: [],
};

describe("MetricStrip", () => {
  it("MetricStrip_NeedsYouNonZero_RendersHotAmberCell", () => {
    render(<MetricStrip metrics={{ ...base, needsYou: 2 }} spend={null} />);
    const cell = screen.getByTestId("metric-needs-you");
    expect(cell).toHaveTextContent("2");
    // p0343c: hot = the mock's .metric.hot amber wash
    expect(cell.className).toContain("hot");
  });

  it("MetricStrip_NeedsYouZero_IsNotHot", () => {
    render(<MetricStrip metrics={base} spend={null} />);
    expect(screen.getByTestId("metric-needs-you").className).not.toContain("hot");
  });

  it("MetricStrip_CostToday_IsTheRollupWithWorkAndSystemApart", () => {
    // 2026-11-24-0fbfb: the server's rollup, whatever the list shows.
    render(<MetricStrip metrics={base} spend={spend} />);
    expect(screen.getByTestId("metric-cost")).toHaveTextContent("$12.65");
    expect(screen.getByTestId("metric-cost")).toHaveTextContent("work $10.65 · system $2.00");
  });

  it("MetricStrip_CostTodayNotRead_IsADash", () => {
    render(<MetricStrip metrics={base} spend={null} />);
    expect(screen.getByTestId("metric-cost")).toHaveTextContent("—");
  });

  it("MetricStrip_FinishedToday_ShowsOkFailSplit", () => {
    render(<MetricStrip metrics={{ ...base, finishedToday: 3, okToday: 2, failToday: 1 }} spend={null} />);
    const cell = screen.getByTestId("metric-finished");
    expect(cell).toHaveTextContent("2 ✓");
    expect(cell).toHaveTextContent("1 ✗");
  });
});
