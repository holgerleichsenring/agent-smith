"use client";

import type { ReactNode } from "react";
import { useDateLocale, useT } from "@/hooks/useT";
import type { T, TextKey } from "@/lib/i18n/translate";
import { useTicketReviews } from "@/lib/tickets/TicketPageStore";
import type { TicketReviewCut, TicketReviewFinding, TicketReviewRound, TicketReviews } from "@/types/tickets";
import { clock } from "./flowWords";

// 2026-10-13-8762l: one spec's pre-build review rounds as a block of its document — per attempt, and the rounds
// the approved set was reviewed in during the design conversation. Each round shows its merged verdict, its
// reviewers, every cut as register · quoted text → replacement with its evidence exactly as stored, the
// wording still standing, the evidence problems and the revision it produced. Composed from the mock's
// .blk/.crit/.ci/.pill classes; the ratified mock has no review surface.

const VERDICT: Record<string, [string, TextKey]> = {
  build: ["ok", "tickets.review.verdict.build"],
  "build-with-cuts": ["amber", "tickets.review.verdict.build-with-cuts"],
  refuse: ["bad", "tickets.review.verdict.refuse"],
};

const MARK: Record<string, string> = { build: "ok", refuse: "bad" };

export function VerdictPill({ verdict }: { verdict: string | null }) {
  const t = useT();
  const known = verdict ? VERDICT[verdict] : undefined;
  const [cls, words] = known ? [known[0], t(known[1])] : ["q", verdict ?? t("tickets.review.notTaken")];
  return <span className={`pill ${cls}`} data-testid="review-verdict">{words}</span>;
}

export function CutLine({ cut, member }: { cut: TicketReviewCut; member?: ReactNode }) {
  const t = useT();
  return (
    <div data-testid="review-cut">
      {member}{cut.register} · {t("common.quoted", { text: cut.quote })} → {cut.replacement ? t("common.quoted", { text: cut.replacement }) : t("tickets.review.removed")}
      <span className="dim-line mono"> {cut.evidence}</span>
    </div>
  );
}

export function FindingLine({ finding }: { finding: TicketReviewFinding }) {
  return <div data-testid="review-finding">{finding.text}<span className="dim-line mono"> {finding.evidence}</span></div>;
}

function RoundBody({ round }: { round: TicketReviewRound }) {
  const { cuts, findings, residues } = roundLists(round);
  const t = useT();
  const problems = round.reviewers.flatMap((r) => r.evidenceProblems);
  return (
    <span>
      <VerdictPill verdict={round.verdict} />
      <span className="dim-line"> {t("tickets.review.reviewers", { list: round.reviewers.map((r) => r.verdict ?? t("tickets.review.notTaken")).join(", ") || t("tickets.review.none") })}</span>
      {cuts.map((cut, i) => <CutLine key={`c${i}`} cut={cut} />)}
      {findings.map((finding, i) => <FindingLine key={`f${i}`} finding={finding} />)}
      {residues.map((residue, i) => (
        <div key={`r${i}`} data-testid="review-residue">{residue.register} · {t("common.quoted", { text: residue.quote })} <span className="pill amber">{t("tickets.review.stillStands")}</span></div>
      ))}
      {[...new Map(problems.map((p) => [`${p.claim}|${p.problem}`, p])).values()].map((p, i) => (
        <div key={`p${i}`} className="dim-line" data-testid="review-evidence-problem">{t("tickets.review.evidence", { claim: p.claim, problem: p.problem })}</div>
      ))}
    </span>
  );
}

function RoundRows({ rounds, label }: { rounds: TicketReviewRound[]; label: string }) {
  const t = useT();
  const locale = useDateLocale();
  return (
    <div className="crit" data-testid="review-group">
      <div className="dim-line">{label}</div>
      {rounds.map((round) => (
        <div key={`${round.scope}-${round.round}`} className="ci" data-testid="review-round">
          <span className={`m ${MARK[round.verdict] ?? "q"}`}>{round.round}</span>
          <RoundBody round={round} />
          <span className="cw">{round.revisionNumber != null ? t("tickets.specs.rev", { n: round.revisionNumber }) : clock(round.at, locale) ?? ""}</span>
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
  t: T, locale: string, reviews: TicketReviews | null, ofRun: (r: TicketReviewRound) => boolean, ofConversation: (r: TicketReviewRound) => boolean,
): RoundGroup[] {
  const groups = (reviews?.attempts ?? [])
    .map((a) => ({ label: t("tickets.review.attempt", { n: a.number }), rounds: a.rounds.filter(ofRun) }))
    .filter((g) => g.rounds.length > 0);
  const approved = reviews?.conversation?.rounds.filter(ofConversation) ?? [];
  if (approved.length > 0) {
    const at = clock(reviews?.conversation?.approvalAt, locale);
    const heading = t("tickets.review.conversation");
    groups.push({ label: at ? t("tickets.review.conversationApproved", { heading, at }) : heading, rounds: approved });
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
  const t = useT();
  const locale = useDateLocale();
  const reviews = useTicketReviews();
  const groups = roundGroups(t, locale, reviews.data, (r) => r.scope === "spec" && r.specId === specId, (r) => r.specId === specId);
  return (
    <div className="blk" data-testid="review-rounds">
      <h4>{t("tickets.review.title")}</h4>
      {groups.length === 0
        ? <div className="dim-line" data-testid="review-rounds-none">{reviews.loading ? t("tickets.review.reading") : t("tickets.review.noneRecorded")}</div>
        : groups.map((group) => <RoundRows key={group.label} rounds={group.rounds} label={group.label} />)}
    </div>
  );
}
