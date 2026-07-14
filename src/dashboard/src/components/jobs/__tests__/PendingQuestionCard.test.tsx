import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { PendingQuestionCard } from "../PendingQuestionCard";
import type { PendingQuestionInfo } from "@/types/hub-events";

// p0327: the answer affordance for a waiting_for_input run — renders the
// pending question and POSTs the operator's answer to /api/runs/{id}/answer.

const question: PendingQuestionInfo = {
  questionId: "q-1",
  type: "Approval",
  text: "Approve this plan?",
  context: "1. patch AuthService\n2. add regression test",
  choices: [],
  defaultAnswer: "reject",
  askedAt: "2026-07-11T12:00:00Z",
  answerDeadlineAt: "2026-07-14T12:00:00Z",
};

describe("PendingQuestionCard", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("PendingQuestionCard_RendersQuestionAndDefaultHint", () => {
    render(<PendingQuestionCard runId="run-1" question={question} />);
    expect(screen.getByTestId("pending-question-card")).toBeInTheDocument();
    expect(screen.getByText("Approve this plan?")).toBeInTheDocument();
    expect(screen.getByText(/patch AuthService/)).toBeInTheDocument();
    expect(screen.getByText(/the default/)).toBeInTheDocument();
  });

  it("PendingQuestionCard_ApprovalType_QuickButtonsPostAnswer", async () => {
    const fetchMock = vi.fn().mockResolvedValue({ ok: true, status: 202 });
    vi.stubGlobal("fetch", fetchMock);

    render(<PendingQuestionCard runId="run-1" question={question} />);
    fireEvent.click(screen.getByTestId("pending-question-choice-approve"));

    await waitFor(() => expect(screen.getByTestId("pending-question-sent")).toBeInTheDocument());
    expect(fetchMock).toHaveBeenCalledWith(
      "/api/runs/run-1/answer",
      expect.objectContaining({
        method: "POST",
        body: JSON.stringify({ answer: "approve" }),
      }),
    );
  });

  it("PendingQuestionCard_FreeText_SubmitPostsTypedAnswer", async () => {
    const fetchMock = vi.fn().mockResolvedValue({ ok: true, status: 202 });
    vi.stubGlobal("fetch", fetchMock);

    render(
      <PendingQuestionCard
        runId="run-1"
        question={{ ...question, type: "FreeText", defaultAnswer: null }}
      />,
    );
    fireEvent.change(screen.getByTestId("pending-question-input"), {
      target: { value: "use the staging config" },
    });
    fireEvent.click(screen.getByTestId("pending-question-submit"));

    await waitFor(() => expect(screen.getByTestId("pending-question-sent")).toBeInTheDocument());
    expect(fetchMock).toHaveBeenCalledWith(
      "/api/runs/run-1/answer",
      expect.objectContaining({
        body: JSON.stringify({ answer: "use the staging config" }),
      }),
    );
  });

  it("PendingQuestionCard_ServerError_SurfacesStatus", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: false, status: 409 }));

    render(<PendingQuestionCard runId="run-1" question={question} />);
    fireEvent.click(screen.getByTestId("pending-question-choice-approve"));

    await waitFor(() => expect(screen.getByText("HTTP 409")).toBeInTheDocument());
    expect(screen.queryByTestId("pending-question-sent")).not.toBeInTheDocument();
  });
});
