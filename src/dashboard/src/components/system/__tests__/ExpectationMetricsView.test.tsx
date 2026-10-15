import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi, beforeEach } from "vitest";
import { ExpectationMetricsView } from "@/components/system/ExpectationMetricsView";
import { useExpectationMetrics } from "@/hooks/useExpectationMetrics";
import * as api from "@/lib/expectationsApi";

// The criteria panel: populated projects render their share and month tally (a project
// with nothing judged shows a dash, never 0%), and an installation with no judged run
// renders the empty state instead of zeros.

// 2026-10-15-4a4de: whether this sign-in reaches a project is useProjectReach's question; here it is an answer.
const reach = vi.hoisted(() => ({ value: "unknown" as "none" | "unknown" }));
vi.mock("@/hooks/useProjectReach", () => ({ useProjectReach: () => reach.value }));
vi.mock("@/lib/expectationsApi", () => ({ fetchExpectationMetrics: vi.fn() }));

const mockedApi = api as unknown as {
  fetchExpectationMetrics: ReturnType<typeof vi.fn>;
};

const zero = { met: 0, unmet: 0, unproven: 0, notApplicable: 0, overruled: 0, staleOverrules: 0 };
const alpha = { ...zero, met: 3, unmet: 1, judged: 4, share: 0.75, staleOverrules: 1 };
const beta = { ...zero, notApplicable: 2, judged: 0, share: null };

// The panel takes the read rather than making it, so the tests drive it through the
// hook that owns the fetch — the same path the Overview composes, endpoint included.
function CriteriaPanel() {
  return <ExpectationMetricsView {...useExpectationMetrics()} />;
}

describe("ExpectationMetricsView", () => {
  beforeEach(() => {
    reach.value = "unknown";
    mockedApi.fetchExpectationMetrics.mockReset();
  });

  it("EmptyAndNoProjectReached_SaysSo", async () => {
    reach.value = "none";
    mockedApi.fetchExpectationMetrics.mockResolvedValue({ runs: 0, counts: { ...zero, judged: 0, share: null }, projects: [] });
    render(<CriteriaPanel />);
    expect(await screen.findByTestId("no-project-reach")).toHaveTextContent("Your sign-in reaches no project on this installation");
    expect(screen.queryByText(/No coding run has been judged/)).toBeNull();
  });

  it("EmptyForAGlobalReader_KeepsTodaysText", async () => {
    mockedApi.fetchExpectationMetrics.mockResolvedValue({ runs: 0, counts: { ...zero, judged: 0, share: null }, projects: [] });
    render(<CriteriaPanel />);
    expect(await screen.findByTestId("expectations-empty")).toHaveTextContent(/No coding run has been judged/);
    expect(screen.queryByTestId("no-project-reach")).toBeNull();
  });

  it("renders per-project shares and month tallies", async () => {
    mockedApi.fetchExpectationMetrics.mockResolvedValue({
      runs: 3,
      counts: { ...alpha, notApplicable: 2 },
      projects: [
        {
          project: "alpha",
          runs: 2,
          counts: alpha,
          months: [{ month: "2026-06", runs: 2, counts: alpha }],
        },
        { project: "beta", runs: 1, counts: beta, months: [] },
      ],
    });

    render(<CriteriaPanel />);

    expect(await screen.findByTestId("expectations-project-alpha")).toBeInTheDocument();
    expect(screen.getByTestId("expectations-share-alpha")).toHaveTextContent("75%");
    expect(screen.getByTestId("expectations-months-alpha")).toHaveTextContent("2026-06: 3/4 met");
    expect(screen.getByTestId("expectations-project-alpha")).toHaveTextContent("1 stale overrule");
    // A project whose criteria were all not applicable has no share, not a fake 0%.
    expect(screen.getByTestId("expectations-share-beta")).toHaveTextContent("—");
  });

  it("renders the empty state when no coding run was judged", async () => {
    mockedApi.fetchExpectationMetrics.mockResolvedValue({
      runs: 0,
      counts: { ...zero, judged: 0, share: null },
      projects: [],
    });

    render(<CriteriaPanel />);

    expect(await screen.findByTestId("expectations-empty")).toBeInTheDocument();
  });
});
