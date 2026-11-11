"use client";

import { TurnResult } from "@/components/dialog/TurnResult";
import type { TurnProposal } from "@/components/dialog/turnProposal";
import { dispositionMark } from "@/lib/tickets/approvalEffect";
import type { TicketApprovalPreview, TicketConversation } from "@/types/tickets";

// 2026-10-13-9a59r: the open proposal of the ticket's conversation — each draft (goal, repository, done
// when) marked with 9a59l's disposition, then the series' other specs as the preview keeps or drops them.
// No revision number: none is served for a proposal not yet approved.
// 2026-10-13-8762i: each draft's review rounds, what still stands, and what is not reviewed yet.
// 2026-10-13-8762t: the mock version a person ratified for a draft; ratifying is the conversation's act.
// 2026-10-28-7a69c: a proposed comment is shown as the text that would be posted.
// 2026-11-10-d854b: rendered as the conversation's own TurnResult — one row per spec, questions and the cap
// banner — with this page's own parts (dispositions, the series' other specs, ratified mocks) passed in as slots.

const DISPOSITION_CLASS: Record<string, string> = { kept: "keep", recut: "recut", appended: "add", dropped: "drop" };

type TicketProposal = NonNullable<TicketConversation["proposal"]>;

interface Props {
  proposal: TicketProposal;
  preview: TicketApprovalPreview | null;
}

export function ProposalBlock({ proposal, preview }: Props) {
  const disposition = (specId: string) => preview?.specs.find((s) => s.specId === specId)?.disposition ?? null;
  const drafted = new Set(proposal.drafts.map((d) => d.specId));
  const others = (preview?.specs ?? []).filter((s) => !drafted.has(s.specId));
  const refused = preview?.effect.startsWith("refused") === true;
  const ratified = (specId: string) => proposal.drafts.find((d) => d.specId === specId)?.ratifiedMock ?? null;
  return (
    <div className="proposal" data-testid="ticket-proposal">
      <div className="ph">
        Proposed change
        <span className={refused ? "pill bad" : "pill amber"}>{refused ? "refused" : "waiting for your approval"}</span>
      </div>
      <div className="pb">
        <TurnResult
          proposal={turnProposalOfTicket(proposal)}
          before={others.map((s) => (
            <span key={s.specId} className={DISPOSITION_CLASS[s.disposition] ?? "keep"} data-testid={`proposal-spec-${s.specId}`}>
              {dispositionMark(s.disposition)} {s.specId} · {s.goal} · {s.disposition}{s.note ? ` — ${s.note}` : ""}
            </span>
          ))}
          rowExtra={(specId) => {
            const kind = disposition(specId);
            const mock = ratified(specId);
            if (!kind && mock == null) return null;
            return (
              <span className="flex flex-wrap gap-2" data-testid={`proposal-draft-${specId}`}>
                {kind && <span className={DISPOSITION_CLASS[kind] ?? "add"}>{dispositionMark(kind)} {kind}</span>}
                {mock != null && <span className="pill ok" data-testid={`proposal-ratified-mock-${specId}`}>mock v{mock} ratified</span>}
              </span>
            );
          }}
        />
      </div>
    </div>
  );
}

function turnProposalOfTicket(proposal: TicketProposal): TurnProposal {
  return {
    kind: proposal.kind,
    drafts: proposal.drafts.map((d) => ({ specId: d.specId, goal: d.goal, repos: d.repos, done: d.done, yaml: d.yaml })),
    findings: proposal.findings, reviewRounds: proposal.reviewRounds, notReviewed: proposal.notReviewed,
    questions: proposal.questions, spend: proposal.spend, comment: proposal.comment,
  };
}
