import type {
  SpecDialogGraphTicket,
  SpecDialogOpenQuestion,
  SpecDialogProposalFinding,
  SpecDialogProposalPush,
  SpecDialogReviewRound,
  SpecDialogSpend,
  SpecDraftFact,
} from "@/types/spec-dialog";

// 2026-11-10-d854b: the one shape a turn's result renders from. The conversation's push and the ticket page's
// proposal view carry the same proposal; the ticket view grew the fields it lacked, so this is a list of drafts
// either way, and a conversation's push is read into it once.

export interface TurnDraft {
  specId: string;
  goal: string;
  repos: string[];
  done: string[];
  yaml?: string | null;
  facts?: SpecDraftFact[];
}

export interface TurnProposal {
  kind: string;
  /** Everything the person approves, in build order; an epic's parent first. */
  drafts: TurnDraft[];
  findings?: SpecDialogProposalFinding[];
  reviewRounds?: SpecDialogReviewRound[];
  notReviewed?: string[] | null;
  questions?: SpecDialogOpenQuestion[];
  spend?: SpecDialogSpend | null;
  comment?: string | null;
  bug?: { title: string; body: string } | null;
  tickets?: SpecDialogGraphTicket[];
}

export type SpecStatus = "clean" | "findings" | "pending";

export const STATUS_LABEL: Record<SpecStatus, string> = {
  clean: "reviewed clean",
  findings: "findings",
  pending: "not reviewed",
};

// The mark a turn's review leaves on a draft it did not get to.
const NOT_REVIEWED_PROBLEMS = new Set(["not reviewed yet"]);

export function turnProposalOf(push: SpecDialogProposalPush): TurnProposal {
  const drafts = [push.parent, push.spec, ...push.children].flatMap((draft) => (draft ? [{
    specId: draft.specId, goal: draft.goal, repos: draft.repos ?? [], done: draft.done, yaml: draft.yaml, facts: draft.facts,
  }] : []));
  return {
    kind: push.kind, drafts, findings: push.findings, reviewRounds: push.reviewRounds, notReviewed: push.notReviewed,
    questions: push.questions, spend: push.spend, comment: push.comment, bug: push.bug, tickets: push.tickets,
  };
}

/** One row per spec in build order. An epic's parent is the epic itself and is never built, so it has no row. */
export function rowsOf(proposal: TurnProposal): TurnDraft[] {
  return proposal.kind === "epic" ? proposal.drafts.slice(1) : proposal.drafts;
}

/** The pill, from what the review left: not reviewed wins, then a finding, then a last round that answered build.
 *  2026-11-12-01b4: no round, no finding and no mark is no review at all — no pill, never "reviewed clean". */
export function statusOf(proposal: TurnProposal, specId: string): SpecStatus | null {
  if ((proposal.notReviewed ?? []).includes(specId)) return "pending";
  const mine = findingsOf(proposal, specId);
  if (mine.some((finding) => NOT_REVIEWED_PROBLEMS.has(finding.problem))) return "pending";
  if (mine.length > 0) return "findings";
  const last = (proposal.reviewRounds ?? []).filter((round) => round.specId === specId).at(-1);
  if (!last) return null;
  return last.verdict.trim().toLowerCase() === "build" ? "clean" : "findings";
}

export function findingsOf(proposal: TurnProposal, specId: string): SpecDialogProposalFinding[] {
  return (proposal.findings ?? []).filter((finding) => finding.specId === specId);
}

/** "1 clean · 2 with findings · 7 not reviewed", leaving out the states no row is in. */
export function statusSummary(proposal: TurnProposal): string {
  const counts = { clean: 0, findings: 0, pending: 0 };
  for (const row of rowsOf(proposal)) {
    const status = statusOf(proposal, row.specId);
    if (status) counts[status] += 1;
  }
  return [
    counts.clean > 0 ? `${counts.clean} clean` : null,
    counts.findings > 0 ? `${counts.findings} with findings` : null,
    counts.pending > 0 ? `${counts.pending} not reviewed` : null,
  ].filter(Boolean).join(" · ");
}

/** What the proposal is called: the bug's title, the epic's or the spec's goal, or a list's size. */
export function titleOf(proposal: TurnProposal): string {
  if (proposal.kind === "tickets") return `${proposal.tickets?.length ?? 0} tickets`;
  if (proposal.kind === "comment") return "A comment on the ticket";
  return proposal.bug?.title ?? proposal.drafts[0]?.goal ?? proposal.kind;
}

/** What "Take proposal" sends: the question and the proposal, readable without the card. */
export function takeText(question: SpecDialogOpenQuestion): string {
  return `${question.question} — ${question.proposal}`;
}

/** How many review rounds the turn ran — the highest round any draft reached. */
export function roundsOf(proposal: TurnProposal): number {
  return (proposal.reviewRounds ?? []).reduce((most, round) => Math.max(most, round.round), 0);
}

/** A span as a person says it: "45 s", "20 min", "1 h 5 min". */
export function durationOf(firstAt?: string | null, lastAt?: string | null): string | null {
  if (!firstAt || !lastAt) return null;
  const seconds = Math.max(0, Math.round((Date.parse(lastAt) - Date.parse(firstAt)) / 1000));
  if (seconds < 60) return `${seconds} s`;
  const minutes = Math.round(seconds / 60);
  return minutes < 60 ? `${minutes} min` : `${Math.floor(minutes / 60)} h ${minutes % 60} min`;
}

export function usd(value: number): string {
  return `$${value.toFixed(2)}`;
}
