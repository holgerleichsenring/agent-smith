"use client";

import { DialogProposalFindings } from "@/components/dialog/DialogProposalFindings";
import { DialogProposalRounds, notReviewedLabel } from "@/components/dialog/DialogProposalRounds";
import { dispositionMark } from "@/lib/tickets/approvalEffect";
import type { TicketApprovalPreview, TicketConversation } from "@/types/tickets";

// 2026-10-13-9a59r: the open proposal of the ticket's conversation — each draft (goal, repository, done
// when) marked with 9a59l's disposition, then the series' other specs as the preview keeps or drops them.
// No revision number: none is served for a proposal not yet approved.
// 2026-10-13-8762i: each draft's review rounds, what still stands, and what is not reviewed yet.

const DISPOSITION_CLASS: Record<string, string> = { kept: "keep", recut: "recut", appended: "add", dropped: "drop" };

interface Props {
  proposal: NonNullable<TicketConversation["proposal"]>;
  preview: TicketApprovalPreview | null;
}

export function ProposalBlock({ proposal, preview }: Props) {
  const disposition = (specId: string) => preview?.specs.find((s) => s.specId === specId)?.disposition ?? null;
  const drafted = new Set(proposal.drafts.map((d) => d.specId));
  const others = (preview?.specs ?? []).filter((s) => !drafted.has(s.specId));
  const refused = preview?.effect.startsWith("refused") === true;
  const waiting = proposal.notReviewed ?? [];
  return (
    <div className="proposal" data-testid="ticket-proposal">
      <div className="ph">
        Proposed change
        <span className={refused ? "pill bad" : "pill amber"}>{refused ? "refused" : "waiting for your approval"}</span>
      </div>
      <div className="pb">
        {others.map((s) => (
          <span key={s.specId} className={DISPOSITION_CLASS[s.disposition] ?? "keep"} data-testid={`proposal-spec-${s.specId}`}>
            {dispositionMark(s.disposition)} {s.specId} · {s.goal} · {s.disposition}{s.note ? ` — ${s.note}` : ""}
          </span>
        ))}
        {proposal.drafts.map((draft) => {
          const kind = disposition(draft.specId);
          return (
            <div key={draft.specId} className="draft" data-testid={`proposal-draft-${draft.specId}`}>
              <span className={kind ? DISPOSITION_CLASS[kind] ?? "add" : "add"}>
                {dispositionMark(kind)} {draft.goal}{draft.repos.length > 0 ? ` · ${draft.repos.join(", ")}` : ""}
                {kind ? ` · ${kind}` : ""}
              </span>
              {draft.done.length > 0 && <span className="done-when">Done when: {draft.done.join("; ")}</span>}
              {waiting.includes(draft.specId) && (
                <span className="pill amber" data-testid={`proposal-not-reviewed-${draft.specId}`}>not reviewed yet</span>
              )}
              <DialogProposalRounds rounds={proposal.reviewRounds?.filter((r) => r.specId === draft.specId)} />
            </div>
          );
        })}
        <DialogProposalRounds rounds={proposal.reviewRounds?.filter((r) => !drafted.has(r.specId))} />
        {waiting.filter((entry) => !drafted.has(entry)).map((entry) => (
          <span key={entry} className="pill amber" data-testid={`proposal-not-reviewed-${entry}`}>{notReviewedLabel(entry)}</span>
        ))}
        <DialogProposalFindings findings={proposal.findings} />
      </div>
    </div>
  );
}
