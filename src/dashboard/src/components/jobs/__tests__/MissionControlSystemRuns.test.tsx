import { act, fireEvent, render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { HubConnectionState } from "@microsoft/signalr";
import type { OverviewSnapshot, RunSnapshot } from "@/types/hub-events";

// 2026-11-24-35ff: the runs list hides the runs the server starts itself behind one toggle, off by default. The toggle
// names how many there are and how many failed, and the choice is remembered per browser.

let mockOverview: OverviewSnapshot | null = null;

vi.mock("@/hooks/useJobsHub", () => ({
  useJobsHub: () => ({
    client: {},
    connectionState: HubConnectionState.Connected,
    overview: mockOverview,
    systemActivity: null,
  }),
}));
const fetchRunsBefore = vi.fn().mockResolvedValue([]);
vi.mock("@/lib/runsApi", () => ({ fetchRunsBefore: (...args: unknown[]) => fetchRunsBefore(...args) }));
vi.mock("next/navigation", () => ({ useRouter: () => ({ push: vi.fn() }) }));
// 2026-11-24-0fbfb: Cost today is the server's rollup since local midnight, whatever the toggle shows.
const fetchRunSpend = vi.fn();
vi.mock("@/lib/runSpendApi", async (actual) => ({
  ...(await actual<object>()),
  fetchRunSpend: (...args: unknown[]) => fetchRunSpend(...args),
}));

import { MissionControl } from "../MissionControl";
import { RunBucketFilterProvider } from "@/lib/RunBucketFilter";
import { resetShowSystemRuns } from "@/lib/runs/showSystemRuns";

function snap(runId: string, status: string, system = false, startedAt = "2026-11-24T10:00:00Z"): RunSnapshot {
  return {
    runId, pipeline: system ? "merge-green" : "code", trigger: "ticket", repos: ["server"], status,
    prUrl: null, summary: null, startedAt,
    finishedAt: status === "running" ? null : "2026-11-24T11:00:00Z",
    sandboxes: 1, stepIndex: 2, stepName: null, totalSteps: 5, lastEventType: null,
    costUsd: 0, llmCalls: 0, ticketId: null, ticketTitle: null, agentName: null,
    cancelRequested: false, system,
  };
}

const backing = new Map<string, string>();
function stubStorage(): void {
  backing.clear();
  Object.defineProperty(window, "localStorage", {
    configurable: true,
    value: {
      getItem: (k: string) => backing.get(k) ?? null,
      setItem: (k: string, v: string) => void backing.set(k, v),
      removeItem: (k: string) => void backing.delete(k),
      clear: () => backing.clear(),
    },
  });
}

const tree = () => (
  <RunBucketFilterProvider>
    <MissionControl />
  </RunBucketFilterProvider>
);

function renderList() {
  window.history.replaceState(null, "", "/runs");
  return render(tree());
}

describe("the system-runs toggle", () => {
  beforeEach(() => {
    fetchRunSpend.mockReset();
    fetchRunSpend.mockReturnValue(new Promise(() => {}));
    stubStorage();
    resetShowSystemRuns();
    mockOverview = {
      active: [snap("work-live", "running")],
      recent: [snap("work-done", "success"), snap("beat-ok", "success", true), snap("beat-bad", "failed", true)],
      systemActivity: null,
    };
  });

  it("CostToday_SystemRunsHidden_IsTheRollupOfEveryRun", async () => {
    fetchRunSpend.mockResolvedValue({
      from: "", totalUsd: 12.65, work: { costUsd: 10.65, runs: 5 }, system: { costUsd: 2, runs: 206 }, llmCalls: 0, slices: [],
    });
    renderList();
    expect(screen.getByTestId("system-runs-checkbox")).not.toBeChecked();
    expect(await screen.findByText("$12.65")).toBeInTheDocument();
    expect(screen.getByTestId("metric-cost")).toHaveTextContent("work $10.65 · system $2.00");
    const midnight = new Date();
    midnight.setHours(0, 0, 0, 0);
    expect(fetchRunSpend).toHaveBeenCalledWith(midnight.toISOString(), expect.anything());
  });

  it("ByDefault_HidesSystemRuns_AndCountsOnlyWorkRuns", () => {
    renderList();
    expect(screen.getByTestId("system-runs-checkbox")).not.toBeChecked();
    expect(screen.getByTestId("run-pills-all-count")).toHaveTextContent("2");
    expect(screen.getByTestId("run-pills-finished-count")).toHaveTextContent("1");
  });

  it("Label_NamesTheHiddenCount_AndAHiddenFailure", () => {
    renderList();
    const toggle = screen.getByTestId("system-runs-toggle");
    expect(toggle).toHaveTextContent("Show system runs (2 · 1 failed)");
    expect(toggle).toHaveAttribute("data-failed", "true");
  });

  it("Label_WithoutFailures_NamesOnlyTheCount", () => {
    mockOverview = { active: [], recent: [snap("work", "success"), snap("beat", "success", true)], systemActivity: null };
    renderList();
    const toggle = screen.getByTestId("system-runs-toggle");
    expect(toggle).toHaveTextContent("Show system runs (1)");
    expect(toggle).not.toHaveAttribute("data-failed");
  });

  it("TurningItOn_ShowsEveryRun_AndIsRemembered", () => {
    const view = renderList();
    fireEvent.click(screen.getByTestId("system-runs-checkbox"));
    expect(screen.getByTestId("run-pills-all-count")).toHaveTextContent("4");
    expect(screen.getByTestId("system-runs-toggle")).not.toHaveAttribute("data-failed");
    expect(backing.get("agentsmith.show-system-runs")).toBe("1");
    view.unmount();

    act(() => resetShowSystemRuns());
    renderList();
    expect(screen.getByTestId("system-runs-checkbox")).toBeChecked();
    expect(screen.getByTestId("run-pills-all-count")).toHaveTextContent("4");
  });

  it("OnlySystemRuns_StillShowsTheListWithTheToggle", () => {
    mockOverview = { active: [], recent: [snap("beat", "failed", true)], systemActivity: null };
    renderList();
    expect(screen.getByTestId("mission-control")).toBeInTheDocument();
    expect(screen.getByTestId("system-runs-toggle")).toHaveTextContent("Show system runs (1 · 1 failed)");
  });

  it("OnlySystemRunsFinished_StillOffersLoadMore", () => {
    mockOverview = { active: [snap("work-live", "running")], recent: [snap("beat", "success", true)], systemActivity: null };
    renderList();
    expect(screen.getByTestId("section-finished")).toBeInTheDocument();
    expect(screen.getByTestId("runs-load-more")).toBeInTheDocument();
  });

  it("BlockedStorage_HidesSystemRuns_AndTheToggleStillWorks", () => {
    Object.defineProperty(window, "localStorage", {
      configurable: true,
      get: () => { throw new Error("blocked"); },
    });
    resetShowSystemRuns();
    renderList();
    expect(screen.getByTestId("run-pills-all-count")).toHaveTextContent("2");
    fireEvent.click(screen.getByTestId("system-runs-checkbox"));
    expect(screen.getByTestId("run-pills-all-count")).toHaveTextContent("4");
  });

  // 2026-11-24-6357: the server sends a work window and a system window; Load more pages from the end of what it pages.
  it("LoadMore_Hidden_PagesWorkRunsFromTheOldestWorkRun", () => {
    fetchRunsBefore.mockClear();
    mockOverview = {
      active: [],
      recent: [
        snap("beat-new", "success", true, "2026-11-24T12:00:00Z"),
        snap("work-old", "success", false, "2026-11-24T08:00:00Z"),
        snap("beat-old", "success", true, "2026-11-24T06:00:00Z"),
      ],
      systemActivity: null,
    };
    renderList();
    fireEvent.click(screen.getByTestId("runs-load-more"));
    expect(fetchRunsBefore).toHaveBeenCalledWith("2026-11-24T08:00:00Z", 20, undefined, false);
  });

  it("LoadMore_Shown_PagesEveryRunFromTheNewerWindowEnd", () => {
    fetchRunsBefore.mockClear();
    mockOverview = {
      active: [],
      recent: [
        snap("beat-new", "success", true, "2026-11-24T12:00:00Z"),
        snap("beat-old", "success", true, "2026-11-24T11:00:00Z"),
        snap("work-old", "success", false, "2026-11-24T08:00:00Z"),
      ],
      systemActivity: null,
    };
    renderList();
    fireEvent.click(screen.getByTestId("system-runs-checkbox"));
    fireEvent.click(screen.getByTestId("runs-load-more"));
    expect(fetchRunsBefore).toHaveBeenCalledWith("2026-11-24T11:00:00Z", 20, undefined, true);
  });

  it("LoadMore_APageOfKnownRuns_MovesTheCursorOn_InsteadOfEnding", async () => {
    fetchRunsBefore.mockReset();
    const known = snap("beat-old", "success", true, "2026-11-24T11:00:00Z");
    mockOverview = {
      active: [],
      recent: [snap("beat-new", "success", true, "2026-11-24T12:00:00Z"), known,
        snap("work-old", "success", false, "2026-11-24T08:00:00Z")],
      systemActivity: null,
    };
    fetchRunsBefore
      .mockResolvedValueOnce([{ ...snap("work-old", "success", false, "2026-11-24T08:00:00Z") }])
      .mockResolvedValueOnce([snap("beat-older", "success", true, "2026-11-24T05:00:00Z")]);
    renderList();
    fireEvent.click(screen.getByTestId("system-runs-checkbox"));
    await act(async () => { fireEvent.click(screen.getByTestId("runs-load-more")); });
    expect(screen.getByTestId("runs-load-more")).toBeInTheDocument();
    await act(async () => { fireEvent.click(screen.getByTestId("runs-load-more")); });
    expect(fetchRunsBefore).toHaveBeenLastCalledWith("2026-11-24T08:00:00Z", 20, undefined, true);
    fetchRunsBefore.mockResolvedValueOnce([]);
    await act(async () => { fireEvent.click(screen.getByTestId("runs-load-more")); });
    expect(fetchRunsBefore).toHaveBeenLastCalledWith("2026-11-24T05:00:00Z", 20, undefined, true);
    expect(screen.queryByTestId("runs-load-more")).toBeNull();
  });

  it("LoadMore_Hidden_OnlySystemRunsLoaded_StillPagesWorkRuns", () => {
    fetchRunsBefore.mockReset();
    fetchRunsBefore.mockResolvedValue([]);
    mockOverview = { active: [], recent: [snap("beat", "success", true, "2026-11-24T12:00:00Z")], systemActivity: null };
    renderList();
    fireEvent.click(screen.getByTestId("runs-load-more"));
    expect(fetchRunsBefore).toHaveBeenCalledWith("2026-11-24T12:00:00Z", 20, undefined, false);
  });
});

