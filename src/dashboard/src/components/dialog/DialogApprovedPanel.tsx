"use client";

import type { ApprovedSetView } from "@/types/spec-dialog";

// 2026-09-25-8e51d: the specification a person APPROVED for the ticket this conversation belongs
// to. It is labelled with the approval it is, deliberately: the branch is the only set a RUN
// reads, and for a ticket already worked the two can differ — the branch may be several revisions
// ahead. A reader must never take this for what the run is doing.

export function DialogApprovedPanel({ approved }: { approved: ApprovedSetView }) {
  return (
    <div className="flex flex-col gap-4" data-testid="dialog-approved-panel">
      <p className="dsh-body text-body" data-testid="dialog-approved-approval">
        {approved.approvedAt
          ? `Approved ${new Date(approved.approvedAt).toLocaleString()}` +
            (approved.approvedBy ? ` by ${approved.approvedBy}` : "") +
            ". This is what a person ratified — not necessarily what a run is working now."
          : "Approved for this ticket. This is what a person ratified — not necessarily what a run is working now."}
      </p>
      <ul className="flex flex-col gap-3">
        {approved.phases.map((phase) => (
          <li key={phase.phaseId} data-testid={`dialog-approved-phase-${phase.phaseId}`}>
            <p className="dsh-body text-ink">
              <span className="mono">{phase.phaseId}</span> — {phase.goal}
            </p>
            {phase.done.length > 0 && (
              <ul className="dsh-body text-body ml-4 list-disc">
                {phase.done.map((line) => (
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
