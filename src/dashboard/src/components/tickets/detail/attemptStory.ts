import { isRunLive } from "@/lib/runLiveness";
import type {
  RunComputePark, RunPasses, TicketAttempt, TicketConversationEntry, TicketSeries, TicketSeriesSpec,
} from "@/types/tickets";
import { channelWords } from "../ticketFormat";
import { prNumber } from "./conversationWords";
import { handbackCaseWords, passEndWords, type CutReviewDetail } from "./flowWords";

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
}

export interface AttemptStory {
  doing: string[] | null;
  did: string[] | null;
  ended: { lead: string; rest: string | null } | null;
}

const plural = (n: number, one: string, many = `${one}s`) => `${n} ${n === 1 ? one : many}`;

/** The spec the live attempt is on, from its OWN cut — null while the read's cut is another attempt's. */
export function liveSpec(attempt: TicketAttempt, cut: TicketSeries | null): {
  spec: TicketSeriesSpec; of: number; step: number | null; steps: number; rest: TicketSeriesSpec[];
} | null {
  if (!cut?.recorded || cut.runId !== attempt.runId || cut.specs.length === 0) return null;
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

function doing(attempt: TicketAttempt, cut: TicketSeries | null): string[] {
  if (attempt.status === "queued") return ["Waiting in the queue for capacity"];
  const live = liveSpec(attempt, cut);
  if (!live) return ["Deriving the series"];
  const { spec, step, rest } = live;
  const action = step !== null ? spec.steps[step - 1]?.action : null;
  const lines = [action ? `Spec ${spec.ordinal}, step ${step}: ${action}` : `Spec ${spec.ordinal}: ${spec.goal}`];
  if (rest.length > 0) lines.push(`Next: ${rest.map((s) => `spec ${s.ordinal}`).join(", then ")}`);
  return lines;
}

function cutLines(entries: TicketConversationEntry[]): string[] {
  return entries.map((entry, i) => {
    const detail = (entry.detail ?? {}) as CutReviewDetail;
    const n = typeof detail.attempt === "number" ? detail.attempt : i + 1;
    if (detail.deliverable === "refused") {
      const findings = detail.findings?.length ?? 0;
      return `Cut ${n}: refused by the reviewer${findings > 0 ? ` (${plural(findings, "finding")})` : ""}`;
    }
    if (detail.deliverable === "accepted") return `Cut ${n}: accepted by the reviewer`;
    return `Cut ${n}: reviewed`;
  });
}

function prLine(attempt: TicketAttempt): string | null {
  const opened = attempt.pullRequests.filter((pr) => pr.status === "opened");
  if (opened.length === 0) return null;
  const names = opened.map((pr) => {
    const n = prNumber(pr.url);
    return n ? `#${n} (${pr.repo})` : pr.repo;
  });
  return `Opened ${names.length === 1 ? "the PR" : "the PRs"} ${names.join(" and ")}`;
}

const SPEC_OUTCOME: Record<string, string> = {
  done: "done", failed: "not done", in_progress: "not finished", handed_back: "handed back",
};

function specLines(attempt: TicketAttempt, passes: RunPasses | null): string[] {
  return [...attempt.executedSpecs]
    .filter((s) => s.status !== "not_started")
    .sort((l, r) => l.ordinal - r.ordinal)
    .map((s) => {
      const own = passes?.specs.find((p) => p.specId === s.specId)?.passes ?? null;
      const clauses = [`Spec ${s.ordinal}: ${SPEC_OUTCOME[s.status] ?? s.status.replace(/_/g, " ")}`];
      if (own && own.length > 0) clauses[0] += own.length === 1 ? " in one pass" : ` in ${own.length} passes`;
      const asked = own?.filter((p) => p.endedBecause === "question").length ?? 0;
      const resumed = own?.filter((p) => p.openedBecause === "after-answer").length ?? 0;
      if (asked > 0) clauses.push(`asked ${plural(asked, "question")}`);
      if (resumed > 0) clauses.push(`resumed ${resumed === 1 ? "once" : `${resumed} times`} after an answer`);
      return clauses.join(", ");
    });
}

function failedSentence(attempt: TicketAttempt, passes: RunPasses | null): { lead: string; rest: string | null } {
  if (attempt.endedOnInfrastructure) return { lead: "Ended on infrastructure.", rest: "The sandbox or host was lost, not the work." };
  const failing = [...attempt.executedSpecs].sort((l, r) => l.ordinal - r.ordinal)
    .find((s) => s.status === "failed") ?? attempt.executedSpecs.find((s) => s.status === "in_progress");
  if (!failing) return { lead: "Failed.", rest: null };
  const own = passes?.specs.find((p) => p.specId === failing.specId)?.passes ?? [];
  const last = [...own].sort((l, r) => l.ordinal - r.ordinal).at(-1);
  const count = own.length > 0 ? ` after ${plural(own.length, "pass", "passes")}` : "";
  const reason = last && last.endedBecause !== "unknown" ? ` — the last pass ${passEndWords(last.endedBecause)}` : "";
  return { lead: "Failed:", rest: `spec ${failing.ordinal} not done${count}${reason}.` };
}

function ended(attempt: TicketAttempt, passes: RunPasses | null, cut: TicketSeries | null): AttemptStory["ended"] {
  if (attempt.handedBack) {
    const handback = cut?.handback?.runId === attempt.runId ? cut.handback : null;
    return { lead: handback ? `Handed back as ${handbackCaseWords(handback.case)}.` : "Handed back.", rest: null };
  }
  if (isRunLive(attempt.status)) return null;
  switch (attempt.status) {
    case "success": return { lead: "Finished.", rest: null };
    case "shortfall": return { lead: "Finished with a shortfall.", rest: null };
    case "cancelled": return { lead: "Cancelled.", rest: null };
    default: return failedSentence(attempt, passes);
  }
}

export function attemptStory({ attempt, passes, cutEntries, cut }: AttemptStoryInput): AttemptStory {
  const live = isRunLive(attempt.status) && !attempt.handedBack;
  if (live) return { doing: doing(attempt, cut), did: null, ended: null };
  const did = [...cutLines(cutEntries), prLine(attempt), ...specLines(attempt, passes)].filter((l): l is string => !!l);
  return { doing: null, did: did.length > 0 ? did : null, ended: ended(attempt, passes, cut) };
}

/** Where a cause came from: "#813" for a pull request, "the ticket" for a comment or a status move. */
export function causeWhere(attempt: TicketAttempt): string | null {
  const source = attempt.cause?.source;
  const pr = prNumber(source?.url);
  if (pr) return `#${pr}`;
  if (attempt.cause?.channel === "ticket_comment" || attempt.cause?.channel === "status_move") return "the ticket";
  return null;
}

/** The cause's label: its channel, and for a status move the status it moved into. */
export function causeLabel(attempt: TicketAttempt): string | null {
  const cause = attempt.cause;
  if (!cause) return null;
  const into = cause.channel === "status_move" && cause.source?.detail ? ` to ${cause.source.detail}` : "";
  return `${channelWords(cause.channel)}${into}`;
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
