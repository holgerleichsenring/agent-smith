"use client";

import { useState, type ReactNode } from "react";
import { DialogProposalFindings } from "./DialogProposalFindings";
import { notReviewedLabel } from "./DialogProposalRounds";
import { TurnActivityLine } from "./TurnActivityLine";
import { TurnQuestions } from "./TurnQuestions";
import { TurnSpecRow } from "./TurnSpecRow";
import { waitsFor } from "./graphTicketWaits";
import { findingsOf, roundsOf, rowsOf, statusOf, statusSummary, titleOf, usd, type TurnProposal } from "./turnProposal";

// 2026-11-10-d854b: one reduced result per proposing turn, live and after a reload, on the conversation and on the
// ticket page: the reply as its summary, one closed activity line, an amber banner when the turn's cost cap ended
// its review, the questions only the operator can answer, and one closed row per spec in build order. The approve
// bar sits only on the proposal awaiting a decision, which the caller passes in. Replaces the card 7da6 settled.

export function TurnResult({
  proposal,
  summary,
  runId,
  onSend,
  heldKey,
  onInspect,
  approve,
  before,
  rowExtra,
}: {
  proposal: TurnProposal;
  summary?: ReactNode;
  runId?: string | null;
  /** 2026-11-12-e2f5: sends the operator's held answers as one reply. */
  onSend?: (text: string) => void;
  /** The proposal id the questions' held state is kept under. */
  heldKey?: string;
  onInspect?: () => void;
  /** The approve bar — given only for the current awaiting proposal. */
  approve?: ReactNode;
  /** The ticket page's own parts: the series' other specs above the rows, and each draft's disposition. */
  before?: ReactNode;
  rowExtra?: (specId: string) => ReactNode;
}) {
  // 2026-11-12-01b4: while own answers are held, Approve would file the questions on their proposals and drop the
  // answers; the bar steps aside until they are sent or dropped.
  const [answersHeld, setAnswersHeld] = useState(0);
  const rows = rowsOf(proposal);
  const pending = rows.filter((row) => statusOf(proposal, row.specId) === "pending").length;
  const drafted = new Set(proposal.drafts.map((draft) => draft.specId));
  return (
    <div className="d-turn" data-testid="turn-result" data-kind={proposal.kind}>
      {summary}
      {onInspect && (
        <div className="d-turn-head">
          <span>{proposal.kind}</span>
          <button type="button" className="d-link ml-auto normal-case tracking-normal" data-testid="dialog-card-inspect"
            aria-label={`Inspect the ${proposal.kind} proposal: ${titleOf(proposal)}`} onClick={onInspect}>
            Inspect →
          </button>
        </div>
      )}
      <TurnActivityLine runId={runId} rounds={roundsOf(proposal)} spend={proposal.spend} />
      {proposal.spend?.capHit && (
        <div className="d-turn-cap" data-testid="turn-cap-banner">
          <span className="font-semibold">
            Cost cap reached{proposal.spend.capUsd != null ? ` at ${usd(proposal.spend.capUsd)}` : ""}
            {pending > 0 ? ` — ${pending} ${pending === 1 ? "spec" : "specs"} not reviewed in their latest form` : ""}
          </span>
          <span>Nothing is lost: an unreviewed spec is reviewed before it is built.</span>
        </div>
      )}
      <TurnQuestions key={heldKey ?? "turn"} questions={proposal.questions ?? []} heldKey={heldKey} onSend={onSend}
        onAnswersHeld={setAnswersHeld} />
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
      {approve && answersHeld > 0 ? (
        <p className="dsh-body text-body" data-testid="turn-approve-held">
          Send your {answersHeld === 1 ? "answer" : `${answersHeld} answers`} first, or drop {answersHeld === 1 ? "it" : "them"}; approving now would file every question on its proposal.
        </p>
      ) : approve}
    </div>
  );
}
