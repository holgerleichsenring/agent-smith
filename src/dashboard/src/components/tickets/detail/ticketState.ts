import { EventType, type RunEvent, type StepStartedEvent } from "@/types/hub-events";
import { translator, type T } from "@/lib/i18n/translate";
import { seriesRunPath } from "@/lib/seriesRunAddress";
import {
  RUN_END_KINDS, type TicketAttempt, type TicketDetail, type TicketHold, type TicketPullRequest, type TicketSeries, type TicketSummary,
} from "@/types/tickets";
import { holdSentence, toldOf } from "../holds/holdWords";
import { currentModelWait, type ModelWait } from "@/components/jobs/runlog/runNowLines";
import { closedWords } from "../TicketActivity";
import { liveSpec } from "./attemptStory";

// 2026-10-16-86e6c: the ticket state block's words — what is going on (one sentence and a tone), what to do (ONE act,
// 2026-10-16-7ed3b: no other options — the Conversation tab is one click away) and which run the sentence is about, so the way to it and to its verbatim cause
// stays in sight. 2026-11-23-31dcg: who acts and the act are the ticket's hold, read on the server — a held ticket's view
// is the hold's sentence (holdWords) and its one act; this module keeps only the states nothing holds: the live run's
// progress line, done, closed, ended, a cancelled or paused head and a pull request in review outside any series.

export type StateTone = "ok" | "bad" | "amber" | "person" | "q";

export type StateAct =
  | { kind: "retry"; attempt: TicketAttempt }
  | { kind: "init"; project: string }
  /** 2026-11-23-31dcg: merge the ticket's pull requests through its series' merge (31dce). */
  | { kind: "merge"; seriesId: string; ticketId: string }
  | { kind: "link"; label: string; href: string; note?: string }
  | { kind: "tab"; label: string; tab: string; note?: string };

export interface TicketStateView {
  tone: StateTone;
  pill: string;
  sentence: string;
  /** The question, the hand-back or the live step — drawn under the sentence. */
  detail: string | null;
  /** 2026-10-16-7ed3c: a hand-back is markdown as posted on the ticket; every other detail is plain text. */
  detailFormat?: "markdown";
  /** The attempt the sentence is about: its run is linked beside the sentence. */
  attempt: TicketAttempt | null;
  act: StateAct | null;
  /** Healthy: no act and no disclosure (the attempt chips still draw). */
  quiet: boolean;
  /** 2026-10-16-7ed3a: every pull request of the ticket while the state is not quiet — a failed head keeps them. */
  pullRequests: TicketPullRequest[];
  /** 2026-11-22-50d7a: the live attempt waits for its model place (the run log's own reading); absent otherwise. */
  modelWait?: ModelWait | null;
  /** 2026-11-23-31dcg: the hold the view was drawn from; absent when nothing holds the ticket. */
  hold?: TicketHold | null;
}

export interface TicketStateInput {
  detail: TicketDetail;
  liveEvents: RunEvent[];
  /** The ticket's default cut — the live attempt's spec and step when it is that attempt's own. */
  series: TicketSeries | null;
  /** 2026-11-23-31dcg: the language the hold's words are drawn in; English when none is given. */
  t?: T;
}

export const CONTINUE: StateAct = { kind: "tab", label: "Continue in conversation", tab: "conversation" };

const base = (attempt: TicketAttempt | null): Omit<TicketStateView, "tone" | "pill" | "sentence"> =>
  ({ detail: null, attempt, act: null, quiet: false, pullRequests: [] });

export function ticketState(input: TicketStateInput): TicketStateView | null {
  const view = stateOf(input);
  return view && !view.quiet ? { ...view, pullRequests: input.detail.summary.pullRequests ?? [] } : view;
}

function stateOf({ detail, liveEvents, series, t = translator("en") }: TicketStateInput): TicketStateView | null {
  const head = detail.attempts.at(-1) ?? null;
  const { summary } = detail;
  const hold = summary.hold ?? null;
  // 2026-11-23-31dcg: someone's hold is the view — its sentence and its one act; nobody's is the state below.
  if (hold && hold.addressee !== "nobody") return held(detail, head, hold, t);
  if (!head) return null;
  switch (summary.bucket) {
    case "working": return working(detail, head, liveEvents, series);
    case "waiting_on_review": return review(detail, head, hold, t);
    case "failed":
    case "stuck":
      return ended(head, hold, t);
    case "pr_closed":
      return { ...base(head), tone: "bad", pill: "PR closed", sentence: "Every pull request was closed without a merge.", act: CONTINUE };
    case "done": return { ...base(head), quiet: true, tone: "ok", pill: "Done", sentence: doneSentence(summary, head) };
    case "closed": return { ...base(head), quiet: true, tone: "q", pill: "Closed", sentence: capital(closedWords(summary)) };
    case "idle": return { ...base(head), quiet: true, tone: "q", pill: "Ended", sentence: endedSentence(head) };
    default: return null;
  }
}

/** 2026-11-22-7103a: why an attempt stopped before any work, by its end kind, without the attempt's number — a run's
 *  status line says the same words. */
const STOP_WORDS: Record<string, string> = {
  [RUN_END_KINDS.imageUntrusted]: "a context names a container image outside the trusted registries",
  [RUN_END_KINDS.bootstrapMissing]: "a repository has no agent-smith bootstrap",
  [RUN_END_KINDS.preflight]: "a precondition did not hold",
};

/** 2026-11-22-7103a: a stopped run's status head from its end kind and detail — "Stopped before any work: …" or
 *  "Failed: …" with no attempt number and no closing full stop; null when the end names no cause. */
export function endCauseHead(head: TicketAttempt): string | null {
  const told = toldOf(head)?.replace(/[.!?]$/, "") ?? null;
  const kind = head.endKind ?? "";
  if (STOP_WORDS[kind]) return `Stopped before any work: ${told ?? STOP_WORDS[kind]}`;
  switch (kind) {
    case RUN_END_KINDS.readinessUnknown: {
      const why = head.endDetail?.subject;
      return `Stopped before any work: the repositories' bootstrap could not be read from the host${why ? `: ${why}` : ""}`;
    }
    case RUN_END_KINDS.criteriaUnmet: return "Failed: the verifier of that time did not accept a criterion";
    default: return told ? `Failed: ${told}` : null;
  }
}

/** 2026-11-23-31dcg: the view of a ticket someone's hold lies on — the hold's sentence, its one act, and the question or
 *  hand-back under it. A stop names the attempt that stopped, a question the attempt that asks. */
function held(detail: TicketDetail, head: TicketAttempt | null, hold: TicketHold, t: T): TicketStateView {
  const { summary } = detail;
  const pending = summary.pendingQuestion;
  const asking = hold.case === "question" && pending ? detail.attempts.find((a) => a.runId === pending.runId) ?? head : head;
  const person = hold.addressee === "merge" || hold.addressee === "plan" || hold.case === "question" || hold.case === "handback";
  const view: TicketStateView = {
    ...base(asking), tone: person ? "person" : "bad", pill: hold.addressee, hold,
    sentence: holdSentence(hold, t), act: holdAct(detail, head, hold, t),
  };
  if (hold.case === "question" && pending) return { ...view, detail: pending.question.text };
  if (hold.case === "handback") return { ...view, detail: summary.handback?.text ?? null, detailFormat: "markdown" };
  return view;
}

/** The hold's act as the box draws it; a target the page cannot act on draws no act. */
function holdAct(detail: TicketDetail, head: TicketAttempt | null, hold: TicketHold, t: T): StateAct | null {
  const act = hold.act;
  switch (act?.kind) {
    case undefined: return null;
    case "answer": return { kind: "tab", label: t("hold.act.answerTab"), tab: "conversation" };
    case "conversation": return CONTINUE;
    case "retry": {
      const attempt = detail.attempts.find((a) => a.runId === act.target) ?? head;
      return attempt ? { kind: "retry", attempt } : null;
    }
    case "init": return { kind: "init", project: act.target ?? detail.summary.project };
    case "settings":
      if (!act.target) return null;
      // An image refusal that recorded no setting opens the sandbox page and says what to add there.
      return hold.case === "image_untrusted" && !hold.detail?.settingsPath
        ? { kind: "link", label: t("hold.act.trust"), href: act.target, note: t("hold.act.trustNote") }
        : { kind: "link", label: t("hold.act.settings"), href: act.target };
    case "merge": return act.target ? { kind: "merge", seriesId: act.target, ticketId: detail.summary.ticketId } : null;
    case "merge_next":
      return act.target ? { kind: "link", label: t("hold.act.merge_next"), href: `${seriesRunPath(act.target)}?section=next` } : null;
  }
}

/** A head nothing holds that ended short: a start its paused series held, a person's cancel, or one the restart takes up. */
function ended(head: TicketAttempt, hold: TicketHold | null, t: T): TicketStateView {
  // 2026-11-19-1b72a: a start its paused series held is not a failure and has nothing to continue — the resume starts it.
  if (head.endKind === RUN_END_KINDS.seriesPaused)
    return { ...base(head), tone: "q", pill: "Paused", sentence: "Its series is paused; it starts again when the series resumes." };
  if (hold?.case === "restarting") return { ...base(head), quiet: true, tone: "amber", pill: "Restarting", sentence: holdSentence(hold, t), hold };
  // 2026-11-17-f9b0a: a cancelled head is in the failed bucket and says it was cancelled; 2026-11-22-7103a: grey.
  return { ...base(head), tone: "q", pill: "Cancelled", sentence: `Attempt ${head.number} was cancelled.`, act: CONTINUE };
}

/** 2026-11-17-f9b0a: done without a pull request is an attempt delivered without one, or a landing marked by hand. */
function doneSentence(summary: TicketSummary, head: TicketAttempt): string {
  if ((summary.pullRequests ?? []).length > 0) return "Done — the pull requests are merged.";
  return head.status === "success" || head.status === "shortfall"
    ? `Done — attempt ${head.number} finished without a pull request.`
    : "Done — marked done by hand.";
}

function working(detail: TicketDetail, head: TicketAttempt, events: RunEvent[], series: TicketSeries | null): TicketStateView {
  const place = detail.summary.queuePlace;
  const at = place != null ? ` — place ${place} in line` : "";
  if (head.status === "queued") {
    return { ...base(head), quiet: true, tone: "amber", pill: "Queued", sentence: `Attempt ${head.number} waits in the queue${at}.` };
  }
  if (head.status === "waiting_for_input") {
    return { ...base(head), quiet: true, tone: "amber", pill: "Resuming", sentence: `Attempt ${head.number} is resuming${at}.` };
  }
  // 2026-11-22-50d7a: a run behind another run's model place is not working — the gate's line replaces "Now: <step>".
  const modelWait = currentModelWait(events);
  return { ...base(head), quiet: true, tone: "amber", pill: "Working", sentence: progress(head, series),
    detail: modelWait?.line ?? liveLine(events), modelWait };
}

/** "Spec n of m, step k of s: <action>" from the attempt's own cut, else what it is doing before it has one. */
export function progress(head: TicketAttempt, series: TicketSeries | null): string {
  const on = liveSpec(head, series);
  if (!on) return `Attempt ${head.number} is deriving the series.`;
  const action = on.step !== null ? on.spec.steps[on.step - 1]?.action : null;
  const step = on.step !== null ? `, step ${on.step} of ${on.steps}` : "";
  return `Spec ${on.spec.ordinal} of ${on.of}${step}: ${action ?? on.spec.goal}`;
}

/** The newest step the live run started, as its display name says. */
export function liveLine(events: RunEvent[]): string | null {
  const started = events.filter((e): e is StepStartedEvent => e.type === EventType.StepStarted).at(-1);
  return started ? `Now: ${started.displayName ?? started.stepName}` : null;
}

/** The pull requests themselves are the way on: the block draws one row each. 2026-11-23-31dcg: one that merges after
 *  another in its series says so, from its hold. */
function review(detail: TicketDetail, head: TicketAttempt, hold: TicketHold | null, t: T): TicketStateView {
  if (hold) return { ...base(head), tone: "person", pill: "In review", sentence: holdSentence(hold, t), hold };
  const open = (detail.summary.pullRequests ?? []).filter((pr) => pr.state === "open").length;
  const count = open === 1 ? "1 pull request is" : `${open} pull requests are`;
  return { ...base(head), tone: "person", pill: "In review", sentence: `Waiting on review: ${count} open.` };
}

function endedSentence(head: TicketAttempt): string {
  if (head.status === "cancelled") return `Attempt ${head.number} was cancelled.`;
  if (head.status === "success" || head.status === "shortfall") return `Attempt ${head.number} finished without a pull request.`;
  return `Attempt ${head.number} ended.`;
}

function capital(words: string): string {
  return words.charAt(0).toUpperCase() + words.slice(1);
}
