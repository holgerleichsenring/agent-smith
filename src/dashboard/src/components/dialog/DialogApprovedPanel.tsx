"use client";

import type { ApprovedSetView } from "@/types/spec-dialog";

// 2026-09-25-8e51d: the specification a person APPROVED for the ticket this conversation belongs
// to, labelled with its approval. 2026-11-17-a0b6c: the specs are read off the ticket branch — the set
// a run works from — so the pane and the run can no longer disagree; a branch that could not be read
// is said so instead of showing a stored copy.

export function DialogApprovedPanel({ approved }: { approved: ApprovedSetView }) {
  return (
    <div className="flex flex-col gap-4" data-testid="dialog-approved-panel">
      <p className="dsh-body text-body" data-testid="dialog-approved-approval">
        {approved.approvedAt
          ? `Approved ${new Date(approved.approvedAt).toLocaleString()}` +
            (approved.approvedBy ? ` by ${approved.approvedBy}` : "") +
            ". The specs below are on the ticket branch, where a run reads them."
          : "Approved for this ticket. The specs below are on the ticket branch, where a run reads them."}
      </p>
      {approved.branchUnread && (
        <p className="dsh-body text-body" data-testid="dialog-approved-branch-unread">
          The ticket branch could not be read, so no specs are shown: {approved.branchUnread}
        </p>
      )}
      <ul className="flex flex-col gap-3">
        {approved.specs.map((spec) => (
          <li key={spec.specId} data-testid={`dialog-approved-spec-${spec.specId}`}>
            <p className="dsh-body text-ink">
              <span className="mono">{spec.specId}</span> — {spec.goal}
            </p>
            {spec.done.length > 0 && (
              <ul className="dsh-body text-body ml-4 list-disc">
                {spec.done.map((line) => (
                  <li key={line}>{line}</li>
                ))}
              </ul>
            )}
          </li>
        ))}
      </ul>
    </div>
  );
}
