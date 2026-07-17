import { render, screen, within } from "@testing-library/react";
import { vi, beforeEach } from "vitest";
import { AppRail } from "../AppRail";
import { AppRailItem } from "../AppRailItem";
import { EventStoreProvider } from "@/lib/eventStore/EventStoreProvider";
import { silentEventStore } from "@/lib/eventStore/__tests__/fakes";
import type { OverviewSnapshot, RunSnapshot } from "@/types/hub-events";

// p0218: AppRail reads the shared system backlog via the EventStore, so renders
// go through a provider wired to a silent source.
const renderRail = () =>
  render(
    <EventStoreProvider store={silentEventStore()}>
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
  it("AppRail_RendersRunsSystemRollupsSections_InOrder", () => {
    renderRail();
    const sections = ["Runs", "System", "Rollups"].map(
      (l) => screen.getByTestId(`app-rail-section-${l}`),
    );
    // DOM order follows section order: Runs before System before Rollups.
    expect(sections[0].compareDocumentPosition(sections[1]))
      .toBe(Node.DOCUMENT_POSITION_FOLLOWING);
    expect(sections[1].compareDocumentPosition(sections[2]))
      .toBe(Node.DOCUMENT_POSITION_FOLLOWING);
  });

  it("AppRail_ActiveItem_DerivesFromCurrentRoute", () => {
    usePathname.mockReturnValue("/system/tracker");
    renderRail();
    expect(screen.getByTestId("app-rail-item-Tracker · ticket polling"))
      .toHaveAttribute("data-active", "true");
    // Runs is not active when the route is a subsystem.
    expect(screen.getByTestId("app-rail-item-Runs")).toHaveAttribute("data-active", "false");
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
    expect(screen.getByTestId("app-rail-item-Needs you")).toHaveAttribute("href", "/#needs-you");
    expect(screen.getByTestId("app-rail-item-Running")).toHaveAttribute("href", "/#running");
    expect(screen.getByTestId("app-rail-item-Queued")).toHaveAttribute("href", "/#queued");
    expect(screen.getByTestId("app-rail-item-Finished")).toHaveAttribute("href", "/#finished");
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
