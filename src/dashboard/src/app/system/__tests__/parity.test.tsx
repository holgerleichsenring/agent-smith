import { render, screen } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { SystemView } from "@/components/system/SystemView";
import { OverviewView } from "@/components/overview/OverviewView";
import { EventStoreProvider } from "@/lib/eventStore/EventStoreProvider";
import { silentEventStore } from "@/lib/eventStore/__tests__/fakes";
import type { OverviewSnapshot, RunSnapshot, SystemActivitySnapshot } from "@/types/hub-events";
import * as expectationsApi from "@/lib/expectationsApi";

// p0343d: the system/rollup pages join the parity design system. Two contracts:
// every rail route under /system renders inside the .mock-shell/.mock-system
// parity scope as a first-class page (.m-head title row), and the pages'
// .health strips carry the REAL numbers from their existing data sources.
// 2026-08-27-7463: the suite follows the page. The cost, today and expectations
// segments are gone from /system, and the KPI assertions that proved their strips
// carried real numbers now assert them on the Overview that renders them.

// Stable hub instance — useSystemEvents' effect deps on `client`, so a fresh
// object per render would loop the effect. Fields are mutated per test.
const HUB = {
  client: {
    systemEvents: { add: () => () => {} },
    subscribeSystem: () => Promise.resolve(() => {}),
  },
  connectionState: 1,
  overview: null as OverviewSnapshot | null,
  systemActivity: null as SystemActivitySnapshot | null,
};
vi.mock("@/hooks/useJobsHub", () => ({ useJobsHub: () => HUB }));

// The REST-fed pages (config / catalog / expectations) resolve to their honest empty
// states so each route settles deterministically. 2026-08-27-1ed6: connections left this
// view for /config/connection-check.
vi.mock("@/lib/configApi", () => ({
  fetchConfig: vi.fn(() =>
    Promise.resolve({ projects: [], repos: [], trackers: [], agents: [] }),
  ),
}));
vi.mock("@/lib/catalogApi", () => ({
  fetchCatalogContents: vi.fn(() =>
    Promise.resolve({ ready: false, masters: [], skills: [], concepts: [] }),
  ),
  fetchSkillBody: vi.fn(() => Promise.resolve(null)),
}));
vi.mock("@/lib/expectationsApi", () => ({ fetchExpectationMetrics: vi.fn() }));

const mockedExpectations = expectationsApi as unknown as {
  fetchExpectationMetrics: ReturnType<typeof vi.fn>;
};

const renderView = (segment: string) =>
  render(
    <EventStoreProvider store={silentEventStore()}>
      <SystemView segment={segment} />
    </EventStoreProvider>,
  );

// Markers that prove each route's async states settled inside the shell.
const SETTLE: Record<string, string> = {
  config: "config-view-empty",
  catalog: "catalog-browser-unready",
};

const ROUTES = ["tracker", "webhooks", "chat", "config", "catalog"];

describe("System & overview pages — parity design system (p0343d)", () => {
  beforeEach(() => {
    HUB.overview = null;
    HUB.systemActivity = null;
    mockedExpectations.fetchExpectationMetrics.mockReset();
    mockedExpectations.fetchExpectationMetrics.mockResolvedValue({ total: 0, projects: [] });
  });

  it("SystemPages_AllRoutes_RenderParityShell", async () => {
    for (const segment of ROUTES) {
      const { container, unmount } = renderView(segment);
      // the parity scope classes on the page root…
      const shell = container.querySelector(".mock-shell.mock-system");
      expect(shell, `route /system/${segment} must render the parity shell`).not.toBeNull();
      // …and a first-class .m-head title row inside it.
      expect(
        container.querySelector(".m-head h1"),
        `route /system/${segment} must render an .m-head title`,
      ).not.toBeNull();
      if (SETTLE[segment]) await screen.findByTestId(SETTLE[segment]);
      unmount();
    }
  });

  it("SystemPages_TodaysNumbers_StillRenderOnTheirSubsystemPages", async () => {
    // 2026-08-27-7463: the today rollup was deleted, not moved — all six of its
    // numbers are on the subsystem page each describes, off the same snapshot.
    HUB.systemActivity = {
      ticketsScanned: 4838,
      ticketsTriggered: 3,
      ticketsSkipped: 4835,
      webhooksReceived: 7,
      webhooksActioned: 2,
      pollCyclesStarted: 104,
      pollCyclesFinished: 104,
      eventsPerSource: {},
    };
    const tracker = renderView("tracker");
    expect(screen.getByTestId("sys-metric-tickets-scanned")).toHaveTextContent("4838");
    expect(screen.getByTestId("sys-metric-tickets-triggered")).toHaveTextContent("3");
    expect(screen.getByTestId("sys-metric-tickets-skipped")).toHaveTextContent("4835");
    expect(screen.getByTestId("sys-metric-poll-cycles")).toHaveTextContent("104");
    tracker.unmount();

    const webhooks = renderView("webhooks");
    expect(screen.getByTestId("sys-metric-webhooks-received")).toHaveTextContent("7");
    expect(screen.getByTestId("sys-metric-webhooks-actioned")).toHaveTextContent("2");
    webhooks.unmount();
  });

  it("OverviewPage_MetricStrips_RenderRealKpis", async () => {
    // COST — real run ledger numbers flow into the .metric cells, on the page
    // that renders them now.
    const now = new Date().toISOString();
    const run = {
      runId: "r1",
      pipeline: "fix-bug",
      repos: ["server"],
      startedAt: now,
      finishedAt: now,
      costUsd: 2.44,
      llmCalls: 19,
      status: "success",
    } as unknown as RunSnapshot;
    HUB.overview = { active: [run], recent: [] } as unknown as OverviewSnapshot;

    const overview = render(<OverviewView />);
    expect(overview.container.querySelector(".mock-shell.mock-system")).not.toBeNull();
    expect(overview.container.querySelector(".m-head h1")).not.toBeNull();
    expect(screen.getByTestId("kcard-cost-today")).toHaveTextContent("$2.44");
    expect(screen.getByTestId("kcard-cost-week")).toHaveTextContent("$2.44");
    expect(screen.getByTestId("kcard-cost-calls-7d")).toHaveTextContent("19");
    // 2026-08-27-559e: the figures read from cards now, not from a strip.
    expect(screen.getByTestId("overview-spend-card").className).toContain("ov-card");
    expect(overview.container.querySelector(".ov-cards")).not.toBeNull();
    expect(overview.container.querySelector(".ov-panels")).not.toBeNull();
    // RUNS BY OUTCOME — the same run, counted where it belongs.
    expect(screen.getByTestId("kcard-runs-total")).toHaveTextContent("1");
    expect(screen.getByTestId("kcard-runs-succeeded")).toHaveTextContent("1");
    await screen.findByTestId("expectations-empty");
    overview.unmount();

    // EXPECTATIONS — overall rates from the recorded ratification outcomes.
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
    render(<OverviewView />);
    expect(await screen.findByTestId("exp-metric-negotiated")).toHaveTextContent("5");
    // 1 verbatim / 4 human-ratified (5 − 1 unratified) = 25%
    expect(screen.getByTestId("exp-metric-hit-rate")).toHaveTextContent("25%");
    // (1 verbatim + 2 edited) / 5 negotiated = 60%
    expect(screen.getByTestId("exp-metric-acceptance")).toHaveTextContent("60%");
    expect(screen.getByTestId("exp-metric-edit-distance")).toHaveTextContent("8");
    expect(screen.getByTestId("exp-metric-hit-rate").className).toContain("metric");
  });
});
