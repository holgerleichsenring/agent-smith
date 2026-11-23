import { isRunLive } from "@/lib/runLiveness";
import { translator, type T, type TextKey } from "@/lib/i18n/translate";
import type {
  RunComputePark, RunPasses, TicketAttempt, TicketConversationEntry, TicketSeries, TicketSeriesSpec,
} from "@/types/tickets";
import { channelWords } from "../ticketFormat";
import { prNumber } from "./conversationWords";
import { handbackCaseWords, passEndWords, type CutReviewDetail } from "./flowWords";
import { isRunRecord } from "./specWords";

// 2026-10-13-9a59s: an attempt card's sentences, composed from recorded fields only — 9a59a's attempt row,
// 9a59d's cause and revision, 9a59e's cut and hand-back case, 9a59f's passes, 9a59h's cut-review entries and
// 9a59j's parks. No run summary text is parsed; a clause whose field is absent, or an end reason of unknown,
// is dropped rather than guessed. What no field records — a PR marked ready, a checker's confirmation — is
// never said.

export interface AttemptStoryInput {
  attempt: TicketAttempt;
  passes: RunPasses | null;
  /** The cut-review entries of this attempt, in order. */
  cutEntries: TicketConversationEntry[];
  /** The series read for this attempt: the default cut for the live one, ?runId= for a handed-back one. */
  cut: TicketSeries | null;
  /** 2026-11-23-5669b: the language the story is told in; English when none is given. */
  t?: T;
}

export interface AttemptStory {
  doing: string[] | null;
  did: string[] | null;
  ended: { lead: string; rest: string | null } | null;
}

/** The spec the live attempt is on, from its OWN cut — null while the read's cut is another attempt's. */
export function liveSpec(attempt: TicketAttempt, cut: TicketSeries | null): {
  spec: TicketSeriesSpec; of: number; step: number | null; steps: number; rest: TicketSeriesSpec[];
} | null {
  if (!cut?.recorded || isRunRecord(cut) || cut.runId !== attempt.runId || cut.specs.length === 0) return null;
  const specs = [...cut.specs].sort((l, r) => l.ordinal - r.ordinal);
  const open = specs.filter((s) => s.state !== "done" && s.state !== "carried");
  const spec = open.find((s) => s.state === "in_progress") ?? open[0];
  if (!spec) return null;
  const worked = new Map((spec.worked ?? []).map((w) => [w.stepId, w.state]));
  const stepAt = spec.steps.findIndex((s) => worked.get(s.id) !== "done");
  return {
    spec, of: specs.length, step: stepAt >= 0 ? stepAt + 1 : null, steps: spec.steps.length,
    rest: open.filter((s) => s.ordinal > spec.ordinal),
  };
}

function doing(attempt: TicketAttempt, cut: TicketSeries | null, t: T): string[] {
  if (attempt.status === "queued") return [t("tickets.story.queued")];
  const live = liveSpec(attempt, cut);
  if (!live) return [t("tickets.story.deriving")];
  const { spec, step, rest } = live;
  const action = step !== null ? spec.steps[step - 1]?.action : null;
  const lines = [action ? t("tickets.story.specStep", { ordinal: spec.ordinal, step: step!, action })
    : t("tickets.story.spec", { ordinal: spec.ordinal, goal: spec.goal })];
  if (rest.length > 0) {
    const next = rest.map((s) => t("tickets.story.specRef", { ordinal: s.ordinal })).join(t("tickets.story.then"));
    lines.push(t("tickets.story.next", { next }));
  }
  return lines;
}

function cutLines(entries: TicketConversationEntry[], t: T): string[] {
  return entries.map((entry, i) => {
    const detail = (entry.detail ?? {}) as CutReviewDetail;
    const n = typeof detail.attempt === "number" ? detail.attempt : i + 1;
    if (detail.deliverable === "refused") {
      const findings = detail.findings?.length ?? 0;
      const counted = findings > 0 ? t("tickets.story.findings", { count: findings }) : "";
      return t("tickets.story.cutRefused", { n, findings: counted });
    }
    if (detail.deliverable === "accepted") return t("tickets.story.cutAccepted", { n });
    return t("tickets.story.cutReviewed", { n });
  });
}

function prLine(attempt: TicketAttempt, t: T): string | null {
  const opened = attempt.pullRequests.filter((pr) => pr.status === "opened" || pr.status === "awaiting_ci"); // 3435b
  if (opened.length === 0) return null;
  const names = opened.map((pr) => {
    const n = prNumber(pr.url);
    return n ? `#${n} (${pr.repo})` : pr.repo;
  });
  return t("tickets.story.opened", { count: names.length, names: names.join(t("tickets.story.and")) });
}

const SPEC_OUTCOME: Record<string, TextKey> = {
  done: "tickets.story.outcome.done", failed: "tickets.story.outcome.failed", in_progress: "tickets.story.outcome.inProgress",
  handed_back: "tickets.story.outcome.handedBack",
};

function specLines(attempt: TicketAttempt, passes: RunPasses | null, t: T): string[] {
  return [...attempt.executedSpecs]
    .filter((s) => s.status !== "not_started")
    .sort((l, r) => l.ordinal - r.ordinal)
    .map((s) => {
      const own = passes?.specs.find((p) => p.specId === s.specId)?.passes ?? null;
      const outcome = SPEC_OUTCOME[s.status] ? t(SPEC_OUTCOME[s.status]) : s.status.replace(/_/g, " ");
      const clauses = [t("tickets.story.specOutcome", { ordinal: s.ordinal, outcome })];
      if (own && own.length > 0) clauses[0] += t("tickets.story.inPasses", { count: own.length });
      const asked = own?.filter((p) => p.endedBecause === "question").length ?? 0;
      const resumed = own?.filter((p) => p.openedBecause === "after-answer").length ?? 0;
      if (asked > 0) clauses.push(t("tickets.story.asked", { count: asked }));
      if (resumed > 0) clauses.push(t("tickets.story.resumed", { count: resumed }));
      return clauses.join(", ");
    });
}

function failedSentence(attempt: TicketAttempt, passes: RunPasses | null, t: T): { lead: string; rest: string | null } {
  if (attempt.endedOnInfrastructure) return { lead: t("tickets.story.infrastructure"), rest: t("tickets.story.infrastructureRest") };
  const failing = [...attempt.executedSpecs].sort((l, r) => l.ordinal - r.ordinal)
    .find((s) => s.status === "failed") ?? attempt.executedSpecs.find((s) => s.status === "in_progress");
  if (!failing) return { lead: t("tickets.story.failed"), rest: null };
  const own = passes?.specs.find((p) => p.specId === failing.specId)?.passes ?? [];
  const last = [...own].sort((l, r) => l.ordinal - r.ordinal).at(-1);
  const count = own.length > 0 ? t("tickets.story.afterPasses", { count: own.length }) : "";
  const reason = last && last.endedBecause !== "unknown" ? t("tickets.story.lastPass", { why: passEndWords(last.endedBecause, t) }) : "";
  return { lead: t("tickets.story.failedColon"), rest: t("tickets.story.notDone", { ordinal: failing.ordinal, count, reason }) };
}

function ended(attempt: TicketAttempt, passes: RunPasses | null, cut: TicketSeries | null, t: T): AttemptStory["ended"] {
  if (attempt.handedBack) {
    const handback = cut?.handback?.runId === attempt.runId ? cut.handback : null;
    return { lead: handback ? t("tickets.story.handedBackAs", { case: handbackCaseWords(handback.case, t) }) : t("tickets.story.handedBack"), rest: null };
  }
  if (isRunLive(attempt.status)) return null;
  switch (attempt.status) {
    case "success": return { lead: t("tickets.story.finished"), rest: null };
    case "shortfall": return { lead: t("tickets.story.shortfall"), rest: null };
    case "cancelled": return { lead: t("tickets.story.cancelled"), rest: null };
    default: return failedSentence(attempt, passes, t);
  }
}

export function attemptStory({ attempt, passes, cutEntries, cut, t = translator("en") }: AttemptStoryInput): AttemptStory {
  const live = isRunLive(attempt.status) && !attempt.handedBack;
  if (live) return { doing: doing(attempt, cut, t), did: null, ended: null };
  const did = [...cutLines(cutEntries, t), prLine(attempt, t), ...specLines(attempt, passes, t)].filter((l): l is string => !!l);
  return { doing: null, did: did.length > 0 ? did : null, ended: ended(attempt, passes, cut, t) };
}

/** Where a cause came from: "#813" for a pull request, "the ticket" for a comment or a status move. */
export function causeWhere(attempt: TicketAttempt, t: T = translator("en")): string | null {
  const source = attempt.cause?.source;
  const pr = prNumber(source?.url);
  if (pr) return `#${pr}`;
  if (attempt.cause?.channel === "ticket_comment" || attempt.cause?.channel === "status_move") return t("tickets.story.theTicket");
  return null;
}

/** The cause's label: its channel, and for a status move the status it moved into. */
export function causeLabel(attempt: TicketAttempt, t: T = translator("en")): string | null {
  const cause = attempt.cause;
  if (!cause) return null;
  const into = cause.channel === "status_move" && cause.source?.detail ? t("tickets.story.into", { status: cause.source.detail }) : "";
  return `${channelWords(t, cause.channel)}${into}`;
}

/** The question parks of one attempt: how many, and how long they waited in total. */
export function questionWaits(parks: RunComputePark[], now: number): { count: number; ms: number } | null {
  const asked = parks.filter((p) => p.kind === "question" && p.askedAt);
  if (asked.length === 0) return null;
  const ms = asked.reduce((sum, p) => {
    const from = Date.parse(p.askedAt!);
    const to = p.answeredAt ? Date.parse(p.answeredAt) : p.resumedAt ? Date.parse(p.resumedAt) : now;
    return sum + Math.max(0, to - from);
  }, 0);
  return { count: asked.length, ms };
}
