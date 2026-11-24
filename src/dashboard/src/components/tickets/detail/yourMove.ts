import type { T, TextKey } from "@/lib/i18n/translate";
import { RUN_END_KINDS, type TicketAttempt, type TicketHold, type TicketServerBucket } from "@/types/tickets";
import type { StateAct, TicketStateView } from "./ticketState";

// 2026-11-16-6877a: the Your move box's words, read off ticketState's view. ticketState decides the state, the
// sentence and the one act; this mapper only picks the box's colour and its one-line title. A stuck ticket's title
// comes from the head attempt's endKind today; once the server sends an end detail, the same function reads it.
// 2026-11-22-7103a: the move carries its next step in words (`next`, the act's own label) so a run's status line names
// the same act the box offers; the one-open-PR review act lives here, not in the box. Tones follow the palette — a
// question or a hand-back is a person's (blue), working is amber, a cancelled head is grey.
// 2026-11-23-31dcg: a held ticket's title, colour and act are its hold's — the bucket and the question kind no longer
// decide who acts; what remains here are the titles of the states nothing holds.
// 2026-11-23-d60d: no in-review state — a pull request that can merge is a merge hold, red CI a decision, and one that
// waits is working with its hold's sentence.

/** amber = needs you or working, person = waiting on a person, ok = passed, bad = stopped, quiet = nothing to do. */
export type MoveTone = "amber" | "person" | "ok" | "bad" | "quiet";

/** The structured end of a run stopped on a precondition, when the server sends one. */
export interface EndDetail {
  subject?: string | null;
  repos?: string[] | null;
  control?: string | null;
  setTo?: string | null;
  settingsPath?: string | null;
}

export interface YourMove {
  tone: MoveTone;
  title: string;
  /** ticketState's sentence — for a held ticket the hold's, the same on every surface. */
  text: string;
  /** The question, the hand-back or the live step, drawn under the text. */
  detail: string | null;
  detailFormat?: "markdown";
  act: StateAct | null;
  /** Only a stopped ticket offers the run's own end text behind "Why". */
  why: boolean;
  /** A run waits on a question: the conversation act answers it. */
  question: boolean;
  /** 2026-11-22-7103a: the next step in words — the act's label, else what the state asks of nobody. */
  next: string;
}

const STUCK_TITLES: Record<string, TextKey> = {
  [RUN_END_KINDS.imageUntrusted]: "tickets.move.stuck.imageUntrusted",
  [RUN_END_KINDS.bootstrapMissing]: "tickets.move.stuck.bootstrapMissing",
  [RUN_END_KINDS.preflight]: "tickets.move.stuck.preflight",
  [RUN_END_KINDS.criteriaUnmet]: "tickets.move.stuck.criteriaUnmet",
};

export function yourMove(
  t: T, view: TicketStateView, bucket: TicketServerBucket, endDetail?: EndDetail | null,
): YourMove {
  const hold = view.hold ?? null;
  const move = hold && hold.addressee !== "nobody" ? heldMove(view, hold, endDetail ?? null, t) : moveOf(view, bucket, t);
  // 2026-11-23-31dch: a plan question's act opens the series' conversation; the others answer here.
  const question = hold?.case === "question" || hold?.case === "question_environment";
  // 2026-11-23-d60d: several open pull requests name no single one — the block's rows under the box are the way on.
  const next = move.act ? actLabel(t, move.act, question)
    : hold?.act?.kind === "pull_request" ? t("hold.act.pull_requests") : t("hold.title.nobody");
  return { ...move, question, next };
}

/** The statuses of an attempt still at work — running, queued, or relaunching. */
const LIVE = new Set(["running", "queued", "waiting_for_input"]);

/** The words a move's act is drawn with — the box's button and a run's next step say the same. */
export function actLabel(t: T, act: StateAct, question: boolean): string {
  switch (act.kind) {
    case "retry": return t("hold.act.retry");
    case "init": return t("hold.act.init");
    case "merge": return t("hold.act.merge");
    case "refresh": return t("hold.act.refresh");
    case "link": return act.label;
    case "tab": return question ? t("hold.act.answer") : t("hold.act.conversation");
  }
}

type Move = Omit<YourMove, "question" | "next">;

/** 2026-11-23-31dcg: the box of a ticket someone's hold lies on — its title by the hold's case, its act the hold's. A
 *  stop is titled by what ended it (the end detail's subject, else the end kind), an admin's stop also says the setting. */
function heldMove(view: TicketStateView, hold: TicketHold, endDetail: EndDetail | null, t: T): Move {
  const base = { text: view.sentence, detail: view.detail, detailFormat: view.detailFormat, act: view.act, why: false };
  switch (hold.case) {
    case "question": return { ...base, tone: "person", title: t("hold.title.question") };
    case "question_plan": return { ...base, tone: "person", title: t("hold.title.question_plan") };
    case "question_environment": return { ...base, tone: "bad", title: t("hold.title.question_environment") };
    case "handback": return { ...base, tone: "person", title: t("hold.title.handback") };
    case "text_edited": return { ...base, tone: "person", title: t("hold.title.text_edited") };
    case "merge_next": return { ...base, tone: "person", title: t("hold.title.merge_next") };
    case "merge_ready": return { ...base, tone: "person", title: t("hold.title.merge_ready") };
    case "ci_failed": return { ...base, tone: "bad", title: t("hold.title.ci_failed") };
    case "pr_conflict": return { ...base, tone: "bad", title: t("hold.title.pr_conflict") }; // 2026-11-24-6811a
    case "refresh_refused": return { ...base, tone: "bad", title: t("hold.title.refresh_refused") }; // 2026-11-24-6811d
  }
  // 2026-11-23-31dci: a wait that outlasted the configured minutes is no stop — the admin was told, nothing failed.
  if (hold.escalation) return { ...base, tone: "amber", title: t("hold.title.escalated") };
  // The hold's sentence stays the text, as on every surface; the setting's instruction goes under it.
  if (hold.addressee === "admin" && endDetail?.subject)
    return { ...base, tone: "bad", why: true, title: t("hold.title.stuckOn", { subject: endDetail.subject }),
      detail: instruction(t, endDetail) ?? base.detail, detailFormat: undefined };
  return { ...base, tone: "bad", why: true, title: stuckTitle(t, view.attempt) };
}

function moveOf(view: TicketStateView, bucket: TicketServerBucket, t: T): Move {
  const base = { text: view.sentence, detail: view.detail, detailFormat: view.detailFormat, act: view.act, why: false };
  switch (bucket) {
    case "failed":
    case "stuck":
      // 2026-11-22-7103a: a start its paused series held, a head a person cancelled, and one the restart takes up are no fault.
      if (view.attempt?.endKind === RUN_END_KINDS.seriesPaused) return { ...base, tone: "quiet", title: t("tickets.move.paused") };
      if (view.hold?.case === "restarting") return { ...base, tone: "amber", title: t("hold.title.nobody") };
      return { ...base, tone: "quiet", title: t("tickets.move.cancelled") };
    case "pr_closed":
      return { ...base, tone: "bad", title: t("tickets.move.prClosed"), why: true };
    case "done":
      return { ...base, tone: "ok", title: t("tickets.move.done") };
    case "working":
      // 2026-11-23-d60d: an ended attempt whose pull request waits — CI, another merge — is nothing for you to do.
      if (view.hold && !LIVE.has(view.attempt?.status ?? "")) return { ...base, tone: "amber", title: t("hold.title.nobody") };
      return { ...base, tone: "amber", title: workingTitle(view.attempt, t) };
    case "closed":
      return { ...base, tone: "quiet", title: t("tickets.move.closed") };
    default:
      return { ...base, tone: "quiet", title: t("tickets.move.nothing") };
  }
}

/** 2026-11-17-6d9ea: exported for the run page's How it ended, which names a stop the way the ticket page does. */
export function stuckTitle(t: T, head: TicketAttempt | null): string {
  if (head?.endedOnInfrastructure) return t("tickets.move.stuck.infrastructure");
  const key = head?.endKind ? STUCK_TITLES[head.endKind] : undefined;
  return key ? t(key) : t("tickets.move.stopped");
}

export function instruction(t: T, detail: EndDetail): string | null {
  if (!detail.control) return null;
  const to = detail.setTo ? t("tickets.move.setTo", { value: detail.setTo }) : "";
  const where = detail.settingsPath ? t("tickets.move.setUnder", { path: detail.settingsPath }) : "";
  return t("tickets.move.set", { control: detail.control, to, where });
}

function workingTitle(head: TicketAttempt | null, t: T): string {
  switch (head?.status) {
    case "queued": return t("tickets.move.queued");
    case "waiting_for_input": return t("tickets.move.resuming");
    default: return t("tickets.move.working");
  }
}
