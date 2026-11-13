import type { TicketApprovalEffect } from "@/lib/tickets/approvalEffect";
import type { SpecDialogProposalPush, SpecDialogQuestionPush } from "@/types/spec-dialog";

/**
 * 2026-11-12-92e9d: what a decision on the awaiting proposal may do and how it is summed up — one rule for the
 * Work it out composer's File and Discard and for the ticket composer's approval card, so File shows exactly where
 * Approve did.
 */
export interface ApprovalDecision {
  /** The server's wait is over: nothing can be decided, and nothing was filed. */
  expired: boolean;
  /** Whether approving is offered: a ticket list on a bound conversation, or a bound preview that refuses, is not. */
  approvable: boolean;
  /** The one line that says what approving files; null when this page cannot count it. */
  summary: string | null;
  /** The bound preview answered: approving continues the ticket rather than filing a new one. */
  bound: boolean;
}

export const BOUND_LIST = "A list of tickets is filed from Work it out without a ticket; this conversation continues its own ticket.";

// The server pushes nothing when a wait expires, so the deadline is what the page has: a click past it is not an
// answer, it posts the literal word as a message and buys a whole design turn.
export function hasExpired(question: SpecDialogQuestionPush): boolean {
  return question.expiresAt !== null && Date.parse(question.expiresAt) <= Date.now();
}

/** The decision an approval question offers on `proposal`; null for any other question. */
export function approvalDecision(
  question: SpecDialogQuestionPush,
  proposal: SpecDialogProposalPush | null,
  ticketEffect?: TicketApprovalEffect | null,
  inTicket?: boolean,
): ApprovalDecision | null {
  if (question.kind !== "approval") return null;
  const effect = ticketEffect ?? null;
  // 2026-10-14-dbcek: the preview there answers a list with a plain refusal and no start arm, which would read as
  // approvable — so approving is withheld and the summary says where a list is filed.
  const boundList = proposal?.kind === "tickets" && (inTicket === true || effect !== null);
  return {
    expired: hasExpired(question),
    approvable: !boundList && !(effect && !effect.approvable),
    summary: boundList ? BOUND_LIST : effect ? effect.summary : proposal ? summaryOf(proposal) : null,
    bound: effect !== null,
  };
}

/** What the proposal says would be filed, and nothing it does not say. */
export function summaryOf(proposal: SpecDialogProposalPush): string | null {
  if (proposal.bug) return "File this bug? One bug ticket.";
  if (proposal.kind === "comment") return "Post this comment on the ticket? Nothing starts.";
  if (proposal.kind === "tickets") {
    // Not "starts when filed": that depends on the landing of what it requires, read at filing.
    const [count, roots] = [proposal.tickets?.length ?? 0, (proposal.tickets ?? []).filter((t) => t.requires.length === 0).length];
    return `File these ${count} tickets? ${roots} ${roots === 1 ? "has" : "have"} nothing to wait for; the rest wait until what they require is merged.`;
  }
  if (proposal.parent) {
    const slices = proposal.children.length;
    // 2026-09-22-b3d7: the button files ONE ticket, whatever the slice count. The slices are what that ticket
    // carries, so this line counts the one ticket and no records.
    return `File this epic? One work ticket carrying ${slices} slice${slices === 1 ? "" : "s"}.`;
  }
  if (proposal.spec) return "File this spec? One ticket.";
  return null;
}
