import type { T, TextKey } from "@/lib/i18n/translate";
import type { TicketConversationEntry } from "@/types/tickets";

// 2026-10-13-9a59q: the words the ticket graph and its node detail are written in, from recorded
// vocabularies only — the pass reasons of MasterPassReasons and the hand-back cases of SpecHandbackCase.

export { money } from "../ticketFormat";

const PASS_WORDS: Record<string, TextKey> = {
  "re-engaged": "tickets.flow.pass.reEngaged",
  "nothing-left": "tickets.flow.pass.nothingLeft",
  budget: "tickets.flow.pass.budget",
  "red-accepted": "tickets.flow.pass.redAccepted",
  verdictless: "tickets.flow.pass.verdictless",
  idle: "tickets.flow.pass.idle",
  blocked: "tickets.flow.pass.blocked",
  "self-refilled": "tickets.flow.pass.selfRefilled",
  cap: "tickets.flow.pass.cap",
  question: "tickets.flow.pass.question",
  failed: "tickets.flow.pass.failed",
  unknown: "tickets.flow.pass.unknown",
  "red-with-open-steps": "tickets.flow.pass.redWithOpenSteps",
  // 2026-11-22-96c8a: no verdict any more — open checklist steps get one re-drive, then the loop stops.
  unfinished: "tickets.flow.pass.unfinished",
  "steps-open": "tickets.flow.pass.stepsOpen",
  "unbacked-done": "tickets.flow.pass.unbackedDone",
  "gate-unsatisfied": "tickets.flow.pass.gateUnsatisfied",
  "unresolved-claims": "tickets.flow.pass.unresolvedClaims", // 2026-10-17-a196c
  challenged: "tickets.flow.pass.challenged",
  first: "tickets.flow.pass.first",
  "after-answer": "tickets.flow.pass.afterAnswer",
  repair: "tickets.flow.pass.repair",
  "review-fix": "tickets.flow.pass.reviewFix",
  "structure-correction": "tickets.flow.pass.structureCorrection",
};

export function passEndWords(t: T, reason: string): string {
  return PASS_WORDS[reason] ? t(PASS_WORDS[reason]) : reason;
}

const CASE_WORDS: Record<string, TextKey> = {
  RequirementsContradictRepository: "tickets.flow.case.requirementsContradictRepository",
  NotImplementable: "tickets.flow.case.notImplementable",
  Refused: "tickets.flow.case.refused",
  Question: "tickets.flow.case.question",
  SpecificationMissingFromBranch: "tickets.flow.case.specificationMissingFromBranch",
  ScopeUnassigned: "tickets.flow.case.scopeUnassigned",
  SeriesOutOfStep: "tickets.flow.case.seriesOutOfStep",
  CiFixBudgetSpent: "tickets.flow.case.ciFixBudgetSpent", // 2026-11-17-3435c
  ReviewObjection: "tickets.flow.case.reviewObjection", // 2026-10-13-8762l
};

export function handbackCaseWords(t: T, handbackCase: string): string {
  return CASE_WORDS[handbackCase] ? t(CASE_WORDS[handbackCase]) : handbackCase;
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

/** "Sep 4 14:26" — a calendar day and a 24-hour clock. 2026-11-23-5669b: in `locale` (useDateLocale) when it is not
 *  English — "4. Sept. 14:26"; English keeps "Sep 4". */
export function clock(iso: string | null | undefined, locale: string): string | null {
  if (!iso) return null;
  const at = new Date(iso);
  if (Number.isNaN(at.getTime())) return null;
  const foreign = locale && !locale.startsWith("en") ? locale : null;
  const day = at.toLocaleDateString(foreign ?? "en-US", { month: "short", day: "numeric" });
  const time = at.toLocaleTimeString(foreign ?? "en-GB", { hour: "2-digit", minute: "2-digit", hour12: false });
  return `${day} ${time}`;
}

/** "6m 12s", "5h 17m" — wall time between two instants, or until now while it runs. */
export function elapsed(t: T, fromIso: string, toIso: string | null): string {
  const ms = Math.max(0, (toIso ? Date.parse(toIso) : Date.now()) - Date.parse(fromIso));
  if (Number.isNaN(ms)) return "";
  const seconds = Math.round(ms / 1000);
  if (seconds < 60) return t("tickets.flow.seconds", { s: seconds });
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return t("tickets.flow.minutes", { m: minutes, s: seconds % 60 });
  return t("tickets.flow.hours", { h: Math.floor(minutes / 60), m: minutes % 60 });
}

const STATUS_WORDS: Record<string, TextKey> = {
  running: "tickets.flow.status.running",
  queued: "tickets.flow.status.queued",
  waiting_for_input: "tickets.flow.status.waitingForInput",
  success: "tickets.flow.status.success",
  shortfall: "tickets.flow.status.shortfall",
  failed: "tickets.flow.status.failed",
  error: "tickets.flow.status.error",
  cancelled: "tickets.flow.status.cancelled",
};

export function statusWords(t: T, status: string): string {
  return STATUS_WORDS[status] ? t(STATUS_WORDS[status]) : status;
}
