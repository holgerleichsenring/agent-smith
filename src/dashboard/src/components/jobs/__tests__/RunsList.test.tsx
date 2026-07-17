import { render, screen, fireEvent, within } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { HubConnectionState } from "@microsoft/signalr";
import type { OverviewSnapshot, RunSnapshot } from "@/types/hub-events";

let mockOverview: OverviewSnapshot | null = null;

vi.mock("@/hooks/useJobsHub", () => ({
  useJobsHub: () => ({
    client: {},
    connectionState: HubConnectionState.Connected,
    overview: mockOverview,
    systemActivity: null,
  }),
}));

import { RunsList } from "../RunsList";

function snap(runId: string, status: string, startedAt: string): RunSnapshot {
  return {
    runId,
    pipeline: "fix-bug",
    trigger: "ticket",
    repos: ["server"],
    status,
    prUrl: null,
    summary: null,
    startedAt,
    finishedAt: status === "running" ? null : startedAt,
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

describe("RunsList", () => {
  beforeEach(() => {
    mockOverview = null;
  });

  it("RunsList_MergesActiveAndRecent_NewestFirst", () => {
    mockOverview = {
      active: [snap("active-old", "running", "2026-06-03T10:00:00Z")],
      recent: [
        snap("recent-newest", "success", "2026-06-03T12:00:00Z"),
        snap("recent-oldest", "failed", "2026-06-03T08:00:00Z"),
      ],
      systemActivity: null,
    };
    render(<RunsList />);
    const list = screen.getByTestId("runs-list");
    const rows = within(list).getAllByTestId(/^run-row-(?!.*-(progress|actions)$).+/);
    const ids = rows.map((r) => r.getAttribute("data-testid"));
    expect(ids).toEqual([
      "run-row-recent-newest",
      "run-row-active-old",
      "run-row-recent-oldest",
    ]);
  });

  it("RunsList_EmptyAfterFilter_ShowsNoRunsMatch", () => {
    mockOverview = {
      active: [],
      recent: [snap("only-success", "success", "2026-06-03T12:00:00Z")],
      systemActivity: null,
    };
    render(<RunsList />);
    // No failed runs — clicking Failed empties the list.
    fireEvent.click(screen.getByTestId("run-filter-fail"));
    expect(screen.getByTestId("runs-empty-filtered")).toHaveTextContent("No runs match.");
  });
});
