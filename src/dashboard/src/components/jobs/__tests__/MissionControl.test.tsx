import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { HubConnectionState } from "@microsoft/signalr";
import type { OverviewSnapshot, PendingQuestionInfo, RunSnapshot } from "@/types/hub-events";

let mockOverview: OverviewSnapshot | null = null;

vi.mock("@/hooks/useJobsHub", () => ({
  useJobsHub: () => ({
    client: {},
    connectionState: HubConnectionState.Connected,
    overview: mockOverview,
    systemActivity: null,
  }),
}));

const fetchRunsBeforeMock = vi.fn();
vi.mock("@/lib/runsApi", () => ({
  fetchRunsBefore: (...args: unknown[]) => fetchRunsBeforeMock(...args),
}));

import { MissionControl } from "../MissionControl";

const question: PendingQuestionInfo = {
  questionId: "q1",
  type: "Freeform",
  text: "Keep the Postgres outbox?",
  context: null,
  choices: ["durable inbox", "keep postgres"],
  defaultAnswer: null,
  askedAt: "2026-07-17T11:00:00Z",
  answerDeadlineAt: "2026-07-17T13:00:00Z",
};

function snap(runId: string, status: string, over: Partial<RunSnapshot> = {}): RunSnapshot {
  return {
    runId,
    pipeline: "fix-bug",
    trigger: "ticket",
    repos: ["server"],
    status,
    prUrl: null,
    summary: null,
    startedAt: "2026-07-17T10:00:00Z",
    finishedAt: status === "running" || status === "waiting_for_input" || status === "queued" ? null : "2026-07-17T11:00:00Z",
    sandboxes: 1,
    stepIndex: 2,
    stepName: null,
    totalSteps: 5,
    lastEventType: null,
    costUsd: 0,
    llmCalls: 0,
    ticketId: null,
    ticketTitle: null,
    agentName: null,
    cancelRequested: false,
    ...over,
  };
}

describe("MissionControl", () => {
  beforeEach(() => {
    mockOverview = null;
    fetchRunsBeforeMock.mockReset();
  });

  it("MissionControl_SectionsOrdered_NeedsYouFirst", () => {
    mockOverview = {
      active: [
        snap("needs", "waiting_for_input", { pendingQuestion: question }),
        snap("run", "running"),
        snap("queue", "queued"),
      ],
      recent: [snap("done", "success")],
      systemActivity: null,
    };
    render(<MissionControl />);
    const root = screen.getByTestId("mission-control");
    const ids = Array.from(root.querySelectorAll("[data-testid^='section-']"))
      .map((el) => el.getAttribute("data-testid"))
      .filter((id): id is string => id !== null && !id.endsWith("-count"));
    expect(ids).toEqual(["section-needs-you", "section-running", "section-queued", "section-finished"]);
  });

  it("MissionControl_NeedsYouZero_StillShowsSectionWithReassurance", () => {
    mockOverview = {
      active: [snap("run", "running")],
      recent: [],
      systemActivity: null,
    };
    render(<MissionControl />);
    // Needs-you always renders (top priority), even empty — with a calm line.
    expect(screen.getByTestId("section-needs-you")).toHaveTextContent("Nothing waiting on you.");
    // An empty Queued section is omitted entirely.
    expect(screen.queryByTestId("section-queued")).not.toBeInTheDocument();
  });

  it("MissionControl_NoRuns_ShowsEmptyState", () => {
    mockOverview = { active: [], recent: [], systemActivity: null };
    render(<MissionControl />);
    expect(screen.getByTestId("mission-empty")).toBeInTheDocument();
  });

  it("MissionControl_SectionHeader_RendersTitleCountChipAndHint", () => {
    // p0343b mock section header: bold h2 title + count chip + right hint.
    mockOverview = {
      active: [snap("needs", "waiting_for_input", { pendingQuestion: question })],
      recent: [],
      systemActivity: null,
    };
    render(<MissionControl />);
    const section = screen.getByTestId("section-needs-you");
    expect(section.querySelector("h2")).toHaveTextContent("Needs you");
    expect(screen.getByTestId("section-needs-you-count")).toHaveTextContent("1");
    expect(section).toHaveTextContent("answer here — the run resumes immediately");
  });

  it("MissionControl_RunningSpineHint_OnlyWhenRunsCarryBeats", () => {
    // Honest hint: "spine shows the beat" only when the running runs actually
    // have server-computed beats — pre-beats rows get no such promise.
    mockOverview = {
      active: [snap("r1", "running")],
      recent: [],
      systemActivity: null,
    };
    const { unmount } = render(<MissionControl />);
    expect(screen.getByTestId("section-running")).not.toHaveTextContent("spine shows the beat");
    unmount();

    mockOverview = {
      active: [
        snap("r2", "running", {
          beats: { ticket: "done", plan: "done", building: "active", verify: "pending", outcome: "pending" },
        }),
      ],
      recent: [],
      systemActivity: null,
    };
    render(<MissionControl />);
    expect(screen.getByTestId("section-running")).toHaveTextContent("live · spine shows the beat");
  });

  it("RunsEndpoint_Pages_NewestFirst_LoadMoreAppendsOlder", async () => {
    mockOverview = {
      active: [],
      recent: [snap("done-new", "success", { startedAt: "2026-07-17T10:00:00Z" })],
      systemActivity: null,
    };
    fetchRunsBeforeMock.mockResolvedValue([
      snap("older-1", "success", { startedAt: "2026-07-16T10:00:00Z" }),
    ]);
    render(<MissionControl />);
    // The older run is not in the live window yet.
    expect(screen.queryByTestId("run-row-older-1")).not.toBeInTheDocument();
    fireEvent.click(screen.getByTestId("runs-load-more"));
    // Paged in and appended after the newer run (newest-first).
    await waitFor(() => expect(screen.getByTestId("run-row-older-1")).toBeInTheDocument());
    expect(fetchRunsBeforeMock).toHaveBeenCalledWith("2026-07-17T10:00:00Z", 20);
    const finished = screen.getByTestId("section-finished");
    const ids = Array.from(finished.querySelectorAll("[data-testid^='run-row-']"))
      .map((el) => el.getAttribute("data-testid"))
      .filter((id): id is string => !!id && !id.endsWith("-progress") && !id.endsWith("-actions"));
    expect(ids).toEqual(["run-row-done-new", "run-row-older-1"]);
  });

  it("RunsLoadMore_NoMoreRuns_HidesButton", async () => {
    mockOverview = {
      active: [],
      recent: [snap("done-new", "success", { startedAt: "2026-07-17T10:00:00Z" })],
      systemActivity: null,
    };
    fetchRunsBeforeMock.mockResolvedValue([]); // nothing older
    render(<MissionControl />);
    fireEvent.click(screen.getByTestId("runs-load-more"));
    await waitFor(() =>
      expect(screen.queryByTestId("runs-load-more")).not.toBeInTheDocument(),
    );
  });
});
