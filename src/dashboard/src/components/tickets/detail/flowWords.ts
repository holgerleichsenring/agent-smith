import type { TicketConversationEntry } from "@/types/tickets";

// 2026-10-13-9a59q: the words the ticket graph and its node detail are written in, from recorded
// vocabularies only — the pass reasons of MasterPassReasons and the hand-back cases of SpecHandbackCase.

export { money } from "../ticketFormat";

const PASS_WORDS: Record<string, string> = {
  "re-engaged": "re-engaged",
  "nothing-left": "nothing left to do",
  budget: "budget spent",
  "red-accepted": "red verdict accepted",
  verdictless: "ended without a verdict",
  idle: "went idle",
  blocked: "blocked",
  "self-refilled": "refilled its own steps",
  cap: "pass cap reached",
  question: "asked a question",
  failed: "failed",
  unknown: "no end recorded",
  "red-with-open-steps": "verdict red with open steps",
  "unbacked-done": "steps marked done that the diff does not back",
  "gate-unsatisfied": "acceptance gate unsatisfied",
  challenged: "challenged on its own stop",
  first: "first pass",
  "after-answer": "after the answer",
  repair: "repair",
  "review-fix": "review fix",
  "structure-correction": "structure correction",
};

export function passEndWords(reason: string): string {
  return PASS_WORDS[reason] ?? reason;
}

const CASE_WORDS: Record<string, string> = {
  RequirementsContradictRepository: "contradiction",
  NotImplementable: "not implementable",
  Refused: "refused",
  Question: "question",
  SpecificationMissingFromBranch: "spec missing from the branch",
  ScopeUnassigned: "scope unassigned",
  SeriesOutOfStep: "series out of step",
  ReviewObjection: "review objection", // 2026-10-13-8762l
};

export function handbackCaseWords(handbackCase: string): string {
  return CASE_WORDS[handbackCase] ?? handbackCase;
}

export interface CutReviewDetail {
  attempt?: number;
  deliverable?: string;
  findings?: Array<{ specId?: string; criterion?: string; problem?: string; why?: string }>;
}

export function cutReviews(entries: TicketConversationEntry[]): Array<{ entry: TicketConversationEntry; detail: CutReviewDetail }> {
  return entries
    .filter((e) => e.source === "spec-cut" && e.kind === "cut-review")
    .map((entry) => ({ entry, detail: (entry.detail ?? {}) as CutReviewDetail }));
}

export function refusedReviews(entries: TicketConversationEntry[]): number {
  return cutReviews(entries).filter((r) => r.detail.deliverable === "refused").length;
}

/** "Sep 4 14:26" — a calendar day and a 24-hour clock. */
export function clock(iso: string | null | undefined): string | null {
  if (!iso) return null;
  const at = new Date(iso);
  if (Number.isNaN(at.getTime())) return null;
  const day = at.toLocaleDateString("en-US", { month: "short", day: "numeric" });
  const time = at.toLocaleTimeString("en-GB", { hour: "2-digit", minute: "2-digit", hour12: false });
  return `${day} ${time}`;
}

/** "6m 12s", "5h 17m" — wall time between two instants, or until now while it runs. */
export function elapsed(fromIso: string, toIso: string | null): string {
  const ms = Math.max(0, (toIso ? Date.parse(toIso) : Date.now()) - Date.parse(fromIso));
  if (Number.isNaN(ms)) return "";
  const seconds = Math.round(ms / 1000);
  if (seconds < 60) return `${seconds}s`;
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}m ${seconds % 60}s`;
  return `${Math.floor(minutes / 60)}h ${minutes % 60}m`;
}

const STATUS_WORDS: Record<string, string> = {
  running: "running",
  queued: "queued",
  waiting_for_input: "waiting for an answer",
  success: "succeeded",
  shortfall: "done with a shortfall",
  failed: "failed",
  error: "failed",
  cancelled: "cancelled",
};

export function statusWords(status: string): string {
  return STATUS_WORDS[status] ?? status;
}
