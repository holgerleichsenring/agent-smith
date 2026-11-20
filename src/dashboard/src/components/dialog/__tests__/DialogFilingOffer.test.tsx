import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { DialogDecisionBar, decides } from "../DialogDecisionBar";
import { approvalDecision } from "../approvalDecision";
import type { SpecDialogProposalPush, SpecDialogQuestionPush } from "@/types/spec-dialog";

// 2026-11-15-bf77: File follows the design partner's offer. A 12-ticket proposal with open questions the master
// has not offered shows no File; once it offers, File shows beside its line; a master that does not lead is unchanged.
// 2026-11-19-7c42b: an unoffered draft puts no decision and says nothing; the button names what it creates.

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
  const onApprove = vi.fn();
  render(<DialogDecisionBar decision={approvalDecision(question, proposal)!} onApprove={onApprove} onKeepChatting={vi.fn()} />);
  return onApprove;
}

describe("filing offer", () => {
  it("OfferLed_NotOffered_PutsNoDecision_AndSaysNothing", () => {
    const decision = approvalDecision(question, { ...base, offerLed: true })!;
    expect(decision.approvable).toBe(false);
    expect(decision.summary).toBeNull();
    expect(decides(decision, null, "p-1")).toBe(false);
  });

  it("OfferLed_Offered_ShowsFileWithTheMastersLine", () => {
    const onDecide = bar({ ...base, offerLed: true, filingOffer: "Files the 12 tickets; the tracker question goes in as decided." });
    expect(screen.getByTestId("dialog-decision-summary")).toHaveTextContent(/^Files the 12 tickets; the tracker question goes in as decided\.$/);
    expect(screen.getByTestId("dialog-file")).toHaveTextContent(/^Create 12 tickets$/);
    fireEvent.click(screen.getByTestId("dialog-file"));
    expect(onDecide).toHaveBeenCalledOnce();
  });

  it("NotOfferLed_KeepsTodaysFileAndCount", () => {
    bar(base);
    expect(screen.getByTestId("dialog-file")).toBeInTheDocument();
    expect(screen.getByTestId("dialog-decision-summary")).toHaveTextContent(/^File these 12 tickets\?/);
  });

  it("TicketDecision_OfferLedNotOffered_PutsNoDecision", () => {
    const spec = {
      ...base, kind: "spec", tickets: [], offerLed: true,
      spec: { specId: "p1", goal: "g", steps: [], tests: [], done: [], requires: [], yaml: "", facts: [], assumptions: [] },
    } as unknown as SpecDialogProposalPush;
    // 2026-11-19-1b72c: the ticket page decides in the composer's place too; an unoffered draft puts no decision.
    const decision = approvalDecision(question, spec, { summary: "Approving continues this ticket's series.", approvable: true }, true);
    expect(decides(decision, null, null)).toBe(false);
    expect(decision!.summary).toBeNull();
  });
});
