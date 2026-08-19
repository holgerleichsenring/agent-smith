import { render, screen } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { HubConnectionState } from "@microsoft/signalr";
import type { OverviewSnapshot, RunSnapshot } from "@/types/hub-events";

// p0458: the rail's monitor items promise to SHOW a bucket. These pin what the
// list does with that promise — one bucket at a time, an empty one named rather
// than blank, and the live list still live while a filter is on.

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

function snap(runId: string, status: string): RunSnapshot {
  return {
    runId, pipeline: "fix-bug", trigger: "ticket", repos: ["server"], status,
    prUrl: null, summary: null, startedAt: "2026-07-17T10:00:00Z",
    finishedAt: status === "success" ? "2026-07-17T11:00:00Z" : null,
    sandboxes: 1, stepIndex: 2, stepName: null, totalSteps: 5, lastEventType: null,
    costUsd: 0, llmCalls: 0, ticketId: null, ticketTitle: null, agentName: null,
    cancelRequested: false,
  };
}

// A FUNCTION, not a constant: React skips a rerender with the identical element.
const tree = () => (
  <RunBucketFilterProvider>
    <MissionControl />
  </RunBucketFilterProvider>
);

function renderAt(search: string) {
  window.history.replaceState(null, "", `/${search}`);
  return render(tree());
}

describe("the rail's bucket filters the run list", () => {
  beforeEach(() => {
    mockOverview = {
      active: [snap("r-run", "running"), snap("r-queued", "queued")],
      recent: [snap("r-done", "success")],
      systemActivity: null,
    };
  });

  it("ChoosingABucket_ShowsOnlyThatBucket", () => {
    renderAt("?bucket=running");
    expect(screen.getByTestId("section-running")).toBeInTheDocument();
    for (const id of ["section-queued", "section-finished", "section-needs-you"]) {
      expect(screen.queryByTestId(id)).not.toBeInTheDocument();
    }
  });

  it("AllRuns_ShowsEveryBucket", () => {
    renderAt("");
    for (const id of ["section-needs-you", "section-running", "section-queued", "section-finished"]) {
      expect(screen.getByTestId(id)).toBeInTheDocument();
    }
  });

  it("AnEmptyChosenBucket_SaysWhichBucketIsEmpty", () => {
    // Asked for BY NAME an empty bucket has to answer, or the click yields a blank page.
    mockOverview = { active: [snap("r-run", "running")], recent: [], systemActivity: null };
    renderAt("?bucket=queued");
    expect(screen.getByTestId("section-queued")).toHaveTextContent("Nothing is queued.");
  });

  it("AnEmptyBucketNobodyAskedFor_IsStillOmitted", () => {
    // Unfiltered, the home screen shows only live buckets — that stays.
    mockOverview = { active: [snap("r-run", "running")], recent: [], systemActivity: null };
    renderAt("");
    expect(screen.queryByTestId("section-queued")).not.toBeInTheDocument();
  });

  it("AFilteredList_KeepsUpdatingWhileTheFilterIsOn", () => {
    const { rerender } = renderAt("?bucket=running");
    expect(screen.queryByTestId("run-row-r-run-2")).not.toBeInTheDocument();
    mockOverview = {
      active: [snap("r-run", "running"), snap("r-run-2", "running"), snap("r-queued", "queued")],
      recent: [snap("r-done", "success")],
      systemActivity: null,
    };
    rerender(tree());
    expect(screen.getByTestId("run-row-r-run-2")).toBeInTheDocument();
    expect(screen.queryByTestId("section-queued")).not.toBeInTheDocument();
  });

  it("AFilterFromTheUrl_SurvivesAReload", () => {
    // A fresh mount is what a reload is — the bucket comes back off the URL.
    renderAt("?bucket=finished");
    expect(screen.getByTestId("mission-control")).toHaveAttribute("data-bucket", "finished");
    expect(screen.getByTestId("run-row-r-done")).toBeInTheDocument();
    expect(screen.queryByTestId("run-row-r-run")).not.toBeInTheDocument();
  });

  it("AnUnknownBucketInTheUrl_ShowsEverything", () => {
    renderAt("?bucket=nonsense");
    expect(screen.getByTestId("mission-control")).toHaveAttribute("data-bucket", "all");
  });
});
