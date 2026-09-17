"use client";

import type { SpecDialogProposalPush } from "@/types/spec-dialog";

// 2026-09-17-c7aed: a turn that produced a proposal says so in the exchange — its kind, what it
// is called and, for an epic, its slices — and the draft itself stays in the pane. The card
// is a pointer, never a second rendering of the draft.

export function DialogProposalCard({
  proposal,
  onInspect,
}: {
  proposal: SpecDialogProposalPush;
  onInspect: (proposal: SpecDialogProposalPush) => void;
}) {
  return (
    <div
      data-testid="dialog-card"
      data-kind={proposal.kind}
      className="mt-2 overflow-hidden rounded-md border border-mute"
    >
      <div className="flex items-center gap-2.5 px-3 py-2">
        <span className="rounded-sm border border-primary-deep px-1.5 font-mono dsh-label text-primary-deep">
          {proposal.kind}
        </span>
        <span className="min-w-0 flex-1 dsh-body font-semibold text-ink">{titleOf(proposal)}</span>
        <button
          type="button"
          data-testid="dialog-card-inspect"
          aria-label={`Inspect the ${proposal.kind} proposal: ${titleOf(proposal)}`}
          onClick={() => onInspect(proposal)}
          className="whitespace-nowrap dsh-body text-primary-deep underline underline-offset-2"
        >
          Inspect →
        </button>
      </div>
      {proposal.children.length > 0 && (
        <div className="flex flex-col gap-0.5 border-t border-mute bg-canvas-soft px-3 py-2 dsh-body">
          {proposal.children.map((child) => (
            <div key={child.phaseId}>
              <b className="font-mono dsh-label font-medium text-ink">{child.phaseId}</b>{" "}
              <span className="text-body">{child.goal}</span>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

function titleOf(proposal: SpecDialogProposalPush): string {
  return proposal.bug?.title ?? proposal.parent?.goal ?? proposal.phase?.goal ?? proposal.kind;
}
