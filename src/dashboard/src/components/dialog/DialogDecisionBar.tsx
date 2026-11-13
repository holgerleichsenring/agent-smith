"use client";

import type { ApprovalDecision } from "./approvalDecision";

// 2026-11-12-92e9d: the decision on the proposal awaiting one, at the composer — the only controls Work it out has
// besides send. File and Discard send the approve and reject decisions the approval card sent; the line beside them
// is the card's summary. No shapes are offered: the operator says a different shape in words.

export function DialogDecisionBar({
  decision,
  onDecide,
}: {
  decision: ApprovalDecision;
  onDecide: (answer: "approve" | "reject") => void;
}) {
  if (decision.expired)
    return (
      <p data-testid="dialog-decision-expired" className="ec-sub">
        No longer waiting — nothing was filed. Anything you write starts a new turn.
      </p>
    );
  return (
    <div data-testid="dialog-decision" className="flex flex-wrap items-center gap-2">
      <span data-testid="dialog-decision-summary" className="dsh-body min-w-44 flex-1 text-ink">
        {decision.summary ?? "A proposal is waiting for your decision; the Proposal tab has it in full."}
      </span>
      {decision.approvable && (
        <button type="button" data-testid="dialog-file" className="btn primary" onClick={() => onDecide("approve")}>
          File
        </button>
      )}
      <button type="button" data-testid="dialog-discard" className="btn" onClick={() => onDecide("reject")}>
        Discard
      </button>
    </div>
  );
}
