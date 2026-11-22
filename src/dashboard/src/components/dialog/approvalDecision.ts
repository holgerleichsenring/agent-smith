import type { TicketApprovalEffect } from "@/lib/tickets/approvalEffect";
import type { T } from "@/lib/i18n/translate";
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

/** 2026-11-22-5485a: the sentence for a list on a bound conversation, in the language in force. */
export function boundList(t: T): string {
  return t("dialog.approval.boundList");
}

// The server pushes nothing when a wait expires, so the deadline is what the page has: a click past it is not an
// answer, it posts the literal word as a message and buys a whole design turn.
export function hasExpired(question: SpecDialogQuestionPush): boolean {
  return question.expiresAt !== null && Date.parse(question.expiresAt) <= Date.now();
}

/** The decision an approval question offers on `proposal`; null for any other question. */
export function approvalDecision(
  t: T,
  question: SpecDialogQuestionPush,
  proposal: SpecDialogProposalPush | null,
  ticketEffect?: TicketApprovalEffect | null,
  inTicket?: boolean,
): ApprovalDecision | null {
  if (question.kind !== "approval") return null;
  const effect = ticketEffect ?? null;
  // 2026-10-14-dbcek: the preview there answers a list with a plain refusal and no start arm, which would read as
  // approvable — so approving is withheld and the summary says where a list is filed.
  const listOnBound = proposal?.kind === "tickets" && (inTicket === true || effect !== null);
  const { offer, unoffered } = offerOf(proposal);
  return {
    expired: hasExpired(question),
    approvable: !listOnBound && !unoffered && !(effect && !effect.approvable),
    // 2026-11-19-7c42b: an unoffered draft says nothing — the design partner's reply already says what is open.
    summary: listOnBound ? boundList(t) : unoffered ? null : offer ?? (effect ? effect.summary : proposal ? summaryOf(t, proposal) : null),
    bound: effect !== null,
    action: proposal ? actionOf(t, proposal, effect !== null) : t("dialog.approval.approve"),
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
export function actionOf(t: T, proposal: ActionSource, bound: boolean): string {
  if (proposal.kind === "config") return t("dialog.approval.applyChanges", { count: proposal.configChanges?.length ?? 0 });
  if (proposal.kind === "comment") return t("dialog.approval.postComment");
  if (proposal.bug || proposal.kind === "bug") return t("dialog.approval.createBug");
  if (proposal.kind === "tickets") return t("dialog.approval.createTickets", { count: proposal.tickets?.length ?? 0 });
  return bound ? t("dialog.approval.writeSpec") : t("dialog.approval.createTicket");
}

/** What the proposal says would be filed, and nothing it does not say. */
export function summaryOf(t: T, proposal: SpecDialogProposalPush): string | null {
  if (proposal.bug) return t("dialog.approval.summaryBug");
  if (proposal.kind === "comment") return t("dialog.approval.summaryComment");
  if (proposal.kind === "config") return t("dialog.approval.summaryConfig", { count: proposal.configChanges?.length ?? 0 });
  if (proposal.kind === "tickets") {
    // Not "starts when filed": that depends on the landing of what it requires, read at filing.
    const [count, roots] = [proposal.tickets?.length ?? 0, (proposal.tickets ?? []).filter((ticket) => ticket.requires.length === 0).length];
    return t("dialog.approval.summaryTickets", { count, free: t("dialog.approval.summaryTicketsFree", { count: roots }) });
  }
  if (proposal.parent) {
    // 2026-09-22-b3d7: the button files ONE ticket, whatever the slice count. The slices are what that ticket
    // carries, so this line counts the one ticket and no records.
    return t("dialog.approval.summaryEpic", { count: proposal.children.length });
  }
  if (proposal.spec) return t("dialog.approval.summarySpec");
  return null;
}
