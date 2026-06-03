import { render, screen } from "@testing-library/react";
import { describe, it, expect, vi } from "vitest";
import { SystemView } from "@/components/system/SystemView";

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
    // subsystem — no client state carried between them.
    const first = render(<SystemView segment="config" />);
    expect(screen.getByTestId("subsystem-detail-config")).toBeInTheDocument();
    first.unmount();

    render(<SystemView segment="config" />);
    expect(screen.getByTestId("subsystem-detail-config")).toBeInTheDocument();
    // a different slug selects a different subsystem.
    expect(screen.queryByTestId("subsystem-detail-tracker")).not.toBeInTheDocument();
  });

  it("SystemPage_NoSlug_RendersDefaultSubsystem", () => {
    render(<SystemView segment={null} />);
    expect(screen.getByTestId("subsystem-detail-tracker")).toBeInTheDocument();
  });

  it("SystemPage_RollupSlug_RendersPlaceholder", () => {
    render(<SystemView segment="cost" />);
    expect(screen.getByTestId("system-rollup-cost")).toHaveTextContent("p0209c");
  });
});
