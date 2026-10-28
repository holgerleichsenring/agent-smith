import type { TicketApprovalPreview } from "@/types/tickets";

// 2026-10-13-9a59r: ONE table of what approving a ticket-bound proposal does, for the ticket composer and
// Work it out's approval card alike. The page renders 9a59l's effect and 9a59m's start arm as data — one
// label per value — and decides nothing itself; an unknown value is shown as the server spelled it.

const EFFECT_LABELS: Record<string, string> = {
  "bug-ticket": "files a bug ticket",
  "follow-up-closed": "files a follow-up ticket linked to this closed one",
  "amend-never-worked": "replaces the series no attempt has worked yet",
  "refused-over-cap": "is refused: the series would hold more specs than one ticket may",
  refused: "is refused: the proposed cut has no order its specs can run in",
  continue: "continues this ticket's series",
  comment: "posts this comment on the ticket and starts nothing",
};

const START_LABELS: Record<string, string> = {
  "start-now": "The next attempt starts now.",
  "wait-for-run": "The next attempt starts when the running one ends.",
  "refused-question-pending": "A run question is waiting: answer it first — approving now starts nothing.",
  "recorded-without-runs-control": "The approval is recorded; starting an attempt needs runs.control.",
  "answers-handback": "It answers the hand-back and starts the next attempt.",
};

export const QUESTION_PENDING_ARM = "refused-question-pending";

export interface ApprovalEffectLabel {
  /** "Approving continues this ticket's series." */
  effect: string;
  /** The start arm's sentence; null when the preview names none. */
  start: string | null;
  /** False while a run question pends (9a59m refuses the start) — no Approve is offered. */
  approvable: boolean;
}

export function approvalEffectLabel(preview: Pick<TicketApprovalPreview, "effect" | "start">): ApprovalEffectLabel {
  const effect = `Approving ${EFFECT_LABELS[preview.effect] ?? preview.effect}.`;
  const arm = preview.start?.kind ?? null;
  return {
    effect,
    start: arm ? START_LABELS[arm] ?? arm : null,
    approvable: arm !== QUESTION_PENDING_ARM,
  };
}

const DISPOSITION_MARKS: Record<string, string> = { kept: " ", recut: "~", appended: "+", dropped: "−" };

/** The mark a draft line carries for its disposition (kept, recut, appended, dropped). */
export function dispositionMark(disposition: string | null | undefined): string {
  return disposition ? DISPOSITION_MARKS[disposition] ?? "?" : "+";
}

/** What the approval card on a ticket-bound conversation says and whether it offers Approve. */
export interface TicketApprovalEffect {
  summary: string;
  approvable: boolean;
}

export function ticketApprovalEffect(preview: Pick<TicketApprovalPreview, "effect" | "start">): TicketApprovalEffect {
  const label = approvalEffectLabel(preview);
  return { summary: label.start ? `${label.effect} ${label.start}` : label.effect, approvable: label.approvable };
}
