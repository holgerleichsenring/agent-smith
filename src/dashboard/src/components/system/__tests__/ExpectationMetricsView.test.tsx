import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi, beforeEach } from "vitest";
import { ExpectationMetricsView } from "@/components/system/ExpectationMetricsView";
import * as api from "@/lib/expectationsApi";

// p0329: the expectation-metrics rollup — populated projects render both
// headline rates (null hit rate renders as an honest dash, never 0%), and an
// empty backend renders the honest empty-state instead of zero-metrics.

vi.mock("@/lib/expectationsApi", () => ({ fetchExpectationMetrics: vi.fn() }));

const mockedApi = api as unknown as {
  fetchExpectationMetrics: ReturnType<typeof vi.fn>;
};

const counts = { total: 5, verbatim: 1, edited: 2, rejected: 1, unratified: 1 };

describe("ExpectationMetricsView", () => {
  beforeEach(() => {
    mockedApi.fetchExpectationMetrics.mockReset();
  });

  it("renders per-project rates and counts", async () => {
    mockedApi.fetchExpectationMetrics.mockResolvedValue({
      total: 6,
      projects: [
        {
          project: "alpha",
          counts,
          expectationHitRate: 0.25,
          firstPrAcceptance: 0.6,
          averageEditDistance: 8,
          months: [{ month: "2026-06", counts }],
        },
        {
          project: "beta",
          counts: { total: 1, verbatim: 0, edited: 0, rejected: 0, unratified: 1 },
          expectationHitRate: null,
          firstPrAcceptance: 0,
          averageEditDistance: null,
          months: [],
        },
      ],
    });

    render(<ExpectationMetricsView />);

    expect(await screen.findByTestId("expectations-project-alpha")).toBeInTheDocument();
    expect(screen.getByTestId("expectations-hit-rate-alpha")).toHaveTextContent("25%");
    expect(screen.getByTestId("expectations-acceptance-alpha")).toHaveTextContent("60%");
    // A project nobody has ratified yet shows a dash, not a fake 0%.
    expect(screen.getByTestId("expectations-hit-rate-beta")).toHaveTextContent("—");
  });

  it("renders the honest empty-state when no ratifications exist", async () => {
    mockedApi.fetchExpectationMetrics.mockResolvedValue({ total: 0, projects: [] });

    render(<ExpectationMetricsView />);

    expect(await screen.findByTestId("expectations-empty")).toBeInTheDocument();
  });
});
