import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { DialogDecisionBar } from "../DialogDecisionBar";
import { DialogQuestionCard } from "../DialogQuestionCard";
import { NOT_OFFERED, approvalDecision } from "../approvalDecision";
import type { SpecDialogProposalPush, SpecDialogQuestionPush } from "@/types/spec-dialog";

// 2026-11-15-bf77: File follows the design partner's offer. A 12-ticket proposal with open questions the master
// has not offered shows no File; once it offers, File shows beside its line; a master that does not lead is unchanged.

const question: SpecDialogQuestionPush = {
  dialogId: "d-1", questionId: "q-1", kind: "approval", text: "", choices: [], at: "2026-11-15T10:01:00Z", expiresAt: null,
};
const tickets = Array.from({ length: 12 }, (_, i) => ({
  key: `t${i}`, title: `T${i}`, requirement: "r", done: [], requires: [], repos: [],
}));
const base = {
  dialogId: "d-1", kind: "tickets", bug: null, spec: null, parent: null, children: [], at: "2026-11-15T10:03:00Z",
  findings: [], tickets, questions: [{ question: "Which tracker?", proposal: "The default one", specIds: [] }],
} as unknown as SpecDialogProposalPush;

function bar(proposal: SpecDialogProposalPush) {
  const onDecide = vi.fn();
  render(<DialogDecisionBar decision={approvalDecision(question, proposal)!} onDecide={onDecide} />);
  return onDecide;
}

describe("filing offer", () => {
  it("OfferLed_NotOffered_ShowsNoFile_KeepsDiscard_AndSaysToAsk", () => {
    bar({ ...base, offerLed: true });
    expect(screen.queryByTestId("dialog-file")).toBeNull();
    expect(screen.getByTestId("dialog-discard")).toBeInTheDocument();
    expect(screen.getByTestId("dialog-decision-summary")).toHaveTextContent(NOT_OFFERED);
  });

  it("OfferLed_Offered_ShowsFileWithTheMastersLine", () => {
    const onDecide = bar({ ...base, offerLed: true, filingOffer: "Files the 12 tickets; the tracker question goes in as decided." });
    expect(screen.getByTestId("dialog-decision-summary")).toHaveTextContent(/^Files the 12 tickets; the tracker question goes in as decided\.$/);
    fireEvent.click(screen.getByTestId("dialog-file"));
    expect(onDecide).toHaveBeenCalledWith("approve");
  });

  it("NotOfferLed_KeepsTodaysFileAndCount", () => {
    bar(base);
    expect(screen.getByTestId("dialog-file")).toBeInTheDocument();
    expect(screen.getByTestId("dialog-decision-summary")).toHaveTextContent(/^File these 12 tickets\?/);
  });

  it("TicketCard_OfferLedNotOffered_OffersNoApprove", () => {
    const spec = {
      ...base, kind: "spec", tickets: [], offerLed: true,
      spec: { specId: "p1", goal: "g", steps: [], tests: [], done: [], requires: [], yaml: "", facts: [], assumptions: [] },
    } as unknown as SpecDialogProposalPush;
    render(<DialogQuestionCard question={question} proposal={spec} onAnswer={vi.fn()}
      ticketEffect={{ summary: "Approving continues this ticket's series.", approvable: true }} inTicket />);
    expect(screen.queryByTestId("dialog-answer-approve")).toBeNull();
    expect(screen.getByTestId("dialog-approval-summary")).toHaveTextContent(NOT_OFFERED);
  });
});
