import { EventType, type RunEvent, type StepStartedEvent } from "@/types/hub-events";
import type { T, TextKey } from "@/lib/i18n/translate";
import { seriesRunPath } from "@/lib/seriesRunAddress";
import {
  RUN_END_KINDS, type TicketAttempt, type TicketDetail, type TicketHold, type TicketPullRequest, type TicketSeries, type TicketSummary,
} from "@/types/tickets";
import { holdSentence, toldOf } from "../holds/holdWords";
import { isAsked } from "../holds/holdRoutes";
import { currentModelWait, type ModelWait } from "@/components/jobs/runlog/runNowLines";
import { closedWords } from "../TicketActivity";
import { liveSpec } from "./attemptStory";

// 2026-10-16-86e6c: the ticket state block's words — what is going on (one sentence and a tone), what to do (ONE act,
// 2026-10-16-7ed3b: no other options — the Conversation tab is one click away) and which run the sentence is about, so the way to it and to its verbatim cause
// stays in sight. 2026-11-23-31dcg: who acts and the act are the ticket's hold, read on the server — a held ticket's view
// is the hold's sentence (holdWords) and its one act; this module keeps only the states nothing holds: the live run's
// progress line, done, closed, ended, a cancelled or paused head. 2026-11-23-d60d: and a pull request that waits on nobody
// (CI running, behind another pull request) under working, in its hold's words — there is no in-review state.

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
  /** 2026-11-23-31dcg: the language the hold's words are drawn in. */
  t: T;
}

export const CONTINUE: StateAct = { kind: "tab", label: "Continue in conversation", tab: "conversation" };

const base = (attempt: TicketAttempt | null): Omit<TicketStateView, "tone" | "pill" | "sentence"> =>
  ({ detail: null, attempt, act: null, quiet: false, pullRequests: [] });

export function ticketState(input: TicketStateInput): TicketStateView | null {
  const view = stateOf(input);
  return view && !view.quiet ? { ...view, pullRequests: input.detail.summary.pullRequests ?? [] } : view;
}

function stateOf({ detail, liveEvents, series, t }: TicketStateInput): TicketStateView | null {
  const head = detail.attempts.at(-1) ?? null;
  const { summary } = detail;
  const hold = summary.hold ?? null;
  // 2026-11-23-31dcg: someone's hold is the view — its sentence and its one act; nobody's is the state below.
  if (hold && hold.addressee !== "nobody") return held(detail, head, hold, t);
  if (!head) return null;
  switch (summary.bucket) {
    case "working": return working(detail, head, liveEvents, series, t);
    case "failed":
    case "stuck":
      return ended(head, hold, t);
    case "pr_closed":
      return { ...base(head), tone: "bad", pill: "PR closed", sentence: t("tickets.state.prClosed"), act: CONTINUE };
    case "done": return { ...base(head), quiet: true, tone: "ok", pill: "Done", sentence: doneSentence(summary, head, t) };
    case "closed": return { ...base(head), quiet: true, tone: "q", pill: "Closed", sentence: capital(closedWords(t, summary)) };
    case "idle": return { ...base(head), quiet: true, tone: "q", pill: "Ended", sentence: endedSentence(head, t) };
    default: return null;
  }
}

/** 2026-11-22-7103a: why an attempt stopped before any work, by its end kind, without the attempt's number — a run's
 *  status line says the same words. */
const STOP_WORDS: Record<string, TextKey> = {
  [RUN_END_KINDS.imageUntrusted]: "tickets.state.stop.imageUntrusted",
  [RUN_END_KINDS.bootstrapMissing]: "tickets.state.stop.bootstrapMissing",
  [RUN_END_KINDS.preflight]: "tickets.state.stop.preflight",
};

/** 2026-11-22-7103a: a stopped run's status head from its end kind and detail — "Stopped before any work: …" or
 *  "Failed: …" with no attempt number and no closing full stop; null when the end names no cause. */
export function endCauseHead(t: T, head: TicketAttempt): string | null {
  const told = toldOf(t, head)?.replace(/[.!?]$/, "") ?? null;
  const kind = head.endKind ?? "";
  if (STOP_WORDS[kind]) return t("tickets.state.stoppedBefore", { why: told ?? t(STOP_WORDS[kind]) });
  switch (kind) {
    case RUN_END_KINDS.readinessUnknown: {
      const why = head.endDetail?.subject;
      const unread = t("tickets.state.stop.readinessUnknown");
      return t("tickets.state.stoppedBefore", { why: why ? `${unread}: ${why}` : unread });
    }
    case RUN_END_KINDS.criteriaUnmet: return t("tickets.state.failed", { why: t("tickets.state.stop.criteriaUnmet") });
    default: return told ? t("tickets.state.failed", { why: told }) : null;
  }
}

/** 2026-11-23-31dcg: the view of a ticket someone's hold lies on — the hold's sentence, its one act, and the question or
 *  hand-back under it. A stop names the attempt that stopped, a question the attempt that asks. */
function held(detail: TicketDetail, head: TicketAttempt | null, hold: TicketHold, t: T): TicketStateView {
  const { summary } = detail;
  const pending = summary.pendingQuestion;
  const asked = isAsked(hold);
  const asking = asked && pending ? detail.attempts.find((a) => a.runId === pending.runId) ?? head : head;
  const person = hold.addressee === "merge" || hold.addressee === "plan" || hold.case === "question" || hold.case === "handback";
  const view: TicketStateView = {
    ...base(asking), tone: person ? "person" : hold.escalation ? "amber" : "bad", pill: hold.addressee, hold,
    sentence: holdSentence(t, hold), act: holdAct(detail, head, hold, t),
  };
  if (asked && pending) return { ...view, detail: pending.question.text };
  if (hold.case === "handback") return { ...view, detail: summary.handback?.text ?? null, detailFormat: "markdown" };
  return view;
}

/** The hold's act as the box draws it; a target the page cannot act on draws no act. */
function holdAct(detail: TicketDetail, head: TicketAttempt | null, hold: TicketHold, t: T): StateAct | null {
  const act = hold.act;
  switch (act?.kind) {
    case undefined: return null;
    case "answer": return { kind: "tab", label: t("hold.act.answerTab"), tab: "conversation" };
    // 2026-11-23-31dch: a conversation with a target is the series' — a plan question is talked through there.
    case "conversation":
      return act.target ? { kind: "link", label: t("hold.act.plan"), href: `${seriesRunPath(act.target)}?section=talk` } : CONTINUE;
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
    // 2026-11-23-849e: a ticket that waits on its series' merges opens the series.
    case "series": return act.target ? { kind: "link", label: t("hold.act.series"), href: seriesRunPath(act.target) } : null;
    // 2026-11-23-d60d: the one open pull request on its host; several are drawn as the block's rows instead.
    case "pull_request": return act.target ? { kind: "link", label: t("hold.act.pull_request"), href: act.target } : null;
  }
}

/** A head nothing holds that ended short: a start its paused series held, a person's cancel, or one the restart takes up. */
function ended(head: TicketAttempt, hold: TicketHold | null, t: T): TicketStateView {
  // 2026-11-19-1b72a: a start its paused series held is not a failure and has nothing to continue — the resume starts it.
  if (head.endKind === RUN_END_KINDS.seriesPaused)
    return { ...base(head), tone: "q", pill: "Paused", sentence: t("tickets.state.seriesPaused") };
  if (hold?.case === "restarting") return { ...base(head), quiet: true, tone: "amber", pill: "Restarting", sentence: holdSentence(t, hold), hold };
  // 2026-11-17-f9b0a: a cancelled head is in the failed bucket and says it was cancelled; 2026-11-22-7103a: grey.
  return { ...base(head), tone: "q", pill: "Cancelled", sentence: t("tickets.state.cancelled", { n: head.number }), act: CONTINUE };
}

/** 2026-11-17-f9b0a: done without a pull request is an attempt delivered without one, or a landing marked by hand. */
function doneSentence(summary: TicketSummary, head: TicketAttempt, t: T): string {
  if ((summary.pullRequests ?? []).length > 0) return t("tickets.state.doneMerged");
  return head.status === "success" || head.status === "shortfall"
    ? t("tickets.state.doneWithoutPr", { n: head.number })
    : t("tickets.state.doneByHand");
}

function working(
  detail: TicketDetail, head: TicketAttempt, events: RunEvent[], series: TicketSeries | null, t: T,
): TicketStateView {
  const place = detail.summary.queuePlace;
  const at = place != null ? t("tickets.state.placeSuffix", { place }) : "";
  // 2026-11-23-31dci: a queued or resuming attempt says since when it waits, from its hold; its place goes under it.
  const hold = detail.summary.hold;
  // 2026-11-23-d60d: an ended attempt whose pull request waits says what it waits for; its pull requests draw under it.
  if (hold && !["running", "queued", "waiting_for_input"].includes(head.status))
    return { ...base(head), tone: "amber", pill: "Waiting", sentence: holdSentence(t, hold), hold };
  const waiting = hold?.since && (hold.case === "queued" || hold.case === "resuming") ? holdSentence(t, hold) : null;
  const placeLine = waiting && place != null ? t("tickets.state.placeLine", { place }) : null;
  if (head.status === "queued") {
    return { ...base(head), quiet: true, tone: "amber", pill: "Queued", sentence: waiting ?? t("tickets.state.queued", { n: head.number, at }), detail: placeLine };
  }
  if (head.status === "waiting_for_input") {
    return { ...base(head), quiet: true, tone: "amber", pill: "Resuming", sentence: waiting ?? t("tickets.state.resuming", { n: head.number, at }), detail: placeLine };
  }
  // 2026-11-22-50d7a: a run behind another run's model place is not working — the gate's line replaces "Now: <step>".
  const modelWait = currentModelWait(events);
  return { ...base(head), quiet: true, tone: "amber", pill: "Working", sentence: progress(t, head, series),
    detail: modelWait?.line ?? liveLine(t, events), modelWait };
}

/** "Spec n of m, step k of s: <action>" from the attempt's own cut, else what it is doing before it has one. */
export function progress(t: T, head: TicketAttempt, series: TicketSeries | null): string {
  const on = liveSpec(head, series);
  if (!on) return t("tickets.state.deriving", { n: head.number });
  const action = on.step !== null ? on.spec.steps[on.step - 1]?.action : null;
  const step = on.step !== null ? t("tickets.state.stepOf", { step: on.step, steps: on.steps }) : "";
  return t("tickets.state.progress", { ordinal: on.spec.ordinal, of: on.of, step, action: action ?? on.spec.goal });
}

/** The newest step the live run started, as its display name says. */
export function liveLine(t: T, events: RunEvent[]): string | null {
  const started = events.filter((e): e is StepStartedEvent => e.type === EventType.StepStarted).at(-1);
  return started ? t("tickets.state.now", { step: started.displayName ?? started.stepName }) : null;
}

function endedSentence(head: TicketAttempt, t: T): string {
  if (head.status === "cancelled") return t("tickets.state.cancelled", { n: head.number });
  if (head.status === "success" || head.status === "shortfall") return t("tickets.state.finishedWithoutPr", { n: head.number });
  return t("tickets.state.ended", { n: head.number });
}

function capital(words: string): string {
  return words.charAt(0).toUpperCase() + words.slice(1);
}
