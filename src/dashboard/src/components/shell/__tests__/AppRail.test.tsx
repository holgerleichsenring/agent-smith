import { act, render, screen, within } from "@testing-library/react";
import { vi, beforeEach } from "vitest";
import { AppRail } from "../AppRail";
import { AppRailItem } from "../AppRailItem";
import { EventStoreProvider } from "@/lib/eventStore/EventStoreProvider";
import { createFakeSource, silentEventStore, flush } from "@/lib/eventStore/__tests__/fakes";
import { EventStore } from "@/lib/eventStore/eventStore";
import { SystemEventType, type SystemEvent } from "@/types/system-events";
import type { OverviewSnapshot, RunSnapshot } from "@/types/hub-events";

// p0218: AppRail reads the shared system backlog via the EventStore, so renders
// go through a provider wired to a silent source.
// p0343b: the rail is contextual — these tests cover the RUNS mode (monitor counts,
// subsystems, insight); the config mode lives in config/__tests__/AppRailConfig.test.tsx
// next to the studio it belongs to.
// 2026-08-27-1ed6: the rail shows the running system and nothing else — no toggle into
// configuration (the header's gear is the one entrance), no tracker footer, no Connections
// entry (the check lives under /config now).
const renderRail = (store = silentEventStore()) =>
  render(
    <EventStoreProvider store={store}>
      <AppRail />
    </EventStoreProvider>,
  );

const usePathname = vi.fn(() => "/");
vi.mock("next/navigation", () => ({
  usePathname: () => usePathname(),
}));

// Stable hub instance PER TEST — useSystemEvents' effect deps on `client`, so a
// fresh object per render would loop the effect into an OOM. 1 = Connected.
// p0345b: `overview` is mutable per test so the monitor counts are testable.
function baseHub(): {
  client: unknown;
  connectionState: number;
  overview: OverviewSnapshot | null;
  systemActivity: null;
} {
  return {
    client: {
      systemEvents: { add: () => () => {} },
      subscribeSystem: () => Promise.resolve(() => {}),
    },
    connectionState: 1,
    overview: null,
    systemActivity: null,
  };
}
const hubRef = { current: baseHub() };
vi.mock("@/hooks/useJobsHub", () => ({ useJobsHub: () => hubRef.current }));

// p0347: the Pull requests monitor item fetches its live open-PR count. The
// factory is hoisted, so the fixture lives inside it; opened entries drive the
// count, non-opened attempts do not.
vi.mock("@/lib/pullRequestsApi", () => ({
  fetchPullRequests: vi.fn().mockResolvedValue([
    { runId: "r1", ticketId: "1", ticketTitle: "a", pipeline: "fix-bug", repo: "server", status: "opened", url: "https://git/pr/1", reason: null, openedAt: "2026-07-17T10:00:00Z" },
    { runId: "r2", ticketId: "2", ticketTitle: "b", pipeline: "fix-bug", repo: "web", status: "opened", url: "https://git/pr/2", reason: null, openedAt: "2026-07-17T10:01:00Z" },
    { runId: "r3", ticketId: "3", ticketTitle: "c", pipeline: "fix-bug", repo: "docs", status: "no_changes", url: null, reason: "nothing", openedAt: "2026-07-17T10:02:00Z" },
  ]),
}));

function snap(runId: string, status: string): RunSnapshot {
  return {
    runId,
    pipeline: "fix-bug",
    trigger: "ticket",
    repos: ["server"],
    status,
    prUrl: null,
    summary: null,
    startedAt: "2026-07-17T10:00:00Z",
    finishedAt: status === "success" ? "2026-07-17T11:00:00Z" : null,
    sandboxes: 1,
    stepIndex: 1,
    stepName: null,
    totalSteps: 5,
    lastEventType: null,
    costUsd: 0,
    llmCalls: 0,
    ticketId: null,
    ticketTitle: null,
    agentName: null,
    cancelRequested: false,
  };
}

beforeEach(() => {
  usePathname.mockReturnValue("/");
  hubRef.current = baseHub();
});

describe("AppRail", () => {
  it("AppRail_RunsMode_RendersMonitorSystemInsightSections_InOrder", () => {
    renderRail();
    const sections = ["Monitor", "System", "Insight"].map(
      (l) => screen.getByTestId(`app-rail-section-${l}`),
    );
    // DOM order follows section order: Monitor before System before Insight.
    expect(sections[0].compareDocumentPosition(sections[1]))
      .toBe(Node.DOCUMENT_POSITION_FOLLOWING);
    expect(sections[1].compareDocumentPosition(sections[2]))
      .toBe(Node.DOCUMENT_POSITION_FOLLOWING);
    expect(screen.getByTestId("app-rail")).toHaveAttribute("data-mode", "runs");
  });

  it("Rail_RunsMode_HasNoToggleNoFooterAndNoConnectionsEntry", () => {
    usePathname.mockReturnValue("/jobs/r1");
    renderRail();
    // Configuration is entered by the header's gear — one entrance, not two.
    expect(screen.queryByTestId("rail-toggle")).toBeNull();
    expect(screen.queryByTestId("rail-toggle-runs")).toBeNull();
    expect(screen.queryByTestId("rail-toggle-config")).toBeNull();
    // The footer duplicated the Tracker and Webhooks entries above it.
    expect(screen.queryByTestId("rail-footer")).toBeNull();
    // The connection check is a question about the installation, asked under /config.
    expect(screen.queryByTestId("app-rail-item-Connections")).toBeNull();
    // And nothing that is a setting is left in it.
    expect(screen.queryByTestId("rail-identity")).toBeNull();
    expect(screen.queryByTestId("rail-release")).toBeNull();
  });

  it("Rail_HasOneInsightEntry_AndNoRollupSection", () => {
    renderRail();
    // 2026-08-27-7463: three rollup entries became one Insight → Overview entry.
    expect(screen.queryByTestId("app-rail-section-Rollups")).toBeNull();
    expect(screen.getByTestId("app-rail-section-Insight")).toBeInTheDocument();
    expect(screen.getByTestId("app-rail-item-Overview")).toHaveAttribute("href", "/overview");
    for (const gone of ["Cost", "Today's activity", "Expectations"]) {
      expect(screen.queryByTestId(`app-rail-item-${gone}`)).toBeNull();
    }
  });

  it("Rail_TheInsightEntry_IsActiveOnTheOverview", () => {
    usePathname.mockReturnValue("/overview");
    renderRail();
    expect(screen.getByTestId("app-rail-item-Overview")).toHaveAttribute("data-active", "true");
  });

  it("AppRail_ActiveItem_DerivesFromCurrentRoute", () => {
    usePathname.mockReturnValue("/system/tracker");
    renderRail();
    expect(screen.getByTestId("app-rail-item-Tracker · ticket polling"))
      .toHaveAttribute("data-active", "true");
    // "All runs" is not active when the route is a subsystem.
    expect(screen.getByTestId("app-rail-item-All runs")).toHaveAttribute("data-active", "false");
  });

  it("AppRail_MonitorSections_ShowLiveCounts", () => {
    // p0345b: the monitor sub-items derive their counts from the SAME
    // bucketing MissionControl renders (overview → mergeNewestFirst → buckets).
    hubRef.current.overview = {
      active: [
        snap("w1", "waiting_for_input"),
        snap("r1", "running"),
        snap("r2", "running"),
        snap("q1", "queued"),
      ],
      recent: [snap("f1", "success")],
      systemActivity: null,
    };
    renderRail();
    // p0348: "All runs" carries the ALL-runs count (no date filter).
    expect(screen.getByTestId("app-rail-count-All runs")).toHaveTextContent("5");
    expect(screen.getByTestId("app-rail-count-Needs you")).toHaveTextContent("1");
    expect(screen.getByTestId("app-rail-count-Running")).toHaveTextContent("2");
    expect(screen.getByTestId("app-rail-count-Queued")).toHaveTextContent("1");
    expect(screen.getByTestId("app-rail-count-Finished")).toHaveTextContent("1");
  });

  it("AppRail_PullRequestsItem_ShowsOpenCount", async () => {
    renderRail();
    const item = screen.getByTestId("app-rail-item-Pull requests");
    expect(item).toHaveAttribute("href", "/pull-requests");
    // Only the two OPENED PRs count — the no_changes attempt does not.
    expect(await screen.findByTestId("app-rail-count-Pull requests")).toHaveTextContent("2");
  });

  it("AppRail_PullRequestsItem_ActiveOnItsRoute", () => {
    usePathname.mockReturnValue("/pull-requests");
    renderRail();
    expect(screen.getByTestId("app-rail-item-Pull requests")).toHaveAttribute("data-active", "true");
  });

  it("AppRail_NeedsYouNonZero_RendersHot_ZeroStaysCalm", () => {
    hubRef.current.overview = {
      active: [snap("w1", "waiting_for_input")],
      recent: [],
      systemActivity: null,
    };
    renderRail();
    expect(screen.getByTestId("app-rail-item-Needs you")).toHaveAttribute("data-hot", "true");
    expect(screen.getByTestId("app-rail-item-Running")).toHaveAttribute("data-hot", "false");
  });

  it("AppRail_NoOverviewYet_MonitorCountsShowZero", () => {
    renderRail();
    expect(screen.getByTestId("app-rail-count-Needs you")).toHaveTextContent("0");
    expect(screen.getByTestId("app-rail-item-Needs you")).toHaveAttribute("data-hot", "false");
  });

  // p0458: the monitor items name a BUCKET of the home screen, not an anchor in
  // it — the href is the filtered view, so it is copyable and openable in a tab.
  it("AppRail_MonitorSections_LinkToTheFilteredHomeScreen", () => {
    renderRail();
    expect(screen.getByTestId("app-rail-item-All runs")).toHaveAttribute("href", "/");
    expect(screen.getByTestId("app-rail-item-Needs you")).toHaveAttribute("href", "/?bucket=needs-you");
    expect(screen.getByTestId("app-rail-item-Running")).toHaveAttribute("href", "/?bucket=running");
    expect(screen.getByTestId("app-rail-item-Queued")).toHaveAttribute("href", "/?bucket=queued");
    expect(screen.getByTestId("app-rail-item-Finished")).toHaveAttribute("href", "/?bucket=finished");
  });

  it("Rail_NoTrackerEventYet_TheTrackerEntryKeepsItsPlainName", () => {
    renderRail();
    expect(screen.getByTestId("app-rail-item-Tracker · ticket polling")).toBeInTheDocument();
  });

  it("Rail_TheTrackerEntry_CarriesTheObservedTrackerName", async () => {
    const fake = createFakeSource();
    renderRail(new EventStore(fake.source));
    const event: SystemEvent = {
      source: "poller",
      type: SystemEventType.PollCycleFinished,
      timestamp: new Date().toISOString(),
      tracker: "azdo",
      ticketsPolled: 3,
      matched: 1,
      spawned: 1,
      statusFiltered: 0,
      zeroMatched: 0,
      durationMs: 120,
    };
    await act(async () => {
      fake.emitSystem(event);
      await flush();
    });
    // The entry names the tracker from its newest event — the one fact the removed
    // footer carried alone — and keeps the freshness it always showed.
    const entry = screen.getByTestId("app-rail-item-Tracker · azdo");
    expect(entry).toHaveAttribute("href", "/system/tracker");
    expect(within(entry).getByText("now")).toBeInTheDocument();
  });
});

describe("AppRailItem", () => {
  it("AppRailItem_LiveSubsystem_ShowsLiveDotAndFreshness", () => {
    render(<AppRailItem label="Tracker" href="/system/tracker" live freshness="42s ago" active={false} />);
    const item = screen.getByTestId("app-rail-item-Tracker");
    expect(within(item).getByTestId("app-rail-item-dot")).toHaveAttribute("aria-label", "live");
    expect(within(item).getByText("42s ago")).toBeInTheDocument();
  });

  it("AppRailItem_HotWithCount_RendersAttentionDotAndCount", () => {
    render(<AppRailItem label="Needs you" href="/#needs-you" active={false} count={2} hot indent />);
    const item = screen.getByTestId("app-rail-item-Needs you");
    expect(item).toHaveAttribute("data-hot", "true");
    expect(within(item).getByTestId("app-rail-item-dot")).toHaveAttribute("aria-label", "needs attention");
    expect(within(item).getByTestId("app-rail-count-Needs you")).toHaveTextContent("2");
  });
});
