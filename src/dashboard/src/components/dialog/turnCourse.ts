import type { SpecDialogReviewRound } from "@/types/spec-dialog";
import { SERIES_TOKEN } from "./DialogProposalRounds";
import { durationOf, findingsOf, roundsOf, usd, type TurnProposal } from "./turnProposal";

// 2026-11-12-92e9c: what one agent turn's message states about its specs and its course, from the proposal's own review
// rounds — never from the trail. A spec's mark is its last round's verdict; a spec no round reached is not reviewed.

export interface SpecMark {
  mark: "✓" | "✂" | "✕" | "⏸" | "·";
  word: "passed" | "cuts" | "refused" | "waits on your answer" | "not reviewed";
  reason?: string;
}

const REASON_CHARS = 120;

function normalised(verdict: string): string {
  return verdict.trim().toLowerCase().replace(/[\s_]+/g, "-");
}

function lastRounds(proposal: TurnProposal): Map<string, SpecDialogReviewRound> {
  const last = new Map<string, SpecDialogReviewRound>();
  for (const round of proposal.reviewRounds ?? []) {
    if (round.specId === SERIES_TOKEN) continue;
    const held = last.get(round.specId);
    if (!held || round.round >= held.round) last.set(round.specId, round);
  }
  return last;
}

export function markOf(proposal: TurnProposal, specId: string): SpecMark {
  const pending = (proposal.notReviewed ?? []).includes(specId)
    || findingsOf(proposal, specId).some((finding) => finding.problem === "not reviewed yet");
  const last = lastRounds(proposal).get(specId);
  if (pending || !last) return { mark: "·", word: "not reviewed" };
  const verdict = normalised(last.verdict);
  if (verdict === "build") return { mark: "✓", word: "passed" };
  if (verdict === "build-with-cuts") return { mark: "✂", word: "cuts" };
  if (waitsOnOwner(last)) return { mark: "⏸", word: "waits on your answer" };
  const said = last.findings[0]?.text ?? findingsOf(proposal, specId)[0]?.problem;
  return { mark: "✕", word: "refused", ...(said ? { reason: shortened(said) } : {}) };
}

/** 2026-11-13-b3a2: a refused round whose every finding is a decision only the owner can make. */
export function waitsOnOwner(round: SpecDialogReviewRound): boolean {
  return normalised(round.verdict) === "refuse" && round.findings.length > 0
    && round.findings.every((finding) => (finding.decision ?? "").trim().length > 0);
}

function shortened(text: string): string {
  const line = text.trim().split("\n")[0];
  return line.length > REASON_CHARS ? `${line.slice(0, REASON_CHARS - 1)}…` : line;
}

/** "2 refusals: 1 open, 1 resolved" — a spec refused in any round, open while its last round still refuses.
 * 2026-11-13-b3a2: a spec whose last round waits on the owner's decisions is not a refusal. */
export function refusalsOf(proposal: TurnProposal): string | null {
  const last = lastRounds(proposal);
  const refused = new Set((proposal.reviewRounds ?? [])
    .filter((round) => round.specId !== SERIES_TOKEN && normalised(round.verdict) === "refuse")
    .map((round) => round.specId)
    .filter((specId) => !waitsOnOwner(last.get(specId)!)));
  if (refused.size === 0) return null;
  const open = [...refused].filter((specId) => normalised(last.get(specId)?.verdict ?? "") === "refuse").length;
  const resolved = refused.size - open;
  const noun = refused.size === 1 ? "refusal" : "refusals";
  if (resolved === 0) return `${open} ${noun} open`;
  if (open === 0) return `${resolved} ${noun} resolved`;
  return `${refused.size} ${noun}: ${open} open, ${resolved} resolved`;
}

/** The course line's words before the run link: duration, cost, review rounds, refusals and a cap that ended the review. */
export function courseOf(proposal: TurnProposal | null, firstAt?: string | null, lastAt?: string | null): string[] {
  const rounds = proposal ? roundsOf(proposal) : 0;
  const spend = proposal?.spend;
  return [
    durationOf(firstAt, lastAt),
    spend ? usd(spend.usd) : null,
    rounds > 0 ? `${rounds} review ${rounds === 1 ? "round" : "rounds"}` : null,
    proposal ? refusalsOf(proposal) : null,
    spend?.capHit ? `cost cap reached${spend.capUsd != null ? ` at ${usd(spend.capUsd)}` : ""}` : null,
  ].filter((part): part is string => part !== null);
}
