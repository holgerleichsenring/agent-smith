import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import type { PendingQuestionInfo, RunSnapshot } from "@/types/hub-events";

const fetchRunMock = vi.fn();
vi.mock("@/lib/runsApi", () => ({
  fetchRun: (...args: unknown[]) => fetchRunMock(...args),
}));

import { NeedsYouCard } from "../NeedsYouCard";

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

function snap(over: Partial<RunSnapshot> = {}): RunSnapshot {
  return {
    runId: "run-1",
    pipeline: "fix-bug",
    trigger: "ticket",
    repos: ["server"],
    status: "waiting_for_input",
    prUrl: null,
    summary: null,
    startedAt: "2026-07-17T10:00:00Z",
    finishedAt: null,
    sandboxes: 1,
    stepIndex: 3,
    stepName: null,
    totalSteps: 7,
    lastEventType: null,
    costUsd: 0,
    llmCalls: 0,
    ticketId: "AZDO-4471",
    ticketTitle: "Migrate messaging",
    agentName: null,
    cancelRequested: false,
    pendingQuestion: question,
    ...over,
  };
}

describe("NeedsYouCard", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    fetchRunMock.mockReset();
    fetchRunMock.mockResolvedValue({ pendingQuestion: null });
  });

  it("NeedsYouCard_InlineQuestion_RendersQuestionWithoutFetching", () => {
    render(<NeedsYouCard snapshot={snap()} />);
    expect(screen.getByTestId("pending-question-card")).toHaveTextContent(question.text);
    // p0343c: the .n-top activity line carries the real pause point.
    expect(screen.getByTestId("needs-you-run-1-toggle")).toHaveTextContent("paused at step 3/7");
  });

  // p0445: a run waiting on an operator is the one row that needs a decision —
  // it must state where it stands, what it cost and how long it has run without
  // being opened, exactly as every finished row does.
  it("AParkedRun_StatesWhereItStandsAndWhatItCost", () => {
    render(<NeedsYouCard snapshot={snap({ costUsd: 4.58, stepIndex: 36, totalSteps: 48 })} />);
    const top = screen.getByTestId("needs-you-run-1-toggle");
    expect(screen.getByTestId("needs-you-run-1-progress")).toHaveTextContent("36/48");
    expect(top).toHaveTextContent("$4.58");
  });

  it("AParkedRun_ShowsTheSameStorySpineAsAFinishedRow", () => {
    render(
      <NeedsYouCard
        snapshot={snap({
          beats: { ticket: "done", plan: "done", building: "active", verify: "pending", outcome: "pending" },
        })}
      />,
    );
    expect(screen.getByTestId("run-row-spine")).toBeInTheDocument();
  });

  it("NeedsYouCard_ParkedRun_HasInlineCancelAndDelete", () => {
    // A parked run must stay fully actionable inline — not just answerable.
    render(<NeedsYouCard snapshot={snap()} />);
    expect(screen.getByTestId("cancel-run-run-1")).toBeInTheDocument();
    expect(screen.getByTestId("delete-run-run-1")).toBeInTheDocument();
  });

  it("NeedsYouCard_AnswerSubmitted_ShowsResumeWithoutNavigation", async () => {
    const fetchMock = vi.fn().mockResolvedValue({ ok: true, status: 202 });
    vi.stubGlobal("fetch", fetchMock);

    render(<NeedsYouCard snapshot={snap()} />);
    fireEvent.change(screen.getByTestId("pending-question-input"), {
      target: { value: "keep postgres" },
    });
    fireEvent.click(screen.getByTestId("pending-question-submit"));

    // The run resumes in place — the card confirms without any route change.
    expect(await screen.findByTestId("pending-question-sent")).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining("/api/runs/run-1/answer"),
      expect.objectContaining({ method: "POST" }),
    );
  });

  // p0458: answering is exactly what makes pendingQuestion null. The card used
  // to read that null as "the question could not be loaded", so the operator
  // whose answer the run had accepted was told it had failed.
  it("AnAcceptedAnswer_IsNeverReportedAsAMissingQuestion", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: true, status: 202 }));
    const { rerender } = render(<NeedsYouCard snapshot={snap()} />);
    fireEvent.click(screen.getByTestId("pending-question-choice-keep postgres"));
    expect(await screen.findByTestId("pending-question-sent")).toBeInTheDocument();

    // The next poll of the run list no longer carries the question.
    rerender(<NeedsYouCard snapshot={snap({ pendingQuestion: null })} />);
    await waitFor(() => expect(screen.getByTestId("pending-question-sent")).toBeInTheDocument());
    expect(screen.queryByTestId("needs-you-run-1-unavailable")).not.toBeInTheDocument();
    expect(fetchRunMock).not.toHaveBeenCalled();
  });

  it("AnAcceptedAnswer_SurvivesCollapsingTheCard", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: true, status: 202 }));
    render(<NeedsYouCard snapshot={snap()} />);
    fireEvent.click(screen.getByTestId("pending-question-choice-keep postgres"));
    expect(await screen.findByTestId("pending-question-sent")).toBeInTheDocument();

    const toggle = screen.getByTestId("needs-you-run-1-toggle");
    fireEvent.click(toggle); // collapse
    fireEvent.click(toggle); // and open again
    expect(screen.getByTestId("pending-question-sent")).toBeInTheDocument();
    expect(screen.queryByTestId("pending-question-submit")).not.toBeInTheDocument();
  });

  // The third state is real and must stay reachable: a run parked before the
  // mid-run question was checkpointed has nothing to answer.
  it("AParkedRunWithNoQuestionToLoad_StillSaysItIsUnavailable", async () => {
    render(<NeedsYouCard snapshot={snap({ pendingQuestion: null })} />);
    expect(await screen.findByTestId("needs-you-run-1-unavailable")).toHaveTextContent(
      "Question unavailable",
    );
  });

  it("AQuestionThatArrivesOnALaterPoll_IsNotCalledUnavailable", async () => {
    const { rerender } = render(<NeedsYouCard snapshot={snap({ pendingQuestion: null })} />);
    expect(await screen.findByTestId("needs-you-run-1-unavailable")).toBeInTheDocument();
    rerender(<NeedsYouCard snapshot={snap()} />);
    await waitFor(() => expect(screen.getByTestId("pending-question-card")).toBeInTheDocument());
    expect(screen.queryByTestId("needs-you-run-1-unavailable")).not.toBeInTheDocument();
  });
});
