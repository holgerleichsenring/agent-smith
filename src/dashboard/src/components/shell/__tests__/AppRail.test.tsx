import { render, screen, within } from "@testing-library/react";
import { vi, beforeEach } from "vitest";
import { AppRail } from "../AppRail";
import { AppRailItem } from "../AppRailItem";
import { EventStoreProvider } from "@/lib/eventStore/EventStoreProvider";
import { silentEventStore } from "@/lib/eventStore/__tests__/fakes";

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

// Stable hub instance — useSystemEvents' effect deps on `client`, so a fresh
// object per render would loop the effect into an OOM. 1 = Connected.
const HUB = {
  client: {
    systemEvents: { add: () => () => {} },
    subscribeSystem: () => Promise.resolve(() => {}),
  },
  connectionState: 1,
  overview: null,
  systemActivity: null,
};
vi.mock("@/hooks/useJobsHub", () => ({ useJobsHub: () => HUB }));

beforeEach(() => {
  usePathname.mockReturnValue("/");
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
});

describe("AppRailItem", () => {
  it("AppRailItem_LiveSubsystem_ShowsLiveDotAndFreshness", () => {
    render(<AppRailItem label="Tracker" href="/system/tracker" live freshness="42s ago" active={false} />);
    const item = screen.getByTestId("app-rail-item-Tracker");
    expect(within(item).getByTestId("app-rail-item-dot")).toHaveAttribute("aria-label", "live");
    expect(within(item).getByText("42s ago")).toBeInTheDocument();
  });
});
