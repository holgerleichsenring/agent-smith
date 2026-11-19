"use client";

import type { ApprovalDecision } from "./approvalDecision";

// 2026-11-12-92e9d: the decision on the proposal awaiting one, at the composer.
// 2026-11-19-7c42b: a decision, not a banner. Once the design partner offered the draft it takes the composer's place
// with exactly two answers — create what the draft holds, or keep chatting — the way a permission prompt does. Keep
// chatting gives the composer back and decides nothing; discarding a draft is said in words. Until the draft is
// offered, nothing shows here.

export function DialogDecisionBar({
  decision,
  onApprove,
  onKeepChatting,
}: {
  decision: ApprovalDecision;
  onApprove: () => void;
  onKeepChatting: () => void;
}) {
  return (
    <div data-testid="dialog-decision" className="dl-decision flex flex-col gap-2">
      {decision.summary && (
        <p data-testid="dialog-decision-summary" className="dsh-body m-0 text-ink">
          {decision.summary}
        </p>
      )}
      <div className="flex flex-wrap items-center gap-2">
        <button type="button" data-testid="dialog-file" className="btn primary" onClick={onApprove}>
          {decision.action}
        </button>
        <button type="button" data-testid="dialog-keep-chatting" className="btn" onClick={onKeepChatting}>
          Keep chatting
        </button>
      </div>
    </div>
  );
}

/** Whether the decision is put to the operator now: offered, still answerable, and not set aside for chatting. */
export function decides(decision: ApprovalDecision | null, chattingOn: string | null, proposalId: string | null): boolean {
  return decision !== null && decision.approvable && !decision.expired && chattingOn !== asideKey(proposalId);
}

/** The key a decision is set aside under for Keep chatting — the awaiting proposal's id, "" when it carries none. */
export function asideKey(proposalId: string | null | undefined): string {
  return proposalId ?? "";
}
