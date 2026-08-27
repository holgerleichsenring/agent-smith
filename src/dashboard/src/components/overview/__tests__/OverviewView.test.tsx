import { render, screen, within } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { OverviewView } from "@/components/overview/OverviewView";
import { deriveCostRollup } from "@/hooks/useCostRollup";
import { deriveSpendBreakdown } from "@/hooks/useSpendBreakdown";
import { deriveRunOutcomes } from "@/lib/runOutcomes";
import { bucketRuns } from "@/components/jobs/mission/missionBuckets";
import { mergeNewestFirst } from "@/components/jobs/RunsList";
import type { OverviewSnapshot, RunSnapshot } from "@/types/hub-events";
import * as expectationsApi from "@/lib/expectationsApi";

// 2026-08-27-7463: the Overview holds the three readings that used to be three
// pages. What is pinned here is that each figure is the one its old page read,
// that the breakdown adds up to the headline it sits under, that the run
// overview is read ONCE for the whole page, and that a section which cannot
// answer costs its own box and not the page.

const useJobsHub = vi.hoisted(() => vi.fn());
vi.mock("@/hooks/useJobsHub", () => ({ useJobsHub: () => useJobsHub() }));
vi.mock("@/lib/expectationsApi", () => ({ fetchExpectationMetrics: vi.fn() }));

const mockedExpectations = expectationsApi as unknown as {
  fetchExpectationMetrics: ReturnType<typeof vi.fn>;
};

const MINUTE_MS = 60 * 1000;
const DAY_MS = 24 * 60 * MINUTE_MS;

// Timestamps are relative to the clock the component reads, and far from both
// window edges — the assertions compare against the same derivations, so the
// few milliseconds between render and assert cannot move a run across a cutoff.
const ago = (ms: number) => new Date(Date.now() - ms).toISOString();

function snap(over: Partial<RunSnapshot> = {}): RunSnapshot {
  return {
    runId: "r1",
    pipeline: "fix-bug",
    trigger: "ticket",
    repos: ["server"],
    status: "success",
    prUrl: null,
    summary: null,
    startedAt: ago(30 * MINUTE_MS),
    finishedAt: ago(10 * MINUTE_MS),
    sandboxes: 1,
    stepIndex: 1,
    stepName: null,
    totalSteps: 5,
    lastEventType: null,
    costUsd: 1,
    llmCalls: 3,
    ticketId: null,
    ticketTitle: null,
    agentName: null,
    cancelRequested: false,
    ...over,
  };
}

// One installation's answer: two repo sets, two pipelines, every run bucket, and
// one finished run older than a day but inside the week.
function overviewSnapshot(): OverviewSnapshot {
  return {
    active: [
      snap({ runId: "a1", status: "running", finishedAt: null, costUsd: 2.5, llmCalls: 10 }),
      snap({ runId: "a2", status: "waiting_for_input", finishedAt: null, costUsd: 0.5, llmCalls: 2 }),
      snap({ runId: "a3", status: "queued", finishedAt: null, costUsd: 0, llmCalls: 0 }),
    ],
    recent: [
      snap({ runId: "f1", costUsd: 4, llmCalls: 20, repos: ["web"], pipeline: "add-feature" }),
      snap({
        runId: "f2",
        status: "failed",
        costUsd: 1.25,
        llmCalls: 5,
        startedAt: ago(3 * DAY_MS),
        finishedAt: ago(3 * DAY_MS),
      }),
      snap({ runId: "f3", status: "cancelled", costUsd: 0.75, llmCalls: 1 }),
    ],
    systemActivity: null,
  };
}

let overview: OverviewSnapshot;

const renderOverview = () => render(<OverviewView />);

beforeEach(() => {
  overview = overviewSnapshot();
  useJobsHub.mockReset();
  useJobsHub.mockReturnValue({ overview });
  mockedExpectations.fetchExpectationMetrics.mockReset();
  mockedExpectations.fetchExpectationMetrics.mockResolvedValue({ total: 0, projects: [] });
});

describe("Overview", () => {
  it("Overview_TheSpendFigure_IsTheOneTheCostViewRead", () => {
    const cost = deriveCostRollup(overview, Date.now());
    renderOverview();
    expect(screen.getByTestId("kcard-cost-today")).toHaveTextContent(`$${cost.today.toFixed(2)}`);
    expect(screen.getByTestId("kcard-cost-week")).toHaveTextContent(`$${cost.week.toFixed(2)}`);
    expect(screen.getByTestId("kcard-cost-calls-7d")).toHaveTextContent(
      cost.llmCalls.toLocaleString(),
    );
    // The run finished three days ago is in the week and not in the day.
    expect(cost.week).toBeGreaterThan(cost.today);
  });

  it("Overview_TheBreakdown_SumsToTheHeadlineSpend", () => {
    const cost = deriveCostRollup(overview, Date.now());
    const slices = deriveSpendBreakdown(overview, Date.now());
    // The grouping is a re-cut of the same runs, so it adds up to the figure
    // above it — a breakdown that does not is a second truth for one number.
    const summed = slices.reduce((total, slice) => total + slice.amountUsd, 0);
    expect(summed).toBeCloseTo(cost.week, 10);

    renderOverview();
    const rows = within(screen.getByTestId("overview-spend-rows")).getAllByText(/^\$/);
    expect(rows).toHaveLength(slices.length);
    // Biggest first, and the work and the pipeline both name themselves.
    expect(screen.getByTestId(`overview-spend-row-${slices[0].key}`)).toHaveTextContent(
      `$${slices[0].amountUsd.toFixed(2)}`,
    );
    const breakdown = screen.getByTestId("overview-spend-rows");
    expect(breakdown).toHaveTextContent("add-feature");
    expect(breakdown).toHaveTextContent("web");
  });

  it("Overview_TheRunOutcomes_MatchTheBucketsTheRailCounts", () => {
    const runs = mergeNewestFirst(overview.active, overview.recent);
    const buckets = bucketRuns(runs);
    const outcomes = deriveRunOutcomes(runs);
    expect(outcomes.needsYou).toBe(buckets.needsYou.length);
    expect(outcomes.running).toBe(buckets.running.length);
    expect(outcomes.queued).toBe(buckets.queued.length);
    expect(outcomes.finished).toBe(buckets.finished.length);

    renderOverview();
    expect(screen.getByTestId("kcard-runs-total")).toHaveTextContent(String(runs.length));
    expect(screen.getByTestId("kcard-runs-needs-you")).toHaveTextContent("1");
    expect(screen.getByTestId("kcard-runs-running")).toHaveTextContent("1");
    expect(screen.getByTestId("kcard-runs-queued")).toHaveTextContent("1");
    // The finished bucket, split by how those runs ended.
    expect(screen.getByTestId("kcard-runs-succeeded")).toHaveTextContent("1");
    expect(screen.getByTestId("kcard-runs-failed")).toHaveTextContent("1");
    expect(screen.getByTestId("kcard-runs-cancelled")).toHaveTextContent("1");
  });

  it("Overview_TheCriteriaOutcomes_AreTheOnesTheExpectationViewRead", async () => {
    mockedExpectations.fetchExpectationMetrics.mockResolvedValue({
      total: 5,
      projects: [
        {
          project: "alpha",
          counts: { total: 5, verbatim: 1, edited: 2, rejected: 1, unratified: 1 },
          expectationHitRate: 0.25,
          firstPrAcceptance: 0.6,
          averageEditDistance: 8,
          months: [],
        },
      ],
    });
    renderOverview();
    // 1 verbatim / 4 human-ratified = 25%; (1 verbatim + 2 edited) / 5 = 60%.
    expect(await screen.findByTestId("exp-metric-negotiated")).toHaveTextContent("5");
    expect(screen.getByTestId("exp-metric-hit-rate")).toHaveTextContent("25%");
    expect(screen.getByTestId("exp-metric-acceptance")).toHaveTextContent("60%");
    expect(screen.getByTestId("expectations-project-alpha")).toBeInTheDocument();
  });

  it("Overview_ReadsTheRunOverviewOnce_ForAllSections", () => {
    renderOverview();
    // useJobsHub is per mount — its own fetch and its own overview subscription
    // each time. The page reads it at the top and passes the values down.
    expect(useJobsHub).toHaveBeenCalledTimes(1);
  });

  it("Overview_CriteriaEmpty_LeavesTheOtherTwoSectionsRendered", async () => {
    renderOverview();
    expect(await screen.findByTestId("expectations-empty")).toBeInTheDocument();
    expect(screen.getByTestId("overview-spend-strip")).toBeInTheDocument();
    expect(screen.getByTestId("overview-runs-strip")).toBeInTheDocument();
  });

  it("Overview_OneSectionFailing_DoesNotBlankThePage", async () => {
    mockedExpectations.fetchExpectationMetrics.mockRejectedValue(new Error("upstream is down"));
    renderOverview();
    expect(await screen.findByTestId("expectations-error")).toHaveTextContent("upstream is down");
    // The two readings that did answer are still on screen, in their own boxes.
    expect(screen.getByTestId("overview-spend-strip")).toBeInTheDocument();
    expect(screen.getByTestId("overview-runs-strip")).toBeInTheDocument();
    expect(screen.getByTestId("overview-page")).toBeInTheDocument();
  });

  it("Overview_NoRunListYet_EachSectionSaysSoInItsOwnBox", () => {
    useJobsHub.mockReturnValue({ overview: null });
    renderOverview();
    expect(screen.getByTestId("overview-spend-loading")).toBeInTheDocument();
    expect(screen.getByTestId("overview-runs-loading")).toBeInTheDocument();
    expect(screen.getByTestId("overview-page")).toBeInTheDocument();
  });

  it("Overview_NoSpendInTheWindow_TheBreakdownSaysSoRatherThanShowingNothing", () => {
    useJobsHub.mockReturnValue({
      overview: { active: [], recent: [], systemActivity: null } as OverviewSnapshot,
    });
    renderOverview();
    expect(screen.getByTestId("overview-spend-empty")).toBeInTheDocument();
    expect(screen.getByTestId("overview-runs-strip")).toBeInTheDocument();
  });

  it("Overview_RendersOnePageHead_AndSectionHeadsBelowIt", () => {
    const { container } = renderOverview();
    const heads = container.querySelectorAll(".m-head h1");
    expect(heads).toHaveLength(1);
    expect(heads[0]).toHaveTextContent("Overview");
    const sections = [...container.querySelectorAll(".section-head h2")].map((h) => h.textContent);
    expect(sections).toContain("Spend");
    expect(sections).toContain("Where the money went");
    expect(sections).toContain("Runs by outcome");
    expect(sections).toContain("Criteria outcomes");
  });
});
