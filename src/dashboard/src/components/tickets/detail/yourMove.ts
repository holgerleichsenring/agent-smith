import { RUN_END_KINDS, type TicketAttempt, type TicketServerBucket } from "@/types/tickets";
import type { StateAct, TicketStateView } from "./ticketState";

// 2026-11-16-6877a: the Your move box's words, read off ticketState's view. ticketState decides the state, the
// sentence and the one act; this mapper only picks the box's colour and its one-line title. A stuck ticket's title
// comes from the head attempt's endKind today; once the server sends an end detail, the same function reads it.

/** amber = needs you, person = in review or waiting on a person, ok = passed, bad = stopped, quiet = nothing to do. */
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
  /** ticketState's sentence, or the end detail's instruction. */
  text: string;
  /** The question, the hand-back or the live step, drawn under the text. */
  detail: string | null;
  detailFormat?: "markdown";
  act: StateAct | null;
  /** Only a stopped ticket offers the run's own end text behind "Why". */
  why: boolean;
}

const STUCK_TITLES: Record<string, string> = {
  [RUN_END_KINDS.imageUntrusted]: "Stuck: an image registry is not trusted",
  [RUN_END_KINDS.bootstrapMissing]: "Stuck: a repository has no bootstrap",
  [RUN_END_KINDS.preflight]: "Stuck: a precondition did not hold",
  [RUN_END_KINDS.criteriaUnmet]: "Stopped: a done criterion was not accepted",
};

export function yourMove(
  view: TicketStateView, bucket: TicketServerBucket, kind: string | null, endDetail?: EndDetail | null,
): YourMove {
  const base = { text: view.sentence, detail: view.detail, detailFormat: view.detailFormat, act: view.act, why: false };
  switch (bucket) {
    case "needs_you":
      return { ...base, tone: "amber", title: kind === "question" ? "Your move: answer the question" : "Your move: the ticket came back to you" };
    case "waiting_on_review":
      return { ...base, tone: "person", title: "Your move: review the pull request" };
    case "failed":
    case "stuck":
      return stuck(view, base, endDetail ?? null);
    case "pr_closed":
      return { ...base, tone: "bad", title: "Stopped: the pull requests were closed", why: true };
    case "done":
      return { ...base, tone: "ok", title: "Done" };
    case "working":
      return { ...base, tone: "quiet", title: workingTitle(view.attempt) };
    case "closed":
      return { ...base, tone: "quiet", title: "Closed" };
    default:
      return { ...base, tone: "quiet", title: "Nothing to do" };
  }
}

/** The stopped box: the end detail's subject when the server sent one, else the head attempt's end kind. */
export function stuck(view: TicketStateView, base: Omit<YourMove, "tone" | "title">, endDetail: EndDetail | null): YourMove {
  if (endDetail?.subject) {
    return { ...base, tone: "bad", why: true, title: `Stuck: ${endDetail.subject}`, text: instruction(endDetail) ?? base.text,
      act: endDetail.settingsPath ? { kind: "link", label: "Open the setting", href: endDetail.settingsPath } : base.act };
  }
  return { ...base, tone: "bad", why: true, title: stuckTitle(view.attempt) };
}

/** 2026-11-17-6d9ea: exported for the run page's How it ended, which names a stop the way the ticket page does. */
export function stuckTitle(head: TicketAttempt | null): string {
  if (head?.endedOnInfrastructure) return "Stopped: the sandbox or host was lost";
  return (head?.endKind && STUCK_TITLES[head.endKind]) || "Stopped";
}

export function instruction(detail: EndDetail): string | null {
  if (!detail.control) return null;
  const to = detail.setTo ? ` to ${detail.setTo}` : "";
  const where = detail.settingsPath ? ` under ${detail.settingsPath}` : "";
  return `Set ${detail.control}${to}${where}. It restarts by itself once you save.`;
}

function workingTitle(head: TicketAttempt | null): string {
  switch (head?.status) {
    case "queued": return "Queued. Nothing for you to do.";
    case "waiting_for_input": return "Resuming. Nothing for you to do.";
    default: return "Working. Nothing for you to do.";
  }
}
