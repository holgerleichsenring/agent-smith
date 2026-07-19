import { render, screen } from "@testing-library/react";
import { describe, it, expect, vi } from "vitest";
import { SystemView } from "@/components/system/SystemView";
import { EventStoreProvider } from "@/lib/eventStore/EventStoreProvider";
import { silentEventStore } from "@/lib/eventStore/__tests__/fakes";

// p0218: SystemView reads its subsystem scope from the shared EventStore, so
// renders go through a provider wired to a silent source.
const renderView = (segment: string | null) =>
  render(
    <EventStoreProvider store={silentEventStore()}>
      <SystemView segment={segment} />
    </EventStoreProvider>,
  );

// The page reads the selected subsystem from the route slug (set by p0209a's
// path-segment rail hrefs) — SystemView takes that already-resolved segment, so
// selection is URL-stable across refresh: the same URL always renders the same
// subsystem with no client selection state to rehydrate. We assert the segment
// → subsystem mapping the route relies on.

// Stable hub instance — useSystemEvents' effect deps on `client`, so a fresh
// object per render would loop the effect (setEvents → render → new client →
// effect) into an OOM. Mirror the real hook's stable client reference.
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

describe("System route (rail-driven master/detail)", () => {
  it("SystemPage_SubsystemSelection_IsUrlStableAcrossRefresh", () => {
    // Two independent mounts of the SAME segment (a refresh) render the same
    // subsystem — no client state carried between them. p0345c: the config
    // page leads with the wiring/drift story; its read-events stream sits
    // collapsed behind the toggle.
    const first = renderView("config");
    expect(screen.getByTestId("config-view")).toBeInTheDocument();
    expect(screen.getByTestId("config-stream-toggle")).toBeInTheDocument();
    first.unmount();

    renderView("config");
    expect(screen.getByTestId("config-view")).toBeInTheDocument();
    // a different slug selects a different subsystem.
    expect(screen.queryByTestId("subsystem-detail-tracker")).not.toBeInTheDocument();
  });

  it("SystemPage_NoSlug_RendersDefaultSubsystem", () => {
    renderView(null);
    expect(screen.getByTestId("subsystem-detail-tracker")).toBeInTheDocument();
  });

  it("SystemPage_RollupSlug_RendersRollupCards", () => {
    renderView("cost");
    // p0209c: the cost/today slugs now render the RollupCards KPI grid in place
    // of the p0209b placeholder.
    expect(screen.getByTestId("rollup-cost")).toBeInTheDocument();
    expect(screen.queryByTestId("subsystem-detail-tracker")).not.toBeInTheDocument();
  });
});
