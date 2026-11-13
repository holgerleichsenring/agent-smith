"use client";

import type { ReactNode } from "react";
import { DialogProposalFindings } from "./DialogProposalFindings";
import { notReviewedLabel } from "./DialogProposalRounds";
import { TurnActivityLine } from "./TurnActivityLine";
import { TurnQuestions } from "./TurnQuestions";
import { TurnSpecRow } from "./TurnSpecRow";
import { waitsFor } from "./graphTicketWaits";
import { findingsOf, roundsOf, rowsOf, statusOf, statusSummary, usd, type TurnProposal } from "./turnProposal";

// 2026-11-10-d854b: one reduced result per proposing turn, live and after a reload, on the conversation and on the
// ticket page: the reply as its summary, one closed activity line, an amber banner when the turn's cost cap ended
// its review, the questions only the operator can answer, and one closed row per spec in build order. The approve
// bar sits only on the proposal awaiting a decision, which the caller passes in. Replaces the card 7da6 settled.
// 2026-11-12-92e9c: the conversation shows each turn as a TurnMessage; this result stays the ticket page's, so the
// conversation's parts (summary, Inspect, approve bar, held answers, the run's steps) left with it.

export function TurnResult({
  proposal,
  before,
  rowExtra,
}: {
  proposal: TurnProposal;
  /** The ticket page's own parts: the series' other specs above the rows, and each draft's disposition. */
  before?: ReactNode;
  rowExtra?: (specId: string) => ReactNode;
}) {
  const rows = rowsOf(proposal);
  const pending = rows.filter((row) => statusOf(proposal, row.specId) === "pending").length;
  const drafted = new Set(proposal.drafts.map((draft) => draft.specId));
  return (
    <div className="d-turn" data-testid="turn-result" data-kind={proposal.kind}>
      <TurnActivityLine rounds={roundsOf(proposal)} spend={proposal.spend} />
      {proposal.spend?.capHit && (
        <div className="d-turn-cap" data-testid="turn-cap-banner">
          <span className="font-semibold">
            Cost cap reached{proposal.spend.capUsd != null ? ` at ${usd(proposal.spend.capUsd)}` : ""}
            {pending > 0 ? ` — ${pending} ${pending === 1 ? "spec" : "specs"} not reviewed in their latest form` : ""}
          </span>
          <span>Nothing is lost: an unreviewed spec is reviewed before it is built.</span>
        </div>
      )}
      <TurnQuestions questions={proposal.questions ?? []} />
      {proposal.bug && (
        <div data-testid="turn-bug" className="d-turn-box px-3 py-2">
          <div className="dsh-body font-semibold text-ink">{proposal.bug.title}</div>
          <p className="dsh-body whitespace-pre-wrap text-ink">{proposal.bug.body}</p>
        </div>
      )}
      {proposal.kind === "comment" && proposal.comment && (
        <p className="dsh-body whitespace-pre-wrap text-ink" data-testid="turn-comment">{proposal.comment}</p>
      )}
      {(proposal.tickets?.length ?? 0) > 0 && (
        <div className="d-turn-head"><span>{proposal.tickets!.length} tickets</span><span>in merge order</span></div>
      )}
      {(proposal.tickets?.length ?? 0) > 0 && (
        <div data-testid="dialog-card-tickets" className="d-turn-box flex flex-col gap-0.5 px-3 py-2 dsh-body">
          {proposal.tickets!.map((ticket) => (
            <div key={ticket.key}>
              <b className="font-mono dsh-label font-medium text-ink">{ticket.key}</b>{" "}
              <span className="text-body">{ticket.title} · {waitsFor(ticket)}</span>
            </div>
          ))}
        </div>
      )}
      {before}
      {rows.length > 0 && (
        <div className="flex flex-col gap-2" data-testid="turn-specs">
          <div className="d-turn-head">
            <span>Specs · in build order</span>
            <span className="normal-case tracking-normal font-normal">{statusSummary(proposal)}</span>
          </div>
          <div className="d-turn-box">
            {rows.map((row) => (
              <TurnSpecRow key={row.specId} draft={row} status={statusOf(proposal, row.specId)}
                findings={findingsOf(proposal, row.specId)}
                rounds={(proposal.reviewRounds ?? []).filter((round) => round.specId === row.specId)}
                extra={rowExtra?.(row.specId)} />
            ))}
          </div>
        </div>
      )}
      {(proposal.notReviewed ?? []).filter((entry) => !drafted.has(entry)).map((entry) => (
        <span key={entry} className="dsh-body text-body" data-testid={`turn-not-reviewed-${entry}`}>{notReviewedLabel(entry)}</span>
      ))}
      <DialogProposalFindings findings={(proposal.findings ?? []).filter((finding) => !drafted.has(finding.specId)
        || (proposal.kind === "epic" && finding.specId === proposal.drafts[0]?.specId))} />
    </div>
  );
}
