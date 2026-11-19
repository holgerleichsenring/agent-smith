import { actionOf, approvalDecision, BOUND_LIST, offerOf, type ApprovalDecision } from "@/components/dialog/approvalDecision";
import { approvalProposal } from "@/components/dialog/approvalProposal";
import { ticketApprovalEffect } from "@/lib/tickets/approvalEffect";
import type { SpecDialogProposalPush, SpecDialogQuestionPush } from "@/types/spec-dialog";
import type { TicketApprovalPreview, TicketConversation } from "@/types/tickets";
import { isOpenApproval } from "./useTicketComposer";

// 2026-11-19-1b72c: the decision the ticket composer puts in its own place — Work it out's rule (approvalDecision, 7c42b)
// on a ticket-bound conversation. Before the first act the approval question is not watched yet, so an open session
// with a proposal is one whose approval waits there and the decision is read from the page's conversation and preview,
// offer gate included; once watched, the awaiting proposal the question names decides, and a gone question nothing.

export interface TicketDecision {
  decision: ApprovalDecision;
  /** The proposal the decision was shown for — what an approve of it names. */
  proposalId: string | null;
}

export interface WatchedApproval {
  question: SpecDialogQuestionPush | null;
  awaiting: SpecDialogProposalPush | null;
}

export function ticketDecision(
  conversation: TicketConversation,
  preview: TicketApprovalPreview | null,
  watched: WatchedApproval | null,
): TicketDecision | null {
  const effect = preview ? ticketApprovalEffect(preview) : null;
  if (watched) {
    if (!isOpenApproval(watched.question)) return null;
    const card = approvalProposal(watched.question, watched.awaiting);
    // approvalProposal holds the card only while its proposalId is the question's, so both paths key by that id.
    if (card) return { decision: approvalDecision(watched.question, card, effect, true)!, proposalId: card.proposalId ?? null };
  } else if (conversation.openDialogId === null) return null;
  const proposal = conversation.proposal;
  if (!proposal || !effect) return null;
  // The page's own id, before and after the question is watched, so Keep chatting holds across the first act; an approve
  // without one names the open question when it is sent (useTicketComposer).
  return { decision: fromConversation(proposal, effect), proposalId: proposal.proposalId ?? null };
}

/** The page's own read of the proposal: approvalDecision's guards on a bound conversation, the offer gate included. */
function fromConversation(proposal: NonNullable<TicketConversation["proposal"]>, effect: ReturnType<typeof ticketApprovalEffect>): ApprovalDecision {
  const list = proposal.kind === "tickets";
  const { offer, unoffered } = offerOf(proposal);
  return {
    expired: false,
    approvable: !list && !unoffered && effect.approvable,
    summary: list ? BOUND_LIST : unoffered ? null : offer ?? effect.summary,
    bound: true,
    action: actionOf(proposal, true),
  };
}
