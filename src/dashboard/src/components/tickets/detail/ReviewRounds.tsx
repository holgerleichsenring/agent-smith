"use client";

import type { ReactNode } from "react";
import { useTicketReviews } from "@/lib/tickets/TicketPageStore";
import type { TicketReviewCut, TicketReviewFinding, TicketReviewRound, TicketReviews } from "@/types/tickets";
import { clock } from "./flowWords";

// 2026-10-13-8762l: one spec's pre-build review rounds as a block of its document — per attempt, and the rounds
// the approved set was reviewed in during the design conversation. Each round shows its merged verdict, its
// reviewers, every cut as register · quoted text → replacement with its evidence exactly as stored, the
// wording still standing, the evidence problems and the revision it produced. Composed from the mock's
// .blk/.crit/.ci/.pill classes; the ratified mock has no review surface.

export const CONVERSATION_HEADING = "Reviewed in the conversation before approval";

const VERDICT: Record<string, [string, string]> = {
  build: ["ok", "build"],
  "build-with-cuts": ["amber", "build with cuts"],
  refuse: ["bad", "refuse"],
};

const MARK: Record<string, string> = { build: "ok", refuse: "bad" };

export function VerdictPill({ verdict }: { verdict: string | null }) {
  const [cls, words] = verdict ? VERDICT[verdict] ?? ["q", verdict] : ["q", "not taken"];
  return <span className={`pill ${cls}`} data-testid="review-verdict">{words}</span>;
}

export function CutLine({ cut, member }: { cut: TicketReviewCut; member?: ReactNode }) {
  return (
    <div data-testid="review-cut">
      {member}{cut.register} · “{cut.quote}” → {cut.replacement ? `“${cut.replacement}”` : "removed"}
      <span className="dim-line mono"> {cut.evidence}</span>
    </div>
  );
}

export function FindingLine({ finding }: { finding: TicketReviewFinding }) {
  return <div data-testid="review-finding">{finding.text}<span className="dim-line mono"> {finding.evidence}</span></div>;
}

function RoundBody({ round }: { round: TicketReviewRound }) {
  const { cuts, findings, residues } = roundLists(round);
  const problems = round.reviewers.flatMap((r) => r.evidenceProblems);
  return (
    <span>
      <VerdictPill verdict={round.verdict} />
      <span className="dim-line"> reviewers: {round.reviewers.map((r) => r.verdict ?? "not taken").join(", ") || "none"}</span>
      {cuts.map((cut, i) => <CutLine key={`c${i}`} cut={cut} />)}
      {findings.map((finding, i) => <FindingLine key={`f${i}`} finding={finding} />)}
      {residues.map((residue, i) => (
        <div key={`r${i}`} data-testid="review-residue">{residue.register} · “{residue.quote}” <span className="pill amber">still stands</span></div>
      ))}
      {[...new Map(problems.map((p) => [`${p.claim}|${p.problem}`, p])).values()].map((p, i) => (
        <div key={`p${i}`} className="dim-line" data-testid="review-evidence-problem">evidence: {p.claim} — {p.problem}</div>
      ))}
    </span>
  );
}

function RoundRows({ rounds, label }: { rounds: TicketReviewRound[]; label: string }) {
  return (
    <div className="crit" data-testid="review-group">
      <div className="dim-line">{label}</div>
      {rounds.map((round) => (
        <div key={`${round.scope}-${round.round}`} className="ci" data-testid="review-round">
          <span className={`m ${MARK[round.verdict] ?? "q"}`}>{round.round}</span>
          <RoundBody round={round} />
          <span className="cw">{round.revisionNumber != null ? `rev ${round.revisionNumber}` : clock(round.at) ?? ""}</span>
        </div>
      ))}
    </div>
  );
}

export interface RoundGroup {
  label: string;
  rounds: TicketReviewRound[];
}

/** Rounds by where they were taken — each attempt that took one, then the design conversation. */
export function roundGroups(
  reviews: TicketReviews | null, ofRun: (r: TicketReviewRound) => boolean, ofConversation: (r: TicketReviewRound) => boolean,
): RoundGroup[] {
  const groups = (reviews?.attempts ?? [])
    .map((a) => ({ label: `Attempt ${a.number}`, rounds: a.rounds.filter(ofRun) }))
    .filter((g) => g.rounds.length > 0);
  const approved = reviews?.conversation?.rounds.filter(ofConversation) ?? [];
  if (approved.length > 0) {
    const at = clock(reviews?.conversation?.approvalAt);
    groups.push({ label: `${CONVERSATION_HEADING}${at ? ` · approved ${at}` : ""}`, rounds: approved });
  }
  return groups;
}

/** A round's cuts, findings and residues: a run round's per reviewer, a conversation round's at round level. */
export function roundLists(round: TicketReviewRound) {
  const conversation = round.scope === "conversation";
  return {
    cuts: conversation ? round.cuts : round.reviewers.flatMap((r) => r.cuts),
    findings: conversation ? round.findings : round.reviewers.flatMap((r) => r.findings),
    residues: conversation ? round.residues : round.reviewers.flatMap((r) => r.residues),
  };
}

export function ReviewRounds({ specId }: { specId: string }) {
  const reviews = useTicketReviews();
  const groups = roundGroups(reviews.data, (r) => r.scope === "spec" && r.specId === specId, (r) => r.specId === specId);
  return (
    <div className="blk" data-testid="review-rounds">
      <h4>Review before build</h4>
      {groups.length === 0
        ? <div className="dim-line" data-testid="review-rounds-none">{reviews.loading ? "Reading the review rounds…" : "no review round was recorded"}</div>
        : groups.map((group) => <RoundRows key={group.label} rounds={group.rounds} label={group.label} />)}
    </div>
  );
}
