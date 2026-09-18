"use client";

import type { SpecDialogProposalFinding } from "@/types/spec-dialog";

// 2026-09-17-042ed: what a fresh instance found wrong with the proposal being approved, listed
// where the decision is made. Each finding shows the framework-minted evidence line it rests on;
// a finding that cites nothing shows what the phase states instead, which is what it quoted.
//
// Nothing is shown for a clean review, and nothing for a review that could not be taken: neither
// is a fault, and a line saying "no findings" would read as a clearance the review never gave.

export function DialogProposalFindings({ findings }: { findings?: SpecDialogProposalFinding[] }) {
  // A proposal stored before this shipped, and every push a version behind, carries no findings
  // field at all — reading .length off it would blank the whole approval card.
  if (!findings?.length) return null;
  return (
    <div data-testid="dialog-proposal-findings" className="mt-1.5 mb-1.5">
      <div className="fl">
        {findings.length === 1 ? "the review found one thing" : `the review found ${findings.length} things`}
      </div>
      <ul className="ml-4 flex list-disc flex-col gap-1">
        {findings.map((finding, index) => (
          <li key={`${finding.phaseId}-${index}`} data-testid="dialog-proposal-finding">
            <span className="dsh-body font-semibold text-ink">
              {finding.phaseId} — {finding.problem}
            </span>
            <span className="dsh-body text-ink">: {finding.why}</span>
            {finding.evidence ? (
              <span data-testid="dialog-finding-evidence" className="ml-1 dsh-mono font-mono text-body">
                {finding.evidence}
              </span>
            ) : (
              finding.quote && (
                <span data-testid="dialog-finding-quote" className="ml-1 dsh-label text-body">
                  it states: “{finding.quote}”
                </span>
              )
            )}
          </li>
        ))}
      </ul>
    </div>
  );
}
