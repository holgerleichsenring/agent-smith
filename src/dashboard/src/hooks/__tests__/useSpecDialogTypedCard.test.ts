import { describe, expect, it } from "vitest";
import { typedCard, type DialogEntry } from "@/hooks/useSpecDialog";
import { approvalProposal } from "@/components/dialog/approvalProposal";
import type { SpecDialogProposalPush, SpecDialogQuestionPush } from "@/types/spec-dialog";

// 2026-11-10-ccd9: a typed approve or reject names the open card only while that card is the latest agent entry.
const question: SpecDialogQuestionPush = {
  dialogId: "d-1", questionId: "p-1", kind: "approval", text: "", choices: [], at: "2026-11-10T10:00:00Z", expiresAt: null,
};
const proposal = { dialogId: "d-1", kind: "spec", proposalId: "p-1" } as unknown as SpecDialogProposalPush;
const card: DialogEntry = { key: "a1", kind: "agent", text: "Here is the cut.", at: "2026-11-10T10:00:00Z", proposal };

describe("typedCard", () => {
  it("TypedWord_CardIsTheLatestAgentEntry_NamesTheCard", () => {
    const user: DialogEntry = { key: "u1", kind: "user", text: "yes", at: "2026-11-10T10:01:00Z" };
    expect(typedCard(question, [card, user])).toBe("p-1");
  });

  it("TypedWord_CardNotLatestEntry_IsAnOrdinaryMessage", () => {
    const later: DialogEntry = { key: "a2", kind: "agent", text: "Because the store is shared.", at: "2026-11-10T10:02:00Z" };
    expect(typedCard(question, [card, later])).toBeUndefined();
  });

  it("TypedWord_NoApprovalQuestion_NamesNothing", () => {
    expect(typedCard(null, [card])).toBeUndefined();
  });

  // Review M1: the latest entry has to carry the card's OWN proposal, not merely some proposal.
  it("TypedWord_LatestEntryCarriesAnotherProposal_NamesNothing", () => {
    const other: DialogEntry = { ...card, proposal: { ...proposal, proposalId: "p-0" } };
    expect(typedCard(question, [other])).toBeUndefined();
  });

  // Review L6: a proposal with open questions is decided by its buttons; a bare typed word is a message.
  it("TypedWord_ProposalWithOpenQuestions_IsAnOrdinaryMessage", () => {
    const asking: DialogEntry = {
      ...card, proposal: { ...proposal, questions: [{ question: "Which store?", proposal: "Redis", specIds: [] }] },
    };
    expect(typedCard(question, [asking])).toBeUndefined();
  });
});

describe("approvalProposal", () => {
  it("ApprovalCard_AwaitingIdMatches_ShowsTheAwaitingProposal", () => {
    expect(approvalProposal(question, proposal)).toBe(proposal);
  });

  it("ApprovalCard_AwaitingIdDiffers_ShowsNothing", () => {
    expect(approvalProposal(question, { ...proposal, proposalId: "p-0" })).toBeNull();
    expect(approvalProposal({ ...question, kind: "confirmation" }, proposal)).toBeNull();
  });
});
