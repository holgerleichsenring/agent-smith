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
vi.mock("@/lib/runsApi", () => ({ fetchRunsBefore: vi.fn().mockResolvedValue([]) }));
vi.mock("next/navigation", () => ({ useRouter: () => ({ push: vi.fn() }) }));

import { MissionControl } from "../MissionControl";
import { RunBucketFilterProvider } from "@/lib/RunBucketFilter";
import { resetShowSystemRuns } from "@/lib/runs/showSystemRuns";

function snap(runId: string, status: string, system = false): RunSnapshot {
  return {
    runId, pipeline: system ? "merge-green" : "code", trigger: "ticket", repos: ["server"], status,
    prUrl: null, summary: null, startedAt: "2026-11-24T10:00:00Z",
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
    stubStorage();
    resetShowSystemRuns();
    mockOverview = {
      active: [snap("work-live", "running")],
      recent: [snap("work-done", "success"), snap("beat-ok", "success", true), snap("beat-bad", "failed", true)],
      systemActivity: null,
    };
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
});
