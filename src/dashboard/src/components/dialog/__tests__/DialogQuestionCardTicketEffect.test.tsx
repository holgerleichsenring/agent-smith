import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { DialogQuestionCard } from "../DialogQuestionCard";
import { DialogDecisionBar } from "../DialogDecisionBar";
import { approvalDecision } from "../approvalDecision";
import type { SpecDialogProposalPush, SpecDialogQuestionPush } from "@/types/spec-dialog";

// 2026-10-13-9a59r: the approval card on a ticket-bound conversation says what approving does there, and
// reads "Approve"; without ticketEffect it is the card it always was.

const question: SpecDialogQuestionPush = {
  dialogId: "d-1", questionId: "q-1", kind: "approval", text: "", choices: [], at: "2026-09-15T10:01:00Z", expiresAt: null,
};
const proposal = {
  dialogId: "d-1", kind: "spec", bug: null, parent: null, children: [], at: "2026-09-15T10:03:00Z", findings: [],
  spec: { specId: "p1", goal: "g", steps: [], tests: [], done: [], requires: [], yaml: "", facts: [], assumptions: [] },
} as unknown as SpecDialogProposalPush;

describe("DialogQuestionCard ticketEffect", () => {
  it("WithTicketEffect_ThePrimaryControlReadsApprove_AndTheSummaryIsTheEffectLabel", () => {
    const onAnswer = vi.fn();
    render(<DialogQuestionCard question={question} proposal={proposal} onAnswer={onAnswer}
      ticketEffect={{ summary: "Approving continues this ticket's series.", approvable: true }} />);
    expect(screen.getByTestId("dialog-answer-approve")).toHaveTextContent(/^Approve$/);
    expect(screen.getByTestId("dialog-approval-summary")).toHaveTextContent(/^Approving continues this ticket's series\.$/);
    fireEvent.click(screen.getByTestId("dialog-answer-approve"));
    expect(onAnswer).toHaveBeenCalledWith("approve", "approved");
  });

  it("WithoutTicketEffect_TheCardIsUnchanged", () => {
    render(<DialogQuestionCard question={question} proposal={proposal} onAnswer={vi.fn()} />);
    expect(screen.getByTestId("dialog-answer-approve")).toHaveTextContent("Approve & file");
    expect(screen.getByTestId("dialog-approval-summary")).toHaveTextContent("File this spec? One ticket.");
  });

  it("ANotApprovableEffect_OffersNoApprove", () => {
    render(<DialogQuestionCard question={question} proposal={proposal} onAnswer={vi.fn()}
      ticketEffect={{ summary: "x", approvable: false }} />);
    expect(screen.queryByTestId("dialog-answer-approve")).toBeNull();
    expect(screen.getByTestId("dialog-answer-reject")).toBeInTheDocument();
  });

  // 2026-11-12-92e9d: the composer's File follows the same preview.
  it("ANotApprovableEffect_DecisionBarOffersNoFile", () => {
    const effect = { summary: "x", approvable: false };
    render(<DialogDecisionBar decision={approvalDecision(question, proposal, effect)!} onDecide={vi.fn()} />);
    expect(screen.queryByTestId("dialog-file")).toBeNull();
    expect(screen.getByTestId("dialog-discard")).toBeInTheDocument();
  });

  it("ApprovalDecision_OtherQuestionKind_IsNull", () => {
    expect(approvalDecision({ ...question, kind: "choice" }, proposal)).toBeNull();
  });
});
