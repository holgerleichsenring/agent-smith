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
  /** 2026-11-17-f3a8c: what the approve button says. 2026-11-19-7c42b: what approving creates, by kind and count —
   *  "Create 6 tickets", "Apply 3 changes" — never the bare "File". */
  action: string;
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
  const { offer, unoffered } = offerOf(proposal);
  return {
    expired: hasExpired(question),
    approvable: !boundList && !unoffered && !(effect && !effect.approvable),
    // 2026-11-19-7c42b: an unoffered draft says nothing — the design partner's reply already says what is open.
    summary: boundList ? BOUND_LIST : unoffered ? null : offer ?? (effect ? effect.summary : proposal ? summaryOf(proposal) : null),
    bound: effect !== null,
    action: proposal ? actionOf(proposal, effect !== null) : "Approve",
  };
}

/** What actionOf reads of a proposal — the ticket page's conversation proposal carries the kind alone (2026-11-19-1b72c). */
export type ActionSource = Pick<SpecDialogProposalPush, "kind"> & Partial<Pick<SpecDialogProposalPush, "bug" | "tickets" | "configChanges">>;

/** What the offer gate reads of a proposal — the push and the ticket page's conversation proposal both carry it. */
export type OfferSource = Partial<Pick<SpecDialogProposalPush, "offerLed" | "filingOffer">>;

/**
 * 2026-11-15-bf77: a master that offers filing leads it — the decision waits for its offer on this draft and shows its
 * line. A proposal from a master whose skill does not know offer_filing (offerLed absent) is never held back.
 * 2026-11-19-1b72c: one rule for Work it out's decision and the ticket page's.
 */
export function offerOf(proposal: OfferSource | null): { offer: string | null; unoffered: boolean } {
  const offer = proposal?.offerLed ? (proposal.filingOffer ?? null) : null;
  return { offer, unoffered: proposal?.offerLed === true && offer === null };
}

/** 2026-11-19-7c42b: the approve button names what it creates, so nobody reads "File" as a file. */
export function actionOf(proposal: ActionSource, bound: boolean): string {
  if (proposal.kind === "config") {
    const count = proposal.configChanges?.length ?? 0;
    return count === 1 ? "Apply 1 change" : `Apply ${count} changes`;
  }
  if (proposal.kind === "comment") return "Post comment";
  if (proposal.bug || proposal.kind === "bug") return "Create bug ticket";
  if (proposal.kind === "tickets") {
    const count = proposal.tickets?.length ?? 0;
    return count === 1 ? "Create 1 ticket" : `Create ${count} tickets`;
  }
  return bound ? "Write spec to ticket" : "Create ticket";
}

/** What the proposal says would be filed, and nothing it does not say. */
export function summaryOf(proposal: SpecDialogProposalPush): string | null {
  if (proposal.bug) return "File this bug? One bug ticket.";
  if (proposal.kind === "comment") return "Post this comment on the ticket? Nothing starts.";
  if (proposal.kind === "config") {
    const count = proposal.configChanges?.length ?? 0;
    return `Apply ${count === 1 ? "this configuration change" : `these ${count} configuration changes`}? A config run writes them through the Config studio's checks.`;
  }
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
