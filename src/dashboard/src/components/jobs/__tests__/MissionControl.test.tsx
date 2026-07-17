import { render, screen } from "@testing-library/react";
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
});
