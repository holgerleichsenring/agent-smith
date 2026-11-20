import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { DialogDecisionBar, decides } from "../DialogDecisionBar";
import { DialogQuestionCard } from "../DialogQuestionCard";
import { approvalDecision } from "../approvalDecision";
import type { SpecDialogProposalPush, SpecDialogQuestionPush } from "@/types/spec-dialog";

// 2026-10-13-9a59r: on a ticket-bound conversation the decision says what approving does there; without ticketEffect
// it is the decision it always was. 2026-11-19-1b72c: the approval card left; the decision bar takes the composer's
// place on the ticket page as on Work it out, and the card answers only the questions that are not an approval.

const question: SpecDialogQuestionPush = {
  dialogId: "d-1", questionId: "q-1", kind: "approval", text: "", choices: [], at: "2026-09-15T10:01:00Z", expiresAt: null,
};
const proposal = {
  dialogId: "d-1", kind: "spec", bug: null, parent: null, children: [], at: "2026-09-15T10:03:00Z", findings: [],
  spec: { specId: "p1", goal: "g", steps: [], tests: [], done: [], requires: [], yaml: "", facts: [], assumptions: [] },
} as unknown as SpecDialogProposalPush;

describe("ticket decision effect", () => {
  it("WithTicketEffect_TheActionWritesTheSpecToTheTicket_AndTheSummaryIsTheEffectLabel", () => {
    const onApprove = vi.fn();
    const decision = approvalDecision(question, proposal, { summary: "Approving continues this ticket's series.", approvable: true }, true)!;
    render(<DialogDecisionBar decision={decision} onApprove={onApprove} onKeepChatting={vi.fn()} />);
    expect(screen.getByTestId("dialog-file")).toHaveTextContent(/^Write spec to ticket$/);
    expect(screen.getByTestId("dialog-decision-summary")).toHaveTextContent(/^Approving continues this ticket's series\.$/);
    fireEvent.click(screen.getByTestId("dialog-file"));
    expect(onApprove).toHaveBeenCalledOnce();
  });

  it("WithoutTicketEffect_TheDecisionIsUnchanged", () => {
    render(<DialogDecisionBar decision={approvalDecision(question, proposal)!} onApprove={vi.fn()} onKeepChatting={vi.fn()} />);
    expect(screen.getByTestId("dialog-file")).toHaveTextContent("Create ticket");
    expect(screen.getByTestId("dialog-decision-summary")).toHaveTextContent("File this spec? One ticket.");
  });

  // 2026-11-12-92e9d: the composer's decision follows the same preview.
  // 2026-11-19-7c42b: a not-approvable decision is not put; the composer stays.
  it("ANotApprovableEffect_DecisionBarIsNotPut", () => {
    const effect = { summary: "x", approvable: false };
    expect(decides(approvalDecision(question, proposal, effect), null, "p-1")).toBe(false);
  });

  it("ApprovalDecision_OtherQuestionKind_IsNull", () => {
    expect(approvalDecision({ ...question, kind: "choice" }, proposal)).toBeNull();
  });

  it("DialogQuestionCard_Approval_OffersNoApproveOrReject", () => {
    render(<DialogQuestionCard question={question} onAnswer={vi.fn()} />);
    expect(screen.queryByTestId("dialog-answer-approve")).toBeNull();
    expect(screen.queryByTestId("dialog-answer-reject")).toBeNull();
  });

  it("DialogQuestionCard_Choice_AnswersWithTheLabel", () => {
    const onAnswer = vi.fn();
    render(<DialogQuestionCard question={{ ...question, kind: "choice", choices: [{ label: "Postgres", description: null }] }} onAnswer={onAnswer} />);
    fireEvent.click(screen.getByTestId("dialog-answer-Postgres"));
    expect(onAnswer).toHaveBeenCalledWith("Postgres");
  });
});
