import { render, screen, fireEvent } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import type { PendingQuestionInfo, RunSnapshot } from "@/types/hub-events";
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
  });

  it("NeedsYouCard_InlineQuestion_RendersQuestionWithoutFetching", () => {
    render(<NeedsYouCard snapshot={snap()} />);
    expect(screen.getByTestId("pending-question-card")).toHaveTextContent(question.text);
    expect(screen.getByText(/paused at step 3\/7/)).toBeInTheDocument();
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
});
