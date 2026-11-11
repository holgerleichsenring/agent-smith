import type { SpecDialogProposalPush, SpecDialogQuestionPush } from "@/types/spec-dialog";

/** 2026-11-10-ccd9 review: the proposal an approval card shows and sends — the awaiting one, and only while its id is
 *  the approval question's. A draft under review, or a card for a proposal this page no longer holds, shows nothing. */
export function approvalProposal(
  question: SpecDialogQuestionPush | null,
  awaiting: SpecDialogProposalPush | null,
): SpecDialogProposalPush | null {
  return question?.kind === "approval" && awaiting?.proposalId === question.questionId ? awaiting : null;
}
