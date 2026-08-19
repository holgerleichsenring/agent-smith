import { fireEvent, render, screen } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { AppRail } from "../AppRail";
import { RunBucketFilterProvider } from "@/lib/RunBucketFilter";
import { EventStoreProvider } from "@/lib/eventStore/EventStoreProvider";
import { silentEventStore } from "@/lib/eventStore/__tests__/fakes";

// p0458: a rail item with a live count next to it reads as a filter, so it is
// one. These pin the half the operator sees: the chosen bucket is the
// highlighted item, choosing one stays on the page, and Pull requests remains
// the separate destination it always was.

const usePathname = vi.fn(() => "/");
const push = vi.fn();
vi.mock("next/navigation", () => ({
  usePathname: () => usePathname(),
  useRouter: () => ({ push }),
}));

vi.mock("@/hooks/useJobsHub", () => ({
  useJobsHub: () => ({
    client: { systemEvents: { add: () => () => {} }, subscribeSystem: () => Promise.resolve(() => {}) },
    connectionState: 1,
    overview: null,
    systemActivity: null,
  }),
}));

vi.mock("@/lib/pullRequestsApi", () => ({ fetchPullRequests: vi.fn().mockResolvedValue([]) }));

function renderRail(url: string) {
  window.history.replaceState(null, "", url);
  return render(
    <EventStoreProvider store={silentEventStore()}>
      <RunBucketFilterProvider>
        <AppRail />
      </RunBucketFilterProvider>
    </EventStoreProvider>,
  );
}

describe("the rail's monitor items are a filter", () => {
  beforeEach(() => {
    usePathname.mockReturnValue("/");
    push.mockReset();
  });

  it("TheChosenBucket_IsTheHighlightedRailItem", () => {
    renderRail("/?bucket=queued");
    expect(screen.getByTestId("app-rail-item-Queued")).toHaveAttribute("data-active", "true");
    expect(screen.getByTestId("app-rail-item-All runs")).toHaveAttribute("data-active", "false");
    expect(screen.getByTestId("app-rail-item-Running")).toHaveAttribute("data-active", "false");
  });

  it("NoBucketChosen_HighlightsAllRuns", () => {
    renderRail("/");
    expect(screen.getByTestId("app-rail-item-All runs")).toHaveAttribute("data-active", "true");
  });

  it("AwayFromTheHomeScreen_NoBucketClaimsToBeOnScreen", () => {
    usePathname.mockReturnValue("/system/cost");
    renderRail("/system/cost");
    for (const label of ["All runs", "Needs you", "Running", "Queued", "Finished"]) {
      expect(screen.getByTestId(`app-rail-item-${label}`)).toHaveAttribute("data-active", "false");
    }
  });

  it("ChoosingABucket_PutsItInTheUrlWithoutLeavingThePage", () => {
    renderRail("/");
    fireEvent.click(screen.getByTestId("app-rail-item-Running"));
    expect(window.location.search).toBe("?bucket=running");
    // No route push — the run list stays mounted and keeps updating.
    expect(push).not.toHaveBeenCalled();
    expect(screen.getByTestId("app-rail-item-Running")).toHaveAttribute("data-active", "true");
  });

  it("ChoosingAllRuns_ClearsTheFilter", () => {
    renderRail("/?bucket=running");
    fireEvent.click(screen.getByTestId("app-rail-item-All runs"));
    expect(window.location.search).toBe("");
    expect(screen.getByTestId("app-rail-item-All runs")).toHaveAttribute("data-active", "true");
  });

  it("ChoosingABucketFromAnotherPage_NavigatesHomeWithIt", () => {
    usePathname.mockReturnValue("/system/cost");
    renderRail("/system/cost");
    fireEvent.click(screen.getByTestId("app-rail-item-Finished"));
    expect(push).toHaveBeenCalledWith("/?bucket=finished");
  });

  it("PullRequests_StaysItsOwnDestination", () => {
    renderRail("/?bucket=finished");
    const item = screen.getByTestId("app-rail-item-Pull requests");
    expect(item).toHaveAttribute("href", "/pull-requests");
    fireEvent.click(item);
    // Not a bucket: it is a route, and clicking it leaves the filter alone.
    expect(window.location.search).toBe("?bucket=finished");
    expect(screen.getByTestId("app-rail-item-Finished")).toHaveAttribute("data-active", "true");
  });
});
