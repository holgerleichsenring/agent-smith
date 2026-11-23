import { isRelaunching, relaunchPlace } from "@/components/jobs/runStatus";
import { retriable } from "@/components/tickets/detail/RetryUnchanged";
import { endCauseHead } from "@/components/tickets/detail/ticketState";
import { NOTHING, type YourMove } from "@/components/tickets/detail/yourMove";
import { formatRunSummary } from "@/lib/formatRunSummary";
import type { T } from "@/lib/i18n/translate";
import type { RunSpecRow } from "@/lib/runSpecsApi";
import type { RunStepRow } from "@/lib/runStepsApi";
import type { RunSnapshot } from "@/types/hub-events";
import { RUN_END_KINDS, type TicketAttempt, type TicketDetail } from "@/types/tickets";
import { pullRequestLinks } from "./runNowLines";

// 2026-11-22-7103a: a run's one status line — what happened, then the next step — read the same on the ticket page
// (its row in Now, Conversation and Runs) and on the run page (the head's state word, How it ended's first line). It
// reads recorded fields only: the run's status, end kind and detail, a hand-back, an infrastructure end, a waiting
// question, its RunSpec rows (status and verdict), its pull requests, its summary and its failed step. A verdict is shown
// as the server wrote it, cut at its first sentence; nothing is matched. The next step is the ticket's Your move.

/** green delivered or passed, amber working, blue waits for a person, red stopped or not delivered, grey nothing to judge. */
export type StatusTone = "ok" | "run" | "person" | "bad" | "quiet";

export interface StatusSpec {
  specId: string;
  ordinal: number;
  /** not_started | in_progress | done | failed | handed_back | carried */
  status: string;
  verdict: string | null;
}

export interface StatusInput {
  status: string;
  endKind?: string | null;
  handedBack?: boolean;
  endedOnInfrastructure?: boolean;
  /** Answered and waiting for its place again. */
  resuming?: boolean;
  queuePlace?: number | null;
  specs: StatusSpec[];
  /** Repositories with a pull request this run opened. */
  pullRequests: string[];
  summary?: string | null;
  /** The failed step's first line, when the page holds the steps. */
  failedStep?: string | null;
  /** The attempt, for its end detail's sentence. */
  attempt?: TicketAttempt | null;
  /** The next step in words; null omits it. */
  next?: string | null;
  /** Add that Retry unchanged gives the undelivered specs another attempt. */
  retryAlso?: boolean;
  /** 2026-11-22-50d7a: a running run waiting for its model place — the head, worded by the caller from currentModelWait. */
  waitingForModel?: string | null;
}

export interface RunStatusLine {
  tone: StatusTone;
  head: string;
  /** The head without its cause ("Failed", "1 of 2 specs delivered") — the run page's state word. */
  short: string;
  clauses: string[];
  next: string | null;
}

/** A text's first sentence: up to the first ". " or line end, whichever comes first. */
export function firstSentence(text: string | null | undefined): string | null {
  const line = (text ?? "").split(/\r?\n/).map((l) => l.trim()).find((l) => l.length > 0);
  if (!line) return null;
  const stop = line.search(/[.!?](\s|$)/);
  return stop >= 0 ? line.slice(0, stop + 1) : line;
}

function delivered(status: string): boolean {
  return status === "done" || status === "carried";
}

/** A head never ends in a full stop: the sentence adds its own. */
function bare(text: string): string {
  return text.replace(/[.!?]+$/, "");
}

/** A failed run's head, first that exists: a failed spec's verdict, the end kind's words (endCauseHead), the summary's
 *  first sentence, the failed step's first line; never the generic "Attempt n failed." */
function failedHead(t: T, input: StatusInput): { head: string; clause: string | null } {
  const failed = input.specs.find((s) => s.status === "failed" && s.verdict);
  const verdict = failed ? firstSentence(failed.verdict) : null;
  const ended = !verdict && input.attempt ? endCauseHead(input.attempt) : null;
  if (ended) return { head: ended, clause: null };
  const why = verdict ?? said(input) ?? firstSentence(input.failedStep);
  if (!why) return { head: t("runs.log.line.failed"), clause: null };
  // A cause that says what failed itself ("Preflight failed: …") follows a bare "Failed", never "Failed: Preflight failed".
  return SAYS_FAILED.test(why)
    ? { head: t("runs.log.line.failed"), clause: why }
    : { head: t("runs.log.line.failedBecause", { why: bare(why) }), clause: null };
}

/** "Preflight failed: …", "dotnet build failed …": a cause whose first words already say what failed. */
const SAYS_FAILED = /^\S+(?: \S+)? failed\b/i;

function said(input: StatusInput): string | null {
  return input.summary ? firstSentence(formatRunSummary(input.summary)) : null;
}

function pullRequestClause(t: T, repos: string[]): string {
  if (repos.length === 0) return t("runs.log.line.noPullRequest");
  return repos.length === 1 ? t("runs.log.line.pullRequestIn", { repo: repos[0] }) : t("runs.log.line.pullRequests", { count: repos.length });
}

function headOf(t: T, input: StatusInput): Omit<RunStatusLine, "next" | "short"> {
  const status = input.status.toLowerCase();
  const specs = [...input.specs].sort((l, r) => l.ordinal - r.ordinal);
  const missing = specs.filter((s) => !delivered(s.status));
  if (input.endKind === RUN_END_KINDS.superseded) return { tone: "quiet", head: t("runs.log.line.superseded"), clauses: [] };
  if (input.endKind === RUN_END_KINDS.seriesPaused) return { tone: "quiet", head: t("runs.log.line.seriesPaused"), clauses: [] };
  if (input.resuming) return { tone: "run", head: input.queuePlace ? t("runs.log.line.resumingPlace", { place: input.queuePlace }) : t("runs.log.line.resuming"), clauses: [] };
  switch (status) {
    case "queued": return { tone: "run", head: input.queuePlace ? t("runs.log.line.queuedPlace", { place: input.queuePlace }) : t("runs.log.line.queued"), clauses: [] };
    case "running": {
      if (input.waitingForModel) return { tone: "run", head: input.waitingForModel, clauses: [] };
      const on = specs.find((s) => s.status === "in_progress");
      return { tone: "run", head: on ? t("runs.log.line.workingOnSpec", { n: on.ordinal, total: specs.length }) : t("runs.log.line.working"), clauses: [] };
    }
    case "waiting_for_input": return { tone: "person", head: t("runs.log.line.waits"), clauses: [] };
  }
  if (input.handedBack) {
    const why = said(input);
    return { tone: "person", head: t("runs.log.line.handedBack"), clauses: why ? [why] : [] };
  }
  if (input.endedOnInfrastructure) return { tone: "bad", head: t("runs.log.line.infrastructure"), clauses: [] };
  switch (status) {
    case "success": {
      if (specs.length === 0) return { tone: "ok", head: t("runs.log.line.done"), clauses: input.pullRequests.length > 0 ? [pullRequestClause(t, input.pullRequests)] : [] };
      const head = specs.length > 1 ? t("runs.log.line.specsDelivered", { done: specs.length, total: specs.length }) : t("runs.log.line.delivered");
      return { tone: "ok", head, clauses: [pullRequestClause(t, input.pullRequests)] };
    }
    case "shortfall": {
      if (specs.length === 0) {
        const why = said(input);
        return { tone: "bad", head: t("runs.log.line.notEverything"), clauses: why ? [why] : [] };
      }
      const head = t("runs.log.line.specsDelivered", { done: specs.length - missing.length, total: specs.length });
      const clauses = missing.map((s) => {
        const why = firstSentence(s.verdict);
        return why ? `${s.specId}: ${why}` : t("runs.log.line.notDelivered", { spec: s.specId });
      });
      return { tone: "bad", head, clauses };
    }
    case "cancelled": return { tone: "quiet", head: t("runs.log.line.cancelled"), clauses: [] };
    default: {
      const verified = specs.filter((s) => s.status === "done").map((s) => s.specId);
      const clauses = verified.length > 0 ? [t("runs.log.line.verifiedNotDelivered", { specs: verified.join(", ") })] : [];
      const { head, clause } = failedHead(t, input);
      return { tone: "bad", head, clauses: clause ? [clause, ...clauses] : clauses };
    }
  }
}

export function runStatus(t: T, input: StatusInput): RunStatusLine {
  const line = headOf(t, input);
  const short = line.head.includes(": ") ? line.head.slice(0, line.head.indexOf(": ")) : line.head;
  let next = input.next ?? null;
  const missing = input.specs.filter((s) => !delivered(s.status)).map((s) => s.specId);
  if (next && input.retryAlso && input.status === "shortfall" && missing.length > 0)
    next = t("runs.log.line.retryAlso", { next, specs: missing.join(", ") });
  return { ...line, short, next };
}

/** What follows the head: the clauses, then "→ next." */
export function statusRest(line: RunStatusLine): string {
  const clauses = line.clauses.map((c) => (/[.!?]$/.test(c) ? c : `${c}.`)).join(" ");
  const next = line.next ? `→ ${line.next}.` : "";
  return [clauses, next].filter(Boolean).join(" ");
}

/** What stands between the head and the rest, the same on every page: " · ", or a space after a head that ends in
 *  its own punctuation. */
export function statusJoin(head: string): string {
  return /[.!?]$/.test(head) ? " " : " · ";
}

/** The whole line as text: "1 of 2 specs delivered · B: … → Review the pull request." */
export function statusSentence(line: RunStatusLine): string {
  const rest = statusRest(line);
  return rest ? `${line.head}${statusJoin(line.head)}${rest}` : line.head;
}

/** An attempt's line as the ticket page holds it: its RunSpec rows, its pull requests, its end. */
export function attemptStatusInput(
  attempt: TicketAttempt, next: string | null = null, retryAlso = false, queuePlace: number | null = null,
): StatusInput {
  return {
    status: attempt.status, endKind: attempt.endKind, handedBack: attempt.handedBack,
    endedOnInfrastructure: attempt.endedOnInfrastructure,
    // An answered question that waits for its place again is resuming, not asking.
    resuming: attempt.status === "waiting_for_input" && !attempt.pendingQuestion, queuePlace,
    specs: attempt.executedSpecs.map((s) => ({ specId: s.specId, ordinal: s.ordinal, status: s.status, verdict: s.verdict })),
    pullRequests: attempt.pullRequests.filter((p) => p.url && p.status !== "failed" && p.status !== "no_changes").map((p) => p.repo),
    summary: attempt.summary, attempt, next, retryAlso,
  };
}

/** What the next step is read from: the ticket's move, its latest attempt, and whether this reader may retry. */
export interface StatusContext {
  move: YourMove | null;
  latest: TicketAttempt | null;
  /** The reader holds runs.control: Retry unchanged is named only to someone who can press it. */
  canRetry: boolean;
}

/** The next step of a run: the ticket's move for its newest attempt, the way to the latest for an older one, none
 *  without a move or when the move is a Retry this reader cannot press. */
export function nextStep(t: T, ctx: StatusContext | null, attempt: TicketAttempt | null): string | null {
  if (!ctx?.move || !attempt) return null;
  if (ctx.latest && ctx.latest.number !== attempt.number) return t("runs.log.line.seeLatest", { number: ctx.latest.number });
  if (ctx.move.act?.kind === "retry" && !ctx.canRetry) return null;
  return ctx.move.next || NOTHING;
}

/** Whether the line adds Retry for a shortfall: the newest attempt, retriable by its own rule, a reader who may retry,
 *  and a move whose act is not Retry already. */
export function retryAlso(ctx: StatusContext | null, attempt: TicketAttempt | null): boolean {
  if (!ctx?.move || !attempt || !ctx.canRetry || !retriable(attempt)) return false;
  if (ctx.latest && ctx.latest.number !== attempt.number) return false;
  return ctx.move.act?.kind !== "retry";
}

/** An attempt's line with its next step, as the ticket page reads it. */
export function attemptStatus(
  t: T, attempt: TicketAttempt, ctx: StatusContext | null, queuePlace: number | null = null, waitingForModel: string | null = null,
): RunStatusLine {
  const newest = !ctx?.latest || ctx.latest.number === attempt.number;
  const input = attemptStatusInput(attempt, nextStep(t, ctx, attempt), retryAlso(ctx, attempt), newest ? queuePlace : null);
  return runStatus(t, { ...input, waitingForModel });
}

/** The ticket's latest attempt. */
export function latestAttempt(detail: TicketDetail | null): TicketAttempt | null {
  return detail?.attempts.reduce<TicketAttempt | null>((top, a) => (!top || a.number > top.number ? a : top), null) ?? null;
}

/** The run page's line: the snapshot, its RunSpec rows and steps, and the ticket's attempt and move when it has one. */
export function snapshotStatus(
  t: T, snapshot: RunSnapshot, specs: RunSpecRow[], steps: RunStepRow[],
  ticket: { attempt: TicketAttempt | null; latest: TicketAttempt | null; move: YourMove | null } | null, canRetry = false,
  waitingForModel: string | null = null,
): RunStatusLine {
  const attempt = ticket?.attempt ?? null;
  const failed = [...steps].reverse().find((s) => s.status === "failed" || s.status === "error");
  const relaunching = isRelaunching(snapshot);
  const status = (snapshot.status ?? "").toLowerCase();
  return runStatus(t, {
    status, endKind: attempt?.endKind, handedBack: attempt?.handedBack, endedOnInfrastructure: attempt?.endedOnInfrastructure,
    resuming: relaunching, queuePlace: relaunching ? relaunchPlace(snapshot) : snapshot.queuePosition,
    specs: specs.map((s) => ({ specId: s.specId, ordinal: s.ordinal, status: s.status, verdict: s.verdict })),
    pullRequests: pullRequestLinks(snapshot).map((p) => p.repo || t("runs.log.line.theRepository")),
    summary: snapshot.summary, failedStep: failed?.resultMessage ?? null, attempt,
    next: ticket ? nextStep(t, { ...ticket, canRetry }, attempt) : null,
    retryAlso: ticket ? retryAlso({ ...ticket, canRetry }, attempt) : false,
    waitingForModel,
  });
}
