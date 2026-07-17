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
// p0343b: the rail is contextual — these tests cover the RUNS mode (toggle,
// monitor counts, footer); the config mode lives in config/__tests__/
// AppRailConfig.test.tsx next to the studio it switches.
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
  it("AppRail_RunsMode_RendersMonitorSystemRollupsSections_InOrder", () => {
    renderRail();
    const sections = ["Monitor", "System", "Rollups"].map(
      (l) => screen.getByTestId(`app-rail-section-${l}`),
    );
    // DOM order follows section order: Monitor before System before Rollups.
    expect(sections[0].compareDocumentPosition(sections[1]))
      .toBe(Node.DOCUMENT_POSITION_FOLLOWING);
    expect(sections[1].compareDocumentPosition(sections[2]))
      .toBe(Node.DOCUMENT_POSITION_FOLLOWING);
    expect(screen.getByTestId("app-rail")).toHaveAttribute("data-mode", "runs");
  });

  it("AppRail_SegmentedToggle_RunsActiveOnRunsRoutes", () => {
    usePathname.mockReturnValue("/jobs/r1");
    renderRail();
    expect(screen.getByTestId("rail-toggle-runs")).toHaveAttribute("data-active", "true");
    expect(screen.getByTestId("rail-toggle-config")).toHaveAttribute("data-active", "false");
    expect(screen.getByTestId("rail-toggle-runs")).toHaveAttribute("href", "/");
    expect(screen.getByTestId("rail-toggle-config")).toHaveAttribute("href", "/config");
  });

  it("AppRail_ActiveItem_DerivesFromCurrentRoute", () => {
    usePathname.mockReturnValue("/system/tracker");
    renderRail();
    expect(screen.getByTestId("app-rail-item-Tracker · ticket polling"))
      .toHaveAttribute("data-active", "true");
    // Today is not active when the route is a subsystem.
    expect(screen.getByTestId("app-rail-item-Today")).toHaveAttribute("data-active", "false");
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
    // p0343b: Today carries the ALL-runs count.
    expect(screen.getByTestId("app-rail-count-Today")).toHaveTextContent("5");
    expect(screen.getByTestId("app-rail-count-Needs you")).toHaveTextContent("1");
    expect(screen.getByTestId("app-rail-count-Running")).toHaveTextContent("2");
    expect(screen.getByTestId("app-rail-count-Queued")).toHaveTextContent("1");
    expect(screen.getByTestId("app-rail-count-Finished")).toHaveTextContent("1");
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

  it("AppRail_MonitorSections_HashLinkToHomeBuckets", () => {
    renderRail();
    expect(screen.getByTestId("app-rail-item-Today")).toHaveAttribute("href", "/");
    expect(screen.getByTestId("app-rail-item-Needs you")).toHaveAttribute("href", "/#needs-you");
    expect(screen.getByTestId("app-rail-item-Running")).toHaveAttribute("href", "/#running");
    expect(screen.getByTestId("app-rail-item-Queued")).toHaveAttribute("href", "/#queued");
    expect(screen.getByTestId("app-rail-item-Finished")).toHaveAttribute("href", "/#finished");
  });

  it("AppRail_Footer_NoEventsYet_ShowsHonestIdleLines", () => {
    renderRail();
    expect(screen.getByTestId("rail-footer-tracker")).toHaveTextContent("tracker · no polls seen");
    expect(screen.getByTestId("rail-footer-webhooks")).toHaveTextContent("webhooks · idle");
  });

  it("AppRail_Footer_TrackerEvent_NamesTrackerAndFreshness", async () => {
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
    // The footer names the tracker from its newest event and reuses the
    // subsystem freshness ("polled now" for a just-emitted event).
    expect(screen.getByTestId("rail-footer-tracker")).toHaveTextContent("azdo · polled now");
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
