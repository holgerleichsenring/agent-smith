import { render, screen } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { SystemView } from "@/components/system/SystemView";
import { EventStoreProvider } from "@/lib/eventStore/EventStoreProvider";
import { silentEventStore } from "@/lib/eventStore/__tests__/fakes";
import type { OverviewSnapshot, RunSnapshot, SystemActivitySnapshot } from "@/types/hub-events";
import * as expectationsApi from "@/lib/expectationsApi";

// p0343d: the system/rollup pages join the parity design system. Two contracts:
// every rail route under /system renders inside the .mock-shell/.mock-system
// parity scope as a first-class page (.m-head title row), and the rollup pages'
// .health strips carry the REAL numbers from their existing data sources.

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

// The REST-fed pages (config / catalog / connections / expectations) resolve to
// their honest empty states so each route settles deterministically.
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
vi.mock("@/lib/diagnosticsApi", () => ({
  fetchConnections: vi.fn(() => Promise.resolve({ connections: [], webhooks: [] })),
  probeConnection: vi.fn(),
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
  connections: "connections-empty",
  expectations: "expectations-empty",
};

const ROUTES = [
  "tracker",
  "webhooks",
  "chat",
  "config",
  "catalog",
  "connections",
  "cost",
  "today",
  "expectations",
];

describe("System & rollup pages — parity design system (p0343d)", () => {
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

  it("RollupPages_MetricStrips_RenderRealKpis", async () => {
    // COST — real run ledger numbers flow into the .metric cells.
    const now = new Date().toISOString();
    const run = {
      runId: "r1",
      startedAt: now,
      finishedAt: now,
      costUsd: 2.44,
      llmCalls: 19,
    } as unknown as RunSnapshot;
    HUB.overview = { active: [run], recent: [] } as unknown as OverviewSnapshot;

    const cost = renderView("cost");
    expect(screen.getByTestId("kcard-cost-today")).toHaveTextContent("$2.44");
    expect(screen.getByTestId("kcard-cost-week")).toHaveTextContent("$2.44");
    expect(screen.getByTestId("kcard-cost-calls-7d")).toHaveTextContent("19");
    expect(screen.getByTestId("kcard-cost-today").className).toContain("metric");
    expect(cost.container.querySelector(".health")).not.toBeNull();
    cost.unmount();

    // TODAY — the server-truth 24h counters.
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
    const today = renderView("today");
    expect(screen.getByTestId("kcard-tickets-scanned")).toHaveTextContent("4838");
    expect(screen.getByTestId("kcard-tickets-triggered")).toHaveTextContent("3");
    expect(screen.getByTestId("kcard-poll-cycles")).toHaveTextContent("104");
    expect(screen.getByTestId("kcard-webhooks-received")).toHaveTextContent("7");
    expect(screen.getByTestId("kcard-tickets-scanned").className).toContain("metric");
    today.unmount();

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
    renderView("expectations");
    expect(await screen.findByTestId("exp-metric-negotiated")).toHaveTextContent("5");
    // 1 verbatim / 4 human-ratified (5 − 1 unratified) = 25%
    expect(screen.getByTestId("exp-metric-hit-rate")).toHaveTextContent("25%");
    // (1 verbatim + 2 edited) / 5 negotiated = 60%
    expect(screen.getByTestId("exp-metric-acceptance")).toHaveTextContent("60%");
    expect(screen.getByTestId("exp-metric-edit-distance")).toHaveTextContent("8");
    expect(screen.getByTestId("exp-metric-hit-rate").className).toContain("metric");
  });
});
